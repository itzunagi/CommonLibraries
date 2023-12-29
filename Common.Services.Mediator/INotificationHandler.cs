using System.Threading;
using System.Threading.Tasks;

namespace Common.Services.Mediator;

/// <summary>
/// Defines a handler for a notification
/// </summary>
/// <typeparam name="TNotification">The type of notification being handled</typeparam>
public interface INotificationHandler<in TNotification> : INotificationHandler
    where TNotification : INotification
{
    /// <summary>
    /// Handles a notification
    /// </summary>
    /// <param name="notification">The notification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Defines a generic handler for a notification of any type
/// </summary>
public interface INotificationHandler
{
}
