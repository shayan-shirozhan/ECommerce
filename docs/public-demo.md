# Public Demo Environment

## Status

The Demo environment boundary is introduced in phase B5.1. The dedicated deterministic payment gateway is registered, but public Demo payment endpoints and demo catalog/user seeding are intentionally delivered in phase B5.2.

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

## Required environment configuration

Configure these values in the Demo deployment secret/environment configuration:

```text
ASPNETCORE_ENVIRONMENT=Demo
DemoPayments__Enabled=true
ConnectionStrings__DemoECommerceDb=Host=<demo-db-host>;Database=ecommerce_demo;Username=<user>;Password=<secret>
ZarinPal__Enabled=false
```

Do not reuse the Production database or Production credentials. Keep the Demo database credentials separate and grant only the permissions required by the Demo application.

## Payment behavior

`DemoPaymentGateway` always returns a successful simulated result with a stable transaction identifier derived from the local payment ID. It does not contact ZarinPal or transfer funds. The existing Development `MockPaymentGateway` remains randomized and is not used in Demo.

The payment endpoints and seeded Demo accounts/catalog are not yet enabled by this first slice. They are added in phase B5.2 after the environment boundary is established.
