using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Payments.InitiatePayment;

public sealed record InitiatePaymentCommand(
    Guid OrderId,
    PaymentMethod PaymentMethod = PaymentMethod.Mock) : IRequest<InitiatePaymentResponse>;
