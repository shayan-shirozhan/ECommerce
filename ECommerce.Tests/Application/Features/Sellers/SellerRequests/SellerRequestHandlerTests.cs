using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Sellers.SellerRequests.ApproveSellerRequest;
using ECommerce.Application.Features.Sellers.SellerRequests.CreateSellerRequest;
using ECommerce.Application.Features.Sellers.SellerRequests.GetSellerRequestById;
using ECommerce.Application.Features.Sellers.SellerRequests.RejectSellerRequest;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Tests.Application.Features.Sellers.SellerRequests;

public sealed class SellerRequestHandlerTests
{
    [Test]
    public async Task CreateSellerRequest_should_create_request_for_current_user_and_return_id()
    {
        var userId = Guid.NewGuid();

        var currentUser = new FakeCurrentUser
        {
            UserId = userId
        };

        var sellerRequestRepository = new FakeSellerRequestRepository();
        var unitOfWork = new FakeUnitOfWork();

        var userRepository = new FakeUserRepository
        {
            SellerStatus = SellerStatus.None
        };

        var handler = new CreateSellerRequestCommandHandler(
            sellerRequestRepository,
            userRepository,
            currentUser,
            unitOfWork);

        var command = new CreateSellerRequestCommand(
            "I want to become a seller.");

        var result = await handler.Handle(
            command,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.EqualTo(Guid.Empty));

            Assert.That(
                sellerRequestRepository.AddedRequest,
                Is.Not.Null);

            Assert.That(
                sellerRequestRepository.AddedRequest!.UserId,
                Is.EqualTo(userId));

            Assert.That(
                sellerRequestRepository.AddedRequest.Reason,
                Is.EqualTo("I want to become a seller."));

            Assert.That(
                sellerRequestRepository.AddedRequest.Status,
                Is.EqualTo(SellerRequestStatus.Pending));

            Assert.That(
                result,
                Is.EqualTo(sellerRequestRepository.AddedRequest.Id));

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.True);

            Assert.That(
                userRepository.LastSellerStatus,
                Is.EqualTo(SellerStatus.Pending));
        });
    }

    [Test]
    public async Task CreateSellerRequest_should_throw_unauthorized_when_current_user_is_not_authenticated()
    {
        var currentUser = new FakeCurrentUser
        {
            UserId = null
        };

        var sellerRequestRepository = new FakeSellerRequestRepository();
        var unitOfWork = new FakeUnitOfWork();

        var userRepository = new FakeUserRepository
        {
            SellerStatus = SellerStatus.None
        };

        var handler = new CreateSellerRequestCommandHandler(
            sellerRequestRepository,
            userRepository,
            currentUser,
            unitOfWork);

        var command = new CreateSellerRequestCommand(
            "I want to become a seller.");

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
                sellerRequestRepository.AddedRequest,
                Is.Null);

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.False);

            Assert.That(
                userRepository.LastSellerStatus,
                Is.Null);

            Assert.That(
                userRepository.SetSellerStatusCalled,
                Is.False);
        });
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public bool ExistsResult { get; set; }

        public bool SetSellerStatusCalled { get; private set; }

        public Guid? LastUserId { get; private set; }

        public SellerStatus? LastSellerStatus { get; private set; }

        public SellerStatus SellerStatus { get; set; } = SellerStatus.None;

        public Task<bool> ExistsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistsResult);
        }

        public Task SetSellerStatusAsync(
            Guid userId,
            SellerStatus status,
            CancellationToken cancellationToken)
        {
            SetSellerStatusCalled = true;
            LastUserId = userId;
            LastSellerStatus = status;

            return Task.CompletedTask;
        }

        public Task<SellerStatus> GetSellerStatusAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SellerStatus);
        }
    }

    private sealed class FakeSellerRequestRepository : ISellerRequestRepository
    {
        public SellerRequest? AddedRequest { get; private set; }

        public SellerRequest? Request { get; set; }

        public SellerRequest? PendingRequest { get; set; }

        public Task AddAsync(
            SellerRequest sellerRequest,
            CancellationToken cancellationToken = default)
        {
            AddedRequest = sellerRequest;
            return Task.CompletedTask;
        }

        public Task<SellerRequest?> GetByIdAsync(
            Guid sellerRequestId,
            CancellationToken cancellationToken = default)
        {
            if (Request is null || Request.Id != sellerRequestId)
                return Task.FromResult<SellerRequest?>(null);

            return Task.FromResult<SellerRequest?>(Request);
        }

        public Task<SellerRequest?> GetPendingByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (PendingRequest is null ||
                PendingRequest.UserId != userId ||
                PendingRequest.Status != SellerRequestStatus.Pending)
            {
                return Task.FromResult<SellerRequest?>(null);
            }

            return Task.FromResult<SellerRequest?>(PendingRequest);
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

        public List<string> Roles { get; } = [];

        public bool IsAuthenticated =>
            UserId.HasValue;

        public bool IsInRole(string role) =>
            Roles.Contains(role);
    }

    private sealed class FakeUserRoleService : IUserRoleService
    {
        public bool AddToRoleCalled { get; private set; }
        public Guid? LastUserId { get; private set; }
        public string? LastRole { get; private set; }

        public Task AddToRoleAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            AddToRoleCalled = true;
            LastUserId = userId;
            LastRole = role;

            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task GetSellerRequestById_should_throw_not_found_when_request_does_not_exist()
    {
        var sellerRequestRepository = new FakeSellerRequestRepository();

        var currentUser = new FakeCurrentUser
        {
            UserId = Guid.NewGuid()
        };

        var handler = new GetSellerRequestByIdQueryHandler(
            sellerRequestRepository,
            currentUser);

        var query = new GetSellerRequestByIdQuery(
            Guid.NewGuid());

        var exception = Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                query,
                CancellationToken.None));

        Assert.That(
            exception!.Message,
            Does.Contain("Seller request"));
    }

    [Test]
    public async Task GetSellerRequestById_should_return_mapped_dto_when_request_exists()
    {
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var sellerRequest = SellerRequest.Create(
            userId,
            "I want to become a seller.");

        sellerRequest.Approve(adminUserId);

        var sellerRequestRepository = new FakeSellerRequestRepository
        {
            Request = sellerRequest
        };

        var  currentUser = new FakeCurrentUser
        {
            UserId = userId
        };

        var handler = new GetSellerRequestByIdQueryHandler(
            sellerRequestRepository,
            currentUser);

        var query = new GetSellerRequestByIdQuery(
            sellerRequest.Id);

        var result = await handler.Handle(
            query,
            CancellationToken.None);

        Assert.That(result, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                result!.Id,
                Is.EqualTo(sellerRequest.Id));

            Assert.That(
                result.UserId,
                Is.EqualTo(userId));

            Assert.That(
                result.Status,
                Is.EqualTo(SellerRequestStatus.Approved));

            Assert.That(
                result.Reason,
                Is.EqualTo("I want to become a seller."));

            Assert.That(
                result.ReviewedByUserId,
                Is.EqualTo(adminUserId));

            Assert.That(
                result.ReviewedAt,
                Is.EqualTo(sellerRequest.ReviewedAt));
        });
    }

    [Test]
    public async Task GetSellerRequestById_should_throw_unauthorized_when_user_is_not_authenticated()
    {
        var sellerRequest = SellerRequest.Create(
            Guid.NewGuid(),
            "I want to become a seller.");

        var sellerRequestRepository = new FakeSellerRequestRepository
        {
            Request = sellerRequest
        };

        var currentUser = new FakeCurrentUser
        {
            UserId = null
        };

        var handler = new GetSellerRequestByIdQueryHandler(
            sellerRequestRepository,
            currentUser);

        var exception = Assert.ThrowsAsync<UnauthorizedException>(
            async () => await handler.Handle(
                new GetSellerRequestByIdQuery(sellerRequest.Id),
                CancellationToken.None));

        Assert.That(
            exception,
            Is.Not.Null);
    }

    [Test]
    public async Task GetSellerRequestById_should_throw_forbidden_when_user_is_not_owner()
    {
        var ownerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        var sellerRequest = SellerRequest.Create(
            ownerId,
            "I want to become a seller.");

        var sellerRequestRepository = new FakeSellerRequestRepository
        {
            Request = sellerRequest
        };

        var currentUser = new FakeCurrentUser
        {
            UserId = anotherUserId
        };

        var handler = new GetSellerRequestByIdQueryHandler(
            sellerRequestRepository,
            currentUser);

        var exception = Assert.ThrowsAsync<ForbiddenException>(
            async () => await handler.Handle(
                new GetSellerRequestByIdQuery(sellerRequest.Id),
                CancellationToken.None));

        Assert.That(
            exception!.Message,
            Does.Contain("not allowed"));
    }

    [Test]
    public async Task GetSellerRequestById_should_allow_admin_to_view_any_request()
    {
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var sellerRequest = SellerRequest.Create(
            userId,
            "I want to become a seller.");

        var sellerRequestRepository = new FakeSellerRequestRepository
        {
            Request = sellerRequest
        };

        var currentUser = new FakeCurrentUser
        {
            UserId = adminUserId
        };

        // FakeCurrentUser فعلی را طوری تنظیم کن که بتواند Role را هم تعیین کند.
        currentUser.Roles.Add(ApplicationRoles.Admin);

        var handler = new GetSellerRequestByIdQueryHandler(
            sellerRequestRepository,
            currentUser);

        var result = await handler.Handle(
            new GetSellerRequestByIdQuery(sellerRequest.Id),
            CancellationToken.None);

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task GetSellerRequestById_should_return_request_for_owner()
    {
        var userId = Guid.NewGuid();

        var sellerRequest = SellerRequest.Create(
            userId,
            "I want to become a seller.");

        var sellerRequestRepository = new FakeSellerRequestRepository
        {
            Request = sellerRequest
        };

        var currentUser = new FakeCurrentUser
        {
            UserId = userId
        };

        var handler = new GetSellerRequestByIdQueryHandler(
            sellerRequestRepository,
            currentUser);

        var result = await handler.Handle(
            new GetSellerRequestByIdQuery(sellerRequest.Id),
            CancellationToken.None);

        Assert.That(result, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(result!.Id, Is.EqualTo(sellerRequest.Id));
            Assert.That(result.UserId, Is.EqualTo(userId));
            Assert.That(result.Status, Is.EqualTo(SellerRequestStatus.Pending));
            Assert.That(result.Reason, Is.EqualTo("I want to become a seller."));
        });
    }

    [Test]
    public async Task ApproveSellerRequest_should_throw_not_found_when_request_does_not_exist()
    {
        var sellerRequestRepository = new FakeSellerRequestRepository();
        var userRepository = new FakeUserRepository
        {
            ExistsResult = true
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = Guid.NewGuid()
        };

        var userRoleService = new FakeUserRoleService();

        var handler = new ApproveSellerRequestCommandHandler(
            sellerRequestRepository,
            userRepository,
            userRoleService,
            unitOfWork,
            currentUser);

        var command = new ApproveSellerRequestCommand(
            Guid.NewGuid());

        var exception = Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(
            command,
            CancellationToken.None));

        Assert.That(
            exception!.Message,
            Does.Contain("Seller request"));

        Assert.Multiple(() =>
        {
            Assert.That(
                userRepository.SetSellerStatusCalled,
                Is.False);

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.False);
        });
    }

    [Test]
    public async Task ApproveSellerRequest_should_approve_request_activate_user_and_save_changes()
    {
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var sellerRequest = SellerRequest.Create(
            userId,
            "I want to become a seller.");

        var sellerRequestRepository = new FakeSellerRequestRepository
        {
            Request = sellerRequest
        };

        var userRepository = new FakeUserRepository
        {
            ExistsResult = true
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = adminUserId
        };

        var userRoleService = new FakeUserRoleService();

        var handler = new ApproveSellerRequestCommandHandler(
            sellerRequestRepository,
            userRepository,
            userRoleService,
            unitOfWork,
            currentUser);

        var command = new ApproveSellerRequestCommand(
            sellerRequest.Id);

        await handler.Handle(
            command,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                userRoleService.AddToRoleCalled,
                Is.True);

            Assert.That(
                userRoleService.LastUserId,
                Is.EqualTo(userId));

            Assert.That(
                userRoleService.LastRole,
                Is.EqualTo(ApplicationRoles.Seller));

            Assert.That(
                sellerRequest.Status,
                Is.EqualTo(SellerRequestStatus.Approved));

            Assert.That(
                sellerRequest.ReviewedByUserId,
                Is.EqualTo(adminUserId));

            Assert.That(
                sellerRequest.ReviewedAt,
                Is.Not.Null);

            Assert.That(
                userRepository.SetSellerStatusCalled,
                Is.True);

            Assert.That(
                userRepository.LastUserId,
                Is.EqualTo(userId));

            Assert.That(
                userRepository.LastSellerStatus,
                Is.EqualTo(SellerStatus.Active));

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.True);
        });
    }

    [Test]
    public async Task ApproveSellerRequest_should_throw_unauthorized_when_current_user_is_not_authenticated()
    {
        var sellerRequest = SellerRequest.Create(
            Guid.NewGuid(),
            "I want to become a seller.");

        var sellerRequestRepository = new FakeSellerRequestRepository
        {
            Request = sellerRequest
        };

        var userRepository = new FakeUserRepository
        {
            ExistsResult = true
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = null
        };

        Assert.That(currentUser.UserId, Is.Null);
        Assert.That(currentUser.IsAuthenticated, Is.False);

        var userRoleService = new FakeUserRoleService();

        var handler = new ApproveSellerRequestCommandHandler(
            sellerRequestRepository,
            userRepository,
            userRoleService,
            unitOfWork,
            currentUser);

        var command = new ApproveSellerRequestCommand(
            sellerRequest.Id);

        var exception = Assert.ThrowsAsync<UnauthorizedException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(
                exception,
                Is.Not.Null);

            Assert.That(
                exception!.Message,
                Does.Contain("Authenticated user"));

            Assert.That(
                userRepository.SetSellerStatusCalled,
                Is.False);

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.False);
        });
    }

    [Test]
    public async Task RejectSellerRequest_should_throw_not_found_when_request_does_not_exist()
    {
        var sellerRequestRepository = new FakeSellerRequestRepository();

        var userRepository = new FakeUserRepository
        {
            ExistsResult = true
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = Guid.NewGuid()
        };

        var handler = new RejectSellerRequestCommandHandler(
            sellerRequestRepository,
            userRepository,
            unitOfWork,
            currentUser);

        var command = new RejectSellerRequestCommand(
            Guid.NewGuid());

        var exception = Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(
            command,
            CancellationToken.None));

        Assert.That(
            exception!.Message,
            Does.Contain("Seller request"));

        Assert.Multiple(() =>
        {
            Assert.That(
                userRepository.SetSellerStatusCalled,
                Is.False);

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.False);
        });
    }

    [Test]
    public async Task RejectSellerRequest_should_reject_request_deactivate_user_and_save_changes()
    {
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var sellerRequest = SellerRequest.Create(
            userId,
            "I want to become a seller.");

        var sellerRequestRepository = new FakeSellerRequestRepository
        {
            Request = sellerRequest
        };

        var userRepository = new FakeUserRepository
        {
            ExistsResult = true
        };

        var unitOfWork = new FakeUnitOfWork();

        var currentUser = new FakeCurrentUser
        {
            UserId = adminUserId
        };

        var handler = new RejectSellerRequestCommandHandler(
            sellerRequestRepository,
            userRepository,
            unitOfWork,
            currentUser);

        var command = new RejectSellerRequestCommand(
            sellerRequest.Id);

        await handler.Handle(
            command,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                sellerRequest.Status,
                Is.EqualTo(SellerRequestStatus.Rejected));

            Assert.That(
                sellerRequest.ReviewedByUserId,
                Is.EqualTo(adminUserId));

            Assert.That(
                sellerRequest.ReviewedAt,
                Is.Not.Null);

            Assert.That(
                userRepository.SetSellerStatusCalled,
                Is.True);

            Assert.That(
                userRepository.LastUserId,
                Is.EqualTo(userId));

            Assert.That(
                userRepository.LastSellerStatus,
                Is.EqualTo(SellerStatus.Rejected));

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.True);
        });
    }

    [Test]
    public async Task CreateSellerRequest_should_throw_conflict_when_user_status_is_pending()
    {
        var userId = Guid.NewGuid();

        var currentUser = new FakeCurrentUser
        {
            UserId = userId
        };

        var sellerRequestRepository = new FakeSellerRequestRepository();

        var userRepository = new FakeUserRepository
        {
            SellerStatus = SellerStatus.Pending
        };

        var unitOfWork = new FakeUnitOfWork();

        var handler = new CreateSellerRequestCommandHandler(
            sellerRequestRepository,
            userRepository,
            currentUser,
            unitOfWork);

        var exception = Assert.ThrowsAsync<BusinessRuleException>(
            async () => await handler.Handle(
                new CreateSellerRequestCommand("Another request."),
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(
                exception!.Message,
                Does.Contain("pending"));

            Assert.That(
                sellerRequestRepository.AddedRequest,
                Is.Null);

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.False);
        });
    }

    [Test]
    public async Task CreateSellerRequest_should_throw_conflict_when_user_is_already_active_seller()
    {
        var userId = Guid.NewGuid();

        var currentUser = new FakeCurrentUser
        {
            UserId = userId
        };

        var sellerRequestRepository = new FakeSellerRequestRepository();

        var userRepository = new FakeUserRepository
        {
            SellerStatus = SellerStatus.Active
        };

        var unitOfWork = new FakeUnitOfWork();

        var handler = new CreateSellerRequestCommandHandler(
            sellerRequestRepository,
            userRepository,
            currentUser,
            unitOfWork);

        var exception = Assert.ThrowsAsync<BusinessRuleException>(
            async () => await handler.Handle(
                new CreateSellerRequestCommand("Another request."),
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(
                exception!.Message,
                Does.Contain("already an active seller"));

            Assert.That(
                sellerRequestRepository.AddedRequest,
                Is.Null);

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.False);
        });
    }

    [Test]
    public async Task CreateSellerRequest_should_throw_conflict_when_seller_is_suspended()
    {
        var userId = Guid.NewGuid();

        var currentUser = new FakeCurrentUser
        {
            UserId = userId
        };

        var sellerRequestRepository = new FakeSellerRequestRepository();

        var userRepository = new FakeUserRepository
        {
            SellerStatus = SellerStatus.Suspended
        };

        var unitOfWork = new FakeUnitOfWork();

        var handler = new CreateSellerRequestCommandHandler(
            sellerRequestRepository,
            userRepository,
            currentUser,
            unitOfWork);

        var exception = Assert.ThrowsAsync<BusinessRuleException>(
            async () => await handler.Handle(
                new CreateSellerRequestCommand("Another request."),
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(
                exception!.Message,
                Does.Contain("Suspended seller"));

            Assert.That(
                sellerRequestRepository.AddedRequest,
                Is.Null);

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.False);
        });
    }

    [Test]
    public async Task CreateSellerRequest_should_allow_user_to_reapply_after_rejection()
    {
        var userId = Guid.NewGuid();

        var currentUser = new FakeCurrentUser
        {
            UserId = userId
        };

        var sellerRequestRepository = new FakeSellerRequestRepository();

        var userRepository = new FakeUserRepository
        {
            SellerStatus = SellerStatus.Rejected
        };

        var unitOfWork = new FakeUnitOfWork();

        var handler = new CreateSellerRequestCommandHandler(
            sellerRequestRepository,
            userRepository,
            currentUser,
            unitOfWork);

        var result = await handler.Handle(
            new CreateSellerRequestCommand("I want to try again."),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.EqualTo(Guid.Empty));

            Assert.That(
                sellerRequestRepository.AddedRequest,
                Is.Not.Null);

            Assert.That(
                sellerRequestRepository.AddedRequest!.UserId,
                Is.EqualTo(userId));

            Assert.That(
                sellerRequestRepository.AddedRequest.Status,
                Is.EqualTo(SellerRequestStatus.Pending));

            Assert.That(
                userRepository.LastSellerStatus,
                Is.EqualTo(SellerStatus.Pending));

            Assert.That(
                unitOfWork.SaveChangesCalled,
                Is.True);
        });
    }
}
