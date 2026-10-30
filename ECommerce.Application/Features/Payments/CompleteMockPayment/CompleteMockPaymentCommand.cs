using MediatR;

namespace ECommerce.Application.Features.Payments.CompleteMockPayment;

public sealed record CompleteMockPaymentCommand(Guid PaymentId)
    : IRequest<CompleteMockPaymentResponse>;
