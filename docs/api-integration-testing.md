# API Integration Tests

The API integration suite boots the real ASP.NET Core application with `WebApplicationFactory<Program>` and a PostgreSQL instance created by Testcontainers.

## Test environment

- `ASPNETCORE_ENVIRONMENT` is set to `Development` so the normal application startup applies migrations and seeds the standard roles, admin, seller, brand, and category.
- The test host sets the API project as its content root, overrides `ConnectionStrings:ECommerceDb` with the Testcontainers connection string, and uses a test-only JWT signing key (no machine user-secrets dependency).
- The fixture sets bootstrap environment variables before `CreateClient`, because top-level `Program` reads the database and JWT configuration before the factory's in-memory configuration callback is applied. Original process environment values are restored during teardown.
- The ZarinPal `IPaymentGateway` is replaced with a deterministic fake. No request is sent to a real payment provider.
- `IEmailSender` is replaced with a capturing test double in API E2E tests. Password-reset tokens are read from the captured message, never logged.
- The real `IRefreshTokenService` remains registered in API E2E tests. A five-second test rotation grace window allows the second-tab `409` / `Retry-After` response to be verified through the real refresh endpoint without timing a simultaneous request race.
- The order-expiration and payment-reconciliation hosted services are removed from automatic host startup. Worker behavior is started explicitly by the test that exercises it.
- Authentication endpoints that issue, rotate, or revoke refresh cookies receive `Origin: http://localhost`, as required by the production CSRF middleware.

## Coverage

`ApiEndToEndTests` exercises the HTTP pipeline and PostgreSQL persistence for:

- Registering a customer and seller applicant, submitting a seller request, and approving it as Admin.
- Creating a product variant and seller offer, adding an item to the cart, creating an order, initiating a fake ZarinPal payment, and completing seller shipping transitions through Delivered.
- Anonymous, Customer, Seller, and Admin authorization boundaries.
- Refresh-cookie issuance, rotation, replay detection, family revocation, logout, and the `409 Conflict` / `Retry-After` path used by a second browser tab.
- Forgot-password delivery, token-based password reset, invalidation of previous refresh sessions, and password change through HTTP.
- Authenticated `/api/users/me` profile retrieval and update.
- Wishlist add/list/remove and address create/list/update/default/delete flows through HTTP.
- ZarinPal callback success, NOK without Verify, and an indeterminate Verify result while reserved inventory remains unchanged; a late NOK cannot overwrite an earlier indeterminate Verify.
- Direct execution of `OrderExpirationBackgroundService` against its real scoped repository and MediatR dependencies.
- A real `SmtpEmailSender` delivery to an isolated Mailpit Testcontainers instance, verified through Mailpit's HTTP API.

## Run

Docker must be running because Testcontainers starts PostgreSQL and Mailpit containers. The Mailpit integration test uses the same pinned Mailpit image as the local Development Compose stack and a random host-port mapping.

```powershell
dotnet test .\ECommerce.slnx --configuration Release --filter "FullyQualifiedName~ECommerce.Tests.API|FullyQualifiedName~ECommerce.Tests.Infrastructure.Email.SmtpEmailSenderIntegrationTests"
```

Run the full regression suite with:

```powershell
dotnet test .\ECommerce.slnx --configuration Release
```
