using System.Threading;
using System.Threading.Tasks;
using HuTao.Services.Core.Messages;
using MediatR;

namespace HuTao.Services.TimeTracking;

public class GenshinTimeTrackingBehavior : INotificationHandler<ReadyNotification>
{
    public Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}