using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Brands.DeleteBrand;

public sealed class DeleteBrandCommandHandler(
    IBrandRepository brandRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteBrandCommand>
{
    public async Task Handle(
        DeleteBrandCommand command,
        CancellationToken cancellationToken)
    {
        var brand = await brandRepository.GetByIdForUpdateAsync(
            command.Id,
            cancellationToken);

        if (brand is null)
            throw new NotFoundException(
                $"Brand with id '{command.Id}' was not found.");

        var hasActiveProducts = await brandRepository.HasActiveProductsAsync(
            brand.Id,
            cancellationToken);

        if (hasActiveProducts)
            throw new BusinessRuleException(
                "A brand with active products cannot be deleted.");

        brand.SoftDelete();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
