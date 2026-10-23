using FreshOrbit.Catalog.Domain.Products;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Storage;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Catalog.Infrastructure.Images;

/// <summary>
/// CAT-R17 daily clean-up: deletes image files under <c>products/</c> older than 24 h whose image row does not exist
/// (an upload whose commit failed and whose compensation did not run).
/// </summary>
public sealed class OrphanImageCleanupJob(FreshOrbitDbContext db, IFileStorage storage, IClock clock) : IRecurringJob
{
    public const string JobId = "catalog.cleanup-orphan-images";
    public const string Root = "products";
    public static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.UtcNow - MinimumAge;
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        var recent = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var file in storage.ListAsync(Root, cancellationToken))
        {
            var segments = file.Key.Split('/');
            if (segments.Length < 4)
            {
                continue;
            }

            var prefix = string.Join('/', segments[..3]);
            (file.LastModifiedAt < cutoff ? prefixes : recent).Add(prefix);
        }

        prefixes.ExceptWith(recent);
        if (prefixes.Count == 0)
        {
            return;
        }

        var candidates = prefixes.ToList();
        var known = await db.Set<ProductImage>().AsNoTracking()
            .Where(i => candidates.Contains(i.StorageKey))
            .Select(i => i.StorageKey)
            .ToListAsync(cancellationToken);
        foreach (var orphan in candidates.Except(known, StringComparer.Ordinal))
        {
            await storage.DeletePrefixAsync(orphan, cancellationToken);
        }
    }
}

public sealed class CatalogRecurringJobs : IRecurringJobRegistration
{
    public IEnumerable<RecurringJobDefinition> GetJobs() => [new(OrphanImageCleanupJob.JobId, "30 3 * * *", typeof(OrphanImageCleanupJob))];
}
