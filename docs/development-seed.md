# Development Seed Data

The API applies migrations and seeds demo data only when `ASPNETCORE_ENVIRONMENT=Development`. The current startup flow calls `DatabaseSeeder.SeedRolesAsync` and `DatabaseSeeder.SeedDevelopmentDataAsync` after the Development database migrations have completed.

## Demo accounts

All demo accounts use the same local-only password:

```text
Password123!
```

| Role | Email | Purpose |
|---|---|---|
| Admin | `admin@test.com` | Administrative catalog and order operations |
| Seller | `seller@test.com` | First seller account |
| Seller | `seller2@test.com` | Second seller account for multi-seller orders |
| Customer | `customer@test.com` | Customer checkout and order history |

These accounts are for local Development use only. Do not use these credentials in shared, staging, or production environments.

## Catalog

The seed creates or ensures the following sample catalog:

- 5 brands: Apple, Samsung, Sony, Logitech, and Philips.
- 4 root categories and 4 child categories:
  - Mobile Phones
  - Computers & Accessories → Laptops, Keyboards & Mice
  - Audio → Headphones
  - Home & Kitchen → Small Appliances
- 6 active products and 12 active variants, each variant identified by a stable SKU.
- 18 product and variant image records using public HTTPS image URLs.
- 18 active seller offers across two seller accounts, including competing offers for selected variants.

Offer prices are sample amounts in Toman and stock values are for local demonstration only.

## Idempotency

The seeder uses stable lookup keys (user email, brand name, category name plus parent, product identity, variant SKU, image identity, and seller/variant pair). Running the API repeatedly in Development does not create duplicate demo records. Existing inactive products, variants, offers, and suspended seller accounts are not automatically reactivated by a subsequent seed run.
