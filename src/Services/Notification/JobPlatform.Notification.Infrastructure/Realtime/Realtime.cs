using JobPlatform.Notification.Application;
using JobPlatform.Notification.Application.DTOs.InApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace JobPlatform.Notification.Infrastructure.Realtime;

/// <summary>
/// SignalR hub /hubs/notifications (US-3.6.2-02): a signed-in user joins the group of their own account id, so a notification is only ever pushed to its
/// recipient (INV-04). The JWT travels as access_token on the connection. An offline user simply finds the notification Unread at next login.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public const string Route = "/hubs/notifications";
    public const string ReceiveMethod = "notification";

    private readonly ConnectionTracker _tracker;

    public NotificationHub(ConnectionTracker tracker) => _tracker = tracker;

    public static string Group(Guid accountId) => $"account:{accountId:N}";

    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirst("sub")?.Value, out var accountId))
        {
            _tracker.Connected(accountId);
            await Groups.AddToGroupAsync(Context.ConnectionId, Group(accountId));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Guid.TryParse(Context.User?.FindFirst("sub")?.Value, out var accountId))
        {
            _tracker.Disconnected(accountId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>Tracks live connections per account so "delivered in real time" is only claimed when someone was actually connected.</summary>
public sealed class ConnectionTracker
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int> _connections = new();

    public void Connected(Guid accountId) => _connections.AddOrUpdate(accountId, 1, (_, n) => n + 1);

    public void Disconnected(Guid accountId) => _connections.AddOrUpdate(accountId, 0, (_, n) => Math.Max(0, n - 1));

    public bool IsOnline(Guid accountId) => _connections.TryGetValue(accountId, out var n) && n > 0;
}

/// <summary>Adapter for <see cref="IRealtimeNotifier"/>: pushes through SignalR to the recipient's group.</summary>
public sealed class SignalRNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ConnectionTracker _tracker;

    public SignalRNotifier(IHubContext<NotificationHub> hub, ConnectionTracker tracker)
    {
        _hub = hub;
        _tracker = tracker;
    }

    public async Task<bool> PushAsync(Guid recipient, InAppNotificationDto notification, CancellationToken ct = default)
    {
        await _hub.Clients.Group(NotificationHub.Group(recipient)).SendAsync(NotificationHub.ReceiveMethod, notification, ct);
        return _tracker.IsOnline(recipient);
    }
}
