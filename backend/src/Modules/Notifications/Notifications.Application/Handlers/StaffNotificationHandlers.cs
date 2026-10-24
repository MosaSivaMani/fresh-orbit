using System.Globalization;
using FreshOrbit.Identity.Public;
using FreshOrbit.Identity.Public.Events;
using FreshOrbit.Notifications.Public;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.Notifications.Application.Handlers;

// Event adapters for staff alerts (docs/product/notifications.md §4). Like the customer adapters they queue through
// INotificationsApi with the event id as DedupeKey (NOT-R4) and never save.

/// <summary>
/// D33: <c>StaffAccountLocked</c> e-mails <c>STAFF_ACCOUNT_LOCKED</c> to every active staff member who can unlock the
/// account (holders of <c>staff.manage</c>; ADMIN always). A locked recipient is skipped by Identity's query.
/// </summary>
public sealed class NotifyStaffAccountLocked(INotificationsApi notifications, IIdentityApi identity, CustomerNotifier formatting) : IEventHandler<StaffAccountLocked>
{
    public const string TemplateCode = "STAFF_ACCOUNT_LOCKED";

    public const string StaffManagePermission = "staff.manage";

    public async Task HandleAsync(StaffAccountLocked domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var recipients = await identity.GetStaffUserIdsWithPermissionAsync(StaffManagePermission, null, cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        var locked = await identity.GetUserAsync(domainEvent.UserId, cancellationToken);
        var zone = await formatting.TimeZoneAsync(cancellationToken);
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["staffEmail"] = locked?.Email ?? string.Empty,
            ["lockedUntilText"] = $"{CustomerNotifier.DateText(domainEvent.LockedUntil, zone)} {CustomerNotifier.TimeText(domainEvent.LockedUntil, zone)}",
            ["lockoutCountText"] = domainEvent.LockoutCount.ToString(CultureInfo.InvariantCulture),
        };

        // A handful of recipients (the administrators); each message is tracked and saved with the inbox row.
        foreach (var userId in recipients)
        {
            await notifications.QueueAsync(
                new QueueNotificationRequest(userId, TemplateCode, data, [NotificationChannel.Email]) { DedupeKey = domainEvent.EventId.ToString("N") },
                cancellationToken);
        }
    }
}
