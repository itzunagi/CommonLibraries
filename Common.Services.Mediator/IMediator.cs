using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Services.Mediator;

public interface IMediator : IPublisher
{
    /// <summary>
    /// Register a notification handler with the mediator
    /// </summary>
    /// <typeparam name="TNotification"></typeparam>
    /// <param name="handler">handler for the notification type</param>
    void RegisterHandler<TNotification>(INotificationHandler<TNotification> handler)
        where TNotification : INotification;
}

internal class Mediator : IMediator, IPublisher
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<INotificationHandler>> _notificationHandlers = new();

    /// <inheritdoc />
    public void RegisterHandler<TNotification>(INotificationHandler<TNotification> handler) where TNotification : INotification
    {
        var collection = _notificationHandlers.GetOrAdd(typeof(TNotification),
            _ => new ConcurrentBag<INotificationHandler>());
        collection.Add(handler);
    }

    /// <inheritdoc />
    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        var handlers = _notificationHandlers.GetOrAdd(typeof(TNotification),
                _ => new ConcurrentBag<INotificationHandler>())
            .ToList()
            .Cast<INotificationHandler<TNotification>>();

        foreach (var handler in handlers)
        {
            await handler.Handle(notification, cancellationToken);
        }
    }
}
