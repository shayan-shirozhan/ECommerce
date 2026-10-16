using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.CreateSellerOffer;

public class CreateSellerOfferCommandHandler(
    ICurrentUser currentUser,
    IProductVariantRepository productVariantRepository,
    ISellerOfferRepository sellerOfferRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateSellerOfferCommand, Guid>
{
    public async Task<Guid> Handle(CreateSellerOfferCommand command, CancellationToken cancellationToken)
    {
        var sellerId = currentUser.UserId ?? throw new UnauthorizedException("Authenticated user is required.");

        var variant = await productVariantRepository.GetByIdWithDetailsAsync(
            command.ProductVariantId,
            cancellationToken);

        if (variant is null ||
            variant.Status != ProductVariantStatus.Active ||
            variant.Product is null ||
            variant.Product.Status != ProductStatus.Active)
        {
            throw new NotFoundException($"Product variant with id '{command.ProductVariantId}' was not found.");
        }

        var sellerOffer = SellerOffer.Create(
            sellerId,
            command.ProductVariantId,
            command.Price,
            command.Stock);

        await sellerOfferRepository.AddAsync(sellerOffer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return sellerOffer.Id;
    }
}
