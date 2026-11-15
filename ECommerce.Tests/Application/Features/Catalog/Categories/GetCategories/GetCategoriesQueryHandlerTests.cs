using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Features.Catalog.Categories.GetCategories;

namespace ECommerce.Tests.Application.Features.Catalog.Categories.GetCategories;

public sealed class GetCategoriesQueryHandlerTests
{
    [Test]
    public async Task Handle_should_map_categories_and_parent_relationships()
    {
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var repository = new FakeCategoryRepository(
        [
            new CategoryListingItem(parentId, "Electronics", null),
            new CategoryListingItem(childId, "Phones", parentId)
        ]);
        var handler = new GetCategoriesQueryHandler(repository);

        var response = await handler.Handle(
            new GetCategoriesQuery(),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(response.Items, Has.Count.EqualTo(2));
            Assert.That(response.Items[0].Id, Is.EqualTo(parentId));
            Assert.That(response.Items[0].Name, Is.EqualTo("Electronics"));
            Assert.That(response.Items[0].ParentCategoryId, Is.Null);
            Assert.That(response.Items[1].Id, Is.EqualTo(childId));
            Assert.That(response.Items[1].Name, Is.EqualTo("Phones"));
            Assert.That(response.Items[1].ParentCategoryId, Is.EqualTo(parentId));
        });
    }

    [Test]
    public async Task Handle_should_return_empty_items_when_no_categories_exist()
    {
        var handler = new GetCategoriesQueryHandler(
            new FakeCategoryRepository([]));

        var response = await handler.Handle(
            new GetCategoriesQuery(),
            CancellationToken.None);

        Assert.That(response.Items, Is.Empty);
    }

    private sealed class FakeCategoryRepository(
        IReadOnlyList<CategoryListingItem> categories) : ICategoryRepository
    {
        public Task<IReadOnlyList<CategoryListingItem>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(categories);

        public Task<bool> ExistsAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task AddAsync(
            ECommerce.Domain.Entities.Category category,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ECommerce.Domain.Entities.Category?> GetByIdAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ECommerce.Domain.Entities.Category?>(null);

        public Task<ECommerce.Domain.Entities.Category?> GetByIdForUpdateAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ECommerce.Domain.Entities.Category?>(null);

        public Task<bool> HasActiveProductsAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasChildrenAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> WouldCreateCycleAsync(
            Guid categoryId,
            Guid? proposedParentCategoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
