namespace ECommerce.Application.Abstractions.Persistence;

public sealed record PendingZarinPalPaymentData(Guid PaymentId, string Authority);
