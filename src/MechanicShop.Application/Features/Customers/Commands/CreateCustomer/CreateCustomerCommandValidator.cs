using FluentValidation;

using Microsoft.Extensions.Logging;
using System.Xml.Linq;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer
{
    public sealed class CreateCustomerCommandValidator: AbstractValidator<CreateCustomerCommand>
    {
        public CreateCustomerCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("nameisregruied").MaximumLength(50);
            RuleFor(x => x.Email).EmailAddress().WithMessage("invaild").MaximumLength(50);

            RuleFor(x => x.PhoneNumber)
             .NotEmpty().WithMessage("Phone number is required.")
             .Matches(@"^\+?\d{7,15}$").WithMessage("Phone number must be 7–15 digits and may start with '+'.");

            RuleFor(x => x.Vehicles)
                .NotNull().WithMessage("Vehicle list cannot be null.")
                .Must (x=>x.Count>0).WithMessage("At least one vehicle is required.");
            RuleForEach(x => x.Vehicles).SetValidator(new CreateVehicleCommandValidator());

        }

    }







}
