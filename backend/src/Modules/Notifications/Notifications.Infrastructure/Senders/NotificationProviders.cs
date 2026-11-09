using FreshOrbit.Notifications.Public;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Notifications.Infrastructure.Senders;

/// <summary>
/// <see cref="INotificationProviders"/> from the bound sender options (D358). Reads only the provider codes; the
/// validators of <see cref="SmsOptions"/>, <see cref="EmailOptions"/> and <see cref="PushOptions"/> have already refused
/// an incomplete real provider at start-up.
/// </summary>
public sealed class NotificationProviders(IOptions<SmsOptions> sms, IOptions<EmailOptions> email, IOptions<SmtpOptions> smtp, IOptions<PushOptions> push) : INotificationProviders
{
    public NotificationProvidersStatus GetStatus()
    {
        var smsProvider = Code(sms.Value.Provider, SmsOptions.DevelopmentProvider);
        var emailProvider = Code(EmailOptions.Effective(email.Value, smtp.Value), EmailOptions.DevelopmentProvider);
        var pushProvider = Code(push.Value.Provider, PushOptions.DevelopmentProvider);
        return new NotificationProvidersStatus(
            new ChannelProviderStatus(smsProvider, smsProvider == SmsOptions.DltProvider),
            new ChannelProviderStatus(emailProvider, emailProvider == EmailOptions.SmtpProvider),
            new ChannelProviderStatus(pushProvider, pushProvider == PushOptions.FcmProvider));
    }

    private static string Code(string? configured, string whenEmpty)
    {
        var value = (configured ?? string.Empty).Trim();
        return value.Length == 0 ? whenEmpty : value;
    }
}
