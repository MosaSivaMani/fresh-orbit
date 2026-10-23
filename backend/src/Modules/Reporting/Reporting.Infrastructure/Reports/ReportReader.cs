using System.Data.Common;
using System.Globalization;
using FreshOrbit.Reporting.Application;
using FreshOrbit.Reporting.Domain;
using FreshOrbit.Reporting.Infrastructure.Persistence;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.Reporting.Infrastructure.Reports;

/// <summary>A resolved report request: period, visible locations and the SQL condition shared by its queries.</summary>
public sealed class PreparedReport
{
    private readonly List<(string Name, object Value)> _parameters = [];

    internal PreparedReport(DateOnly from, DateOnly to, IReadOnlyList<Guid>? locations, IReadOnlyDictionary<string, object?> filters, string timeZone)
    {
        From = from;
        To = to;
        Locations = locations;
        Filters = filters;
        TimeZone = timeZone;
        Where = "business_date BETWEEN @from AND @to";
        Add("from", from);
        Add("to", to);
        if (locations is not null)
        {
            Where += " AND fulfillment_location_id = ANY(@locs)";
            Add("locs", locations.ToArray());
        }
    }

    public DateOnly From { get; }

    public DateOnly To { get; }

    /// <summary>Visible locations; <c>null</c> = all.</summary>
    public IReadOnlyList<Guid>? Locations { get; }

    public IReadOnlyDictionary<string, object?> Filters { get; }

    public string TimeZone { get; }

    public string Where { get; private set; }

    public DateTimeOffset? AsOf { get; internal set; }

    internal void AddCondition(string condition, object value)
    {
        var name = "f" + _parameters.Count.ToString(CultureInfo.InvariantCulture);
        Where += " AND (" + condition.Replace("@value", "@" + name, StringComparison.Ordinal) + ")";
        Add(name, value);
    }

    internal void Add(string name, object value) => _parameters.Add((name, value));

    internal void Bind(NpgsqlCommand command)
    {
        foreach (var (name, value) in _parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
    }
}

/// <summary>
/// Report queries (REP-R10..R12). All SQL is read-only against <c>reporting.*</c> views or, for the dashboard and scope
/// lookups, other schemas (Reporting is the only module allowed to read across schemas).
/// </summary>
public sealed class ReportReader(FreshOrbitDbContext db, ICurrentActor actor, IClock clock)
{
    public const int CsvMaxRows = 100_000;
    private const string DefaultTimeZone = "Asia/Kolkata";

    /// <summary>Resolves scope (REP-R11), period (REP-R10), report filters (G1) and <c>asOf</c>.</summary>
    public async Task<PreparedReport> PrepareAsync(ReportQuery query, IReadOnlyList<ReportView> views, IReadOnlyList<ReportFilter> reportFilters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(reportFilters);
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var cityLocations = query.CityId is { } cityId
                ? await ListAsync("SELECT id FROM locations.fulfillment_locations WHERE city_id = @city", c => c.Parameters.AddWithValue("city", cityId), r => r.GetGuid(0), cancellationToken)
                : [];
            var locations = ReportScope.Resolve(actor, query.FulfillmentLocationId, query.CityId, cityLocations);
            var timeZone = await TimeZoneOfAsync(locations, cancellationToken);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);
            var (from, to) = ReportPeriod.Resolve(query.FromDate, query.ToDate, today);

            var filters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["fromDate"] = from,
                ["toDate"] = to,
            };
            if (query.CityId is not null)
            {
                filters["cityId"] = query.CityId;
            }

            if (query.FulfillmentLocationId is not null)
            {
                filters["fulfillmentLocationId"] = query.FulfillmentLocationId;
            }

            var prepared = new PreparedReport(from, to, locations, filters, timeZone);
            foreach (var filter in reportFilters)
            {
                if (query.Extra.TryGetValue(filter.Parameter, out var raw) && !string.IsNullOrWhiteSpace(raw))
                {
                    var value = ParseFilter(filter, raw);
                    filters[filter.Parameter] = value;
                    prepared.AddCondition(filter.Condition, value);
                }
            }

            prepared.AsOf = await AsOfAsync(views, cancellationToken);
            return prepared;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>One JSON page plus totals over all matching rows.</summary>
    public async Task<ReportResponse> ReadAsync(ReportDefinition report, ReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(query);
        ValidatePage(query);
        var order = OrderBy(report, query.Sort);
        var prepared = await PrepareAsync(query, [report.View], report.Filters, cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var (count, totals) = await TotalsAsync(report, prepared, cancellationToken);
            var items = new List<IReadOnlyDictionary<string, object?>>();
            var sql = $"{SelectList(report)} WHERE {prepared.Where} ORDER BY {order} LIMIT @limit OFFSET @offset";
            await using var command = Command(sql, prepared);
            command.Parameters.AddWithValue("limit", query.PageSize);
            command.Parameters.AddWithValue("offset", (query.Page - 1) * query.PageSize);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                var values = ReadRow(report, reader);
                for (var i = 0; i < report.Columns.Count; i++)
                {
                    row[report.Columns[i].JsonName] = values[i];
                }

                items.Add(row);
            }

            return new ReportResponse(prepared.AsOf, prepared.From, prepared.To, prepared.Filters, ToJson(report, totals), items, query.Page, query.PageSize, count);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Totals and every row (no paging) for CSV export; refuses more than <see cref="CsvMaxRows"/> rows.</summary>
    public async Task<CsvExport> ExportAsync(ReportDefinition report, ReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(query);
        var order = OrderBy(report, query.Sort);
        var prepared = await PrepareAsync(query, [report.View], report.Filters, cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var (count, totals) = await TotalsAsync(report, prepared, cancellationToken);
            if (count > CsvMaxRows)
            {
                throw new RefusalException([new FieldError("toDate", ReportingErrors.ValidationFailed, $"The export has {count} rows; narrow the range to at most {CsvMaxRows} rows.")]);
            }

            return new CsvExport(report, prepared, totals, $"{SelectList(report)} WHERE {prepared.Where} ORDER BY {order}");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Streams the export rows (column order of the report).</summary>
    public async IAsyncEnumerable<object?[]> StreamAsync(CsvExport export, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(export);
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = Command(export.Sql, export.Prepared);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                yield return ReadRow(export.Report, reader);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Repeat customers and monthly cohorts for the range (REP-R7, G5).</summary>
    public async Task<ReportResponse> RepeatCustomersAsync(ReportQuery query, int cohortMonths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (cohortMonths is < 1 or > 12)
        {
            throw new RefusalException([new FieldError("cohortMonths", ReportingErrors.ValidationFailed, "Cohort months must be between 1 and 12.")]);
        }

        var prepared = await PrepareAsync(query, [ReportViews.CustomerDailyOrders], [], cancellationToken);
        var scope = prepared.Locations is null ? "TRUE" : "fulfillment_location_id = ANY(@locs)";
        var scoped = $"""
            WITH scoped AS (SELECT customer_id, business_date, delivered_orders FROM reporting.customer_daily_orders WHERE {scope}),
            firsts AS (SELECT customer_id, min(business_date) AS first_date FROM scoped GROUP BY customer_id)
            """;
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var summary = await ListAsync(
                scoped + """
                    ,
                    active AS (SELECT DISTINCT customer_id FROM scoped WHERE business_date BETWEEN @from AND @to),
                    upto AS (SELECT customer_id, sum(delivered_orders) AS orders FROM scoped WHERE business_date <= @to GROUP BY customer_id)
                    SELECT (SELECT count(*) FROM active),
                           (SELECT count(*) FROM active a JOIN upto u ON u.customer_id = a.customer_id WHERE u.orders >= 2),
                           (SELECT count(*) FROM firsts WHERE first_date BETWEEN @from AND @to)
                    """,
                prepared.Bind,
                r => (Active: r.GetInt64(0), Repeat: r.GetInt64(1), New: r.GetInt64(2)),
                cancellationToken);
            var (active, repeat, newCustomers) = summary[0];

            var cohorts = await ListAsync(
                scoped + """
                    ,
                    cohorts AS (SELECT customer_id, date_trunc('month', first_date)::date AS cohort_month FROM firsts WHERE first_date BETWEEN @from AND @to),
                    activity AS (SELECT DISTINCT customer_id, date_trunc('month', business_date)::date AS month FROM scoped),
                    offsets AS (
                        SELECT c.cohort_month,
                               ((date_part('year', a.month) * 12 + date_part('month', a.month))
                                - (date_part('year', c.cohort_month) * 12 + date_part('month', c.cohort_month)))::integer AS month_offset,
                               a.customer_id
                        FROM cohorts c JOIN activity a ON a.customer_id = c.customer_id AND a.month >= c.cohort_month)
                    SELECT o.cohort_month, o.month_offset, count(DISTINCT o.customer_id)::bigint,
                           (SELECT count(*) FROM cohorts c2 WHERE c2.cohort_month = o.cohort_month)::bigint
                    FROM offsets o
                    WHERE o.month_offset < @months
                    GROUP BY o.cohort_month, o.month_offset
                    ORDER BY o.cohort_month, o.month_offset
                    """,
                c =>
                {
                    prepared.Bind(c);
                    c.Parameters.AddWithValue("months", cohortMonths);
                },
                r => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["cohortMonth"] = r.GetFieldValue<DateOnly>(0),
                    ["monthOffset"] = r.GetInt32(1),
                    ["cohortSize"] = r.GetInt64(3),
                    ["activeCustomers"] = r.GetInt64(2),
                    ["retentionBp"] = r.GetInt64(3) == 0 ? null : r.GetInt64(2) * 10_000 / r.GetInt64(3),
                },
                cancellationToken);

            var totals = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["activeCustomers"] = active,
                ["repeatCustomers"] = repeat,
                ["repeatRateBp"] = active == 0 ? null : repeat * 10_000 / active,
                ["newCustomers"] = newCustomers,
            };
            var filters = new Dictionary<string, object?>(prepared.Filters, StringComparer.Ordinal) { ["cohortMonths"] = cohortMonths };
            return new ReportResponse(prepared.AsOf, prepared.From, prepared.To, filters, totals, cohorts, 1, cohorts.Count, cohorts.Count);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Live dashboard tiles for today at the locations in scope (REP-R8).</summary>
    public async Task<DashboardResponse> DashboardAsync(ReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var prepared = await PrepareAsync(query with { FromDate = null, ToDate = null }, [], [], cancellationToken);
        var today = prepared.To;
        string Scope(string column) => prepared.Locations is null ? "TRUE" : $"{column} = ANY(@locs)";
        void Bind(NpgsqlCommand c)
        {
            prepared.Bind(c);
            c.Parameters.AddWithValue("today", today);
        }

        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var ordersByStatus = await ListAsync(
                $"""
                SELECT o.status, count(*)::bigint
                FROM ordering.orders o
                JOIN locations.fulfillment_locations fl ON fl.id = o.fulfillment_location_id
                JOIN locations.cities c ON c.id = fl.city_id
                WHERE (o.slot_start_at AT TIME ZONE c.time_zone)::date = @today AND {Scope("o.fulfillment_location_id")}
                GROUP BY o.status ORDER BY o.status
                """,
                Bind,
                r => new DashboardCount(r.GetString(0), r.GetInt64(1)),
                cancellationToken);
            var slots = await ListAsync(
                $"""
                SELECT s.id, s.fulfillment_location_id, s.start_at, s.end_at, s.booked_count, s.capacity
                FROM delivery.delivery_slots s
                WHERE s.slot_date = @today AND {Scope("s.fulfillment_location_id")}
                ORDER BY s.start_at, s.fulfillment_location_id
                """,
                Bind,
                r => new DashboardSlot(
                    r.GetGuid(0),
                    r.GetGuid(1),
                    r.GetFieldValue<DateTimeOffset>(2),
                    r.GetFieldValue<DateTimeOffset>(3),
                    r.GetInt32(4),
                    r.GetInt32(5),
                    r.GetInt32(5) == 0 ? null : r.GetInt32(4) * 10_000 / r.GetInt32(5)),
                cancellationToken);
            var lowStock = await ListAsync(
                $"""
                SELECT si.id, si.fulfillment_location_id, si.variant_id, v.sku, v.name, (si.on_hand - si.reserved)::bigint, si.low_stock_threshold::bigint
                FROM inventory.stock_items si
                LEFT JOIN catalog.product_variants v ON v.id = si.variant_id
                WHERE si.on_hand - si.reserved <= si.low_stock_threshold AND {Scope("si.fulfillment_location_id")}
                ORDER BY si.on_hand - si.reserved, si.id
                LIMIT 50
                """,
                Bind,
                r => new DashboardLowStock(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetInt64(5), r.GetInt64(6)),
                cancellationToken);
            var openComplaints = await ScalarAsync(
                $"SELECT count(*) FROM ordering.complaints k JOIN ordering.orders o ON o.id = k.order_id WHERE k.status = 'OPEN' AND {Scope("o.fulfillment_location_id")}", Bind, cancellationToken);
            var refunds = await ListAsync(
                $"SELECT count(*), coalesce(sum(amount_paise), 0)::bigint FROM payments.refunds WHERE status = 'REQUESTED' AND {Scope("fulfillment_location_id")}",
                Bind,
                r => (Count: r.GetInt64(0), Amount: r.GetInt64(1)),
                cancellationToken);
            var issues = await ScalarAsync(
                $"SELECT count(*) FROM payments.reconciliation_issues WHERE status = 'OPEN' AND {Scope("fulfillment_location_id")}", Bind, cancellationToken);

            return new DashboardResponse(
                clock.UtcNow,
                today,
                prepared.Filters.Where(f => f.Key is "cityId" or "fulfillmentLocationId").ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal),
                ordersByStatus,
                slots,
                lowStock,
                openComplaints,
                refunds[0].Count,
                refunds[0].Amount,
                issues);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    internal static object? ReadValue(ReportColumn column, DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return column.Kind switch
        {
            ColumnKind.Date => reader.GetFieldValue<DateOnly>(ordinal),
            ColumnKind.Uuid => reader.GetGuid(ordinal),
            ColumnKind.Text => reader.GetString(ordinal),
            _ => Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture),
        };
    }

    private static object?[] ReadRow(ReportDefinition report, DbDataReader reader)
    {
        var values = new object?[report.Columns.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadValue(report.Columns[i], reader, i);
        }

        return values;
    }

    private static string SelectList(ReportDefinition report)
        => $"SELECT {string.Join(", ", report.Columns.Select(c => c.Name))} FROM {report.View.QualifiedName}";

    private static void ValidatePage(ReportQuery query)
    {
        var errors = new List<FieldError>();
        if (query.Page < 1)
        {
            errors.Add(new FieldError("page", "PAGE_OUT_OF_RANGE", "Page must be 1 or more."));
        }

        if (query.PageSize is < 1 or > 100)
        {
            errors.Add(new FieldError("pageSize", "PAGE_SIZE_OUT_OF_RANGE", "Page size must be between 1 and 100."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }
    }

    /// <summary>Default <c>-businessDate</c>; allowed sorts are the report's columns (camelCase, <c>-</c> for descending).</summary>
    private static string OrderBy(ReportDefinition report, string? sort)
    {
        var value = string.IsNullOrWhiteSpace(sort) ? "-businessDate" : sort.Trim();
        var descending = value.StartsWith('-');
        var column = report.FindColumn(descending ? value[1..] : value)
            ?? throw new RefusalException([new FieldError("sort", ReportingErrors.ValidationFailed, $"Sort by one of: {string.Join(", ", report.Columns.Select(c => c.JsonName))}.")]);
        return $"{column.Name} {(descending ? "DESC" : "ASC")} NULLS LAST, {string.Join(", ", report.Grain)}";
    }

    private static object ParseFilter(ReportFilter filter, string raw)
    {
        if (filter.Kind == ColumnKind.Uuid)
        {
            return Guid.TryParse(raw, CultureInfo.InvariantCulture, out var id)
                ? id
                : throw new RefusalException([new FieldError(filter.Parameter, ReportingErrors.ValidationFailed, $"{filter.Parameter} must be a UUID.")]);
        }

        return ErrorCodes.IsUpperSnake(raw.Trim())
            ? raw.Trim()
            : throw new RefusalException([new FieldError(filter.Parameter, ReportingErrors.ValidationFailed, $"{filter.Parameter} must be an UPPER_SNAKE code.")]);
    }

    private static Dictionary<string, object?> ToJson(ReportDefinition report, Dictionary<string, object?> totals)
        => report.Columns.Where(c => totals.ContainsKey(c.Name)).ToDictionary(c => c.JsonName, c => totals[c.Name], StringComparer.Ordinal);

    private async Task<(int Count, Dictionary<string, object?> Totals)> TotalsAsync(ReportDefinition report, PreparedReport prepared, CancellationToken cancellationToken)
    {
        var summed = report.Columns.Where(c => c.Summed).ToList();
        var sums = string.Concat(summed.Select(c => $", coalesce(sum({c.Name}), 0)::bigint"));
        await using var command = Command($"SELECT count(*)::integer{sums} FROM {report.View.QualifiedName} WHERE {prepared.Where}", prepared);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var totals = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < summed.Count; i++)
        {
            totals[summed[i].Name] = reader.GetInt64(i + 1);
        }

        var count = reader.GetInt32(0);
        foreach (var column in report.Columns.Where(c => !c.Summed && c.Kind is ColumnKind.Money or ColumnKind.BasisPoints))
        {
            totals[column.Name] = null;
        }

        report.DeriveTotals(totals);
        return (count, totals);
    }

    private NpgsqlCommand Command(string sql, PreparedReport prepared)
    {
        var command = new NpgsqlCommand(sql, (NpgsqlConnection)db.Database.GetDbConnection());
        prepared.Bind(command);
        return command;
    }

    private async Task<List<T>> ListAsync<T>(string sql, Action<NpgsqlCommand> bind, Func<DbDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)db.Database.GetDbConnection());
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(map(reader));
        }

        return list;
    }

    private async Task<long> ScalarAsync(string sql, Action<NpgsqlCommand> bind, CancellationToken cancellationToken)
        => (await ListAsync(sql, bind, r => r.GetInt64(0), cancellationToken))[0];

    private async Task<string> TimeZoneOfAsync(IReadOnlyList<Guid>? locations, CancellationToken cancellationToken)
    {
        var zones = await ListAsync(
            locations is null
                ? "SELECT DISTINCT c.time_zone FROM locations.cities c JOIN locations.fulfillment_locations fl ON fl.city_id = c.id"
                : "SELECT DISTINCT c.time_zone FROM locations.cities c JOIN locations.fulfillment_locations fl ON fl.city_id = c.id WHERE fl.id = ANY(@locs)",
            c =>
            {
                if (locations is not null)
                {
                    c.Parameters.AddWithValue("locs", locations.ToArray());
                }
            },
            r => r.GetString(0),
            cancellationToken);
        return zones.Count == 1 ? zones[0] : DefaultTimeZone;
    }

    private async Task<DateTimeOffset?> AsOfAsync(IReadOnlyList<ReportView> views, CancellationToken cancellationToken)
    {
        if (views.Count == 0)
        {
            return clock.UtcNow;
        }

        var names = views.Select(v => v.Name).ToList();
        var refreshed = await db.Set<RefreshState>().AsNoTracking()
            .Where(s => names.Contains(s.ViewName) && s.RefreshedAt != null)
            .Select(s => s.RefreshedAt)
            .ToListAsync(cancellationToken);
        return refreshed.Count == names.Count ? refreshed.Min() : null;
    }
}

/// <summary>A CSV export ready to stream.</summary>
public sealed record CsvExport(ReportDefinition Report, PreparedReport Prepared, Dictionary<string, object?> Totals, string Sql);

public sealed record DashboardCount(string Status, long Count);

public sealed record DashboardSlot(Guid SlotId, Guid FulfillmentLocationId, DateTimeOffset StartAt, DateTimeOffset EndAt, int BookedCount, int Capacity, int? FillBp);

public sealed record DashboardLowStock(Guid StockItemId, Guid FulfillmentLocationId, Guid VariantId, string? Sku, string? VariantName, long Available, long LowStockThreshold);

/// <summary>Live dashboard (REP-R8); <see cref="AsOf"/> is now.</summary>
public sealed record DashboardResponse(
    DateTimeOffset AsOf,
    DateOnly Date,
    IReadOnlyDictionary<string, object?> Filters,
    IReadOnlyList<DashboardCount> OrdersByStatus,
    IReadOnlyList<DashboardSlot> Slots,
    IReadOnlyList<DashboardLowStock> LowStock,
    long OpenComplaints,
    long RefundsRequested,
    long RefundsRequestedPaise,
    long OpenPaymentIssues);
