using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Sellers.SellerRequests.CreateSellerRequest;

public sealed class CreateSellerRequestCommandHandler(
    ISellerRequestRepository sellerRequestRepository,
    IUserRepository userRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateSellerRequestCommand, Guid>
{
    public async Task<Guid> Handle(CreateSellerRequestCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Authenticated user is required.");

        var sellerStatus = await userRepository.GetSellerStatusAsync(userId, cancellationToken);

        switch (sellerStatus)
        {
            case SellerStatus.Pending:
                throw new BusinessRuleException("A seller request is already pending.");
            case SellerStatus.Active:
                throw new BusinessRuleException("User is already an active seller.");
            case SellerStatus.Suspended:
                throw new BusinessRuleException("Suspended seller cannot submit a new request.");
        }

        var pendingRequest = await sellerRequestRepository.GetPendingByUserIdAsync(userId, cancellationToken);

        if (pendingRequest is not null)
            throw new BusinessRuleException("A pending seller request already exists.");

        var sellerRequest = SellerRequest.Create(userId, command.Reason);

        await sellerRequestRepository.AddAsync(sellerRequest, cancellationToken);

        await userRepository.SetSellerStatusAsync(userId, SellerStatus.Pending, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return sellerRequest.Id;
    }
}
