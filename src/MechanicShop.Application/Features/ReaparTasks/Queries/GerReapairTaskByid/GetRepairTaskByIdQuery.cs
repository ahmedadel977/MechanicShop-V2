using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Dtos;
using MechanicShop.Domain.Common.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace MechanicShop.Application.Features.ReaparTasks.Queries.GerReapairTaskByid
{
    public sealed record GetRepairTaskByIdQuery(Guid RepairTaskId) : ICachedQuery<Result<RepairTaskDto>>
    {
        public string CacheKey => $"repair-task-{RepairTaskId}";

        public string[] Tags => ["repair-task"];

        public TimeSpan Expiration => TimeSpan.FromMinutes(10);
    }
}


