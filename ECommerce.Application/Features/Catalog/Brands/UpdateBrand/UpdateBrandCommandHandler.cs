using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Brands.UpdateBrand;

public sealed class UpdateBrandCommandHandler(
    IBrandRepository brandRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateBrandCommand>
{
    public async Task Handle(
        UpdateBrandCommand command,
        CancellationToken cancellationToken)
    {
        var brand = await brandRepository.GetByIdForUpdateAsync(
            command.Id,
            cancellationToken);

        if (brand is null)
            throw new NotFoundException(
                $"Brand with id '{command.Id}' was not found.");

        brand.UpdateName(command.Name);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
