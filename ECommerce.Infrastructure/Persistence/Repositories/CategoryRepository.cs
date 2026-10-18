using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(
    ECommerceDbContext dbContext) : ICategoryRepository
{
    public async Task<bool> ExistsAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Categories.AnyAsync(
            category => category.Id == categoryId && !category.IsDeleted,
            cancellationToken);
    }

    public async Task AddAsync(
        Category category,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Categories.AddAsync(category, cancellationToken);
    }

    public async Task<Category?> GetByIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                category => category.Id == categoryId && !category.IsDeleted,
                cancellationToken);
    }

    public async Task<Category?> GetByIdForUpdateAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Categories.FirstOrDefaultAsync(
            category => category.Id == categoryId && !category.IsDeleted,
            cancellationToken);
    }

    public async Task<bool> HasActiveProductsAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Products.AnyAsync(
            product =>
                product.CategoryId == categoryId
                && product.Status == ProductStatus.Active,
            cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Categories.AnyAsync(
            category =>
                category.ParentCategoryId == categoryId
                && !category.IsDeleted,
            cancellationToken);
    }

    public async Task<bool> WouldCreateCycleAsync(
        Guid categoryId,
        Guid? proposedParentCategoryId,
        CancellationToken cancellationToken = default)
    {
        var currentId = proposedParentCategoryId;
        var visited = new HashSet<Guid>();

        while (currentId.HasValue)
        {
            var parentId = currentId.Value;

            if (parentId == categoryId || !visited.Add(parentId))
                return true;

            currentId = await dbContext.Categories
                .Where(category =>
                    category.Id == parentId && !category.IsDeleted)
                .Select(category => category.ParentCategoryId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }
}
