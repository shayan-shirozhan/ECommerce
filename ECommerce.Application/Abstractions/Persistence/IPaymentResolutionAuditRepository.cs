using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IPaymentResolutionAuditRepository
{
    Task AddAsync(PaymentResolutionAudit audit, CancellationToken cancellationToken = default);
}
