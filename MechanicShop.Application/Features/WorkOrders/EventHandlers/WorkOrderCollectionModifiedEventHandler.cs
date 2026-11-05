using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Workorders.Events;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace MechanicShop.Application.Features.WorkOrders.EventHandlers
{
    public class WorkOrderCollectionModifiedEventHandler(IWorkOrderNotifier notifier) :
        INotificationHandler<WorkOrderCollectionModified>
    {
        private readonly IWorkOrderNotifier _notifier = notifier;

        public Task Handle(WorkOrderCollectionModified notification, CancellationToken cancellationToken)
        {
            return _notifier.NotifyWorkOrdersChangedAsync(
                notification.BranchId,
                notification.LaborId,
                cancellationToken);
            
        }
    }

}
