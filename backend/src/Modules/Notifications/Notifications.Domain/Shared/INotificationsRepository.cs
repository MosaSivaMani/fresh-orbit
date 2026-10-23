using FreshOrbit.Notifications.Domain.Devices;
using FreshOrbit.Notifications.Domain.Messages;
using FreshOrbit.Notifications.Domain.Preferences;
using FreshOrbit.Notifications.Domain.Templates;

namespace FreshOrbit.Notifications.Domain.Shared;

public interface INotificationsRepository
{
    Task<IReadOnlyList<Template>> GetTemplatesAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<Template>> GetAllTemplatesAsync(CancellationToken cancellationToken);

    Task<Template?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken);

    void Add(Template newTemplate);

    Task<IReadOnlyList<Preference>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Preference preference);

    /// <summary>True when a message with this composed dedupe key is pending in this unit of work or stored.</summary>
    Task<bool> DedupeKeyExistsAsync(string composedKey, CancellationToken cancellationToken);

    void Add(Message message);

    Task<Message?> GetMessageAsync(Guid userId, Guid messageId, CancellationToken cancellationToken);

    /// <summary>Locks up to <paramref name="take"/> QUEUED messages due at <paramref name="now"/> (FOR UPDATE SKIP LOCKED) in the open transaction.</summary>
    Task<IReadOnlyList<Message>> LockQueuedBatchAsync(int take, DateTimeOffset now, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Message> Items, int TotalCount)> GetInAppAsync(Guid userId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<DeviceToken?> FindDeviceTokenAsync(string pushToken, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeviceToken>> GetDeviceTokensAsync(Guid userId, CancellationToken cancellationToken);

    void Add(DeviceToken token);

    void Remove(DeviceToken token);

    Task<TResult> InTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken cancellationToken);
}
