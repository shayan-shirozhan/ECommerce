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
| `hasManualReview` | No | — | `true` returns only orders with a `RequiresManualReview` payment; `false` excludes those orders |

Date filters accept ISO 8601 timestamps with an explicit UTC offset. The application converts them to UTC before querying PostgreSQL. When both bounds are supplied, `createdFromUtc` must be earlier than or equal to `createdToUtc`.

The response contains `Items`, `Page`, `PageSize`, and `TotalCount`. Each item includes the order and customer identifiers, customer email/name, order status, creation time, total amount in Toman, seller-group count, total item count, and `HasPaymentRequiringManualReview`. A value of `true` means a payment result could not be determined within the configured verification retry budget; the order remains `AwaitingPayment` and its reserved inventory remains held. Pending ZarinPal payments are retried by the independent reconciliation worker every two minutes by default; this process is not delayed until the order's 30-minute expiration window has elapsed. Once the configured attempt or age limit is reached, the payment is marked `RequiresManualReview` and is excluded from automatic expiration.

Example:

```http
GET /api/admin/orders?page=1&pageSize=20&status=2&hasManualReview=true&createdFromUtc=2026-11-01T00:00:00%2B00:00&createdToUtc=2026-11-18T23:59:59%2B00:00
Authorization: Bearer <admin-access-token>
```

## Order details

`GET /api/admin/orders/{orderId}`

Returns the complete order snapshot: customer details, shipping address, all seller groups and their items, tracking information, and payment attempts. Payment details intentionally omit the provider `Authority` value and other checkout-session credentials; they expose payment method/status, amount, reference ID, lifecycle timestamps, verification attempt count, first/last verification timestamps, the last provider error code, and `ManualReviewRequiredAt`. `PaymentStatus.RequiresManualReview=6` is distinct from `Failed`: it means the payment outcome is unknown, so the order must not be cancelled automatically and inventory must not be released until an operator has reconciled the payment with the provider.

## Resolve a payment requiring manual review

`POST /api/admin/payments/{paymentId}/resolve` requires the `Admin` role. The payment ID is available in the `Payments` collection returned by order details. The payment must still be `RequiresManualReview`, its order must still be `AwaitingPayment`, and it must be a ZarinPal payment with a saved Authority. The endpoint can be called again only while the payment remains in `RequiresManualReview`: `VerifyAgain` may return it to this state when verification is indeterminate, while `Approve`, `RejectAndCancel`, and definitive verification results leave it in a state that this endpoint cannot resolve again.

The request requires a non-empty `reason` (maximum 1000 characters). `action` uses the `PaymentResolutionAction` enum values below. `refId` is required only for `Approve` and must be positive.

| Action | `action` value | Behavior |
|---|---:|---|
| `VerifyAgain` | `1` | Reopens the payment for one explicit server-to-server Verify using the saved amount and Authority. The two-minute verification lease is still enforced. Previous attempt count and timestamps are preserved. A successful result confirms the order; a definitive `-51` result marks the payment Failed but leaves the order open for a new payment attempt; an indeterminate result returns the payment to `RequiresManualReview`. |
| `Approve` | `2` | Manually marks the payment Successful using the supplied positive `refId`, then confirms the order. Use only after independently confirming the transaction in the provider dashboard or settlement evidence. |
| `RejectAndCancel` | `3` | Marks the payment Failed, cancels the order, and restores all reserved seller-offer stock. Payment, order, stock changes, and the completed audit record are saved in one database transaction. Use only after confirming that the provider did not collect the payment. |

Example — retry server-side verification:

```http
POST /api/admin/payments/{{PaymentId}}/resolve
Authorization: Bearer <admin-access-token>
Content-Type: application/json

{
  "action": 1,
  "reason": "Reviewed provider dashboard; retrying server-side verification."
}
```

Example — manually approve a verified transaction:

```http
POST /api/admin/payments/{{PaymentId}}/resolve
Authorization: Bearer <admin-access-token>
Content-Type: application/json

{
  "action": 2,
  "reason": "Matched the successful transaction in the provider dashboard.",
  "refId": 987654321
}
```

Example — reject a payment and restore reserved stock:

```http
POST /api/admin/payments/{{PaymentId}}/resolve
Authorization: Bearer <admin-access-token>
Content-Type: application/json

{
  "action": 3,
  "reason": "Provider settlement evidence confirms that no payment was collected."
}
```

Every accepted resolution creates a `PaymentResolutionAudits` record containing the payment/order/admin IDs, action, reason, provider `RefId` when available, provider error code when returned, start time, completion time, final outcome and payment/order statuses. Rejection also records the number of restored order items and total restored quantity. `VerifyAgain` writes its initial audit record before contacting the provider; an unfinished record indicates that the operation did not reach its completion write (for example, the request process was interrupted).

A manual approval is an operational assertion, not a provider refund or a replacement for settlement reconciliation. The endpoint deliberately has no action that changes an already successful payment to Failed.

Both `GET /api/admin/orders` and `GET /api/admin/orders/{orderId}` remain read-only. Only the payment-resolution endpoint changes payment/order/inventory state.
