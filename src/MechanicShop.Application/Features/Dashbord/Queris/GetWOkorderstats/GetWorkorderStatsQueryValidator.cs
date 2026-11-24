using FluentValidation;

namespace MechanicShop.Application.Features.Dashbord.Queris.GetWOkorderstats
{
    public class GetWorkorderStatsQueryValidator : AbstractValidator<GetWorkorderStatsQuery>
    {
        public GetWorkorderStatsQueryValidator()
        {
            RuleFor(x => x.Date)
                .NotEmpty().WithMessage("Date is required.");
        }
    }


}
