using System.Text;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Payments;
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
        bool enableMockPaymentGateway = false)
    {
        var connectionString = configuration.GetConnectionString("ECommerceDb") ??
                               throw new InvalidOperationException("ECommerceDb is not configured");
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
            .AddEntityFrameworkStores<ECommerceDbContext>();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ??
                         throw new InvalidOperationException("Jwt configuration is not configured.");
        if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey))
            throw new InvalidOperationException("Jwt SecretKey is not configured.");

        var secretKeyBytes = Encoding.UTF8.GetBytes(jwtOptions.SecretKey);

        if (secretKeyBytes.Length < 32)
            throw new InvalidOperationException("Jwt SecretKey must be at least 32 bytes long.");

        services.Configure<JwtOptions>(jwtSection);

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
        if (enableMockPaymentGateway)
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
}
