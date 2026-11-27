using System.Net;
using System.Threading.RateLimiting;
using ECommerce.API.BackgroundServices;
using ECommerce.API.HealthChecks;
using ECommerce.API.Middleware;
using ECommerce.API.Options;
using ECommerce.API.Payments;
using ECommerce.Application;
using ECommerce.Application.Features.Payments.CompleteMockPayment;
using ECommerce.Application.Options;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;

var runInitialAdminBootstrap = args.Contains("--bootstrap-admin", StringComparer.OrdinalIgnoreCase);
var builderArgs = args
    .Where(argument => !string.Equals(argument, "--bootstrap-admin", StringComparison.OrdinalIgnoreCase))
    .ToArray();
var builder = WebApplication.CreateBuilder(builderArgs);
var isDevelopment = builder.Environment.IsDevelopment();
var isDemo = DemoPaymentEnvironment.ValidateAndIsEnabled(
    builder.Environment.EnvironmentName,
    builder.Configuration);
var zarinPalEnabled = builder.Configuration.GetValue<bool>("ZarinPal:Enabled");

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration,
    enableMockPaymentGateway: isDevelopment,
    enableDevelopmentEmailSender: isDevelopment,
    enableDemoPaymentGateway: isDemo,
    connectionStringName: isDemo ? "DemoECommerceDb" : "ECommerceDb");

if (isDemo)
{
    builder.Services.AddOptions<DemoSeedOptions>()
        .Bind(builder.Configuration.GetSection(DemoSeedOptions.SectionName))
        .Validate(options => options.HasValidPasswords, DemoSeedOptions.ValidationErrorMessage)
        .ValidateOnStart();
}

builder.Services.AddHostedService<OrderExpirationBackgroundService>();
if (zarinPalEnabled)
    builder.Services.AddHostedService<ZarinPalPaymentReconciliationBackgroundService>();

builder.Services.AddOptions<FrontendOptions>()
    .Bind(builder.Configuration.GetSection(FrontendOptions.SectionName))
    .Validate(options => !zarinPalEnabled ||
                         Uri.TryCreate(options.PaymentResultUrl, UriKind.Absolute, out var uri) &&
                         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                         string.IsNullOrEmpty(uri.UserInfo),
        "Frontend:PaymentResultUrl must be an absolute HTTP or HTTPS URL when ZarinPal is enabled.")
    .ValidateOnStart();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var trustedProxyAddresses = builder.Configuration
    .GetSection("ForwardedHeaders:KnownProxies")
    .Get<string[]>() ?? [];

var parsedTrustedProxyAddresses = trustedProxyAddresses.Select(address =>
    IPAddress.TryParse(address, out var parsedAddress)
        ? parsedAddress
        : throw new InvalidOperationException($"ForwardedHeaders:KnownProxies contains an invalid IP address: '{address}'."))
    .ToArray();

if (parsedTrustedProxyAddresses.Length > 0)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var address in parsedTrustedProxyAddresses)
            options.KnownProxies.Add(address);
    });
}

var authPermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
var authWindowSeconds = builder.Configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60);

if (authPermitLimit <= 0)
    throw new InvalidOperationException("RateLimiting:Auth:PermitLimit must be greater than zero.");

if (authWindowSeconds <= 0)
    throw new InvalidOperationException("RateLimiting:Auth:WindowSeconds must be greater than zero.");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

builder.Services.AddAuthorization();

builder.Services
    .AddOptions<OrderExpirationOptions>()
    .Bind(builder.Configuration.GetSection(OrderExpirationOptions.SectionName))
    .Validate(options => options.PaymentWindowMinutes > 0,
        "PaymentWindowMinutes must be greater than zero.")
    .Validate(options => options.ScanIntervalSeconds > 0,
        "ScanIntervalSeconds must be greater than zero.")
    .Validate(options => options.ReconciliationIntervalSeconds >= 30,
        "ReconciliationIntervalSeconds must be at least 30 seconds.")
    .Validate(options => options.MaxVerificationAttempts > 0,
        "MaxVerificationAttempts must be greater than zero.")
    .Validate(options => options.MaxVerificationAgeMinutes > 0,
        "MaxVerificationAgeMinutes must be greater than zero.")
    .ValidateOnStart();

var app = builder.Build();

// This command is deliberately separate from normal startup and is only available in Production.
// Supply its credentials through BootstrapAdmin__* environment variables, never command-line args.
if (runInitialAdminBootstrap)
{
    if (!app.Environment.IsProduction())
        throw new InvalidOperationException("--bootstrap-admin can only run when ASPNETCORE_ENVIRONMENT=Production.");

    using var bootstrapScope = app.Services.CreateScope();
    var bootstrapServices = bootstrapScope.ServiceProvider;

    await InitialAdminBootstrapper.CreateAsync(
        bootstrapServices.GetRequiredService<ECommerceDbContext>(),
        bootstrapServices.GetRequiredService<UserManager<ApplicationUser>>(),
        bootstrapServices.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
        app.Configuration);

    app.Logger.LogInformation("The initial administrator account was created successfully.");
    return;
}

// Production migrations are deployed separately. Development and the isolated Demo database are self-contained.
using (var scope = app.Services.CreateScope())
{
    // Resolve and validate Demo credentials before touching the dedicated Demo database.
    var demoSeedOptions = isDemo
        ? scope.ServiceProvider.GetRequiredService<IOptions<DemoSeedOptions>>().Value
        : null;

    var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

    if (app.Environment.IsDevelopment() || isDemo)
        await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    await DatabaseSeeder.SeedRolesAsync(roleManager);

    if (app.Environment.IsDevelopment() || isDemo)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (app.Environment.IsDevelopment())
            await DatabaseSeeder.SeedDevelopmentDataAsync(dbContext, userManager);
        else
            await DatabaseSeeder.SeedDemoDataAsync(dbContext, userManager, demoSeedOptions!);
    }
}

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (app.Configuration.GetValue("HttpsRedirection:Enabled", true))
    app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseMiddleware<CookieRequestOriginMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

Func<HttpContext, HealthReport, Task> healthCheckResponseWriter = async (HttpContext context, HealthReport report) =>
{
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description
        })
    });
};

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = healthCheckResponseWriter
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = healthCheckResponseWriter
}).AllowAnonymous();

app.MapControllers();

// Public Demo has its own deterministic payment routes. These are never mapped in
// Development, Production, or any other environment.
if (isDemo)
    app.MapDemoPaymentEndpoints();

// These existing Development mock endpoints remain unavailable in Demo and Production.
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/payments/orders/{orderId:guid}/initiate-mock",
        async (Guid orderId, MediatR.ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new ECommerce.Application.Features.Payments.InitiatePayment.InitiatePaymentCommand(
                    orderId,
                    ECommerce.Domain.Enums.PaymentMethod.Mock),
                cancellationToken);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Development Payments")
        .WithName("InitiateMockPayment")
        .Produces<ECommerce.Application.Features.Payments.InitiatePayment.InitiatePaymentResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

    app.MapPost("/api/payments/{paymentId:guid}/complete-mock",
        async (Guid paymentId, MediatR.ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new CompleteMockPaymentCommand(paymentId),
                cancellationToken);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Development Payments")
        .WithName("CompleteMockPayment")
        .Produces<CompleteMockPaymentResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
}

app.Run();

// Expose the top-level entry point to WebApplicationFactory-based integration tests.
public partial class Program { }
