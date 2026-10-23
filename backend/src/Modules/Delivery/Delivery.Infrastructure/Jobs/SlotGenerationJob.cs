using FreshOrbit.Delivery.Application.Slots;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Delivery.Infrastructure.Jobs;

internal sealed class DeliveryJobs : IRecurringJobRegistration
{
    public const string GenerateSlotsJobId = "delivery.generate-slots";

    public IEnumerable<RecurringJobDefinition> GetJobs() => [new(GenerateSlotsJobId, "5 * * * *", typeof(SlotGenerationJob))];
}

/// <summary>
/// DEL-R9 hourly: one transaction per location, nothing written when no slot is missing. A unique (location, start_at)
/// conflict with a concurrent run skips the location; the next run fills it.
/// </summary>
public sealed partial class SlotGenerationJob(SlotGenerationService generation, FreshOrbitDbContext db, ILogger<SlotGenerationJob> logger) : IRecurringJob
{
    public async Task ExecuteAsync(CancellationToken cancellationToken) => await RunAsync(cancellationToken);

    /// <summary>Returns the number of slots inserted.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var inserted = 0;
        foreach (var locationId in await generation.GetCandidateLocationIdsAsync(cancellationToken))
        {
            try
            {
                var added = await generation.TrackMissingSlotsAsync(locationId, cancellationToken);
                if (added > 0)
                {
                    await db.SaveChangesAsync(cancellationToken);
                    inserted += added;
                }
            }
            catch (DbUpdateException exception)
            {
                LogSkipped(exception, locationId);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }

        return inserted;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Slot generation for location {LocationId} lost a race; the next run retries")]
    private partial void LogSkipped(Exception exception, Guid locationId);
}
