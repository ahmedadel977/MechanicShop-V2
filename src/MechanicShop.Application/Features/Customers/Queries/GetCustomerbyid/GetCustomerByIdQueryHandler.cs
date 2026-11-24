using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Customers.Mappers;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace MechanicShop.Application.Features.Customers.Queries.GetCustomerbyid
{
    public sealed class GetCustomerByIdQueryHandler(IAppDbContext context, ILogger<GetCustomerByIdQueryHandler> logger) : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
    {
        private readonly IAppDbContext _context = context;
        private readonly ILogger<GetCustomerByIdQueryHandler> _logger = logger;
        public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
        {
            var customer = await _context.Customers.AsNoTracking()
               .Include(c => c.Vehicles)
               .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
            if (customer == null)
            {
              _logger.LogWarning("Customer with id '{CustomerId}' was not found", request.CustomerId);
                return Error.NotFound(
                code: "Customer_NotFound",
                description: $"Customer with id '{request.CustomerId}' was not found");
            }

            return customer.ToDto();
        }
    }
}
