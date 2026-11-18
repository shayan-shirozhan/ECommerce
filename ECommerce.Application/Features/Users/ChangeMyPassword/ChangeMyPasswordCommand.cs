using MediatR;

namespace ECommerce.Application.Features.Users.ChangeMyPassword;

public sealed record ChangeMyPasswordCommand(
    string CurrentPassword,
    string NewPassword) : IRequest;
