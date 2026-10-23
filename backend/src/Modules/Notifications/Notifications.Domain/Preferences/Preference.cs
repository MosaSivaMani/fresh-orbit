using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Notifications.Domain.Preferences;

/// <summary>One (category, channel) switch of a user. Key (user_id, category, channel).</summary>
public sealed class Preference
{
    private Preference()
    {
    }

    public Guid UserId { get; private set; }

    public Category Category { get; private set; }

    public Channel Channel { get; private set; }

    public bool Enabled { get; private set; }

    public static Preference Create(Guid userId, Category category, Channel channel, bool enabled)
        => new() { UserId = userId, Category = category, Channel = channel, Enabled = enabled };

    public void Set(bool enabled) => Enabled = enabled;

    /// <summary>NOT-R2: missing rows mean enabled for ORDER_UPDATES and disabled for OFFERS.</summary>
    public static bool DefaultFor(Category category) => category == Category.OrderUpdates;

    /// <summary>NOT-R8: ORDER_UPDATES SMS cannot be disabled.</summary>
    public static void EnsureAllowed(Category category, Channel channel, bool enabled)
    {
        if (!enabled && category == Category.OrderUpdates && channel == Channel.Sms)
        {
            throw new DomainException(NotificationsErrors.PreferenceRequired, "Order update SMS cannot be turned off.", "preferences");
        }
    }
}
