using MediatR;

namespace ECommerce.Application.Features.Payments.CompleteZarinPalPayment;

public sealed record CompleteZarinPalPaymentCommand(
    Guid PaymentId,
    string Authority,
    string Status) : IRequest<CompleteZarinPalPaymentResponse>;
