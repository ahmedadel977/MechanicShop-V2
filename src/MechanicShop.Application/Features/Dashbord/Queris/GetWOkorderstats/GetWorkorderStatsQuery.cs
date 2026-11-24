using MechanicShop.Application.Features.Dashboard.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace MechanicShop.Application.Features.Dashbord.Queris.GetWOkorderstats
{
    public sealed record GetWorkorderStatsQuery (DateOnly Date) : IRequest<Result<TodayWorkOrderStatsDto>>
    {
    }


}
