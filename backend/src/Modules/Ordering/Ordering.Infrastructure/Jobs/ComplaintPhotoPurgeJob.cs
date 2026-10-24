using FreshOrbit.Ordering.Application.Abstractions;
using FreshOrbit.Ordering.Domain.Complaints;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Storage;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Ordering.Infrastructure.Jobs;

/// <summary>
/// ORD-R66 daily purge: photos of complaints closed (RESOLVED at <c>resolved_at</c>, REJECTED at <c>decided_at</c>)
/// more than <see cref="OrderingOptions.ComplaintPhotoRetentionDays"/> ago. Rows go first in one save, then the
/// files, so a failed file delete leaves an unreferenced private file, never a row without its file. At most
/// <see cref="BatchSize"/> photos per run; the next run takes the rest.
/// </summary>
public sealed partial class ComplaintPhotoPurgeJob(FreshOrbitDbContext db, IFileStorage storage, IClock clock, OrderingOptions options, ILogger<ComplaintPhotoPurgeJob> logger) : IRecurringJob
{
    public const string JobId = "ordering.purge-complaint-photos";
    public const int BatchSize = 500;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (options.ComplaintPhotoRetentionDays is not { } days)
        {
            LogNotConfigured();
            return;
        }

        if (days < 1)
        {
            throw new InvalidOperationException($"{OrderingOptions.SectionName}:{nameof(OrderingOptions.ComplaintPhotoRetentionDays)} must be at least 1 day.");
        }

        var cutoff = clock.UtcNow.AddDays(-days);
        var photos = await (
                from photo in db.Set<ComplaintPhoto>()
                join complaint in db.Set<Complaint>() on photo.ComplaintId equals complaint.Id
                where (complaint.Status == ComplaintStatus.Resolved && complaint.ResolvedAt < cutoff)
                    || (complaint.Status == ComplaintStatus.Rejected && complaint.DecidedAt < cutoff)
                orderby photo.UploadedAt, photo.Id
                select photo)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        if (photos.Count == 0)
        {
            return;
        }

        db.RemoveRange(photos);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var photo in photos)
        {
            await storage.DeleteAsync(photo.StorageKey, cancellationToken);
        }

        var purged = photos.Count;
        LogPurged(purged, days);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Complaint photos are kept with no time limit: Ordering:ComplaintPhotoRetentionDays is not set")]
    private partial void LogNotConfigured();

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} complaint photos of complaints closed more than {Days} days ago")]
    private partial void LogPurged(int count, int days);
}
