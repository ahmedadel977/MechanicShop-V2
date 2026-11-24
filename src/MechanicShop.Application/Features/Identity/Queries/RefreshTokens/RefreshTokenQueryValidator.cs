using FluentValidation;

namespace MechanicShop.Application.Features.Identity.Queries.RefreshTokens;

public sealed class RefreshTokenQueryValidator : AbstractValidator<RefreshTokenQuery>
{
    public RefreshTokenQueryValidator()
    {
        RuleFor(query => query.RefreshToken).NotEmpty().MaximumLength(512);
        RuleFor(query => query.ExpiredAccessToken).NotEmpty().MaximumLength(16_384);
    }
}
