using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.Notifications.Infrastructure.Senders.Fcm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Notifications.Infrastructure.Senders;

/// <summary>Configuration section <c>Notifications:Push</c> (D324, D325; BE-3 / L-20).</summary>
public sealed class PushOptions
{
    public const string SectionName = "Notifications:Push";

    /// <summary>Firebase Cloud Messaging HTTP v1 (<see cref="FcmPushSender"/>) with <c>Notifications:Push:Fcm</c>.</summary>
    public const string FcmProvider = "FCM";

    /// <summary>Development-only sender: logs the device count and the title, never sends.</summary>
    public const string DevelopmentProvider = "DEVELOPMENT";

    /// <summary>Push switched off on purpose: every push message fails with <c>PUSH_DISABLED</c>, logged Critical at start.</summary>
    public const string DisabledProvider = "DISABLED";

    /// <summary>
    /// <c>FCM</c>, <c>DISABLED</c> or <c>DEVELOPMENT</c>. Empty means <c>DEVELOPMENT</c>, which the hosts refuse outside
    /// Development, Testing and Demo.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    public FcmOptions Fcm { get; set; } = new();
}

/// <summary>Section <c>Notifications:Push:Fcm</c>. The service account key comes from the environment, never from a tracked file.</summary>
public sealed class FcmOptions
{
    /// <summary>The Firebase project id (Project settings → General), e.g. <c>fresh-orbit-prod</c>.</summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// The whole service account key JSON (Project settings → Service accounts → Generate new private key), on one
    /// line. It must carry <c>client_email</c> and <c>private_key</c>.
    /// </summary>
    public string? ServiceAccountJson { get; set; }

    /// <summary>Seconds to wait for Google before the send counts as failed (1 to 60).</summary>
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Rule 7 / D325: the Api and Worker refuse to start outside Development, Testing and Demo with the development push
/// sender (which never delivers), and refuse an unknown provider or an incomplete FCM configuration everywhere.
/// </summary>
internal sealed class PushOptionsValidator(IHostEnvironment environment) : IValidateOptions<PushOptions>
{
    public ValidateOptionsResult Validate(string? name, PushOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var provider = (options.Provider ?? string.Empty).Trim();
        switch (provider)
        {
            case "" or PushOptions.DevelopmentProvider when !DevelopmentSenderEnvironments.Allow(environment):
                return ValidateOptionsResult.Fail(
                    $"{PushOptions.SectionName}:Provider must be {PushOptions.FcmProvider} (or {PushOptions.DisabledProvider}) in the {environment.EnvironmentName} environment; the development push sender never delivers messages.");
            case "" or PushOptions.DevelopmentProvider or PushOptions.DisabledProvider:
                return ValidateOptionsResult.Success;
            case PushOptions.FcmProvider:
                var problems = FcmSettings.Problems(options.Fcm);
                return problems.Count == 0
                    ? ValidateOptionsResult.Success
                    : ValidateOptionsResult.Fail($"The FCM push provider is not configured; set {string.Join(", ", problems)}.");
            default:
                return ValidateOptionsResult.Fail(
                    $"{PushOptions.SectionName}:Provider '{provider}' is unknown; use {PushOptions.FcmProvider} or {PushOptions.DisabledProvider} (or {PushOptions.DevelopmentProvider} in Development).");
        }
    }
}

/// <summary>
/// Development, Testing and Demo push sender (D323): logs only the device count and the title, and records
/// <see cref="DevelopmentDelivery.NotDelivered"/> as the provider id, so nobody reads the message as delivered.
/// </summary>
public sealed partial class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender, IDevelopmentOnlySender
{
    public Task<PushResult> SendAsync(PushMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogPush(message.Tokens.Count, message.Title);
        return Task.FromResult(new PushResult(DevelopmentDelivery.NotDelivered, [], message.Tokens.Count));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "DEV push to {DeviceCount} device(s) not delivered (development sender): {Title}")]
    private partial void LogPush(int deviceCount, string title);
}

/// <summary><c>Notifications:Push:Provider = DISABLED</c> (D325): every push fails with <c>PUSH_DISABLED</c>.</summary>
public sealed class DisabledPushSender : IPushSender
{
    public Task<PushResult> SendAsync(PushMessage message, CancellationToken cancellationToken)
        => throw new ChannelDisabledException(Channel.Push);
}

public static class PushSenderRegistration
{
    /// <summary>
    /// Registers the <see cref="IPushSender"/> named by <c>Notifications:Push:Provider</c> (read when first resolved, so
    /// hosts and tests can override configuration; a sender registered earlier wins) and the FCM HTTP client. The
    /// settings are validated at host start, before any hosted service runs (D325).
    /// </summary>
    public static IServiceCollection AddFreshOrbitPush(this IServiceCollection services)
    {
        services.AddOptions<PushOptions>().BindConfiguration(PushOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PushOptions>, PushOptionsValidator>());
        services.AddHttpClient(FcmPushSender.HttpClientName)
            .ConfigureHttpClient((provider, client) =>
            {
                var timeout = provider.GetRequiredService<IOptions<PushOptions>>().Value.Fcm.TimeoutSeconds;
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeout, 1, 60));
            });
        services.TryAdd(ServiceDescriptor.Singleton<IPushSender>(provider =>
        {
            var configured = (provider.GetRequiredService<IOptions<PushOptions>>().Value.Provider ?? string.Empty).Trim();
            return configured switch
            {
                PushOptions.FcmProvider => ActivatorUtilities.CreateInstance<FcmPushSender>(provider),
                PushOptions.DisabledProvider => new DisabledPushSender(),
                "" or PushOptions.DevelopmentProvider => ActivatorUtilities.CreateInstance<LoggingPushSender>(provider),
                _ => throw new InvalidOperationException($"{PushOptions.SectionName}:Provider '{configured}' is unknown; use {PushOptions.FcmProvider} or {PushOptions.DisabledProvider}."),
            };
        }));
        return services;
    }
}

/// <summary>
/// D322 / D325: a channel switched off by configuration is not silent: the Api and Worker log it as Critical at every
/// start, so the operator sees that e-mail or push messages are failing on purpose.
/// </summary>
internal sealed partial class DisabledChannelsStartupReport(IOptions<EmailOptions> email, IOptions<SmtpOptions> smtp, IOptions<PushOptions> push, ILogger<DisabledChannelsStartupReport> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (EmailOptions.Effective(email.Value, smtp.Value) == EmailOptions.DisabledProvider)
        {
            LogDisabled(EmailOptions.SectionName + ":Provider", "EMAIL_DISABLED");
        }

        if ((push.Value.Provider ?? string.Empty).Trim() == PushOptions.DisabledProvider)
        {
            LogDisabled(PushOptions.SectionName + ":Provider", "PUSH_DISABLED");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Critical, Message = "{Setting} is DISABLED: these notifications are not sent; each message fails with {FailureCode}.")]
    private partial void LogDisabled(string setting, string failureCode);
}
