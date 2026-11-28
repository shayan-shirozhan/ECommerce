# Production configuration and first administrator

## Required production configuration

Set these values in the deployment secret store / process environment. Do not place secrets in a committed `appsettings.Production.json` file.

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__ECommerceDb` – PostgreSQL connection string.
- `Jwt__SecretKey` – cryptographically random secret with at least 32 UTF-8 bytes.
- `Jwt__Issuer` and `Jwt__Audience` – values matching the token consumers.
- `Cors__AllowedOrigins__0` (and `__1`, etc.) – exact frontend origins, including scheme and port, without path segments.
- `RateLimiting__Auth__PermitLimit` and `RateLimiting__Auth__WindowSeconds` – optional overrides for the authentication policy.
- `RateLimiting__Write__Enabled=true` – opt in to the shared per-IP write limiter in Production. It is disabled by default in `appsettings.Production.json`; `RateLimiting__Write__PermitLimit` (default `30`) and `RateLimiting__Write__WindowSeconds` (default `60`) control the quota. Set `Enabled=false` to disable it explicitly in another environment.
- `AllowedHosts` – explicit semicolon-separated API host names, without schemes or ports (for example `api.example.com`). The committed default permits loopback hosts only; wildcard entries are rejected in Production.
- `DataProtection__KeysPath` – an absolute path to a durable, writable directory for ASP.NET Core Data Protection keys (for example `/home/app/.aspnet/DataProtection-Keys`).

The production CORS origin list is intentionally empty by default. Configure the real frontend origin(s) before enabling browser clients. CORS is not an authentication mechanism.

Production startup refuses wildcard `AllowedHosts` and refuses to start without an absolute `DataProtection:KeysPath`. Mount that directory on durable storage, grant write access only to the API service identity, and share the same directory/key ring across replicas. Do not use a container's writable layer or a per-replica local directory. The default Data Protection application name is `ECommerce.API`, so replicas using the same key ring can validate each other's protected tokens. Protect the volume and its backups as secrets; the key files are not additionally encrypted by this configuration.

HSTS is enabled outside Development with a one-year max age and is emitted on HTTPS responses. The API also adds `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, and a restrictive `Permissions-Policy`. When TLS terminates at a reverse proxy, configure its IP under `ForwardedHeaders__KnownProxies__...`; forwarded headers are processed before HSTS and HTTPS redirection. Never trust arbitrary client-supplied forwarded headers.

## Password-reset email delivery

The application can send password-reset messages through the shared MailKit SMTP sender. To enable it in Production, configure:

- `SmtpEmail__Enabled=true`
- `SmtpEmail__Host` and `SmtpEmail__Port`
- `SmtpEmail__Security=StartTls` or `SslOnConnect` (plaintext SMTP is rejected outside Development)
- `SmtpEmail__FromAddress` and `SmtpEmail__FromName`
- `PasswordResetEmail__FrontendUrl` – absolute HTTPS URL of the frontend reset-password page, without user information, query, or fragment
- `SmtpEmail__Username` and `SmtpEmail__Password` when the relay requires authentication; provide both or neither

Keep SMTP credentials in the deployment secret store or process environment. The SMTP sender does not log reset links or email bodies. When `SmtpEmail__Enabled` is not true, the API retains `DisabledEmailSender`: password-reset requests keep their generic response, but no email is sent. The local Development Compose stack uses Mailpit; do not expose Mailpit as a production mail relay or public inbox.

When the API is behind a reverse proxy or load balancer, set `ForwardedHeaders__KnownProxies__0` (and subsequent indices) to the proxy IP addresses. The API enables `X-Forwarded-For` and `X-Forwarded-Proto` only for those trusted proxies. Do not trust arbitrary `X-Forwarded-For` values; both rate-limiting policies partition by the remote IP address observed by the application. The write quota is shared across the actions marked with `[WriteRateLimited]`; it includes Cart/order/payment operations, Wishlist and address mutations, seller-request mutations, profile updates, and registration. Counters are in process memory, so use an edge/API-gateway limit as well when a deployment has multiple replicas.

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
- `Frontend__PaymentResultUrl`: absolute HTTP(S) URL of the frontend payment-result page. The API callback redirects to this configured URL with `orderId` and `status` query parameters; it never accepts a redirect destination from the callback request. Set this explicitly in Production when ZarinPal is enabled.
- `ZarinPal__TimeoutSeconds` (optional): HTTP timeout in seconds; it must be between 1 and 59 because the application uses a two-minute lease before retrying a gateway request with an unknown outcome.

The application's canonical monetary unit is **Toman**. `SellerOffer.Price`, order item prices and totals, order totals, and payment amounts are all stored and exchanged by the application in Toman. The ZarinPal v4 adapter converts Toman to Rials (multiply by 10) only when creating or verifying a provider payment. No other application layer should perform this conversion.

Before deploying this change against a database containing existing business data, verify that its stored prices and totals are already interpreted as Toman. If any existing values were entered as Rials, migrate or normalize those values before enabling the gateway; do not try to compensate with a runtime setting. The adapter rejects amounts that cannot be represented as a whole number of Rials.

For the provider flow, `POST /api/payments/orders/{orderId}/initiate` creates a local payment, requests an Authority, stores it, and returns `RedirectUrl`. The public callback uses only the saved payment amount and Authority and verifies them server-to-server before confirming an `OK` payment. An initial provider callback with `Status=NOK` (before any server-side Verify attempt) is recorded as a failed attempt without calling Verify, in line with the ZarinPal integration guide. A late or duplicate `NOK` callback cannot override a payment that has already entered server-side verification; that uncertain payment remains pending for reconciliation. Failed attempts leave the order awaiting payment and permit a new payment attempt. Verification codes `100` and `101` are treated as successful; the returned `ref_id` is persisted. After processing the callback, the API responds with an HTTP redirect to `Frontend__PaymentResultUrl`, adding `orderId` and `status`. A duplicate `OK` callback received inside the persisted two-minute Verify lease does not make a concurrent provider call; it redirects with the current `pending` state. The frontend status is one of `success`, `pending`, `failed`, `manual_review`, or `refunded`; it is a display hint only. The customer can refresh `GET /api/orders/my/{orderId}` to read the authoritative order status; detailed manual-review metadata is exposed only through the Admin order endpoints.

A timeout, transport exception, incomplete response body, malformed provider response, transient HTTP response, or unknown verification code must not be treated as proof that a payment failed. The local payment remains `Pending`. The current conservative verification policy treats only provider code `-51` as a definitive failure; all other non-success outcomes remain indeterminate. The current [ZarinPal Verify documentation](https://www.zarinpal.com/docs/sdk/nodejs/method/verify) documents `100`/`101` success and shows other results as failed, but it does not publish a complete error-code table that safely distinguishes every permanent rejection from a transient/ambiguous result. Do not add more automatic-failure codes without confirming their meaning against current official provider documentation and sandbox behavior.

For an initiation with no saved Authority, the same local `PaymentId` may be retried after its two-minute request lease expires; a recent request is blocked to avoid concurrent provider requests. The expiration worker also respects this lease and does not release stock while a checkout-session request may still be completing. Verification attempts use a separate persisted two-minute retry delay. The `ZarinPalPaymentReconciliationBackgroundService` runs immediately on application startup and then every `OrderExpiration__ReconciliationIntervalSeconds` (default 120 seconds). It independently selects pending ZarinPal payments that already have an Authority and at least one previous verification attempt whose two-minute lease has elapsed; it does not wait for the 30-minute order-expiration window. It invokes the same application verifier as the callback and expiration flow, so callback, reconciliation, and expiration share one attempt counter, one lease, and one state-transition policy. Payments with no previous verification are first verified by the customer callback or, as a final safeguard, by the expiration handler after the payment window. The migration backfills existing payments that already have `LastVerificationAttemptedAt` with that timestamp as their first known attempt and a minimum attempt count of one; the exact historical count cannot be reconstructed from the previous schema. The two-minute request lease is deliberately longer than the maximum configured HTTP timeout, leaving a recovery buffer after the HTTP timeout. ZarinPal's v4 request contract does not expose a provider idempotency key in the sample payload, so this local retry mechanism does not claim provider-side idempotency; any Authority from a timed-out request is unknown and is never returned to the client.

The 30-minute payment window is measured from the most recently created local `Payment` attempt, falling back to `Order.CreatedAt` if no payment exists. Starting a new payment attempt therefore gives the customer a fresh payment window; retrying the same local payment or repeating a Verify call does not extend it. The expiration query and the `InitiatePayment` handler use the same anchor.

Before an expired order with a pending ZarinPal payment that has an Authority releases reserved inventory, the expiration handler performs a final server-side verification when no other verification lease is active. A confirmed payment moves the order to `Confirmed`; a definitive `-51` verification failure allows the order to expire; an indeterminate result keeps the payment, order, and reserved stock intact. The independent reconciliation worker continues retrying indeterminate payments before the order-expiration window closes. Customer-initiated cancellation follows the same safety rule: when a ZarinPal Authority exists, the API performs server-side Verify and only permits cancellation after a definitive `-51` rejection; a successful, indeterminate, or currently leased verification keeps the order and reserved inventory intact. If session creation is still inside its two-minute lease and no Authority has been persisted, cancellation is rejected until that request settles. Verification attempts and outcomes are persisted around the provider call so a process restart does not reset the retry budget. With the defaults `OrderExpiration__ReconciliationIntervalSeconds=120`, `OrderExpiration__MaxVerificationAttempts=5`, and `OrderExpiration__MaxVerificationAgeMinutes=20`, the system performs at most five verification attempts and stops automatic verification after 20 minutes from the first attempt. Once either limit is reached, the payment moves to `RequiresManualReview`; the order remains `AwaitingPayment`, and reserved inventory is deliberately not released. This state appears in the admin order list and payment details, including attempt count, last provider error code, and review timestamp. It requires an operational/manual decision and is not automatically cancelled.

Manual review is resolved only through the Admin payment-resolution workflow documented in [admin-order-management.md](admin-order-management.md). An administrator can retry Verify after the existing two-minute lease expires, manually approve with a positive provider `refId`, or reject and cancel the order with stock restoration. A retry preserves the historical attempt count and timestamps; an indeterminate retry returns to `RequiresManualReview`. Approval confirms the order. Rejection, order cancellation, reserved-stock restoration, and the completed audit record run in one database transaction. Each accepted operation records the administrator, UTC creation/completion times, action, reason, provider reference when supplied, final statuses, and restoration totals. Manual approval requires external evidence and must not be used as a refund operation.

The mock initiation/completion endpoints are mapped only in Development.
