
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Customers.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

public class CreateCustomerCommandHandler(
    ILogger<CreateCustomerCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache
    )
    : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly ILogger<CreateCustomerCommandHandler> _logger = logger;
    private readonly IAppDbContext _context = context;
    private readonly HybridCache _cache = cache;

    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim().ToLower();

        var exists = await _context.Customers.AnyAsync(
            c => c.Email!.ToLower() == email,
            ct);

        if (exists)
        {
            _logger.LogWarning("Customer creation aborted. Email already exists.");

            return CustomerErrors.CustomerExists;
        }

        List<Vehicle> vehicles = [];

        foreach (var v in command.Vehicles)
        {
            var vehicleResult = Vehicle.Create(Guid.NewGuid(), v.Make, v.Model, v.Year, v.LicensePlate);

            if (vehicleResult.IsError)
            {
                return vehicleResult.Errors;
            }

            vehicles.Add(vehicleResult.Value);
        }

        var customercreateresult = Customer.Create(Guid.NewGuid(), command.Name, command.PhoneNumber, command.Email, vehicles);
        if (customercreateresult.IsError)
        {
            return customercreateresult.Errors;
        }
        var customer = customercreateresult.Value;
        _context.Customers.Add(customer);
       await _context.SaveChangesAsync(ct);
        await _cache.RemoveByTagAsync ("customer", ct);
        return customer.ToDto();


    }
}