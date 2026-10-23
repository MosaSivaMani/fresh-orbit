namespace FreshOrbit.Notifications.Domain.Shared;

/// <summary>Error codes of the Notifications module (docs/api §2).</summary>
public static class NotificationsErrors
{
    public const string TemplatePlaceholderUnknown = "TEMPLATE_PLACEHOLDER_UNKNOWN";
    public const string ConfigurationMissing = "CONFIGURATION_MISSING";
    public const string PreferenceRequired = "PREFERENCE_REQUIRED";
    public const string NoRecipientAddress = "NO_RECIPIENT_ADDRESS";

    /// <summary>NOT-R6: attempts before a message is FAILED.</summary>
    public const int MaxAttempts = 5;
}

/// <summary>Stored as SMS / EMAIL / PUSH / IN_APP.</summary>
public enum Channel
{
    Sms,
    Email,
    Push,
    InApp,
}

/// <summary>Stored as ORDER_UPDATES / OFFERS.</summary>
public enum Category
{
    OrderUpdates,
    Offers,
}
