using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Payments.ResolveAdminPayment;

public sealed record ResolveAdminPaymentCommand(
    Guid PaymentId,
    PaymentResolutionAction Action,
    string Reason,
    long? RefId = null)
    : IRequest<ResolveAdminPaymentResponse>;
