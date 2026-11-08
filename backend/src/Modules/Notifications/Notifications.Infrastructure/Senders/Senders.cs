using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Domain.Shared;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FreshOrbit.Notifications.Infrastructure.Senders;

/// <summary>Configuration section <c>Notifications:Smtp</c>, used by the <c>SMTP</c> e-mail provider.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Notifications:Smtp";

    public string? Host { get; set; }

    /// <summary>Mailpit listens on 1025 in development.</summary>
    public int Port { get; set; } = 1025;

    public bool UseTls { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "no-reply@freshorbit.local";

    public string FromName { get; set; } = "Fresh Orbitt";
}

/// <summary>Configuration section <c>Notifications:Email</c> (D322, BE-4 / L-19).</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Notifications:Email";

    /// <summary>SMTP through MailKit with <c>Notifications:Smtp</c>.</summary>
    public const string SmtpProvider = "SMTP";

    /// <summary>Development-only sender: logs the masked address and the subject, never sends.</summary>
    public const string DevelopmentProvider = "DEVELOPMENT";

    /// <summary>E-mail switched off on purpose: every e-mail message fails with <c>EMAIL_DISABLED</c>, logged Critical at start.</summary>
    public const string DisabledProvider = "DISABLED";

    /// <summary>
    /// <c>SMTP</c>, <c>DISABLED</c> or <c>DEVELOPMENT</c>. Empty means <c>SMTP</c> when <c>Notifications:Smtp:Host</c>
    /// is set and <c>DEVELOPMENT</c> otherwise; the hosts refuse <c>DEVELOPMENT</c> outside Development, Testing and Demo.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The provider in force: <see cref="Provider"/>, or derived from the SMTP host when it is empty.</summary>
    public static string Effective(EmailOptions email, SmtpOptions smtp)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(smtp);
        var provider = email.Provider.Trim();
        if (provider.Length > 0)
        {
            return provider;
        }

        return string.IsNullOrWhiteSpace(smtp.Host) ? DevelopmentProvider : SmtpProvider;
    }
}

/// <summary>The environments where a development sender (one that delivers nothing) is allowed.</summary>
internal static class DevelopmentSenderEnvironments
{
    public static bool Allow(IHostEnvironment environment)
        => environment.IsDevelopment() || environment.IsEnvironment("Testing") || environment.IsEnvironment(NotificationsModule.DemoEnvironmentName);
}

/// <summary>
/// Rule 7 / D322: the Api and Worker refuse to start outside Development, Testing and Demo when e-mail would go to the
/// development sender (no SMTP host and no explicit provider), and refuse an unknown provider or an SMTP provider
/// without a host or a sender address everywhere.
/// </summary>
internal sealed class EmailOptionsValidator(IHostEnvironment environment, IOptions<SmtpOptions> smtp) : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = smtp.Value;
        var provider = EmailOptions.Effective(options, settings);
        switch (provider)
        {
            case EmailOptions.DevelopmentProvider when !DevelopmentSenderEnvironments.Allow(environment):
                return ValidateOptionsResult.Fail(
                    $"{SmtpOptions.SectionName}:Host must be set (or {EmailOptions.SectionName}:Provider must be {EmailOptions.DisabledProvider}) in the {environment.EnvironmentName} environment; the development e-mail sender never delivers messages.");
            case EmailOptions.DevelopmentProvider or EmailOptions.DisabledProvider:
                return ValidateOptionsResult.Success;
            case EmailOptions.SmtpProvider:
                var problems = new List<string>();
                if (string.IsNullOrWhiteSpace(settings.Host))
                {
                    problems.Add($"{SmtpOptions.SectionName}:Host");
                }

                if (string.IsNullOrWhiteSpace(settings.FromAddress) || !MailboxAddress.TryParse(settings.FromAddress, out var sender) || sender.Address.IndexOf('@', StringComparison.Ordinal) <= 0)
                {
                    problems.Add($"{SmtpOptions.SectionName}:FromAddress (an e-mail address)");
                }

                if (settings.Port is < 1 or > 65535)
                {
                    problems.Add($"{SmtpOptions.SectionName}:Port (1 to 65535)");
                }

                return problems.Count == 0
                    ? ValidateOptionsResult.Success
                    : ValidateOptionsResult.Fail($"The SMTP e-mail provider is not configured; set {string.Join(", ", problems)}.");
            default:
                return ValidateOptionsResult.Fail(
                    $"{EmailOptions.SectionName}:Provider '{provider}' is unknown; use {EmailOptions.SmtpProvider} or {EmailOptions.DisabledProvider} (or {EmailOptions.DevelopmentProvider} in Development).");
        }
    }
}

/// <summary>
/// Development, Testing and Demo e-mail sender (D322, D323): logs only the masked address and the subject, and records
/// <see cref="DevelopmentDelivery.NotDelivered"/> as the provider id, so nobody reads the message as delivered.
/// </summary>
public sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender, IDevelopmentOnlySender
{
    public Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (logger.IsEnabled(LogLevel.Information))
        {
            LogEmail(EmailMasking.Mask(message.To), message.Subject);
        }

        return Task.FromResult<string?>(DevelopmentDelivery.NotDelivered);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "DEV e-mail to {To} not delivered (development sender): {Subject}")]
    private partial void LogEmail(string to, string subject);
}

/// <summary><c>Notifications:Email:Provider = DISABLED</c> (D322): every e-mail fails with <c>EMAIL_DISABLED</c>.</summary>
public sealed class DisabledEmailSender : IEmailSender
{
    public Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        => throw new ChannelDisabledException(Channel.Email);
}

/// <summary>SMTP through MailKit (Mailpit in development).</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;
        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host ?? throw new InvalidOperationException("Notifications:Smtp:Host is not configured."), settings.Port, settings.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
        if (!string.IsNullOrEmpty(settings.UserName))
        {
            await client.AuthenticateAsync(settings.UserName, settings.Password ?? string.Empty, cancellationToken);
        }

        var response = await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
        return string.IsNullOrEmpty(response) ? mime.MessageId : response;
    }
}

public static class EmailSenderRegistration
{
    /// <summary>
    /// Registers the <see cref="IEmailSender"/> named by <c>Notifications:Email:Provider</c> (read when first resolved,
    /// so hosts and tests can override configuration; a sender registered earlier wins). The settings are validated
    /// at host start, before any hosted service runs (D322).
    /// </summary>
    public static IServiceCollection AddFreshOrbitEmail(this IServiceCollection services)
    {
        services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName);
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>());
        services.TryAdd(ServiceDescriptor.Singleton<IEmailSender>(provider =>
        {
            var configured = EmailOptions.Effective(provider.GetRequiredService<IOptions<EmailOptions>>().Value, provider.GetRequiredService<IOptions<SmtpOptions>>().Value);
            return configured switch
            {
                EmailOptions.SmtpProvider => ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider),
                EmailOptions.DisabledProvider => new DisabledEmailSender(),
                EmailOptions.DevelopmentProvider => ActivatorUtilities.CreateInstance<LoggingEmailSender>(provider),
                _ => throw new InvalidOperationException($"{EmailOptions.SectionName}:Provider '{configured}' is unknown; use {EmailOptions.SmtpProvider} or {EmailOptions.DisabledProvider}."),
            };
        }));
        return services;
    }
}
