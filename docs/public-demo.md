# Public Demo Environment

## Status

The isolated Demo boundary, authenticated deterministic mock payment routes, configuration-backed Demo account/catalog seed, opt-in OpenAPI/Scalar, SMTP-backed password-reset email delivery, and periodic visitor-data reset are implemented. A dedicated Docker Compose deployment profile provisions an isolated PostgreSQL database, persistent database and Data Protection volumes, TLS SMTP configuration, and a loopback-bound API behind a trusted host reverse proxy.

## Isolation rules

- `ASPNETCORE_ENVIRONMENT=Demo` is a dedicated environment; it is not Development.
- `DemoPayments:Enabled` must be explicitly set to `true` in Demo.
- Enabling `DemoPayments:Enabled` in Development, Production, or another environment stops startup.
- Demo requires `ConnectionStrings:DemoECommerceDb`; it never falls back to `ConnectionStrings:ECommerceDb`.
- Startup rejects a Demo connection string that resolves to the same PostgreSQL host, port, and database name as the normal application connection string, even when credentials differ. The deployment must also ensure that different hostnames or proxies do not point to the same underlying database resource; that physical infrastructure aliasing cannot be proven from a connection string alone.
- ZarinPal must remain disabled in Demo. Demo registers only its deterministic mock gateway.
- Development retains its existing randomized mock gateway and Development email sender.
- Demo does not register Development-only services, expose Development OpenAPI/Scalar endpoints by default, or enable Development email logging. Password-reset delivery uses the configured SMTP sender and requires TLS.
- Demo automatically applies EF Core migrations to its dedicated Demo database. Production migrations remain a separate deployment step.
- Demo seed credentials are validated before the application applies migrations or seeds the Demo database.
- `docker-compose.demo.yml` uses a dedicated bridge network and named PostgreSQL/Data Protection volumes. It does not publish the database port.
- The Compose API port binds to `127.0.0.1` by default. A host-level reverse proxy must terminate HTTPS and forward the original `Host`, `X-Forwarded-For`, and `X-Forwarded-Proto` headers. The configured Docker bridge gateway is the only trusted proxy by default.

## Docker Compose deployment

The repository includes a separate deployment profile. It does not reuse the Development Compose stack or its Mailpit configuration.

1. Copy the template and edit every placeholder:

   PowerShell:
   ```powershell
   Copy-Item .env.demo.example .env.demo
   notepad .env.demo
   ```

   Bash:
   ```bash
   cp .env.demo.example .env.demo
   chmod 600 .env.demo
   ```

2. Set unique PostgreSQL and JWT secrets, four distinct Demo seed passwords, the public API host name(s), an HTTPS password-reset page URL, and an external SMTP provider with STARTTLS or implicit TLS. Keep the SMTP username and password both empty when the provider does not require authentication. For the PostgreSQL password, use a random hexadecimal/alphanumeric value so it can safely appear in the Npgsql connection string.

3. Validate and start the isolated stack:

   ```bash
   docker compose --env-file .env.demo -f docker-compose.demo.yml config --quiet
   docker compose --env-file .env.demo -f docker-compose.demo.yml up --build -d
   docker compose --env-file .env.demo -f docker-compose.demo.yml logs -f api
   ```

4. For a local smoke test, use `http://127.0.0.1:8081/health/live` and `http://127.0.0.1:8081/health/ready`. Set `DEMO_API_DOCUMENTATION_ENABLED=true` only when the interactive reference is wanted; then Scalar is available at `http://127.0.0.1:8081/scalar/v1`.

The API is intentionally bound to loopback by default. On a public host, configure a host-level reverse proxy to `127.0.0.1:8081`, terminate TLS there, preserve the `Host` header, and set `X-Forwarded-For` and `X-Forwarded-Proto`. The default Docker subnet is `172.30.0.0/24` with gateway `172.30.0.1`; the gateway is trusted for forwarded headers. Change both values together if that subnet conflicts with an existing network, and keep `DEMO_DOCKER_GATEWAY` aligned with the configured gateway. Do not expose the API port directly to the Internet.

The Compose profile persists PostgreSQL data and the ASP.NET Core Data Protection key ring in separate named volumes. A normal `docker compose ... down` preserves both. Adding `--volumes` permanently deletes both the database and key ring, invalidating outstanding reset tokens and removing all Demo activity. The application also resets visitor-created activity periodically; this is independent of the Compose volume lifecycle.

The Compose profile does not publish Mailpit. Password-reset mail is sent to the configured external SMTP provider. Verify sender-domain authentication and provider delivery limits before making forgot-password available to public visitors.

## Required environment configuration

Configure these values in the Demo deployment secret/environment configuration. Never commit actual passwords to `appsettings*.json`, `.env.example`, or source control.

```text
ASPNETCORE_ENVIRONMENT=Demo
DemoPayments__Enabled=true
ConnectionStrings__DemoECommerceDb=Host=<demo-db-host>;Database=ecommerce_demo;Username=<user>;Password=<secret>
Jwt__SecretKey=<unique-random-secret-at-least-32-bytes>
Jwt__Issuer=ECommerce.API
Jwt__Audience=ECommerce.API
ZarinPal__Enabled=false
SmtpEmail__Enabled=true
SmtpEmail__Host=<smtp-host>
SmtpEmail__Port=587
SmtpEmail__Security=StartTls
SmtpEmail__FromAddress=<verified-sender-address>
SmtpEmail__FromName=ECommerce
SmtpEmail__Username=<smtp-username>
SmtpEmail__Password=<smtp-password>
PasswordResetEmail__FrontendUrl=https://<frontend-host>/reset-password
DemoSeed__AdminPassword=<unique-admin-password>
DemoSeed__SellerPassword=<unique-seller-password>
DemoSeed__SecondSellerPassword=<unique-second-seller-password>
DemoSeed__CustomerPassword=<unique-customer-password>
DemoReset__IntervalSeconds=86400
```

All four Demo seed passwords are required, must be different from one another, and must satisfy the configured ASP.NET Core Identity password policy: at least 8 characters, including uppercase, lowercase, digit, and non-alphanumeric characters. Startup fails with a configuration error if any value is missing, weak, or reused. Password values are not written to application logs.

`DemoReset__IntervalSeconds` is optional and defaults to `86400` (24 hours). Use an integer of at least `60`; the setting is bound and validated only in the Demo environment.

Do not reuse the Production database or Production credentials. Keep Demo database credentials separate and grant only the permissions required by the Demo application. Keep the seeded Admin password operator-only; never publish it or provide it to public Demo visitors.

## API documentation

OpenAPI and Scalar are disabled in Demo by default. To expose the interactive API reference for a public portfolio deployment, set:

```text
DemoApiDocumentation__Enabled=true
```

When enabled, the endpoints are available at `/openapi/v1.json` and `/scalar/v1` on the API host. The setting is accepted only when `ASPNETCORE_ENVIRONMENT=Demo`; enabling it in Development, Production, or another environment stops startup. This setting exposes API metadata and the interactive client only. It does not bypass authentication or authorization on API operations.

Do not enable Development mode on a public deployment.

## Password-reset email delivery

Demo startup requires `SmtpEmail__Enabled=true` and a valid SMTP configuration. Use a mail provider or relay intended for the Demo deployment; do not expose Mailpit publicly. `SmtpEmail__Security` must be `StartTls` or `SslOnConnect` outside Development. Plaintext SMTP (`None`) is accepted only in Development; reserve it for local Mailpit and never use it with an external relay. The reset-page URL must use HTTPS in Demo and Production.

The SMTP configuration requires a host, port, valid `FromAddress`, and sender name. `TimeoutSeconds` defaults to 15 and must be between 1 and 60. SMTP authentication is optional, but `Username` and `Password` must either both be supplied or both be omitted. Store credentials in deployment secrets/environment variables, never in the repository. `PasswordResetEmail__FrontendUrl` must be an absolute HTTPS reset-page URL without user information, a query, or a fragment. HTTP is accepted only in Development.

`POST /api/auth/forgot-password` keeps the same generic response for known and unknown accounts. For an existing account, the API sends the single-use reset link through SMTP; the link lifetime is controlled by `PasswordResetEmail__TokenLifespanMinutes`. Reset tokens and email bodies are never written to logs by the SMTP sender. If SMTP is unavailable, delivery fails and the API preserves the generic response while recording the delivery failure without exposing the token. Identity reset tokens depend on the ASP.NET Core Data Protection key ring. The supplied Demo Compose profile persists the key ring in a named volume at `/home/app/.aspnet/DataProtection-Keys`. Non-Compose deployments must configure an equivalent durable shared key repository; replicas must share the same key ring. The key ring is not currently protected with a deployment certificate or external key-encryption service.

## Seeded accounts and catalog

The Demo seed creates these accounts in its isolated database:

| Role | Email | Use |
|---|---|---|
| Admin | `admin@test.com` | Operator-only administration and manual-review operations |
| Seller | `seller@test.com` | First seller account |
| Seller | `seller2@test.com` | Second seller account for multi-seller orders |
| Customer | `customer@test.com` | Customer cart, checkout, payment, and order history |

The password for each account comes from its matching `DemoSeed__...Password` setting. The seeder uses those values when it creates a missing account; it does not overwrite the password of an account that already exists. Changing a setting therefore does not rotate an existing account password. Existing suspended or rejected seller accounts are not automatically reactivated.

The catalog seed creates or ensures 5 brands, 8 categories (4 root and 4 child categories), 6 products, 12 variants, 18 HTTPS image records, and 18 active seller offers across two active sellers. Prices are sample amounts in Toman and stock values are for demonstration only.

Seeding is idempotent: repeated application startup does not duplicate seeded users or catalog records. Existing inactive products, variants, offers, and suspended seller accounts are not automatically reactivated. The Demo-only reset worker runs after each configured interval (24 hours by default; set `DemoReset__IntervalSeconds` to override it). The first reset occurs after the first full interval, not immediately at startup. Each reset uses one database transaction, removes visitor accounts and their seller offers, all carts, wishlists, addresses, seller requests, orders, payments, payment-resolution audit rows, and refresh tokens, then restores the two seeded sellers’ offer stocks to 15 and 8. The four seeded accounts, catalog, offer prices, and offer activation states are retained. A reset failure is logged and retried on the next scheduled interval. The current worker is process-local, so run a single Demo API replica unless a distributed reset lock is added.

## Payment behavior

`DemoPaymentGateway` always returns a successful simulated result with a stable transaction identifier derived from the local payment ID. It does not contact ZarinPal or transfer funds. The existing Development `MockPaymentGateway` remains randomized and is not used in Demo.

The authenticated Demo payment flow is:

- `POST /api/demo/payments/orders/{orderId}/initiate` creates a pending payment using `PaymentMethod.Mock`.
- `POST /api/demo/payments/{paymentId}/complete` completes that pending payment through `DemoPaymentGateway`.

Both endpoints are mapped only when the Demo environment guard is enabled. Existing order ownership, payment ownership, order-state, payment-window, and payment-state checks remain in the application handlers. The Development-only `/api/payments/.../initiate-mock` and `/complete-mock` routes are not exposed in Demo.
