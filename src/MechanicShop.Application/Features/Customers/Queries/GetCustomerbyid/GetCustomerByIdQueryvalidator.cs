using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Queries.GetCustomerbyid
{
    public class GetCustomerByIdQueryvalidator : AbstractValidator<GetCustomerByIdQuery>
    {
        public GetCustomerByIdQueryvalidator()
        {
            RuleFor(x => x.CustomerId).NotEmpty().WithMessage("CustomerId is required.");
        }
    }
}
