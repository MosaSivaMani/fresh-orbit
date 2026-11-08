using FreshOrbit.Settings.Public;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FreshOrbit.Api;

/// <summary>
/// Readiness (<c>/health/ready</c>, BE-1, D295): unhealthy while the business profile lacks what invoicing needs, the
/// same rule that makes checkout answer 422 <c>STORE_NOT_READY</c>. Liveness (<c>/health</c>) keeps checking the
/// database only, so a store that is not configured yet is not restarted by its container health check.
/// </summary>
internal sealed class StoreReadinessHealthCheck(ISettingsApi settings) : IHealthCheck
{
    public const string Name = "store";
    public const string ReadyTag = "ready";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var readiness = await settings.GetStoreReadinessAsync(cancellationToken);
        if (readiness.IsReady)
        {
            return HealthCheckResult.Healthy();
        }

        var data = new Dictionary<string, object> { ["missingProfileFields"] = readiness.MissingProfileFields };
        return HealthCheckResult.Unhealthy("The business profile lacks what invoicing needs; orders are refused with STORE_NOT_READY.", data: data);
    }
}
