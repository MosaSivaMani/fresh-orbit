using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Notifications.Domain.Devices;

/// <summary>Stored as FCM (Ruling D28: Flutter app, Firebase Cloud Messaging).</summary>
public enum PushProvider
{
    Fcm,
}

/// <summary>Stored as ANDROID / IOS / WEB.</summary>
public enum DevicePlatform
{
    Android,
    Ios,
    Web,
}

/// <summary>A push registration of an app install (NOT-R7). <c>push_token</c> is unique.</summary>
public sealed class DeviceToken : AggregateRoot
{
    public const int MaxTokenLength = 4096;

    private DeviceToken()
    {
        PushToken = string.Empty;
    }

    public Guid UserId { get; private set; }

    public string PushToken { get; private set; }

    public PushProvider Provider { get; private set; }

    public DevicePlatform Platform { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public static DeviceToken Register(Guid userId, string pushToken, PushProvider provider, DevicePlatform platform, DateTimeOffset now)
        => new() { UserId = userId, PushToken = pushToken, Provider = provider, Platform = platform, LastSeenAt = now };

    /// <summary>Re-registration: the token moves to the caller (a device changes hands on re-login).</summary>
    public void Refresh(Guid userId, PushProvider provider, DevicePlatform platform, DateTimeOffset now)
    {
        UserId = userId;
        Provider = provider;
        Platform = platform;
        LastSeenAt = now;
    }
}
