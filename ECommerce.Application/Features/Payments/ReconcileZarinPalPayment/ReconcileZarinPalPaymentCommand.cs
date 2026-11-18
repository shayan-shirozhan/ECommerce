using ECommerce.Application.Features.Payments.Shared;
using MediatR;

namespace ECommerce.Application.Features.Payments.ReconcileZarinPalPayment;

public sealed record ReconcileZarinPalPaymentCommand(
    Guid PaymentId,
    string Authority) : IRequest<ZarinPalVerificationOutcome>;
