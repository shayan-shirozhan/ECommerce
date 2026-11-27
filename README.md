# ECommerce — Multi-Vendor Marketplace Backend

A multi-vendor marketplace backend built with **.NET 10**, **ASP.NET Core**, and **PostgreSQL**. The project is designed as a portfolio-quality backend: it demonstrates the core customer purchase journey, seller-specific order fulfillment, payment verification and reconciliation, and role-protected administration APIs.

> **Project status:** MVP backend in active development. The repository contains the API and its automated tests; a production frontend is not included.

## What this project demonstrates

- Clean Architecture with separate Domain, Application, Infrastructure, and API projects.
- CQRS request handling with MediatR and FluentValidation.
- ASP.NET Core Identity, JWT access tokens, and refresh-token rotation.
- Multi-vendor checkout with seller-specific `OrderGroup` records.
- Inventory reservation during order creation and stock restoration on permitted cancellation paths.
- Optimistic concurrency for sensitive state transitions.
- ZarinPal payment initiation, callback verification, background reconciliation, and administrative manual resolution.
- PostgreSQL persistence with EF Core migrations.
- NUnit tests, including PostgreSQL integration tests with Testcontainers and API end-to-end tests with `WebApplicationFactory`.
- Development-only marketplace seed data for a quick local demonstration.

## Technology stack

| Area | Technologies |
|---|---|
| Runtime and API | .NET 10, ASP.NET Core Web API |
| Architecture | Clean Architecture, CQRS, MediatR |
| Validation | FluentValidation |
| Persistence | PostgreSQL, Entity Framework Core |
| Identity and security | ASP.NET Core Identity, JWT, HttpOnly refresh-token cookie, token rotation and replay detection |
| Payments | ZarinPal integration, payment verification and reconciliation, development-only mock endpoints |
| Logging and health | Serilog, `/health/live`, `/health/ready` |
| API exploration | OpenAPI, Scalar (Development only) |
| Testing | NUnit, Testcontainers, `WebApplicationFactory` |
| Local environment | Docker, Docker Compose |

## Solution architecture

The solution is split by responsibility. The Domain project does not depend on outer layers; Application contains use cases and ports; Infrastructure implements persistence and external integrations; API is the HTTP composition and delivery layer.

```mermaid
flowchart TB
    Client[Browser / HTTP Client] --> API[ECommerce.API<br/>Controllers, middleware, DI]
    API --> App[ECommerce.Application<br/>CQRS handlers, validators, ports]
    API --> Infra[ECommerce.Infrastructure<br/>EF Core, PostgreSQL, Identity, JWT, gateways]
    App --> Domain[ECommerce.Domain<br/>Entities, value rules, state transitions]
    Infra --> App
    Infra --> Domain
    Infra --> DB[(PostgreSQL)]
    Infra --> ZP[ZarinPal]
    Tests[ECommerce.Tests<br/>NUnit, Testcontainers, WebApplicationFactory]
    Tests --> API
    Tests --> App
    Tests --> Domain
    Tests --> Infra
```

### Projects

| Project | Responsibility |
|---|---|
| `ECommerce.Domain` | Domain entities, enums, invariants, and business state transitions. |
| `ECommerce.Application` | CQRS commands/queries, handlers, validators, response contracts, and abstractions. |
| `ECommerce.Infrastructure` | EF Core mappings and repositories, PostgreSQL, Identity/JWT, payment and email integrations, background services. |
| `ECommerce.API` | HTTP controllers, authentication/authorization configuration, middleware, dependency injection, health checks, and API documentation. |
| `ECommerce.Tests` | Domain and application tests, PostgreSQL integration tests, and API end-to-end tests. |

## Quick start — Docker Compose

The quickest way to run the API and PostgreSQL locally is Docker Compose.

### Prerequisites

- Docker Desktop (or Docker Engine with the Compose plugin).
- Internet access to pull the .NET and PostgreSQL images.

### 1. Create your local environment file

PowerShell:

```powershell
Copy-Item .env.example .env
```

Bash:

```bash
cp .env.example .env
```

The template values are intended for local development only. Do not reuse them in shared, staging, or production environments. Keep `.env` out of source control.

### 2. Start the application

```bash
docker compose up --build -d
docker compose logs -f api
```

The API waits for PostgreSQL to become healthy. In the `Development` environment, startup applies EF Core migrations and then seeds development roles, accounts, and marketplace catalog data.

### 3. Open the API

- Scalar: <http://localhost:8080/scalar/v1>
- OpenAPI JSON: <http://localhost:8080/openapi/v1.json>
- Liveness: <http://localhost:8080/health/live>
- Readiness: <http://localhost:8080/health/ready>

Scalar and the generated OpenAPI document are intentionally exposed only in Development. Do not deploy the application with `ASPNETCORE_ENVIRONMENT=Development`.

### 4. Stop the application

Stop containers and preserve the database volume:

```bash
docker compose down
```

To remove the local database volume as well (this permanently deletes the local database data):

```bash
docker compose down --volumes
```

The API and PostgreSQL ports bind to loopback by default. The supplied `.env.example` publishes PostgreSQL on host port `15432` (`POSTGRES_PORT=15432`) while the container itself listens on `5432`. Change `POSTGRES_PORT` if `15432` is already in use; when running the API on the host, use the same host port in its connection string. The API container connects to `postgres:5432` over the Compose network.

## Run the API from the host

Use this option when you have the .NET 10 SDK installed and want to run the API outside Docker while PostgreSQL remains containerized.

```bash
docker compose up -d postgres
```

Configure local user secrets (PowerShell or Bash):

```bash
dotnet user-secrets set "ConnectionStrings:ECommerceDb" "Host=localhost;Port=15432;Database=ecommerce;Username=postgres;Password=local-dev-password" --project ECommerce.API
dotnet user-secrets set "Jwt:SecretKey" "development-only-change-this-to-a-random-secret-with-at-least-32-bytes" --project ECommerce.API
```

Then run:

```bash
dotnet run --project ECommerce.API --launch-profile http
```

The API uses the URLs configured in `ECommerce.API/Properties/launchSettings.json`; the `http` profile listens on `http://localhost:5115`. Open `/scalar/v1` on that address. If you use the `https` profile instead, use `https://localhost:7102`. The default host-side PostgreSQL port in `.env.example` is `15432`; if you change `POSTGRES_PORT` or `POSTGRES_PASSWORD`, update the connection string accordingly.

## Development demo accounts and catalog

The Development seeder creates local-only accounts with the following roles:

| Role | Email | Password |
|---|---|---|
| Admin | `admin@test.com` | `Password123!` |
| Seller | `seller@test.com` | `Password123!` |
| Seller | `seller2@test.com` | `Password123!` |
| Customer | `customer@test.com` | `Password123!` |

These credentials must never be used outside a local Development environment.

The demo catalog includes multiple brands, a hierarchical category tree, products, variants, HTTPS product images, and seller offers from two active sellers. Catalog seeding is designed to be idempotent: restarting the Development API should not create duplicate demo catalog records. See [Development seed](docs/development-seed.md).

## Five-minute API demo

The repository includes an HTTP request collection at [`ECommerce.API/ECommerce.API.http`](ECommerce.API/ECommerce.API.http). Run it with the REST Client extension in Visual Studio Code or another editor that supports `.http` files. The collection contains the customer, seller, admin, cart, order, payment, and fulfillment requests used below.

1. Start the stack with `docker compose up --build -d` and wait until the API is listening. Open Scalar at <http://localhost:8080/scalar/v1>.
2. In the `.http` file, set `@ECommerce_API_HostAddress` to `http://localhost:8080` for Docker Compose. For the host-run `http` launch profile, use `http://localhost:5115`; the `https` profile uses `https://localhost:7102`.
3. Run **Login - Seller 1**, copy its `accessToken` into `@SellerToken`, then run **Get My Seller Offers** and copy one active offer ID into `@SellerOfferId`. Run **Login - Seller 2**, replace `@SellerToken` with its `accessToken`, run **Get My Seller Offers** again, and copy one active offer ID into `@SecondSellerOfferId`. The offer IDs must belong to different sellers.
4. Sign in as `customer@test.com` and copy the returned access token into `@CustomerToken`. Run **Add First Seller Offer to Cart**, **Add Second Seller Offer to Cart**, then **Get My Cart**.
5. Run **Create Order from Cart**. The response contains the new `orderId`; copy it into `@OrderId`. This creates the order and reserves stock. Run **Initiate Mock Payment** and copy the returned `paymentId` into `@PaymentId`, then run **Complete Mock Payment**. These mock endpoints are Development-only.
6. Sign in as each seller in turn, replacing `@SellerToken` with that seller's `accessToken`, and run **Get Seller Orders** to inspect its order group. The existing status-update requests demonstrate processing, shipment tracking, and delivery. Sign in as Admin to inspect `GET /api/admin/orders` and the payment-resolution operations when a payment requires manual review.

The `.http` collection uses placeholders for tokens and IDs because they are generated by the running application; copy values from each response into the corresponding variables at the top of the file. For local checkout, use only the Development mock flow. ZarinPal is disabled by default until its configuration is explicitly supplied.

> Mock payment completion uses a simulated gateway result and may
> return either success or failure. Always inspect the response
> before proceeding; a failed payment is an expected demo outcome.

## Architecture and important workflows

### Multi-vendor order and inventory model

A customer order can contain items sold by more than one seller. The system represents the purchase as one `Order` with seller-specific `OrderGroup` records. Each group contains only the items and fulfillment state belonging to that seller. This keeps seller order visibility and shipment updates isolated while preserving a single customer purchase.

Inventory is reserved as part of order creation. The reservation is not released merely because a payment result is uncertain: an order that requires manual payment review remains in `AwaitingPayment` and its reserved stock is retained. Stock is restored only through permitted cancellation/expiration or the explicit administrative rejection-and-cancellation path.

### Order lifecycle

```mermaid
stateDiagram-v2
    [*] --> Pending: order created from cart / stock reserved
    Pending --> AwaitingPayment: payment initiated
    Pending --> Cancelled: permitted cancellation before payment
    AwaitingPayment --> Confirmed: payment succeeds
    AwaitingPayment --> Cancelled: permitted cancellation or safe expiration
    Confirmed --> Processing: seller-group fulfillment starts
    Processing --> Shipped: all seller groups are shipped or delivered
    Shipped --> Delivered: all seller groups are delivered
    Cancelled --> [*]
    Delivered --> [*]
```

Seller fulfillment progresses independently on each `OrderGroup` (`Pending` → `Processing` → `Shipped` → `Delivered`, or `Pending` → `Cancelled`). The aggregate `Order` status is derived from the states of its seller groups; a multi-seller order can remain `Processing` while different groups are at different fulfillment stages.

### Payment lifecycle

```mermaid
stateDiagram-v2
    [*] --> Pending: payment initiated
    Pending --> Successful: provider verification succeeds
    Pending --> Failed: definitive provider failure (-51)
    Pending --> RequiresManualReview: retry or age threshold reached
    Pending --> Cancelled: safe local cancellation before usable Authority
    RequiresManualReview --> Pending: admin VerifyAgain starts a new attempt
    Pending --> RequiresManualReview: VerifyAgain result remains indeterminate
    Pending --> Failed: VerifyAgain returns definitive -51
    RequiresManualReview --> Successful: admin approval with positive RefId
    RequiresManualReview --> Failed: admin rejection and order cancellation
    Successful --> [*]
    Failed --> [*]
    Cancelled --> [*]
```

Important payment rules:

- ZarinPal callback handling, order expiration, and the reconciliation background service share the same payment verifier to keep gateway-result interpretation consistent.
- Only ZarinPal result code `-51` is treated as a definitive payment failure. Other uncertain outcomes must not be converted into a false failure.
- After five verification attempts or twenty minutes, an unresolved payment moves to `RequiresManualReview`. The order remains `AwaitingPayment` and its stock reservation is retained.
- The payment window is thirty minutes from the most recent payment attempt. Application prices, order totals, and payment amounts are stored in Toman; the ZarinPal adapter converts amounts to Rials when calling the provider.
- Admins can explicitly retry verification, approve with a payment reference ID, or reject the payment and cancel the order. Each resolution action is audited.
- The ZarinPal integration is disabled by default. Mock payment endpoints are restricted to Development.

See [Payment state machine](docs/payment-state-machine.md) and [Admin order management](docs/admin-order-management.md).

## Engineering decisions

| Concern | Approach | Reason |
|---|---|---|
| Domain boundaries | Domain entities own invariants and state transitions; Application handlers coordinate use cases. | Keep business rules independent of HTTP and persistence concerns. |
| Multi-vendor checkout | A single order contains seller-specific `OrderGroup` records. | Preserve one customer purchase while isolating seller fulfillment. |
| Inventory | Reserve stock when the order is created; release it only on an allowed cancellation path. | Avoid overselling while payment is pending or under review. |
| Concurrent updates | Use optimistic concurrency (`RowVersion`) on sensitive aggregate changes. | Detect conflicting updates instead of silently overwriting state. |
| Payment certainty | Share a verifier across callback, expiration, and reconciliation paths. | Avoid inconsistent interpretation of delayed or repeated gateway responses. |
| Ambiguous payment result | Move to `RequiresManualReview`; keep order and stock reservation intact until an explicit resolution. | Do not mark an uncertain payment as failed or release stock prematurely. |
| Refresh tokens | Store refresh-token state server-side, rotate on refresh, and detect replay at the token-family level. | Limit the value of a stolen refresh token and detect reuse. |

## API overview

The API is organized around customer, seller, and administrator workflows. The table below lists the principal API areas and selected routes; the `.http` request collection and Development Scalar/OpenAPI document are the source of truth for the complete route list, parameters, headers, and request bodies.

| API area | Main capabilities | Access |
|---|---|---|
| Authentication | Login, refresh-token rotation, logout, password change, forgot/reset password | Public or authenticated, depending on operation |
| Current user | Read and update the authenticated profile via `GET /api/users/me` and `PUT /api/users/me` | Authenticated user |
| Catalog | `GET /api/brands`, `GET /api/categories`, `GET /api/products`; product browsing, filtering, and variant management | Public reads; protected management operations |
| Cart and wishlist | `GET /api/cart`, `POST /api/cart/items`, update/remove cart items, and manage saved products | Authenticated user (customer workflow) |
| Addresses | Create, list, update, delete, and select a default shipping address | Authenticated user |
| Customer orders | `POST /api/orders/from-cart`, list and inspect the customer's orders, and cancel eligible orders | Authenticated user (customer workflow) |
| Seller operations | View seller-specific orders and update fulfillment/shipping information for owned order groups | Seller |
| Admin order management | `GET /api/admin/orders` and `GET /api/admin/orders/{orderId}`; filter orders with `hasManualReview` and inspect marketplace orders | Admin |
| Admin payment resolution | `POST /api/admin/payments/{paymentId}/resolve`; retry verification, approve with a reference ID, or reject and cancel | Admin |
| Operational | `GET /health/live` and `GET /health/ready` | Health probes |

### Origin header for non-browser clients

Login, refresh, and logout validate the request `Origin` (or, when `Origin` is absent, `Referer`) as part of the browser-origin protection. The source origin must match the API request origin or one of the configured `Cors:AllowedOrigins`. Browser clients should send requests with credentials enabled so the HttpOnly refresh cookie is included. HTTP clients, command-line tools, and other non-browser clients must send an `Origin` header for these authentication endpoints; the `.http` collection demonstrates this with the API base address.

Example:

```http
Origin: https://your-frontend.example
```

Use the actual origin configured for your environment. Do not copy the example domain literally.

### Frontend deployment note

The refresh cookie uses `SameSite=Strict`. A browser frontend should therefore be deployed same-site with the API (for example, behind the same reverse proxy or under sibling subdomains of the same site with HTTPS and correct cookie/CORS configuration). A cross-site frontend deployment is not supported by the current cookie policy without changing that design.

## Configuration and deployment notes

- Required configuration includes `ConnectionStrings:ECommerceDb` and JWT settings (`Jwt:SecretKey`, `Jwt:Issuer`, and `Jwt:Audience`). Environment variables can use double underscores, for example `Jwt__SecretKey`.
- JWT signing secrets must be at least 32 bytes. Store real credentials in a secret manager or deployment environment, never in source control.
- EF Core migrations are applied automatically only in Development. Apply migrations as an explicit deployment step in non-development environments before starting the API.
- Scalar and generated OpenAPI are Development-only by default. Do not enable the Development environment on a public deployment.
- ZarinPal is disabled by default; configure it explicitly before enabling real payment traffic. Keep merchant credentials and callback/frontend URLs outside source control.
- Development seed accounts and catalog data are not production bootstrap data. Production admin creation uses the explicit `--bootstrap-admin` workflow with `BootstrapAdmin__Email`, `BootstrapAdmin__Password`, `BootstrapAdmin__FirstName`, and `BootstrapAdmin__LastName` supplied through secure configuration. The command is accepted only in the Production environment.
- Persist ASP.NET Core Data Protection keys in a durable location in deployed environments. Ephemeral container filesystems can invalidate protected tokens after a restart.

## Build and tests

Restore and build:

```bash
dotnet restore ECommerce.slnx
dotnet build ECommerce.slnx --configuration Release
```

Run the complete test suite:

```bash
dotnet test ECommerce.slnx --configuration Release
```

The integration and end-to-end tests use Testcontainers and require Docker to be available. The API end-to-end test setup and coverage are described in [API integration testing](docs/api-integration-testing.md).

## Documentation

- [Development seed](docs/development-seed.md) — seeded accounts, demo catalog, and idempotency behavior.
- [Public Demo environment](docs/public-demo.md) — isolated Demo deployment, configuration-provisioned seed accounts/catalog, and simulated payment endpoints.
- [Payment state machine](docs/payment-state-machine.md) — payment verification, reconciliation, manual review, and resolution paths.
- [Admin order management](docs/admin-order-management.md) — admin order listing, details, filters, and payment operations.
- [API integration testing](docs/api-integration-testing.md) — `WebApplicationFactory`, Testcontainers, and end-to-end test setup.
- [Production readiness](docs/production-readiness.md) — configuration and deployment considerations.

## Scope

This repository focuses on the marketplace MVP and its core purchase lifecycle. Coupons, product reviews/ratings, refunds, seller settlement/commission accounting, and notification delivery are intentionally outside the current MVP scope.
