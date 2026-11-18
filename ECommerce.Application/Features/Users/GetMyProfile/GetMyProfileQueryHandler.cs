using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Users.GetMyProfile;

public sealed class GetMyProfileQueryHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository)
    : IRequestHandler<GetMyProfileQuery, GetMyProfileResponse>
{
    public async Task<GetMyProfileResponse> Handle(
        GetMyProfileQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var profile = await userRepository.GetProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"User with id '{userId}' was not found.");

        return new GetMyProfileResponse(
            profile.Id,
            profile.Email,
            profile.FirstName,
            profile.LastName,
            profile.SellerStatus);
    }
}
