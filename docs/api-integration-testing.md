# API Integration Tests

The API integration suite boots the real ASP.NET Core application with `WebApplicationFactory<Program>` and a PostgreSQL instance created by Testcontainers.

## Test environment

- `ASPNETCORE_ENVIRONMENT` is set to `Development` so the normal application startup applies migrations and seeds the standard roles, admin, seller, brand, and category.
- The test host sets the API project as its content root, overrides `ConnectionStrings:ECommerceDb` with the Testcontainers connection string, and uses a test-only JWT signing key (no machine user-secrets dependency).
- The fixture sets bootstrap environment variables before `CreateClient`, because top-level `Program` reads the database and JWT configuration before the factory's in-memory configuration callback is applied. Original process environment values are restored during teardown.
- The ZarinPal `IPaymentGateway` is replaced with a deterministic fake. No request is sent to a real payment provider.
- The order-expiration and payment-reconciliation hosted services are removed from automatic host startup. Worker behavior is started explicitly by the test that exercises it.
- Authentication endpoints that issue, rotate, or revoke refresh cookies receive `Origin: http://localhost`, as required by the production CSRF middleware.

## Coverage

`ApiEndToEndTests` exercises the HTTP pipeline and PostgreSQL persistence for:

- Registering a customer and seller applicant, submitting a seller request, and approving it as Admin.
- Creating a product variant and seller offer, adding an item to the cart, creating an order, initiating a fake ZarinPal payment, and completing seller shipping transitions through Delivered.
- Anonymous, Customer, Seller, and Admin authorization boundaries.
- Refresh-cookie issuance, rotation, replay detection, family revocation, and logout.
- ZarinPal callback success, NOK without Verify, and an indeterminate Verify result while reserved inventory remains unchanged; a late NOK cannot overwrite an earlier indeterminate Verify.
- Direct execution of `OrderExpirationBackgroundService` against its real scoped repository and MediatR dependencies.

## Run

Docker must be running because Testcontainers starts PostgreSQL containers.

```powershell
dotnet test .\ECommerce.slnx --filter FullyQualifiedName~ECommerce.Tests.API
```

Run the full regression suite with:

```powershell
dotnet test .\ECommerce.slnx
```
