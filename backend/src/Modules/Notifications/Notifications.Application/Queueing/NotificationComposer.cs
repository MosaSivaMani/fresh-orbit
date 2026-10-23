using FreshOrbit.Customers.Public;
using FreshOrbit.Identity.Public;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Domain.Messages;
using FreshOrbit.Notifications.Domain.Preferences;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.Notifications.Domain.Templates;
using FreshOrbit.Notifications.Public;
using FreshOrbit.Settings.Public;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Notifications.Application.Queueing;

/// <summary>Renders templates into messages (NOT-R2–R5). Tracks them on the unit of work; the caller saves.</summary>
public sealed class NotificationComposer(
    INotificationsRepository repository,
    ICustomerAccountDirectory accounts,
    ICustomersApi customers,
    ISettingsApi businessSettings,
    NotificationsOptions settings,
    IClock clock) : INotificationsApi
{
    public Task QueueAsync(QueueNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return QueueCoreAsync(request.UserId, request.TemplateCode, request.Data, request.Channels, request.DedupeKey, cancellationToken);
    }

    public async Task QueueForCustomerAsync(QueueCustomerNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = await accounts.FindUserIdAsync(request.CustomerId, cancellationToken);
        if (userId is { } id)
        {
            var data = request.Data;
            if (!data.ContainsKey("recipientName") && await customers.GetCustomerAsync(request.CustomerId, cancellationToken) is { } customer)
            {
                data = new Dictionary<string, string>(data, StringComparer.Ordinal) { ["recipientName"] = customer.FullName };
            }

            await QueueCoreAsync(id, request.TemplateCode, data, request.Channels, request.DedupeKey, cancellationToken);
        }
    }

    /// <summary>The messages tracked; empty when every channel was filtered out or deduplicated.</summary>
    public async Task<IReadOnlyList<Message>> QueueCoreAsync(
        Guid userId,
        string templateCode,
        IReadOnlyDictionary<string, string> data,
        IReadOnlyList<NotificationChannel>? channels,
        string? dedupeKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        var templates = (await repository.GetTemplatesAsync(templateCode, cancellationToken)).Where(t => t.IsActive).ToList();
        if (templates.Count == 0)
        {
            throw new DomainException(NotificationsErrors.ConfigurationMissing, $"No active template for {templateCode}.");
        }

        var requested = channels?.Select(ToChannel).ToHashSet();
        var preferences = await repository.GetPreferencesAsync(userId, cancellationToken);
        var values = await ValuesAsync(data, cancellationToken);
        var queued = new List<Message>();
        foreach (var template in templates.Where(t => requested is null || requested.Contains(t.Channel)))
        {
            if (!IsAllowed(template, preferences))
            {
                continue;
            }

            if (dedupeKey is not null && await repository.DedupeKeyExistsAsync(Message.ComposeDedupeKey(dedupeKey, template.Channel, userId), cancellationToken))
            {
                continue;
            }

            var subject = template.Subject is null ? null : TemplateText.Render(template.Subject, values);
            var message = Message.Queue(userId, template.Channel, template.Code, subject, TemplateText.Render(template.Body, values), data, dedupeKey, clock.UtcNow);
            repository.Add(message);
            queued.Add(message);
        }

        return queued;
    }

    /// <summary>
    /// Common variables first, so the caller's data can override them (e.g. recipientName). Business name and support
    /// contacts come from the Settings business profile (NOT-R1); until an admin saves the profile, the configured
    /// <c>Notifications</c> values are used so OTP messages still go out.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> ValuesAsync(IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        BusinessProfile? profile;
        try
        {
            profile = await businessSettings.GetBusinessProfileAsync(cancellationToken);
        }
        catch (DomainException)
        {
            profile = null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["businessName"] = NonEmpty(profile?.TradeName) ?? settings.BusinessName,
            ["supportPhone"] = NonEmpty(profile?.SupportPhone) ?? settings.SupportPhone,
            ["supportEmail"] = NonEmpty(profile?.SupportEmail) ?? settings.SupportEmail,
            ["recipientName"] = string.Empty,
        };
        foreach (var (key, value) in data)
        {
            values[key] = value;
        }

        return values;
    }

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>NOT-R2.</summary>
    public static bool IsAllowed(Template template, IReadOnlyList<Preference> preferences)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(preferences);
        if (template.Category is not { } category)
        {
            return true;
        }

        if (category == Category.OrderUpdates && template.Channel == Channel.Sms)
        {
            return true;
        }

        var row = preferences.FirstOrDefault(p => p.Category == category && p.Channel == template.Channel);
        return row?.Enabled ?? Preference.DefaultFor(category);
    }

    public static Channel ToChannel(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Sms => Channel.Sms,
        NotificationChannel.Email => Channel.Email,
        NotificationChannel.Push => Channel.Push,
        _ => Channel.InApp,
    };
}
