namespace FreshOrbit.Notifications.Public;

/// <summary>What other modules may ask the Notifications module.</summary>
public interface INotificationsApi
{
    /// <summary>
    /// Queues a templated message for a user (sent later by the Worker). Tracked on the shared DbContext and saved by
    /// the caller; never saves.
    /// </summary>
    Task QueueAsync(QueueNotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>As <see cref="QueueAsync"/> with the recipient given as a customer id. Unknown customer → nothing queued.</summary>
    Task QueueForCustomerAsync(QueueCustomerNotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Stored as SMS / EMAIL / PUSH / IN_APP.</summary>
public enum NotificationChannel
{
    Sms,
    Email,
    Push,
    InApp,
}

/// <param name="Channels">Channels to use; null means every channel the template and the user's preferences allow.</param>
public sealed record QueueNotificationRequest(Guid UserId, string TemplateCode, IReadOnlyDictionary<string, string> Data, IReadOnlyList<NotificationChannel>? Channels)
{
    /// <summary>
    /// Makes queuing idempotent: with the same key (normally the source event id) a channel gets at most one message.
    /// </summary>
    public string? DedupeKey { get; init; }
}

/// <param name="Channels">Channels to use; null means every channel the template and the user's preferences allow.</param>
/// <param name="DedupeKey">Normally the source event id; see <see cref="QueueNotificationRequest.DedupeKey"/>.</param>
public sealed record QueueCustomerNotificationRequest(Guid CustomerId, string TemplateCode, IReadOnlyDictionary<string, string> Data, IReadOnlyList<NotificationChannel>? Channels, string? DedupeKey);
