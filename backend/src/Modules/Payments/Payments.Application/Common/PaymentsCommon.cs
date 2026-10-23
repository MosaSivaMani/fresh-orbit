using System.Text.Json;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Serialization;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Payments.Application.Common;

/// <summary>Staff not allowed at this fulfillment location (HTTP 403 <c>LOCATION_ACCESS_DENIED</c>).</summary>
public sealed class LocationAccessDeniedException()
    : ForbiddenException(PaymentsErrors.LocationAccessDenied, "You do not have access to this fulfillment location.");

/// <summary>Webhook signature check failed (HTTP 401 <c>WEBHOOK_SIGNATURE_INVALID</c>); the event is stored REJECTED.</summary>
public sealed class WebhookSignatureInvalidException()
    : UnauthorizedException(PaymentsErrors.WebhookSignatureInvalid, "The webhook signature is invalid.");

/// <summary>Configuration section <c>Payments</c>.</summary>
public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>Adapter key used for ONLINE payments (e.g. <c>FAKE</c>); empty = online payments unavailable.</summary>
    public string OnlineProvider { get; set; } = string.Empty;

    /// <summary>
    /// Lifetime of an online attempt at the provider. Used until <c>OrderSummary</c> carries the order's payment
    /// deadline (reservation expiry); decision D17 sets the reservation window to 15 minutes.
    /// </summary>
    public int OnlineAttemptMinutes { get; set; } = 15;

    /// <summary>A PENDING attempt is reused when it is still valid for at least this long (PAY-R5).</summary>
    public int ReuseMarginMinutes { get; set; } = 2;

    /// <summary>Provider calls per reconciliation run (PAY-R21).</summary>
    public int ReconciliationProviderCallLimit { get; set; } = 200;
}

public static class LocationGuard
{
    public static void EnsureAccess(ICurrentActor actor, Guid fulfillmentLocationId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.CanAccessLocation(fulfillmentLocationId))
        {
            throw new LocationAccessDeniedException();
        }
    }

    /// <summary>The locations a list is restricted to; null = all.</summary>
    public static IReadOnlyCollection<Guid>? Scope(ICurrentActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.ActorType == ActorType.System || actor.IsAllLocations ? null : [.. actor.LocationIds];
    }

    public static Guid RequireUser(ICurrentActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.UserId ?? throw new InvalidOperationException("This command needs an authenticated user.");
    }
}

/// <summary>Opens reconciliation issues without duplicates (PAY-R22: one OPEN issue per kind and subject).</summary>
public sealed class IssueRecorder(IReconciliationIssueRepository issues, IClock clock)
{
    public async Task<ReconciliationIssue?> OpenAsync(
        IssueKind kind,
        IssueSubjectType subjectType,
        Guid subjectId,
        Guid? orderId,
        Guid? fulfillmentLocationId,
        object details,
        CancellationToken cancellationToken)
    {
        if (await issues.FindOpenAsync(kind, subjectId, cancellationToken) is not null)
        {
            return null;
        }

        var issue = ReconciliationIssue.Open(kind, subjectType, subjectId, orderId, fulfillmentLocationId, Serialize(details), clock.UtcNow);
        issues.Add(issue);
        return issue;
    }

    public static string Serialize(object details) => JsonSerializer.Serialize(details, FreshOrbitJson.Options);
}

/// <summary>Masks a customer UPI id for lists (<c>pri***@okicici</c>, PAY-R24).</summary>
public static class UpiMask
{
    public static string? Mask(string? upiId)
    {
        if (string.IsNullOrEmpty(upiId))
        {
            return upiId;
        }

        var at = upiId.IndexOf('@', StringComparison.Ordinal);
        var name = at < 0 ? upiId : upiId[..at];
        var domain = at < 0 ? string.Empty : upiId[at..];
        var visible = Math.Min(3, Math.Max(1, name.Length / 2));
        return name[..visible] + "***" + domain;
    }
}

/// <summary>Retries a command from Read after a lost optimistic race (max 3 retries), then lets the race surface as 409.</summary>
public static class SaveRetry
{
    public const int MaxRetries = 3;

    public static async Task<T> RunAsync<T>(IPaymentsSession session, Func<Task<T>> command)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(command);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await command();
            }
            catch (ConcurrentSaveException) when (attempt < MaxRetries)
            {
                session.Reset();
            }
        }
    }
}
