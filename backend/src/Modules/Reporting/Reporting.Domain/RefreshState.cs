namespace FreshOrbit.Reporting.Domain;

/// <summary>
/// Freshness of one materialized view (REP-R9). FRESH / STALE is derived: the view is stale when the current source
/// watermark is newer than <see cref="SourceWatermark"/>. A failed refresh keeps the previous data and
/// <see cref="RefreshedAt"/> and stores <see cref="LastError"/>.
/// </summary>
public sealed class RefreshState
{
    private RefreshState()
    {
        ViewName = string.Empty;
    }

    public string ViewName { get; private set; }

    public DateTimeOffset? RefreshedAt { get; private set; }

    public DateTimeOffset? SourceWatermark { get; private set; }

    public int? DurationMs { get; private set; }

    public string? LastError { get; private set; }

    public static RefreshState Never(string viewName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
        return new RefreshState { ViewName = viewName };
    }

    /// <summary>True when the view must be refreshed for <paramref name="watermark"/> (never refreshed, newer data or a failed run).</summary>
    public bool NeedsRefresh(DateTimeOffset? watermark)
        => RefreshedAt is null || LastError is not null || (watermark is not null && (SourceWatermark is null || watermark > SourceWatermark));

    public void Refreshed(DateTimeOffset at, DateTimeOffset? watermark, int durationMs)
    {
        RefreshedAt = at;
        SourceWatermark = watermark;
        DurationMs = durationMs;
        LastError = null;
    }

    public void Failed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        LastError = error.Length > 2000 ? error[..2000] : error;
    }
}
