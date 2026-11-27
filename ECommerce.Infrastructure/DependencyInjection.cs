using Microsoft.AspNetCore.DataProtection;
using System.Text;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Options;
using ECommerce.Infrastructure.Email;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Payments;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableMockPaymentGateway = false,
        bool enableDevelopmentEmailSender = false,
        bool enableDemoPaymentGateway = false,
        string connectionStringName = "ECommerceDb",
        bool requireSmtpEmailSender = false)
    {
        var demoPaymentsEnabled = configuration.GetValue<bool>("DemoPayments:Enabled");
        if (demoPaymentsEnabled != enableDemoPaymentGateway)
        {
            throw new InvalidOperationException(
                "DemoPayments:Enabled and enableDemoPaymentGateway must be configured together.");
        }

        if (enableDemoPaymentGateway)
        {
            if (enableMockPaymentGateway || enableDevelopmentEmailSender)
            {
                throw new InvalidOperationException(
                    "The Demo payment gateway cannot be combined with Development-only services.");
            }

            if (!string.Equals(connectionStringName, "DemoECommerceDb", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The Demo payment gateway requires the dedicated DemoECommerceDb connection string.");
            }

            if (configuration.GetValue<bool>("ZarinPal:Enabled"))
            {
                throw new InvalidOperationException(
                    "ZarinPal cannot be enabled together with the Demo payment gateway.");
            }
        }

        var smtpEmailOptions = configuration
            .GetSection(SmtpEmailOptions.SectionName)
            .Get<SmtpEmailOptions>() ?? new SmtpEmailOptions();
        if (requireSmtpEmailSender && !smtpEmailOptions.Enabled)
        {
            throw new InvalidOperationException(
                $"{SmtpEmailOptions.SectionName}:Enabled must be true when SMTP email delivery is required.");
        }

        if (smtpEmailOptions.Enabled &&
            !smtpEmailOptions.HasValidConfiguration(allowInsecureTransport: enableDevelopmentEmailSender))
        {
            throw new InvalidOperationException(
                "SmtpEmail configuration is invalid. SMTP requires a host, valid port and sender address, a timeout from 1 to 60 seconds, matching optional credentials, and TLS outside Development.");
        }

        var resetFrontendUrl = configuration[$"{PasswordResetEmailOptions.SectionName}:{nameof(PasswordResetEmailOptions.FrontendUrl)}"];
        if ((enableDevelopmentEmailSender || smtpEmailOptions.Enabled) &&
            !IsValidPasswordResetFrontendUrl(resetFrontendUrl, allowHttp: enableDevelopmentEmailSender))
        {
            throw new InvalidOperationException(
                "PasswordResetEmail:FrontendUrl must be an absolute HTTPS URL outside Development (HTTP is allowed only in Development), without user information, a query, or a fragment.");
        }

        var connectionString = configuration.GetConnectionString(connectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"{connectionStringName} is not configured");
        services.AddDbContext<ECommerceDbContext>(options => options.UseNpgsql(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ECommerceDbContext>()
            .AddDefaultTokenProviders();

        var dataProtectionApplicationName = configuration["DataProtection:ApplicationName"];
        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName(
                string.IsNullOrWhiteSpace(dataProtectionApplicationName)
                    ? "ECommerce.API"
                    : dataProtectionApplicationName);

        var dataProtectionKeysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
        {
            if (!Path.IsPathFullyQualified(dataProtectionKeysPath))
            {
                throw new InvalidOperationException(
                    "DataProtection:KeysPath must be an absolute path.");
            }

            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
        }

        var passwordResetTokenLifespanMinutes = configuration.GetValue(
            $"{PasswordResetEmailOptions.SectionName}:{nameof(PasswordResetEmailOptions.TokenLifespanMinutes)}",
            30);
        if (passwordResetTokenLifespanMinutes is < 5 or > 120)
            throw new InvalidOperationException("PasswordResetEmail:TokenLifespanMinutes must be between 5 and 120.");

        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromMinutes(passwordResetTokenLifespanMinutes));

        var passwordResetEmailOptions = services.AddOptions<PasswordResetEmailOptions>()
            .Bind(configuration.GetSection(PasswordResetEmailOptions.SectionName));

        if (enableDevelopmentEmailSender || smtpEmailOptions.Enabled)
        {
            passwordResetEmailOptions
                .Validate(
                    options => IsValidPasswordResetFrontendUrl(
                        options.FrontendUrl,
                        allowHttp: enableDevelopmentEmailSender),
                    "PasswordResetEmail:FrontendUrl must be an absolute HTTPS URL outside Development (HTTP is allowed only in Development), without user information, a query, or a fragment.")
                .ValidateOnStart();
        }

        if (smtpEmailOptions.Enabled)
        {
            services.AddOptions<SmtpEmailOptions>()
                .Bind(configuration.GetSection(SmtpEmailOptions.SectionName))
                .Validate(
                    options => options.HasValidConfiguration(allowInsecureTransport: enableDevelopmentEmailSender),
                    "SmtpEmail configuration is invalid. SMTP requires a host, valid port and sender address, a timeout from 1 to 60 seconds, matching optional credentials, and TLS outside Development.")
                .ValidateOnStart();

            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else if (enableDevelopmentEmailSender)
        {
            services.AddScoped<IEmailSender, DevelopmentEmailSender>();
        }
        else
        {
            // Never log reset links or tokens outside Development. Demo requires SMTP explicitly.
            services.AddScoped<IEmailSender, DisabledEmailSender>();
        }

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ??
                         throw new InvalidOperationException("Jwt configuration is not configured.");
        if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey))
            throw new InvalidOperationException("Jwt SecretKey is not configured.");

        var secretKeyBytes = Encoding.UTF8.GetBytes(jwtOptions.SecretKey);

        if (secretKeyBytes.Length < 32)
            throw new InvalidOperationException("Jwt SecretKey must be at least 32 bytes long.");

        services.Configure<JwtOptions>(jwtSection);

        services.AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
            .Validate(options => options.ExpirationDays is > 0 and <= 90,
                "RefreshTokens:ExpirationDays must be between 1 and 90 days.")
            .Validate(options => options.RetentionDays is > 0 and <= 365,
                "RefreshTokens:RetentionDays must be between 1 and 365 days.")
            .Validate(options => options.CleanupIntervalHours is > 0 and <= 168,
                "RefreshTokens:CleanupIntervalHours must be between 1 and 168 hours.")
            .Validate(options => options.RotationGracePeriodSeconds is >= 0 and <= 30,
                "RefreshTokens:RotationGracePeriodSeconds must be between 0 and 30 seconds.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IAccessTokenService, JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IUserRoleService, UserRoleService>();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IBrandRepository, BrandRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductVariantRepository, ProductVariantRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<ISellerOfferRepository, SellerOfferRepository>();
        services.AddScoped<ISellerRequestRepository, SellerRequestRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ECommerceDbContext>());
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IWishlistRepository, WishlistRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentResolutionAuditRepository, PaymentResolutionAuditRepository>();
        if (enableDemoPaymentGateway)
            services.AddScoped<IMockPaymentGateway, DemoPaymentGateway>();
        else if (enableMockPaymentGateway)
            services.AddScoped<IMockPaymentGateway, MockPaymentGateway>();

        var zarinPalSection = configuration.GetSection(ZarinPalOptions.SectionName);
        if (zarinPalSection.GetValue<bool>(nameof(ZarinPalOptions.Enabled)))
        {
            services.AddOptions<ZarinPalOptions>()
                .Bind(zarinPalSection)
                .Validate(options => Guid.TryParse(options.MerchantId, out var merchantId) && merchantId != Guid.Empty,
                    "ZarinPal MerchantId must be a valid non-empty GUID when the gateway is enabled.")
                .Validate(options => IsHttpsUrl(options.ApiBaseUrl),
                    "ZarinPal ApiBaseUrl must be a valid HTTPS URL.")
                .Validate(options => IsHttpsUrl(options.PaymentPageBaseUrl),
                    "ZarinPal PaymentPageBaseUrl must be a valid HTTPS URL.")
                .Validate(options => IsHttpsUrl(options.CallbackUrl),
                    "ZarinPal CallbackUrl must be a valid HTTPS URL.")
                .Validate(options => options.TimeoutSeconds > 0,
                    "ZarinPal TimeoutSeconds must be greater than zero.")
                .Validate(options => options.TimeoutSeconds < 60,
                    "ZarinPal TimeoutSeconds must be less than 60 seconds so the two-minute payment request retry lease includes a recovery buffer.")
                .ValidateOnStart();

            services.AddHttpClient<IPaymentGateway, ZarinPalPaymentGateway>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ZarinPalOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });
        }
    }

    private static bool IsHttpsUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           uri.Scheme == Uri.UriSchemeHttps;

    private static bool IsValidPasswordResetFrontendUrl(string? value, bool allowHttp)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttps || allowHttp && uri.Scheme == Uri.UriSchemeHttp) &&
           string.IsNullOrEmpty(uri.UserInfo) &&
           string.IsNullOrEmpty(uri.Query) &&
           string.IsNullOrEmpty(uri.Fragment);
}
