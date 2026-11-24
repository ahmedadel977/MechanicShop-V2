using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Domain.Common.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace MechanicShop.Application.Features.Customers.Queries.GetCustomerbyid
{
   public sealed record GetCustomerByIdQuery : ICachedQuery<Result<CustomerDto>>
    {
        public GetCustomerByIdQuery(Guid customerId)
        {
            CustomerId = customerId;
        }

        public Guid CustomerId { get; }
        public string CacheKey => $"GetCustomerById:{CustomerId}";
        public string[] Tags => new[] { "customer" };
        public TimeSpan Expiration => TimeSpan.FromMinutes(5);
    }
}
