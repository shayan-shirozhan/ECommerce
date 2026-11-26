using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders;
using ECommerce.Application.Features.Payments.Shared;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Payments.ResolveAdminPayment;

public sealed class ResolveAdminPaymentCommandHandler(
    ICurrentUser currentUser,
    IOrderRepository orderRepository,
    ISellerOfferRepository sellerOfferRepository,
    IPaymentResolutionAuditRepository auditRepository,
    IUnitOfWork unitOfWork,
    ZarinPalPaymentVerifier paymentVerifier)
    : IRequestHandler<ResolveAdminPaymentCommand, ResolveAdminPaymentResponse>
{
    public async Task<ResolveAdminPaymentResponse> Handle(
        ResolveAdminPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var adminUserId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated administrator is required.");

        var order = await orderRepository.GetByPaymentIdForAdminForUpdateAsync(
            command.PaymentId,
            cancellationToken);

        var payment = order?.Payments.SingleOrDefault(candidate => candidate.Id == command.PaymentId);
        if (order is null || payment is null)
            throw new NotFoundException($"Payment with id '{command.PaymentId}' was not found.");

        if (order.Status != OrderStatus.AwaitingPayment)
            throw new BusinessRuleException("Only payments belonging to orders awaiting payment can be resolved.");

        if (payment.Status != PaymentStatus.RequiresManualReview)
            throw new BusinessRuleException("Only payments requiring manual review can be resolved.");

        if (payment.PaymentMethod != PaymentMethod.ZarinPal || string.IsNullOrWhiteSpace(payment.Authority))
            throw new BusinessRuleException("Only ZarinPal payments with a saved authority can be resolved through this endpoint.");

        if (command.Action == PaymentResolutionAction.VerifyAgain &&
            !PaymentGatewayRetryPolicy.CanRetry(payment.LastVerificationAttemptedAt, DateTime.UtcNow))
        {
            throw new BusinessRuleException(
                "A previous verification request is still inside its two-minute lease. Retry after the lease expires.");
        }

        var audit = PaymentResolutionAudit.Create(
            payment.Id,
            order.Id,
            adminUserId,
            command.Action,
            command.Reason,
            command.RefId);

        switch (command.Action)
        {
            case PaymentResolutionAction.VerifyAgain:
                return await VerifyAgainAsync(order, payment, audit, cancellationToken);

            case PaymentResolutionAction.Approve:
                return await ApproveAsync(order, payment, audit, command.RefId!.Value, cancellationToken);

            case PaymentResolutionAction.RejectAndCancel:
                return await RejectAndCancelAsync(order, payment, audit, cancellationToken);

            default:
                throw new BusinessRuleException("The requested payment resolution action is not supported.");
        }
    }

    private async Task<ResolveAdminPaymentResponse> VerifyAgainAsync(
        Order order,
        Payment payment,
        PaymentResolutionAudit audit,
        CancellationToken cancellationToken)
    {
        payment.ResumeManualReviewForVerification();
        await auditRepository.AddAsync(audit, cancellationToken);

        try
        {
            var outcome = await paymentVerifier.VerifyAsync(
                order,
                payment,
                cancellationToken,
                allowAdminVerificationRetry: true);

            // A concurrent verifier may acquire the lease between the pre-check and
            // the shared verifier. Keep the payment visible for manual review in that case.
            if (outcome == ZarinPalVerificationOutcome.Skipped && payment.Status == PaymentStatus.Pending)
                payment.MarkRequiresManualReview();

            audit.Complete(
                outcome.ToString(),
                payment.Status,
                order.Status,
                resolvedRefId: payment.RefId,
                providerErrorCode: payment.LastVerificationErrorCode);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return CreateResponse(audit, payment, order, outcome, null);
        }
        catch (PaymentGatewayException)
        {
            // Configuration/transport failures must not strand a formerly reviewed
            // payment as an ordinary Pending payment. The verifier persists its attempt
            // before the remote call, so this transition is safe and retains the history.
            if (payment.Status == PaymentStatus.Pending)
                payment.MarkRequiresManualReview();

            audit.Complete(
                "GatewayError",
                payment.Status,
                order.Status);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<ResolveAdminPaymentResponse> ApproveAsync(
        Order order,
        Payment payment,
        PaymentResolutionAudit audit,
        long refId,
        CancellationToken cancellationToken)
    {
        var previousProviderErrorCode = payment.LastVerificationErrorCode;

        await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            payment.ApproveManualReview(refId);
            order.Confirm();

            await auditRepository.AddAsync(audit, transactionToken);
            audit.Complete(
                "Approved",
                payment.Status,
                order.Status,
                providerErrorCode: previousProviderErrorCode);

            await unitOfWork.SaveChangesAsync(transactionToken);
        }, cancellationToken);

        return CreateResponse(audit, payment, order, null, null);
    }

    private async Task<ResolveAdminPaymentResponse> RejectAndCancelAsync(
        Order order,
        Payment payment,
        PaymentResolutionAudit audit,
        CancellationToken cancellationToken)
    {
        OrderInventoryRestorationResult? restoration = null;

        await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            payment.RejectManualReview();
            restoration = await OrderCancellationProcessor.CancelAndRestoreAsync(
                order,
                sellerOfferRepository,
                transactionToken);

            await auditRepository.AddAsync(audit, transactionToken);
            audit.Complete(
                "RejectedAndCancelled",
                payment.Status,
                order.Status,
                restoration.RestoredItemCount,
                restoration.RestoredQuantity,
                providerErrorCode: payment.LastVerificationErrorCode);

            await unitOfWork.SaveChangesAsync(transactionToken);
        }, cancellationToken);

        return CreateResponse(audit, payment, order, null, restoration);
    }

    private static ResolveAdminPaymentResponse CreateResponse(
        PaymentResolutionAudit audit,
        Payment payment,
        Order order,
        ZarinPalVerificationOutcome? verificationOutcome,
        OrderInventoryRestorationResult? restoration)
        => new(
            audit.Id,
            payment.Id,
            order.Id,
            audit.Action,
            payment.Status,
            order.Status,
            payment.RefId,
            verificationOutcome,
            restoration?.RestoredItemCount,
            restoration?.RestoredQuantity,
            audit.CompletedAtUtc!.Value);
}
