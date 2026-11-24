using MechanicShop.Application.Features.RepairTasks.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace MechanicShop.Application.Features.ReaparTasks.Commands.CreateRepairTask
{
    public sealed record CreateRepairTaskCommand(
      string? Name,
      decimal LaborCost,
      RepairDurationInMinutes? EstimatedDurationInMins,
      List<CreateRepairTaskPartCommand> Parts
  ) : IRequest<Result<RepairTaskDto>>;
}

