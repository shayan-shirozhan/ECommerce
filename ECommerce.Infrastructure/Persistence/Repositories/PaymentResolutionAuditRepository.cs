using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class PaymentResolutionAuditRepository(ECommerceDbContext dbContext)
    : IPaymentResolutionAuditRepository
{
    public async Task AddAsync(
        PaymentResolutionAudit audit,
        CancellationToken cancellationToken = default)
    {
        await dbContext.PaymentResolutionAudits.AddAsync(audit, cancellationToken);
    }
}
