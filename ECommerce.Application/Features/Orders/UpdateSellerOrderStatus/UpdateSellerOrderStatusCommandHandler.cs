using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Enums;
using FluentValidation;
using MediatR;

namespace ECommerce.Application.Features.Orders.UpdateSellerOrderStatus;

public sealed class UpdateSellerOrderStatusCommandHandler(
    IOrderRepository orderRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateSellerOrderStatusCommand, UpdateSellerOrderStatusResponse>
{
    public async Task<UpdateSellerOrderStatusResponse> Handle(UpdateSellerOrderStatusCommand command,
        CancellationToken cancellationToken)
    {
        var sellerId = currentUser.UserId ?? throw new UnauthorizedException("Authenticated user is required.");

        var order = await orderRepository.GetByIdForSellerForUpdateAsync(command.OrderId, sellerId, cancellationToken);

        if (order is null)
            throw new NotFoundException($"Order with id '{command.OrderId}' was not found.");

        switch (command.NewStatus)
        {
            case OrderGroupStatus.Processing:
                order.MoveSellerGroupToProcessing(sellerId);
                break;

            case OrderGroupStatus.Shipped:
                order.MoveSellerGroupToShipped(sellerId);
                break;

            case OrderGroupStatus.Delivered:
                order.MoveSellerGroupToDelivered(sellerId);
                break;

            default:
                throw new ValidationException("Requested order group status is invalid.");
        }

        var orderGroup = order.Groups.Single(group => group.SellerId == sellerId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateSellerOrderStatusResponse(order.Id, order.Status, orderGroup.Id, orderGroup.Status);
    }
}
