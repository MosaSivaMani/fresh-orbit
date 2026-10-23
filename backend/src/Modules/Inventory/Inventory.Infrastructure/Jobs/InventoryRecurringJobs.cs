using FreshOrbit.Inventory.Application.Jobs;
using FreshOrbit.SharedKernel.Jobs;

namespace FreshOrbit.Inventory.Infrastructure.Jobs;

/// <summary>Inventory's recurring jobs (UTC cron).</summary>
internal sealed class InventoryJobRegistration : IRecurringJobRegistration
{
    public const string ExpireBatchesId = "inventory.expire-batches";
    public const string LowStockDigestId = "inventory.low-stock-digest";

    public IEnumerable<RecurringJobDefinition> GetJobs() =>
    [
        new(ExpireBatchesId, "*/15 * * * *", typeof(BatchExpiryJob)),
        new(LowStockDigestId, "0 * * * *", typeof(LowStockDigestJob)),
    ];
}

public sealed class BatchExpiryJob(BatchExpiryService service) : IRecurringJob
{
    public Task ExecuteAsync(CancellationToken cancellationToken) => service.RunAsync(cancellationToken);
}

public sealed class LowStockDigestJob(LowStockDigestService service) : IRecurringJob
{
    public Task ExecuteAsync(CancellationToken cancellationToken) => service.RunAsync(cancellationToken);
}
