using FreshOrbit.Notifications.Domain.Devices;
using FreshOrbit.Notifications.Domain.Messages;
using FreshOrbit.Notifications.Domain.Preferences;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.Notifications.Domain.Templates;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Notifications.Infrastructure.Persistence.Repositories;

internal sealed class NotificationsRepository(FreshOrbitDbContext db) : INotificationsRepository
{
    public async Task<IReadOnlyList<Template>> GetTemplatesAsync(string code, CancellationToken cancellationToken)
        => await db.Set<Template>().Where(t => t.Code == code).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Template>> GetAllTemplatesAsync(CancellationToken cancellationToken)
        => await db.Set<Template>().OrderBy(t => t.Code).ThenBy(t => t.Channel).ToListAsync(cancellationToken);

    public Task<Template?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken)
        => db.Set<Template>().FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

    public void Add(Template newTemplate) => db.Add(newTemplate);

    public async Task<IReadOnlyList<Preference>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken)
        => await db.Set<Preference>().Where(p => p.UserId == userId).ToListAsync(cancellationToken);

    public void Add(Preference preference) => db.Add(preference);

    public async Task<bool> DedupeKeyExistsAsync(string composedKey, CancellationToken cancellationToken)
        => db.Set<Message>().Local.Any(m => m.DedupeKey == composedKey)
            || await db.Set<Message>().AnyAsync(m => m.DedupeKey == composedKey, cancellationToken);

    public void Add(Message message) => db.Add(message);

    public Task<Message?> GetMessageAsync(Guid userId, Guid messageId, CancellationToken cancellationToken)
        => db.Set<Message>().FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Message>> LockQueuedBatchAsync(int take, DateTimeOffset now, CancellationToken cancellationToken)
        => await db.Set<Message>()
            .FromSql($"SELECT m.*, m.xmin FROM notifications.messages m WHERE m.status = 'QUEUED' AND (m.next_attempt_at IS NULL OR m.next_attempt_at <= {now}) ORDER BY m.queued_at LIMIT {take} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Message> Items, int TotalCount)> GetInAppAsync(Guid userId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken)
    {
        var query = db.Set<Message>().AsNoTracking().Where(m => m.UserId == userId && m.Channel == Domain.Shared.Channel.InApp);
        query = unreadOnly ? query.Where(m => m.Status == MessageStatus.Sent) : query.Where(m => m.Status == MessageStatus.Sent || m.Status == MessageStatus.Read);
        var total = await query.CountAsync(cancellationToken);
        if (take == 0)
        {
            return ([], total);
        }

        var items = await query.OrderByDescending(m => m.QueuedAt).ThenByDescending(m => m.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
        => db.Set<Message>()
            .Where(m => m.UserId == userId && m.Channel == Domain.Shared.Channel.InApp && m.Status == MessageStatus.Sent)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MessageStatus.Read).SetProperty(m => m.ReadAt, now), cancellationToken);

    public Task<DeviceToken?> FindDeviceTokenAsync(string pushToken, CancellationToken cancellationToken)
        => db.Set<DeviceToken>().FirstOrDefaultAsync(t => t.PushToken == pushToken, cancellationToken);

    public async Task<IReadOnlyList<DeviceToken>> GetDeviceTokensAsync(Guid userId, CancellationToken cancellationToken)
        => await db.Set<DeviceToken>().Where(t => t.UserId == userId).ToListAsync(cancellationToken);

    public void Add(DeviceToken token) => db.Add(token);

    public void Remove(DeviceToken token) => db.Remove(token);

    public async Task<TResult> InTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (db.Database.CurrentTransaction is not null)
        {
            return await work();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await work();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
