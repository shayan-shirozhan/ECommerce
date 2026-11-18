# Admin order management

The admin order endpoints are read-only and require the `Admin` role.

## List orders

`GET /api/admin/orders`

Query parameters:

| Parameter | Required | Default | Description |
|---|---:|---:|---|
| `page` | No | `1` | One-based page number |
| `pageSize` | No | `10` | Page size from 1 through 100 |
| `status` | No | — | Exact `OrderStatus` enum value: `Pending=1`, `AwaitingPayment=2`, `Confirmed=3`, `Processing=4`, `Shipped=5`, `Delivered=6`, `Cancelled=7` |
| `createdFromUtc` | No | — | Inclusive lower bound for `Order.CreatedAt` |
| `createdToUtc` | No | — | Inclusive upper bound for `Order.CreatedAt` |

Date filters accept ISO 8601 timestamps with an explicit UTC offset. The application converts them to UTC before querying PostgreSQL. When both bounds are supplied, `createdFromUtc` must be earlier than or equal to `createdToUtc`.

The response contains `Items`, `Page`, `PageSize`, and `TotalCount`. Each item includes the order and customer identifiers, customer email/name, order status, creation time, total amount in Toman, seller-group count, total item count, and `HasPaymentRequiringManualReview`. A value of `true` means a payment result could not be determined within the configured verification retry budget; the order remains `AwaitingPayment` and its reserved inventory remains held. Pending ZarinPal payments are retried by the independent reconciliation worker every two minutes by default; this process is not delayed until the order's 30-minute expiration window has elapsed. Once the configured attempt or age limit is reached, the payment is marked `RequiresManualReview` and is excluded from automatic expiration.

Example:

```http
GET /api/admin/orders?page=1&pageSize=20&status=2&createdFromUtc=2026-11-01T00:00:00%2B00:00&createdToUtc=2026-11-18T23:59:59%2B00:00
Authorization: Bearer <admin-access-token>
```

## Order details

`GET /api/admin/orders/{orderId}`

Returns the complete order snapshot: customer details, shipping address, all seller groups and their items, tracking information, and payment attempts. Payment details intentionally omit the provider `Authority` value and other checkout-session credentials; they expose payment method/status, amount, reference ID, lifecycle timestamps, verification attempt count, first/last verification timestamps, the last provider error code, and `ManualReviewRequiredAt`. `PaymentStatus.RequiresManualReview=6` is distinct from `Failed`: it means the payment outcome is unknown, so the order must not be cancelled automatically and inventory must not be released until an operator has reconciled the payment with the provider.

Both endpoints are read-only. They do not change order, payment, inventory, or seller-group state.
