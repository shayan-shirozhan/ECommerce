using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Payments.ReconcileZarinPalPayment;
using ECommerce.Application.Features.Payments.Shared;
using ECommerce.Application.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace ECommerce.API.BackgroundServices;

/// <summary>
/// Re-verifies pending ZarinPal payments independently from order expiration.
/// This allows uncertain payments to be reconciled during the payment window.
/// </summary>
public sealed class ZarinPalPaymentReconciliationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<OrderExpirationOptions> options,
    ILogger<ZarinPalPaymentReconciliationBackgroundService> logger)
    : BackgroundService
{
    private const int MaxPaymentsPerCycle = 100;

    private readonly TimeSpan _scanInterval =
        TimeSpan.FromSeconds(options.Value.ReconciliationIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Run once on startup so a deployment does not wait a full interval
            // before resuming payments that were already pending at process shutdown.
            await ProcessDuePaymentsAsync(stoppingToken);

            using var timer = new PeriodicTimer(_scanInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await ProcessDuePaymentsAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("ZarinPal payment reconciliation background service is stopping.");
        }
    }

    private async Task ProcessDuePaymentsAsync(CancellationToken cancellationToken)
    {
        var retryCutoffUtc = DateTime.UtcNow.Subtract(PaymentGatewayRetryPolicy.RetryDelay);
        IReadOnlyList<PendingZarinPalPaymentData> candidates;

        try
        {
            await using var queryScope = scopeFactory.CreateAsyncScope();
            var repository = queryScope.ServiceProvider.GetRequiredService<IOrderRepository>();
            candidates = await repository.GetPendingZarinPalPaymentsForReconciliationAsync(
                retryCutoffUtc,
                MaxPaymentsPerCycle,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to find pending ZarinPal payments for reconciliation.");
            return;
        }

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var outcome = await sender.Send(
                    new ReconcileZarinPalPaymentCommand(candidate.PaymentId, candidate.Authority),
                    cancellationToken);

                switch (outcome)
                {
                    case ZarinPalVerificationOutcome.Successful:
                        logger.LogInformation(
                            "ZarinPal payment {PaymentId} was confirmed by reconciliation.",
                            candidate.PaymentId);
                        break;
                    case ZarinPalVerificationOutcome.RequiresManualReview:
                        logger.LogWarning(
                            "ZarinPal payment {PaymentId} requires manual review.",
                            candidate.PaymentId);
                        break;
                    case ZarinPalVerificationOutcome.Failed:
                        logger.LogInformation(
                            "ZarinPal payment {PaymentId} was definitively rejected by the provider.",
                            candidate.PaymentId);
                        break;
                }
            }
            catch (ConcurrencyConflictException exception)
            {
                logger.LogDebug(
                    exception,
                    "A concurrent callback or worker already updated ZarinPal payment {PaymentId}.",
                    candidate.PaymentId);
            }
            catch (PersistenceConflictException exception)
            {
                logger.LogWarning(
                    exception,
                    "Persistence conflict while reconciling ZarinPal payment {PaymentId}.",
                    candidate.PaymentId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to reconcile ZarinPal payment {PaymentId}.",
                    candidate.PaymentId);
            }
        }
    }
}
