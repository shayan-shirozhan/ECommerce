# Money and Currency Policy

The marketplace uses **Toman** as its single application-level monetary unit.

- Seller offer prices, cart totals, order item prices, discounts, order/group totals, and payment amounts are represented in Toman.
- API request and response fields containing monetary values use Toman unless an endpoint explicitly documents otherwise.
- Persist monetary values in PostgreSQL in Toman. Do not persist some records in Rial and others in Toman.
- Convert only at an external provider boundary. The ZarinPal v4 adapter multiplies Toman by 10 to send whole Rials; callers and application handlers must not pre-convert.
- Before adopting this policy on a database with existing business data, verify the unit already used by those records. Normalize any Rial-denominated legacy values before enabling real payments.

The unit is a domain/application contract, not a per-deployment switch. Do not add a runtime `AmountUnit` option to choose between Toman and Rial.
