# Public Demo Environment

## Status

The isolated Demo boundary, authenticated deterministic mock payment routes, and configuration-backed Demo account/catalog seed are implemented. OpenAPI/Scalar can be enabled explicitly for Demo. Demo email delivery, periodic data reset, and the dedicated deployment Compose setup remain follow-up work.

## Isolation rules

- `ASPNETCORE_ENVIRONMENT=Demo` is a dedicated environment; it is not Development.
- `DemoPayments:Enabled` must be explicitly set to `true` in Demo.
- Enabling `DemoPayments:Enabled` in Development, Production, or another environment stops startup.
- Demo requires `ConnectionStrings:DemoECommerceDb`; it never falls back to `ConnectionStrings:ECommerceDb`.
- Startup rejects a Demo connection string that resolves to the same PostgreSQL host, port, and database name as the normal application connection string, even when credentials differ. The deployment must also ensure that different hostnames or proxies do not point to the same underlying database resource; that physical infrastructure aliasing cannot be proven from a connection string alone.
- ZarinPal must remain disabled in Demo. Demo registers only its deterministic mock gateway.
- Development retains its existing randomized mock gateway and Development email sender.
- Demo does not register Development-only services, expose Development OpenAPI/Scalar endpoints, or enable Development email logging.
- Demo automatically applies EF Core migrations to its dedicated Demo database. Production migrations remain a separate deployment step.
- Demo seed credentials are validated before the application applies migrations or seeds the Demo database.

## Required environment configuration

Configure these values in the Demo deployment secret/environment configuration. Never commit actual passwords to `appsettings*.json`, `.env.example`, or source control.

```text
ASPNETCORE_ENVIRONMENT=Demo
DemoPayments__Enabled=true
ConnectionStrings__DemoECommerceDb=Host=<demo-db-host>;Database=ecommerce_demo;Username=<user>;Password=<secret>
ZarinPal__Enabled=false
DemoSeed__AdminPassword=<unique-admin-password>
DemoSeed__SellerPassword=<unique-seller-password>
DemoSeed__SecondSellerPassword=<unique-second-seller-password>
DemoSeed__CustomerPassword=<unique-customer-password>
```

All four Demo seed passwords are required, must be different from one another, and must satisfy the configured ASP.NET Core Identity password policy: at least 8 characters, including uppercase, lowercase, digit, and non-alphanumeric characters. Startup fails with a configuration error if any value is missing, weak, or reused. Password values are not written to application logs.

Do not reuse the Production database or Production credentials. Keep Demo database credentials separate and grant only the permissions required by the Demo application. Keep the seeded Admin password operator-only; never publish it or provide it to public Demo visitors.

## API documentation

OpenAPI and Scalar are disabled in Demo by default. To expose the interactive API reference for a public portfolio deployment, set:

```text
DemoApiDocumentation__Enabled=true
```

When enabled, the endpoints are available at `/openapi/v1.json` and `/scalar/v1` on the API host. The setting is accepted only when `ASPNETCORE_ENVIRONMENT=Demo`; enabling it in Development, Production, or another environment stops startup. This setting exposes API metadata and the interactive client only. It does not bypass authentication or authorization on API operations.

Do not enable Development mode on a public deployment.

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

Seeding is idempotent: repeated application startup does not duplicate seeded users or catalog records. Existing inactive products, variants, offers, and suspended seller accounts are not automatically reactivated. The current seed does not delete visitor-created carts, wishlists, orders, or other Demo activity; a separate periodic reset mechanism is still pending.

## Payment behavior

`DemoPaymentGateway` always returns a successful simulated result with a stable transaction identifier derived from the local payment ID. It does not contact ZarinPal or transfer funds. The existing Development `MockPaymentGateway` remains randomized and is not used in Demo.

The authenticated Demo payment flow is:

- `POST /api/demo/payments/orders/{orderId}/initiate` creates a pending payment using `PaymentMethod.Mock`.
- `POST /api/demo/payments/{paymentId}/complete` completes that pending payment through `DemoPaymentGateway`.

Both endpoints are mapped only when the Demo environment guard is enabled. Existing order ownership, payment ownership, order-state, payment-window, and payment-state checks remain in the application handlers. The Development-only `/api/payments/.../initiate-mock` and `/complete-mock` routes are not exposed in Demo.
