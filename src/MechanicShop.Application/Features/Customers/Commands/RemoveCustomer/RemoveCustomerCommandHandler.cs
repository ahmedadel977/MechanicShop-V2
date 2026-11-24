using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;

public class RemoveCustomerCommandHandler(ILogger<RemoveCustomerCommandHandler> logger, IAppDbContext context, HybridCache cache)
    : IRequestHandler<RemoveCustomerCommand, Result<Deleted>>
{
    private readonly ILogger<RemoveCustomerCommandHandler> _logger = logger;
    private readonly IAppDbContext _context = context;
    private readonly HybridCache _cache = cache;

    public async Task<Result<Deleted>> Handle(RemoveCustomerCommand request, CancellationToken cancellationToken)
    {
       var customer =await _context.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (customer is null)
        {
            _logger.LogWarning("Customer with ID {CustomerId} not found.", request.CustomerId);
            return ApplicationErrors.CustomerNotFound;
        }
        var hasAssociatedWorkOrders = await _context.WorkOrders
            .AnyAsync(w => w.Vehicle != null && w.Vehicle.CustomerId == request.CustomerId, cancellationToken);
        if (hasAssociatedWorkOrders)
        {
            _logger.LogWarning("Customer with ID {CustomerId} has associated work orders.", request.CustomerId);
            return CustomerErrors.CannotDeleteCustomerWithWorkOrders;
        }
        _context.Customers.Remove(customer);
       await _context.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByTagAsync("customer", cancellationToken);
        _logger.LogInformation("Customer with ID {CustomerId} removed successfully.", request.CustomerId);
        return Result.Deleted;
    }
}

