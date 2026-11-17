# Shipment tracking

Shipment metadata belongs to `OrderGroup`, because each seller in a multi-seller order ships their own group independently.

- `ShippingCarrier` and `TrackingCode` are nullable until the seller moves the group to `Shipped`.
- Both values are required, trimmed, and limited to 100 characters at the transition.
- Shipment metadata is immutable through the seller status endpoint after the group is shipped.
- Buyer order details return shipment metadata for every group in the buyer-owned order.
- Seller order details return metadata only for the seller-owned group.
- Database columns remain nullable to represent groups that have not shipped yet and to preserve existing rows.

Apply the `AddShippingTrackingToOrderGroups` EF Core migration before deploying the application update.
