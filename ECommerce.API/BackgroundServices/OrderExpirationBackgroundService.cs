using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders.ExpireOrders;
using ECommerce.Application.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace ECommerce.API.BackgroundServices;

public sealed class OrderExpirationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<OrderExpirationOptions> expirationOptions,
    ILogger<OrderExpirationBackgroundService> logger)
    : BackgroundService
{
    private const int MaxOrdersPerCycle = 100;

    private readonly TimeSpan _paymentWindow =
        TimeSpan.FromMinutes(expirationOptions.Value.PaymentWindowMinutes);

    private readonly TimeSpan _scanInterval =
        TimeSpan.FromSeconds(expirationOptions.Value.ScanIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_scanInterval);

        try
        {
            do
            {
                try
                {
                    await ProcessExpiredOrdersAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to scan expired orders.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Order expiration background service is stopping.");
        }
    }

    private async Task ProcessExpiredOrdersAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var cutoffUtc = nowUtc.Subtract(_paymentWindow);
        var verificationRetryCutoffUtc = nowUtc.Subtract(PaymentGatewayRetryPolicy.RetryDelay);

        IReadOnlyList<Guid> expiredOrderIds;

        await using (var queryScope = scopeFactory.CreateAsyncScope())
        {
            var orderRepository = queryScope.ServiceProvider
                .GetRequiredService<IOrderRepository>();

            expiredOrderIds = await orderRepository.GetExpiredOrderIdsAsync(
                cutoffUtc,
                verificationRetryCutoffUtc,
                MaxOrdersPerCycle,
                cancellationToken);
        }

        foreach (var orderId in expiredOrderIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                var processed = await sender.Send(
                    new ExpireOrderCommand(orderId, cutoffUtc),
                    cancellationToken);

                if (processed)
                {
                    logger.LogInformation(
                        "Expired order {OrderId} was cancelled and its stock restored.",
                        orderId);
                }
            }
            catch (ConcurrencyConflictException exception)
            {
                logger.LogWarning(
                    exception,
                    "Concurrency conflict while expiring order {OrderId}.",
                    orderId);
            }
            catch (PersistenceConflictException exception)
            {
                logger.LogWarning(
                    exception,
                    "Persistence conflict while expiring order {OrderId}.",
                    orderId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to expire order {OrderId}.",
                    orderId);
            }
        }
    }
}
