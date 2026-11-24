using MechanicShop.Application.Features.Scheduling.Queries.GetDailyScheduleQuery;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Workorders.Enums;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Queries.GetDailyScheduleQuery;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetDailyScheduleQueryHandlerTests(WebAppFactory factory)
{
    private readonly MediatR.IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenDateHasNoWorkOrders_ShouldReturnAllSpotsWithQuarterHourSlots()
    {
        var date = new DateOnly(2001, 1, 1);
        var result = await _mediator.Send(
            new MechanicShop.Application.Features.Scheduling.Queries.GetDailyScheduleQuery.GetDailyScheduleQuery(TimeZoneInfo.Utc, date),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(date, result.Value.OnDate);
        Assert.True(result.Value.EndOfDay);
        Assert.Equal(Enum.GetValues<Spot>().Length, result.Value.Spots.Count);
        foreach (var spot in result.Value.Spots)
        {
            Assert.Equal(96, spot.Slots.Count);
            Assert.All(spot.Slots, slot => Assert.False(slot.IsAvailable));
            Assert.All(spot.Slots, slot => Assert.False(slot.IsOccupied));
        }
    }

    [Fact]
    public async Task Handle_WhenWorkOrderOverlapsTheDate_ShouldReturnItAsOccupied()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var date = DateOnly.FromDateTime(order.StartAtUtc.UtcDateTime);

        var result = await _mediator.Send(
            new MechanicShop.Application.Features.Scheduling.Queries.GetDailyScheduleQuery.GetDailyScheduleQuery(TimeZoneInfo.Utc, date),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var slot = Assert.Single(
            result.Value.Spots.SelectMany(spot => spot.Slots),
            item => item.WorkOrderId == order.Id);
        Assert.True(slot.IsOccupied);
        Assert.False(slot.IsAvailable);
        Assert.Equal(order.Spot, slot.Spot);
        Assert.Equal(order.Id, slot.WorkOrderId);
    }

    [Fact]
    public async Task Handle_WhenLaborFilterIsProvided_ShouldOnlyIncludeThatLaborsWorkOrders()
    {
        var first = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var second = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            first.StartAtUtc.AddHours(2),
            TestContext.Current.CancellationToken);
        var date = DateOnly.FromDateTime(first.StartAtUtc.UtcDateTime);

        var result = await _mediator.Send(
            new MechanicShop.Application.Features.Scheduling.Queries.GetDailyScheduleQuery.GetDailyScheduleQuery(
                TimeZoneInfo.Utc,
                date,
                first.LaborId),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var workOrderIds = result.Value.Spots
            .SelectMany(spot => spot.Slots)
            .Where(slot => slot.WorkOrderId.HasValue)
            .Select(slot => slot.WorkOrderId!.Value)
            .Distinct()
            .ToArray();
        Assert.Equal([first.Id], workOrderIds);
        Assert.DoesNotContain(second.Id, workOrderIds);
    }

    [Fact]
    public async Task Handle_WhenWorkOrderHasNoLabor_ShouldReturnOccupiedSlotWithoutLabor()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken,
            includeLabor: false);
        var date = DateOnly.FromDateTime(order.StartAtUtc.UtcDateTime);

        var result = await _mediator.Send(
            new MechanicShop.Application.Features.Scheduling.Queries.GetDailyScheduleQuery.GetDailyScheduleQuery(TimeZoneInfo.Utc, date),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var slot = Assert.Single(
            result.Value.Spots.SelectMany(spot => spot.Slots),
            item => item.WorkOrderId == order.Id);
        Assert.True(slot.IsOccupied);
        Assert.Null(slot.Labor);
    }
}
