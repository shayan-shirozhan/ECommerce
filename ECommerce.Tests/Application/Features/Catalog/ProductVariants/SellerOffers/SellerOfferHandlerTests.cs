using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.ActiveSellerOffer;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.CreateSellerOffer;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.DeactivateSellerOffer;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetSellerOfferById;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.UpdateSellerOfferPrice;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.UpdateSellerOfferStock;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Tests.Application.Features.Catalog.ProductVariants.SellerOffers;

public sealed class SellerOfferHandlerTests
{
    [Test]
    public async Task CreateSellerOffer_should_create_offer_for_current_user_and_return_id()
    {
        var sellerId = Guid.NewGuid();
        var variant = CreateActiveProductVariant();
        var productVariantId = variant.Id;

        var currentUser = new FakeCurrentUser()
        {
            UserId = sellerId
        };

        var productVariantRepository = new FakeProductVariantRepository
        {
            Variant = variant
        };

        var sellerOfferRepository = new FakeSellerOfferRepository();
        var unitOfWork = new FakeUnitOfWork();

        var handler = new CreateSellerOfferCommandHandler(
            currentUser,
            productVariantRepository,
            sellerOfferRepository,
            unitOfWork);

        var command = new CreateSellerOfferCommand(
            productVariantId,
            100_000,
            10);

        var result = await handler.Handle(
            command,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.EqualTo(Guid.Empty));
            Assert.That(
                sellerOfferRepository.AddedOffer,
                Is.Not.Null);

            Assert.That(
                sellerOfferRepository.AddedOffer!.SellerId,
                Is.EqualTo(sellerId));

            Assert.That(
                sellerOfferRepository.AddedOffer.ProductVariantId,
                Is.EqualTo(productVariantId));

            Assert.That(
                sellerOfferRepository.AddedOffer.Price,
                Is.EqualTo(100_000));

            Assert.That(
                sellerOfferRepository.AddedOffer.Stock,
                Is.EqualTo(10));

            Assert.That(
                result,
                Is.EqualTo(sellerOfferRepository.AddedOffer.Id));

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.True);
        });
    }

    [Test]
    public async Task CreateSellerOffer_should_throw_not_found_when_product_variant_does_not_exist()
    {
        var currentUser = new FakeCurrentUser()
        {
            UserId = Guid.NewGuid()
        };

        var productVariantRepository = new FakeProductVariantRepository();

        var sellerOfferRepository = new FakeSellerOfferRepository();
        var unitOfWork = new FakeUnitOfWork();

        var handler = new CreateSellerOfferCommandHandler(
            currentUser,
            productVariantRepository,
            sellerOfferRepository,
            unitOfWork);

        var command = new CreateSellerOfferCommand(
            Guid.NewGuid(),
            100_000,
            10);

        var exception = Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.That(
            exception!.Message,
            Does.Contain("Product variant"));

        Assert.That(
            sellerOfferRepository.AddedOffer,
            Is.Null);

        Assert.That(
            unitOfWork.SaveChangesCalled,
            Is.False);
    }

    [Test]
    public async Task CreateSellerOffer_should_throw_not_found_when_product_variant_is_not_active()
    {
        var sellerId = Guid.NewGuid();
        var product = CreateActiveProduct();
        var variant = ProductVariant.Create(product, $"SKU-{Guid.NewGuid():N}", "Standard");

        var productVariantRepository = new FakeProductVariantRepository
        {
            Variant = variant
        };
        var sellerOfferRepository = new FakeSellerOfferRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateSellerOfferCommandHandler(
            new FakeCurrentUser { UserId = sellerId },
            productVariantRepository,
            sellerOfferRepository,
            unitOfWork);

        var exception = Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(
            new CreateSellerOfferCommand(variant.Id, 100_000, 10),
            CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("Product variant"));
            Assert.That(sellerOfferRepository.AddedOffer, Is.Null);
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
        });
    }

    [Test]
    public async Task CreateSellerOffer_should_throw_not_found_when_parent_product_is_not_active()
    {
        var sellerId = Guid.NewGuid();
        var product = CreateActiveProduct();
        var variant = ProductVariant.Create(product, $"SKU-{Guid.NewGuid():N}", "Standard");
        variant.Activate(product);
        product.Deactivate();

        var productVariantRepository = new FakeProductVariantRepository
        {
            Variant = variant
        };
        var sellerOfferRepository = new FakeSellerOfferRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateSellerOfferCommandHandler(
            new FakeCurrentUser { UserId = sellerId },
            productVariantRepository,
            sellerOfferRepository,
            unitOfWork);

        var exception = Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(
            new CreateSellerOfferCommand(variant.Id, 100_000, 10),
            CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("Product variant"));
            Assert.That(sellerOfferRepository.AddedOffer, Is.Null);
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
        });
    }

    [Test]
    public async Task CreateSellerOffer_should_throw_unauthorized_when_current_user_is_not_authenticated()
    {
        var currentUser = new FakeCurrentUser
        {
            UserId = null
        };

        var productVariantRepository = new FakeProductVariantRepository();

        var sellerOfferRepository = new FakeSellerOfferRepository();
        var unitOfWork = new FakeUnitOfWork();

        var handler = new CreateSellerOfferCommandHandler(
            currentUser,
            productVariantRepository,
            sellerOfferRepository,
            unitOfWork);

        var command = new CreateSellerOfferCommand(
            Guid.NewGuid(),
            100_000,
            10);

        var exception = Assert.ThrowsAsync<UnauthorizedException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.That(
            exception!.Message,
            Does.Contain("Authenticated user"));

        Assert.Multiple(() =>
        {
            Assert.That(
                sellerOfferRepository.AddedOffer,
                Is.Null);

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.False);
        });
    }

    private static Product CreateActiveProduct()
    {
        var product = Product.Create(
            "Test product",
            "Test product description",
            Guid.NewGuid(),
            Guid.NewGuid());
        product.Activate();
        return product;
    }

    private static ProductVariant CreateActiveProductVariant()
    {
        var product = CreateActiveProduct();
        var variant = ProductVariant.Create(product, $"SKU-{Guid.NewGuid():N}", "Standard");
        variant.Activate(product);
        return variant;
    }

    private sealed class FakeProductVariantRepository : IProductVariantRepository
    {
        public ProductVariant? Variant { get; set; }

        public Task<bool> ExistsAsync(
            Guid productVariantId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Variant?.Id == productVariantId);
        }

        public Task AddAsync(
            ProductVariant productVariant,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<ProductVariant?> GetByIdWithDetailsAsync(
            Guid productVariantId,
            CancellationToken cancellationToken = default)
        {
            if (Variant is null || Variant.Id != productVariantId)
                return Task.FromResult<ProductVariant?>(null);

            return Task.FromResult<ProductVariant?>(Variant);
        }

        public Task<ProductVariant?> GetByIdForUpdateAsync(
            Guid productVariantId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProductVariant?>(null);
        }
    }

    private sealed class FakeSellerOfferRepository : ISellerOfferRepository
    {
        public SellerOffer? AddedOffer { get; private set; }

        public SellerOffer? Offer { get; set; }

        public SellerOffer? OfferForUpdate { get; set; }

        public Task AddAsync(
            SellerOffer sellerOffer,
            CancellationToken cancellationToken = default)
        {
            AddedOffer = sellerOffer;
            return Task.CompletedTask;
        }

        public Task<SellerOffer?> GetByIdAsync(
            Guid sellerOfferId,
            CancellationToken cancellationToken = default)
        {
            if (Offer is null || Offer.Id != sellerOfferId)
                return Task.FromResult<SellerOffer?>(null);

            return Task.FromResult<SellerOffer?>(Offer);
        }

        public Task<SellerOffer?> GetByIdForUpdateAsync(
            Guid sellerOfferId,
            uint expectedRowVersion,
            CancellationToken cancellationToken = default)
        {
            if (OfferForUpdate is null ||
                OfferForUpdate.Id != sellerOfferId)
            {
                return Task.FromResult<SellerOffer?>(null);
            }

            return Task.FromResult<SellerOffer?>(OfferForUpdate);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool SaveChangesCalled { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCalled = true;
            return Task.FromResult(1);
        }
    }
    
    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => true;
    }
    
    [Test]
    public async Task GetSellerOfferById_should_throw_not_found_when_offer_does_not_exist()
    {
        var sellerOfferRepository = new FakeSellerOfferRepository();

        var handler = new GetSellerOfferByIdQueryHandler(
            sellerOfferRepository);

        var query = new GetSellerOfferByIdQuery(
            Guid.NewGuid());

        var exception = Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                query,
                CancellationToken.None));

        Assert.That(
            exception!.Message,
            Does.Contain("Seller offer"));
    }

    [Test]
    public async Task GetSellerOfferById_should_throw_not_found_when_offer_is_inactive()
    {
        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            Offer = SellerOffer.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                250_000,
                15)
        };

        var handler = new GetSellerOfferByIdQueryHandler(sellerOfferRepository);
        var query = new GetSellerOfferByIdQuery(sellerOfferRepository.Offer!.Id);

        var exception = Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(query, CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("Seller offer"));
    }

    [Test]
    public async Task UpdateSellerOferPrice_should_throw_not_found_when_offer_does_not_exist()
    {
        var sellerOfferRepository = new FakeSellerOfferRepository();
        var unitOfWork =  new FakeUnitOfWork();
        var currentUser = new FakeCurrentUser();

        var handler = new UpdateSellerOfferPriceCommandHandler(sellerOfferRepository, unitOfWork, currentUser);

        var command = new UpdateSellerOfferPriceCommand(
            Guid.NewGuid(),
            150_000,
            1);

        var exception = Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(
            command,
            CancellationToken.None));
        
        Assert.That(exception!.Message, Does.Contain("Seller offer"));

        Assert.That(unitOfWork.SaveChangesCalled, Is.False);
    }

    [Test]
    public async Task UpdateSellerOfferPrice_should_update_price_and_save_changes()
    {
        var sellerId = Guid.NewGuid();
        
        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = sellerId
        };

        var handler = new UpdateSellerOfferPriceCommandHandler(sellerOfferRepository, unitOfWork, currentUser);

        var command = new UpdateSellerOfferPriceCommand(
            sellerOffer.Id,
            150_000,
            1);

        await handler.Handle(command, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(sellerOffer.Price, Is.EqualTo(150_000));

            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }
    
    [Test]
    public async Task UpdateSellerOfferPrice_should_update_price_for_owner()
    {
        var sellerId = Guid.NewGuid();

        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = sellerId
        };

        var handler = new UpdateSellerOfferPriceCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new UpdateSellerOfferPriceCommand(
            sellerOffer.Id,
            150_000,
            1);

        await handler.Handle(
            command,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                sellerOffer.Price,
                Is.EqualTo(150_000));

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.True);
        });
    }
    
    [Test]
    public async Task UpdateSellerOfferPrice_should_throw_unauthorized_when_user_is_not_authenticated()
    {
        var sellerOffer = SellerOffer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = null
        };

        var handler = new UpdateSellerOfferPriceCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new UpdateSellerOfferPriceCommand(
            sellerOffer.Id,
            150_000,
            1);

        var exception = Assert.ThrowsAsync<UnauthorizedException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
            Assert.That(sellerOffer.Price, Is.EqualTo(100_000));
        });
    }
    
    [Test]
    public async Task UpdateSellerOfferPrice_should_throw_forbidden_when_user_is_not_owner()
    {
        var sellerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = anotherUserId
        };

        var handler = new UpdateSellerOfferPriceCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new UpdateSellerOfferPriceCommand(
            sellerOffer.Id,
            150_000,
            1);

        var exception = Assert.ThrowsAsync<ForbiddenException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
            Assert.That(sellerOffer.Price, Is.EqualTo(100_000));
        });
    }

    [Test]
    public async Task UpdateSellerOfferStock_should_throw_not_found_when_offer_does_not_exist()
    {
        var sellerOfferRepository = new FakeSellerOfferRepository();
        var unitOfWork =  new FakeUnitOfWork();
        var currentUser = new FakeCurrentUser();

        var handler = new UpdateSellerOfferStockCommandHandler(sellerOfferRepository, unitOfWork, currentUser);

        var command = new UpdateSellerOfferStockCommand(
            Guid.NewGuid(),
            0,
            1);

        var exception = Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(
            command, CancellationToken.None));
        
        Assert.That(exception!.Message, Does.Contain("Seller offer"));

        Assert.That(unitOfWork.SaveChangesCalled, Is.False);
    }
    
    [Test]
    public async Task UpdateSellerOfferStock_should_update_stock_and_save_changes()
    {
        var sellerId = Guid.NewGuid();
        
        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = sellerId
        };

        var handler = new UpdateSellerOfferStockCommandHandler(sellerOfferRepository, unitOfWork, currentUser);

        var command = new UpdateSellerOfferStockCommand(
            sellerOffer.Id,
            0,
            1);

        await handler.Handle(command, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(sellerOffer.Stock, Is.EqualTo(0));

            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }
    
    [Test]
    public async Task UpdateSellerOfferStock_should_update_stock_for_owner()
    {
        var sellerId = Guid.NewGuid();

        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = sellerId
        };

        var handler = new UpdateSellerOfferStockCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new UpdateSellerOfferStockCommand(
            sellerOffer.Id,
            0,
            1);

        await handler.Handle(
            command,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                sellerOffer.Stock,
                Is.EqualTo(0));

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.True);
        });
    }
    
    [Test]
    public async Task UpdateSellerOfferStock_should_throw_unauthorized_when_user_is_not_authenticated()
    {
        var sellerOffer = SellerOffer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = null
        };

        var handler = new UpdateSellerOfferStockCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new UpdateSellerOfferStockCommand(
            sellerOffer.Id,
            5,
            1);

        var exception = Assert.ThrowsAsync<UnauthorizedException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
            Assert.That(sellerOffer.Stock, Is.EqualTo(10));
        });
    }
    
    [Test]
    public async Task UpdateSellerOfferStock_should_throw_forbidden_when_user_is_not_owner()
    {
        var sellerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = anotherUserId
        };

        var handler = new UpdateSellerOfferStockCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new UpdateSellerOfferStockCommand(
            sellerOffer.Id,
            5,
            1);

        var exception = Assert.ThrowsAsync<ForbiddenException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
            Assert.That(sellerOffer.Stock, Is.EqualTo(10));
        });
    }
    
    [Test]
    public async Task ActivateSellerOffer_should_throw_not_found_when_offer_does_not_exist()
    {
        var sellerOfferRepository = new FakeSellerOfferRepository();
        var unitOfWork = new FakeUnitOfWork();
        var currentUser = new FakeCurrentUser();

        var handler = new ActivateSellerOfferCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new ActivateSellerOfferCommand(
            Guid.NewGuid(),
            1);

        var exception = Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.That(
            exception!.Message,
            Does.Contain("Seller offer"));

        Assert.That(
            unitOfWork.SaveChangesCalled,
            Is.False);
    }

    [Test]
    public async Task ActivateSellerOffer_should_activate_offer_and_save_changes()
    {
        var sellerId = Guid.NewGuid();
        
        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = sellerId
        };

        var handler = new ActivateSellerOfferCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new ActivateSellerOfferCommand(
            sellerOffer.Id,
            1);

        await handler.Handle(
            command,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                sellerOffer.Status,
                Is.EqualTo(SellerOfferStatus.Active));

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.True);
        });
    }
    
    [Test]
    public async Task ActivateSellerOffer_should_activate_offer_for_owner()
    {
        var sellerId = Guid.NewGuid();

        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = sellerId
        };

        var handler = new ActivateSellerOfferCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new ActivateSellerOfferCommand(
            sellerOffer.Id,
            1);

        await handler.Handle(
            command,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                sellerOffer.Status,
                Is.EqualTo(SellerOfferStatus.Active));

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.True);
        });
    }
    
    [Test]
    public async Task ActivateSellerOffer_should_throw_unauthorized_when_user_is_not_authenticated()
    {
        var sellerOffer = SellerOffer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = null
        };

        var handler = new ActivateSellerOfferCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new ActivateSellerOfferCommand(
            sellerOffer.Id,
            1);

        var exception = Assert.ThrowsAsync<UnauthorizedException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
            Assert.That(
                sellerOffer.Status,
                Is.EqualTo(SellerOfferStatus.Inactive));
        });
    }
    
    [Test]
    public async Task ActivateSellerOffer_should_throw_forbidden_when_user_is_not_owner()
    {
        var sellerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = anotherUserId
        };

        var handler = new ActivateSellerOfferCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new ActivateSellerOfferCommand(
            sellerOffer.Id,
            1);

        var exception = Assert.ThrowsAsync<ForbiddenException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
            Assert.That(
                sellerOffer.Status,
                Is.EqualTo(SellerOfferStatus.Inactive));
        });
    }
    
    [Test]
    public async Task DeactivateSellerOffer_should_throw_not_found_when_offer_does_not_exist()
    {
        var sellerOfferRepository = new FakeSellerOfferRepository();
        var unitOfWork = new FakeUnitOfWork();
        var currentUser = new FakeCurrentUser();

        var handler = new DeactivateSellerOfferCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new DeactivateSellerOfferCommand(
            Guid.NewGuid(),
            1);

        var exception = Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.That(
            exception!.Message,
            Does.Contain("Seller offer"));

        Assert.That(
            unitOfWork.SaveChangesCalled,
            Is.False);
    }

    [Test]
    public async Task DeactivateSellerOffer_should_deactivate_offer_and_save_changes()
    {
        var sellerId = Guid.NewGuid();
        
        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        sellerOffer.Activate();

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = sellerId
        };

        var handler = new DeactivateSellerOfferCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new DeactivateSellerOfferCommand(
            sellerOffer.Id,
            1);

        await handler.Handle(
            command,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                sellerOffer.Status,
                Is.EqualTo(SellerOfferStatus.Inactive));

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.True);
        });
    }
    
    [Test]
    public async Task DeactivateSellerOffer_should_deactivate_offer_for_owner()
    {
        var sellerId = Guid.NewGuid();

        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        sellerOffer.Activate();

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = sellerId
        };

        var handler = new DeactivateSellerOfferCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new DeactivateSellerOfferCommand(
            sellerOffer.Id,
            1);

        await handler.Handle(
            command,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                sellerOffer.Status,
                Is.EqualTo(SellerOfferStatus.Inactive));

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.True);
        });
    }
    
    [Test]
    public async Task DeactivateSellerOffer_should_throw_unauthorized_when_user_is_not_authenticated()
    {
        var sellerOffer = SellerOffer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100_000,
            10);

        sellerOffer.Activate();

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = null
        };

        var handler = new DeactivateSellerOfferCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new DeactivateSellerOfferCommand(
            sellerOffer.Id,
            1);

        var exception = Assert.ThrowsAsync<UnauthorizedException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.False);

            Assert.That(
                sellerOffer.Status,
                Is.EqualTo(SellerOfferStatus.Active));
        });
    }
    
    [Test]
    public async Task DeactivateSellerOffer_should_throw_forbidden_when_user_is_not_owner()
    {
        var sellerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        var sellerOffer = SellerOffer.Create(
            sellerId,
            Guid.NewGuid(),
            100_000,
            10);

        sellerOffer.Activate();

        var sellerOfferRepository = new FakeSellerOfferRepository
        {
            OfferForUpdate = sellerOffer
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = anotherUserId
        };

        var handler = new DeactivateSellerOfferCommandHandler(
            sellerOfferRepository,
            unitOfWork,
            currentUser);

        var command = new DeactivateSellerOfferCommand(
            sellerOffer.Id,
            1);

        var exception = Assert.ThrowsAsync<ForbiddenException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.False);

            Assert.That(
                sellerOffer.Status,
                Is.EqualTo(SellerOfferStatus.Active));
        });
    }
}
