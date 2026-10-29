using MediatR;

namespace ECommerce.Application.Features.Payments.InitiatePayment;

public sealed record InitiatePaymentCommand(Guid OrderId) : IRequest<InitiatePaymentResponse>;
