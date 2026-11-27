# Public Demo Environment

## Status

The Demo environment boundary and its first payment slice are implemented. The isolated Demo environment exposes dedicated initiate/complete payment endpoints backed by the deterministic `DemoPaymentGateway`. Demo account/catalog seeding, API documentation exposure, deployment automation, and the remaining public-demo operational controls are still pending.

## Isolation rules

- `ASPNETCORE_ENVIRONMENT=Demo` is a dedicated environment; it is not Development.
- `DemoPayments:Enabled` must be explicitly set to `true` in Demo.
- Enabling `DemoPayments:Enabled` in Development, Production, or another environment stops startup.
- Demo requires `ConnectionStrings:DemoECommerceDb`; it never falls back to `ConnectionStrings:ECommerceDb`.
- Startup rejects a Demo connection string that resolves to the same PostgreSQL host, port, and database name as the normal application connection string, even when credentials differ. The deployment must also ensure that different hostnames or proxies do not point to the same underlying database resource; that physical infrastructure aliasing cannot be proven from a connection string alone.
- ZarinPal must remain disabled in Demo. Demo registers only its deterministic mock gateway.
- Development retains its existing randomized mock gateway and Development email sender.
- Demo does not register Development-only services or enable Development email logging. OpenAPI/Scalar exposure is a separate pending decision; the current Demo configuration does not expose either endpoint.
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

### Demo payment endpoints

The authenticated customer payment flow uses these routes only when `ASPNETCORE_ENVIRONMENT=Demo` and `DemoPayments:Enabled=true`:

- `POST /api/demo/payments/orders/{orderId}/initiate` creates a pending payment using `PaymentMethod.Mock`.
- `POST /api/demo/payments/{paymentId}/complete` completes that pending payment through `DemoPaymentGateway`.

Both commands retain the existing ownership, order-state, payment-window, and payment-state checks. The completion handler only finds a payment belonging to the authenticated customer. The endpoints are not mapped in Development, Production, or other environments. The Development-only `/api/payments/.../initiate-mock` and `/complete-mock` routes remain unchanged.

The deterministic gateway always simulates success, returns a stable transaction identifier derived from the payment ID, and never contacts a provider or transfers real funds.

Demo account/catalog seeding and the remaining operational controls are still pending.
