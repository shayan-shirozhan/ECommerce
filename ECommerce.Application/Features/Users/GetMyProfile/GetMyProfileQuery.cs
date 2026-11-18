using MediatR;

namespace ECommerce.Application.Features.Users.GetMyProfile;

public sealed record GetMyProfileQuery : IRequest<GetMyProfileResponse>;
