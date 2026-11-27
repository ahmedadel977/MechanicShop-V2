using FluentValidation;

namespace MechanicShop.Application.Features.Dashbord.Queris.GetWOkorderstats
{
    public class GetWorkorderStatsQueryValidator : AbstractValidator<GetWorkorderStatsQuery>
    {
        public GetWorkorderStatsQueryValidator()
        {
            RuleFor(x => x.FromUtc)
                .Must(fromUtc => fromUtc != default)
                .WithMessage("FromUtc is required.");

            RuleFor(x => x.ToUtc)
                .Must(toUtc => toUtc != default)
                .WithMessage("ToUtc is required.");

            RuleFor(x => x.FromUtc)
                .Must((query, fromUtc) => fromUtc < query.ToUtc)
                .WithMessage("FromUtc must be earlier than ToUtc.");
        }
    }
}
