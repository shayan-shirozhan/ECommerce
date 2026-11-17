# Production configuration and first administrator

## Required production configuration

Set these values in the deployment secret store / process environment. Do not place secrets in a committed `appsettings.Production.json` file.

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__ECommerceDb` – PostgreSQL connection string.
- `Jwt__SecretKey` – cryptographically random secret with at least 32 UTF-8 bytes.
- `Jwt__Issuer` and `Jwt__Audience` – values matching the token consumers.
- `Cors__AllowedOrigins__0` (and `__1`, etc.) – exact frontend origins, including scheme and port, without path segments.
- `RateLimiting__Auth__PermitLimit` and `RateLimiting__Auth__WindowSeconds` – optional overrides.

The production CORS origin list is intentionally empty by default. Configure the real frontend origin(s) before enabling browser clients. CORS is not an authentication mechanism.

When the API is behind a reverse proxy or load balancer, set `ForwardedHeaders__KnownProxies__0` (and subsequent indices) to the proxy IP addresses. The API enables `X-Forwarded-For` and `X-Forwarded-Proto` only for those trusted proxies. Do not trust arbitrary `X-Forwarded-For` values; the auth rate limiter partitions by the remote IP address observed by the application.

## Deploy database migrations

Production startup does not apply migrations automatically. Apply the existing migrations as a deployment step before starting the API. From the repository root, use the normal EF Core toolchain with `ECommerce.Infrastructure` as the migrations project and `ECommerce.API` as the startup project.

```powershell
dotnet ef database update --project ECommerce.Infrastructure --startup-project ECommerce.API
```

## Create the first administrator

The first administrator is not seeded with a fixed username or password. Run this explicit one-time command after the database migrations have been applied and before starting the normal API process.

Provide these values through the deployment secret manager or process environment:

- `BootstrapAdmin__Email`
- `BootstrapAdmin__Password`
- `BootstrapAdmin__FirstName`
- `BootstrapAdmin__LastName`

Do not put the password in command-line arguments, shell scripts committed to source control, Docker image layers, or appsettings files.

```powershell
dotnet ECommerce.API.dll --bootstrap-admin
```

The command only runs in `Production`, creates the Admin role if necessary, refuses to proceed if an administrator already exists, refuses to modify an existing account, and creates the user and role assignment in a database transaction. It exits after creation; it does not start the web server. Remove the bootstrap environment variables from the execution environment after the one-time operation.

Normal API startup continues to ensure the application roles exist. It does not create or promote an administrator.

## Health endpoints

- `GET /health/live` – process liveness; does not require PostgreSQL.
- `GET /health/ready` – readiness; returns unhealthy when PostgreSQL cannot be reached.

Both endpoints are anonymous so container orchestrators and load balancers can probe them.


## ZarinPal payment gateway

The real payment gateway is disabled by default. Enable it with deployment environment variables; never commit a merchant ID or payment credentials. The callback URL must be publicly reachable over HTTPS and must point to `/api/payments/zarinpal/callback`.

Required settings when `ZarinPal__Enabled=true`:

- `ZarinPal__MerchantId`: non-empty merchant UUID.
- `ZarinPal__ApiBaseUrl`: provider API root (use the provider's sandbox host for tests).
- `ZarinPal__PaymentPageBaseUrl`: provider payment-page root, ending in `/pg/StartPay`.
- `ZarinPal__CallbackUrl`: public HTTPS callback URL. The application appends `paymentId` to it.

The application's canonical monetary unit is **Toman**. `SellerOffer.Price`, order item prices and totals, order totals, and payment amounts are all stored and exchanged by the application in Toman. The ZarinPal v4 adapter converts Toman to Rials (multiply by 10) only when creating or verifying a provider payment. No other application layer should perform this conversion.

Before deploying this change against a database containing existing business data, verify that its stored prices and totals are already interpreted as Toman. If any existing values were entered as Rials, migrate or normalize those values before enabling the gateway; do not try to compensate with a runtime setting. The adapter rejects amounts that cannot be represented as a whole number of Rials.

For the provider flow, `POST /api/payments/orders/{orderId}/initiate` creates a local payment, requests an Authority, stores it, and returns `RedirectUrl`. The public callback uses only the saved payment amount and Authority and verifies them server-to-server before confirming an `OK` payment. A provider callback with `Status=NOK` is recorded as a failed attempt without calling Verify, in line with the ZarinPal integration guide. Failed attempts leave the order awaiting payment and permit a new payment attempt. Verification codes `100` and `101` are treated as successful; the returned `ref_id` is persisted.

The mock initiation/completion endpoints are mapped only in Development.
