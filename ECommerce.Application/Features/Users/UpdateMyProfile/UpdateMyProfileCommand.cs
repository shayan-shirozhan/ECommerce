using MediatR;

namespace ECommerce.Application.Features.Users.UpdateMyProfile;

public sealed record UpdateMyProfileCommand(
    string FirstName,
    string LastName) : IRequest;
