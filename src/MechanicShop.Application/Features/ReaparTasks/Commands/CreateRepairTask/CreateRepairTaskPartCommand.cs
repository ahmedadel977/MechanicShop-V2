using MechanicShop.Application.Features.RepairTasks.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.ReaparTasks.Commands.CreateRepairTask;
public sealed record CreateRepairTaskPartCommand(
    string Name,
    decimal Cost,
    int Quantity
) : IRequest<Result<Success>>;

