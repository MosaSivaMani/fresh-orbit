namespace FreshOrbit.Notifications.Public;

/// <summary>
/// Which provider each outgoing channel uses in this server (D358, store readiness). Answers only provider codes and
/// whether the channel really delivers; never a key, host, sender id or any other configuration value.
/// </summary>
public interface INotificationProviders
{
    NotificationProvidersStatus GetStatus();
}

/// <param name="Sms">Provider <c>DLT</c> or <c>DEVELOPMENT</c>.</param>
/// <param name="Email">Provider <c>SMTP</c>, <c>DISABLED</c> or <c>DEVELOPMENT</c>.</param>
/// <param name="Push">Provider <c>FCM</c>, <c>DISABLED</c> or <c>DEVELOPMENT</c>.</param>
public sealed record NotificationProvidersStatus(ChannelProviderStatus Sms, ChannelProviderStatus Email, ChannelProviderStatus Push);

/// <param name="Provider">The provider code in force (an empty setting is reported as <c>DEVELOPMENT</c>).</param>
/// <param name="Delivers">
/// True only for a real provider (<c>DLT</c>, <c>SMTP</c>, <c>FCM</c>). The hosts refuse to start when a real provider's
/// settings are incomplete (SEC-1, D322, D325), so a running server with a real provider is configured.
/// </param>
public sealed record ChannelProviderStatus(string Provider, bool Delivers);
