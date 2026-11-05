using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Identity.Commands.Logout;

public sealed record LogoutCommand(string UserId) : IRequest<Result<Updated>>;
