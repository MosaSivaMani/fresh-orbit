using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FreshOrbit.Hosting.Telemetry;

/// <summary>
/// The application's own instruments (meter <see cref="MeterName"/>, BE-8, D333). They cost nothing when no exporter
/// listens (OpenTelemetry is not registered without <c>Telemetry:OtlpEndpoint</c>, D332).
/// <list type="bullet">
/// <item><c>freshorbit.outbox.pending</c>: outbox rows waiting for delivery (not processed, not dead-lettered).</item>
/// <item><c>freshorbit.outbox.oldest_pending_age</c>: age in seconds of the oldest waiting row (the outbox lag).</item>
/// <item><c>freshorbit.outbox.dead_letters_24h</c>: dead letters written in the last 24 hours.</item>
/// <item><c>freshorbit.jobs.runs</c>, <c>freshorbit.jobs.failures</c>, <c>freshorbit.jobs.duration</c>: recurring
/// job runs, failed runs and run time, tagged <c>job.id</c>.</item>
/// </list>
/// The outbox gauges show the last snapshot of <see cref="OutboxMetricsSampler"/>, which runs only while telemetry is
/// enabled in a host that runs the background jobs.
/// </summary>
public sealed class FreshOrbitMetrics
{
    public const string MeterName = "FreshOrbit";

    /// <summary>Spans of the application's own work (recurring job runs).</summary>
    public const string ActivitySourceName = "FreshOrbit";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private readonly Counter<long> _jobRuns;
    private readonly Counter<long> _jobFailures;
    private readonly Histogram<double> _jobDuration;
    private long _outboxPending;
    private long _deadLettersLastDay;
    private double _oldestPendingAgeSeconds;

    public FreshOrbitMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        meter.CreateObservableGauge("freshorbit.outbox.pending", () => Interlocked.Read(ref _outboxPending), "{message}", "Outbox rows waiting for delivery.");
        meter.CreateObservableGauge("freshorbit.outbox.oldest_pending_age", () => Volatile.Read(ref _oldestPendingAgeSeconds), "s", "Age of the oldest outbox row waiting for delivery.");
        meter.CreateObservableGauge("freshorbit.outbox.dead_letters_24h", () => Interlocked.Read(ref _deadLettersLastDay), "{dead_letter}", "Dead letters written in the last 24 hours.");
        _jobRuns = meter.CreateCounter<long>("freshorbit.jobs.runs", "{run}", "Recurring job runs.");
        _jobFailures = meter.CreateCounter<long>("freshorbit.jobs.failures", "{run}", "Recurring job runs that failed.");
        _jobDuration = meter.CreateHistogram<double>("freshorbit.jobs.duration", "s", "Run time of recurring jobs.");
    }

    /// <summary>Records one finished run of recurring job <paramref name="jobId"/>.</summary>
    public void RecordJobRun(string jobId, TimeSpan duration, bool failed)
    {
        var tag = new KeyValuePair<string, object?>("job.id", jobId);
        _jobRuns.Add(1, tag);
        _jobDuration.Record(duration.TotalSeconds, tag);
        if (failed)
        {
            _jobFailures.Add(1, tag);
        }
    }

    /// <summary>Stores the outbox snapshot the gauges report.</summary>
    public void SetOutboxSnapshot(long pending, TimeSpan oldestPendingAge, long deadLettersLastDay)
    {
        Interlocked.Exchange(ref _outboxPending, pending);
        Interlocked.Exchange(ref _oldestPendingAgeSeconds, Math.Max(0, oldestPendingAge.TotalSeconds));
        Interlocked.Exchange(ref _deadLettersLastDay, deadLettersLastDay);
    }
}
