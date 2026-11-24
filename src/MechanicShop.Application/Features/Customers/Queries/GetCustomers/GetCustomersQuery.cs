using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Domain.Common.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace MechanicShop.Application.Features.Customers.Queries.GetCustomers
{
    public sealed record GetCustomersQuery  :ICachedQuery <Result<List<CustomerDto>>>
    {
        public string CacheKey => "GetCustomers";
        public string[] Tags => new[] { "customer" };
        public TimeSpan Expiration => TimeSpan.FromMinutes(5);
    }

}
