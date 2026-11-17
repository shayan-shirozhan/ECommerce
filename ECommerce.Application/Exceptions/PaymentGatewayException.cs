namespace ECommerce.Application.Exceptions;

public sealed class PaymentGatewayException(string message, Exception? innerException = null)
    : Exception(message, innerException);
