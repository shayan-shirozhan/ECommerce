# ECommerce

A multi-seller marketplace backend built with ASP.NET Core and PostgreSQL on .NET 10. The solution follows Clean Architecture and uses CQRS/MediatR, FluentValidation, Entity Framework Core, ASP.NET Core Identity, and JWT bearer authentication.

## Solution structure

- `ECommerce.Domain` — domain entities, enums, and business rules.
- `ECommerce.Application` — use cases, CQRS handlers, validation, and application abstractions.
- `ECommerce.Infrastructure` — EF Core persistence, PostgreSQL mappings, Identity, JWT, and repositories.
- `ECommerce.API` — HTTP controllers, middleware, dependency-injection entry point, and development API documentation.
- `ECommerce.Tests` — unit and PostgreSQL integration tests.

## Quick start with Docker Compose

Requirements: Docker Desktop (or Docker Engine with Compose) and a working internet connection to pull the .NET and PostgreSQL images.

1. Copy the environment template to a local, untracked file.

   **PowerShell**
   ```powershell
   Copy-Item .env.example .env
   ```

   **Bash**
   ```bash
   cp .env.example .env
   ```

2. For local development, the values in `.env.example` are usable. Do not reuse them in shared, staging, or production environments.

3. Build and start PostgreSQL and the API.

   ```bash
   docker compose up --build -d
   docker compose logs -f api
   ```

   The API waits for PostgreSQL to become healthy. When running in `Development`, it applies EF Core migrations before seeding roles and sample development data.

4. Open the interactive API reference at **http://localhost:8080/scalar**. The OpenAPI JSON document is available at **http://localhost:8080/openapi/v1.json**. These endpoints are intentionally enabled only in the Development environment.

5. Stop the containers while retaining the database volume:

   ```bash
   docker compose down
   ```

   To delete the local database volume as well (this permanently removes the Compose database data):

   ```bash
   docker compose down --volumes
   ```

The API and PostgreSQL ports bind to loopback (`127.0.0.1`) so they are not exposed on every network interface by default. Set `POSTGRES_PORT` in `.env` if local port 5432 is already in use; when running the API on the host, use that same host port in its connection string.

## Run the API from the host

To use the local .NET SDK with a containerized database instead of the API container:

```bash
docker compose up -d postgres
```

Configure the API project's user secrets (PowerShell or Bash):

```bash
dotnet user-secrets set "ConnectionStrings:ECommerceDb" "Host=localhost;Port=5432;Database=ecommerce;Username=postgres;Password=local-dev-password" --project ECommerce.API
dotnet user-secrets set "Jwt:SecretKey" "development-only-change-this-to-a-random-secret-with-at-least-32-bytes" --project ECommerce.API
dotnet run --project ECommerce.API
```

The local API uses the URLs from `ECommerce.API/Properties/launchSettings.json`. In Development, open `/scalar` on the printed HTTP or HTTPS address. The values above match the default `.env.example`; if you change `POSTGRES_PASSWORD` in `.env`, update the user-secret connection string accordingly.

## Development accounts

The Development seeder creates the following sample accounts:

| Role | Email | Password |
|---|---|---|
| Admin | `admin@test.com` | `Password123!` |
| Seller | `seller@test.com` | `Password123!` |

These credentials are for local development only. They are seeded only in Development and must never be used in a deployed environment.

## Build and tests

```bash
dotnet restore ECommerce.slnx
dotnet build ECommerce.slnx --configuration Release
dotnet test ECommerce.slnx --configuration Release
```

The integration tests use Testcontainers and require Docker to be available.

## Configuration and deployment notes

- The API requires the `ConnectionStrings:ECommerceDb` configuration value and `Jwt:SecretKey`, `Jwt:Issuer`, and `Jwt:Audience` settings. Environment variables can supply these using double underscores (for example, `Jwt__SecretKey`).
- JWT secrets must be at least 32 bytes long. Keep real credentials in a secret manager or environment configuration, not in source control.
- EF Core migrations run automatically only when `ASPNETCORE_ENVIRONMENT=Development`. Apply migrations as an explicit deployment step in non-development environments before starting the API. The pending-seller-request migration requires at most one pending request per user; check an existing database before applying migrations:

  ```sql
  SELECT "UserId", COUNT(*)
  FROM "SellerRequests"
  WHERE "Status" = 1
  GROUP BY "UserId"
  HAVING COUNT(*) > 1;
  ```
- Scalar and the generated OpenAPI document are exposed only in Development by default. Do not enable the Development environment in a public deployment.
- Cart, order, and payment entities exist in the domain/persistence model, but their end-to-end application use cases and API endpoints are not yet implemented.
