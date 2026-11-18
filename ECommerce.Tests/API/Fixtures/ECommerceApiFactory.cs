using ECommerce.API.BackgroundServices;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Infrastructure.Identity;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.API.Fixtures;

/// <summary>
/// Boots the real API pipeline against a Testcontainers PostgreSQL database.
/// External ZarinPal calls and periodic hosted workers are isolated for deterministic tests.
/// </summary>
public sealed class ECommerceApiFactory(
    string connectionString,
    IPaymentGateway paymentGateway) : WebApplicationFactory<Program>
{
    private const string TestJwtSigningKey = "ECommerce.Api.Integration.Tests.Signing.Key.2026!";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(ResolveApiContentRoot());

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ECommerceDb"] = connectionString,
                // A deterministic test-only signing key keeps the host independent of local user-secrets.
                ["Jwt:SecretKey"] = TestJwtSigningKey,
                ["ZarinPal:Enabled"] = "true",
                ["ZarinPal:MerchantId"] = "11111111-1111-1111-1111-111111111111",
                ["ZarinPal:ApiBaseUrl"] = "https://sandbox.zarinpal.com",
                ["ZarinPal:PaymentPageBaseUrl"] = "https://sandbox.zarinpal.com/pg/StartPay",
                ["ZarinPal:CallbackUrl"] = "https://api.test/api/payments/zarinpal/callback",
                ["ZarinPal:TimeoutSeconds"] = "15",
                ["Frontend:PaymentResultUrl"] = "https://frontend.test/payment-result",
                ["HttpsRedirection:Enabled"] = "false",
                ["Cors:AllowedOrigins:0"] = "http://localhost",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Keep token issuance and JwtBearer validation on exactly the same test-only key.
            // PostConfigure is intentional: AddInfrastructure registers the production validation
            // parameters before ConfigureTestServices is applied.
            services.PostConfigure<JwtOptions>(options =>
                options.SecretKey = TestJwtSigningKey);

            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options => options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSigningKey)));

            // Keep the real auth rate-limiting middleware and endpoint policy in the test host,
            // but replace the production-sized threshold with a deterministic test-only limit.
            // Program captures configuration values while registering the policy, so changing
            // IConfiguration from WebApplicationFactory is not sufficient for this policy.
            services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
            services.Configure<RateLimiterOptions>(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("auth", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10_000,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });

            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(paymentGateway);

            // The tests start the expiration worker explicitly when exercising it.
            // Payment reconciliation is covered at the API callback and command boundary;
            // its timer must not race the test-controlled fake gateway.
            var hostedServicesToRemove = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    (descriptor.ImplementationType == typeof(OrderExpirationBackgroundService) ||
                     descriptor.ImplementationType == typeof(ZarinPalPaymentReconciliationBackgroundService)))
                .ToArray();

            foreach (var descriptor in hostedServicesToRemove)
                services.Remove(descriptor);
        });
    }

    private static string ResolveApiContentRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var apiProjectDirectory = Path.Combine(directory.FullName, "ECommerce.API");
            if (File.Exists(Path.Combine(apiProjectDirectory, "ECommerce.API.csproj")))
                return apiProjectDirectory;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the ECommerce.API project directory from the integration test output folder.");
    }
}
