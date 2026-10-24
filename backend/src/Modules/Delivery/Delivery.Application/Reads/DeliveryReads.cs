using System.Globalization;
using FreshOrbit.Delivery.Application.Templates;
using FreshOrbit.Delivery.Domain.Riders;
using FreshOrbit.Delivery.Domain.Shipments;
using FreshOrbit.Delivery.Domain.Slots;
using FreshOrbit.Delivery.Domain.Templates;

namespace FreshOrbit.Delivery.Application.Reads;

public sealed record SlotTemplateResponse(
    Guid Id,
    Guid FulfillmentLocationId,
    int DayOfWeek,
    string StartTime,
    string EndTime,
    int Capacity,
    int CutoffMinutes,
    bool IsActive,
    string Version);

public sealed record DeliverySlotResponse(
    Guid Id,
    Guid FulfillmentLocationId,
    Guid? SlotTemplateId,
    DateOnly SlotDate,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    DateTimeOffset CutoffAt,
    int Capacity,
    int BookedCount,
    SlotStatus Status,
    SlotClosedReason? ClosedReason,
    SlotDisplayStatus DisplayStatus,
    string Version);

public sealed record RiderResponse(Guid Id, Guid FulfillmentLocationId, string FullName, string Phone, bool IsActive, string Version);

/// <summary>
/// Minimal rider row for pickers of staff without <c>delivery.manage</c> (G7, cash settlement): no full phone, no
/// version (it is not an edit source). <c>PhoneLast4</c> tells two riders of one name apart.
/// </summary>
public sealed record RiderLookupItem(Guid Id, Guid FulfillmentLocationId, string FullName, string PhoneLast4, bool IsActive);

public sealed record DeliveryAttemptResponse(int AttemptNo, AttemptOutcome Outcome, string? Notes, DateTimeOffset AttemptedAt);

/// <summary>Admin view of a shipment. The delivery code itself is never shown to staff (the customer reads it out).</summary>
public sealed record ShipmentResponse(
    Guid Id,
    Guid OrderId,
    Guid FulfillmentLocationId,
    Guid DeliverySlotId,
    Guid? RiderId,
    ShipmentStatus Status,
    DateTimeOffset PlacedAt,
    DateTimeOffset? PackedAt,
    bool ColdBoxUsed,
    decimal? DispatchTemperatureC,
    bool TemperatureWarning,
    DateTimeOffset? DispatchedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ReturnedAt,
    DateTimeOffset? CancelledAt,
    int DeliveryCodeFailures,
    bool DeliveryCodeLocked,
    long? CashCollectedPaise,
    CashMode? CashMode,
    string? UpiReference,
    DateTimeOffset? CashCollectedAt,
    IReadOnlyList<DeliveryAttemptResponse> Attempts,
    string Version);

/// <summary>Customer slot: <c>status</c> is the displayed status (AVAILABLE, FULL, CUTOFF_PASSED, CLOSED).</summary>
public sealed record CustomerSlotResponse(Guid Id, DateOnly SlotDate, DateTimeOffset StartAt, DateTimeOffset EndAt, DateTimeOffset CutoffAt, SlotDisplayStatus Status);

public sealed record CustomerSlotsResponse(Guid FulfillmentLocationId, IReadOnlyList<CustomerSlotResponse> Items);

public sealed record Page<T>(IReadOnlyList<T> Items, int TotalCount);

/// <summary>Filters; <c>Locations</c> null = every location (staff scope applied by the caller).</summary>
public sealed record TemplateQuery(Guid? FulfillmentLocationId, IReadOnlyCollection<Guid>? Locations, int Skip, int Take);

public sealed record SlotQuery(Guid? FulfillmentLocationId, DateOnly? FromDate, DateOnly? ToDate, IReadOnlyCollection<Guid>? Locations, int Skip, int Take);

public sealed record RiderQuery(Guid? FulfillmentLocationId, bool? IsActive, IReadOnlyCollection<Guid>? Locations, int Skip, int Take);

public sealed record RiderLookupQuery(Guid? FulfillmentLocationId, bool? IsActive, string? Q, IReadOnlyCollection<Guid>? Locations, int Skip, int Take);

/// <summary>Shipment list filters; <c>SlotDate</c> = local date of the delivery slot, <c>Sort</c> = keys in order (empty = placedAt); id breaks ties (G10).</summary>
public sealed record ShipmentQuery(
    Guid? FulfillmentLocationId,
    Guid? SlotId,
    Guid? RiderId,
    IReadOnlyCollection<ShipmentStatus> Statuses,
    IReadOnlyCollection<Guid>? Locations,
    int Skip,
    int Take)
{
    public DateOnly? SlotDate { get; init; }

    public IReadOnlyList<ShipmentSortKey> Sort { get; init; } = [];
}

/// <summary>Shipment list sort fields (G10).</summary>
public enum ShipmentSortField
{
    PlacedAt,
    SlotStartAt,
    Status,
    DispatchedAt,
}

public sealed record ShipmentSortKey(ShipmentSortField Field, bool Descending);

/// <summary>
/// Parses the shipment list <c>sort</c> (docs/api §1.5): a comma list of <c>placedAt</c>, <c>slotStartAt</c>,
/// <c>status</c>, <c>dispatchedAt</c>, each optionally prefixed with <c>-</c> for descending. Unknown or repeated
/// fields are refused (422 VALIDATION_FAILED on <c>sort</c>).
/// </summary>
public static class ShipmentSort
{
    public static readonly IReadOnlyDictionary<string, ShipmentSortField> Fields = new Dictionary<string, ShipmentSortField>(StringComparer.Ordinal)
    {
        ["placedAt"] = ShipmentSortField.PlacedAt,
        ["slotStartAt"] = ShipmentSortField.SlotStartAt,
        ["status"] = ShipmentSortField.Status,
        ["dispatchedAt"] = ShipmentSortField.DispatchedAt,
    };

    public static IReadOnlyList<ShipmentSortKey> Parse(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return [];
        }

        var keys = new List<ShipmentSortKey>();
        foreach (var part in sort.Split(',', StringSplitOptions.TrimEntries))
        {
            var descending = part.StartsWith('-');
            var name = descending ? part[1..] : part;
            if (!Fields.TryGetValue(name, out var field) || keys.Exists(k => k.Field == field))
            {
                throw new FreshOrbit.SharedKernel.Errors.RefusalException(new FreshOrbit.SharedKernel.Errors.FieldError(
                    "sort",
                    FreshOrbit.Delivery.Domain.Shared.DeliveryErrors.ValidationFailed,
                    $"Sort by {string.Join(", ", Fields.Keys)} (once each, '-' for descending)."));
            }

            keys.Add(new ShipmentSortKey(field, descending));
        }

        return keys;
    }
}

/// <summary>Read side (no tracking) of the Delivery admin and customer lists.</summary>
public interface IDeliveryReads
{
    Task<Page<SlotTemplateResponse>> ListTemplatesAsync(TemplateQuery query, CancellationToken cancellationToken);

    Task<Page<DeliverySlotResponse>> ListSlotsAsync(SlotQuery query, CancellationToken cancellationToken);

    Task<Page<RiderResponse>> ListRidersAsync(RiderQuery query, CancellationToken cancellationToken);

    /// <summary>G7: minimal rider rows (name, location, last 4 phone digits), ordered by name.</summary>
    Task<Page<RiderLookupItem>> LookupRidersAsync(RiderLookupQuery query, CancellationToken cancellationToken);

    Task<Page<ShipmentResponse>> ListShipmentsAsync(ShipmentQuery query, CancellationToken cancellationToken);

    Task<ShipmentResponse?> GetShipmentAsync(Guid shipmentId, CancellationToken cancellationToken);

    /// <summary>Slots of the location on the local dates <c>[fromDate, toDate]</c>, ordered by start.</summary>
    Task<IReadOnlyList<CustomerSlotResponse>> ListCustomerSlotsAsync(Guid fulfillmentLocationId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
}

/// <summary>Aggregate → response mappings shared by commands and reads.</summary>
public static class DeliveryMappings
{
    public static string FormatVersion(uint version) => version.ToString(CultureInfo.InvariantCulture);

    public static SlotTemplateResponse ToResponse(this SlotTemplate t)
    {
        ArgumentNullException.ThrowIfNull(t);
        return new(t.Id, t.FulfillmentLocationId, t.DayOfWeek, TimeFormat.Format(t.StartTime), TimeFormat.Format(t.EndTime), t.Capacity, t.CutoffMinutes, t.IsActive, FormatVersion(t.Version));
    }

    public static DeliverySlotResponse ToResponse(this DeliverySlot s, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(s);
        return new(s.Id, s.FulfillmentLocationId, s.SlotTemplateId, s.SlotDate, s.StartAt, s.EndAt, s.CutoffAt, s.Capacity, s.BookedCount, s.Status, s.ClosedReason, s.GetDisplayStatus(now), FormatVersion(s.Version));
    }

    public static RiderResponse ToResponse(this Rider r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new(r.Id, r.FulfillmentLocationId, r.FullName, r.Phone, r.IsActive, FormatVersion(r.Version));
    }

    public static ShipmentResponse ToResponse(this Shipment s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return new(
            s.Id,
            s.OrderId,
            s.FulfillmentLocationId,
            s.DeliverySlotId,
            s.RiderId,
            s.Status,
            s.PlacedAt,
            s.PackedAt,
            s.ColdBoxUsed,
            s.DispatchTemperatureC,
            s.TemperatureWarning,
            s.DispatchedAt,
            s.DeliveredAt,
            s.ReturnedAt,
            s.CancelledAt,
            s.DeliveryCodeFailures,
            s.IsDeliveryCodeLocked,
            s.CashCollectedPaise,
            s.CashMode,
            s.UpiReference,
            s.CashCollectedAt,
            [.. s.Attempts.OrderBy(a => a.AttemptNo).Select(a => new DeliveryAttemptResponse(a.AttemptNo, a.Outcome, a.Notes, a.AttemptedAt))],
            FormatVersion(s.Version));
    }
}
