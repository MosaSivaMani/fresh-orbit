using System.Collections.Concurrent;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.SharedKernel.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Notifications.Infrastructure.Senders;

/// <summary>Configuration section <c>Notifications:Sms</c> (SEC-1).</summary>
public sealed class SmsOptions
{
    public const string SectionName = "Notifications:Sms";

    /// <summary>Development-only sender: keeps messages in memory, never sends.</summary>
    public const string DevelopmentProvider = "DEVELOPMENT";

    /// <summary>A TRAI DLT-registered SMS provider (<see cref="DltSmsSender"/>).</summary>
    public const string DltProvider = "DLT";

    /// <summary>
    /// <c>DLT</c> or <c>DEVELOPMENT</c>. Empty means <c>DEVELOPMENT</c>, which the host refuses to start with outside
    /// Development and Testing.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    public DltSmsOptions Dlt { get; set; } = new();
}

/// <summary>Section <c>Notifications:Sms:Dlt</c>. Secrets come from the environment, never from appsettings.</summary>
public sealed class DltSmsOptions
{
    /// <summary>Provider API endpoint (HTTPS).</summary>
    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>The 6-character DLT sender id (header).</summary>
    public string? SenderId { get; set; }

    /// <summary>The DLT principal entity id of the business.</summary>
    public string? PrincipalEntityId { get; set; }
}

/// <summary>A sender that never delivers anything; allowed only in Development and Testing.</summary>
public interface IDevelopmentOnlySender
{
}

/// <summary>The last messages of <see cref="DevelopmentSmsSender"/>, readable through the Development-only endpoint.</summary>
public sealed class DevelopmentSmsOutbox
{
    public const int Capacity = 200;

    private readonly ConcurrentQueue<DevelopmentSms> _messages = new();

    public void Add(DevelopmentSms message)
    {
        _messages.Enqueue(message);
        while (_messages.Count > Capacity && _messages.TryDequeue(out _))
        {
        }
    }

    /// <summary>Messages to <paramref name="phone"/>, newest first.</summary>
    public IReadOnlyList<DevelopmentSms> For(string phone)
        => [.. _messages.Where(m => string.Equals(m.Phone, phone, StringComparison.Ordinal)).Reverse()];
}

public sealed record DevelopmentSms(string Phone, string Text, string? DltTemplateId, DateTimeOffset SentAt);

/// <summary>
/// Development and Testing SMS sender: keeps each message in <see cref="DevelopmentSmsOutbox"/> (read it with
/// <c>GET /api/v1/dev/sms?phone=…</c>) and logs only the masked phone and the length, never the text (SEC-1).
/// </summary>
public sealed partial class DevelopmentSmsSender(DevelopmentSmsOutbox outbox, IClock clock, ILogger<DevelopmentSmsSender> logger) : ISmsSender, IDevelopmentOnlySender
{
    public Task<string?> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        outbox.Add(new DevelopmentSms(message.Phone, message.Text, message.DltTemplateId, clock.UtcNow));
        LogSms(PhoneMasking.Mask(message.Phone), message.DltTemplateId ?? "-", message.Text.Length);
        return Task.FromResult<string?>(null);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "DEV SMS to {Phone} (DLT {DltTemplateId}, {Length} characters) kept in the development outbox")]
    private partial void LogSms(string phone, string dltTemplateId, int length);
}

/// <summary>
/// Adapter stub for a TRAI DLT-registered SMS provider (SEC-1). It refuses to send when <c>Notifications:Sms:Dlt</c>
/// is incomplete or the message has no DLT template id. The provider HTTP call is not implemented until a provider is
/// chosen: sending throws, so a failure is never silent.
/// </summary>
public sealed class DltSmsSender(IOptionsMonitor<SmsOptions> options) : ISmsSender
{
    /// <summary>Names of the missing <c>Notifications:Sms:Dlt</c> settings; empty when configured.</summary>
    public IReadOnlyList<string> MissingSettings() => MissingSettings(options.CurrentValue.Dlt);

    /// <summary>Names of the settings missing from <paramref name="dlt"/>; empty when configured.</summary>
    public static IReadOnlyList<string> MissingSettings(DltSmsOptions dlt)
    {
        ArgumentNullException.ThrowIfNull(dlt);
        var missing = new List<string>();
        if (!Uri.TryCreate(dlt.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            missing.Add($"{SmsOptions.SectionName}:Dlt:BaseUrl (https)");
        }

        if (string.IsNullOrWhiteSpace(dlt.ApiKey))
        {
            missing.Add($"{SmsOptions.SectionName}:Dlt:ApiKey");
        }

        if (string.IsNullOrWhiteSpace(dlt.SenderId))
        {
            missing.Add($"{SmsOptions.SectionName}:Dlt:SenderId");
        }

        if (string.IsNullOrWhiteSpace(dlt.PrincipalEntityId))
        {
            missing.Add($"{SmsOptions.SectionName}:Dlt:PrincipalEntityId");
        }

        return missing;
    }

    public Task<string?> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var missing = MissingSettings();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"The DLT SMS provider is not configured; set {string.Join(", ", missing)}.");
        }

        if (string.IsNullOrWhiteSpace(message.DltTemplateId))
        {
            throw new InvalidOperationException("The SMS template has no DLT template id; register it with the operator and set dltTemplateId.");
        }

        throw new NotSupportedException("The DLT SMS provider client is not implemented yet; implement DltSmsSender.SendAsync for the chosen provider.");
    }
}

public static class SmsSenderRegistration
{
    /// <summary>
    /// Registers the <see cref="ISmsSender"/> named by <c>Notifications:Sms:Provider</c> (read when first resolved, so
    /// hosts and tests can override configuration; a sender registered earlier wins). The settings are validated at
    /// host start, before any hosted service runs.
    /// </summary>
    public static IServiceCollection AddFreshOrbitSms(this IServiceCollection services)
    {
        services.AddOptions<SmsOptions>().BindConfiguration(SmsOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SmsOptions>, SmsOptionsValidator>());
        services.TryAddSingleton<DevelopmentSmsOutbox>();
        services.TryAdd(ServiceDescriptor.Singleton<ISmsSender>(provider =>
        {
            var configured = provider.GetRequiredService<IOptions<SmsOptions>>().Value.Provider.Trim();
            return configured switch
            {
                "" or SmsOptions.DevelopmentProvider => ActivatorUtilities.CreateInstance<DevelopmentSmsSender>(provider),
                SmsOptions.DltProvider => ActivatorUtilities.CreateInstance<DltSmsSender>(provider),
                _ => throw new InvalidOperationException($"{SmsOptions.SectionName}:Provider '{configured}' is unknown; use {SmsOptions.DltProvider} (or {SmsOptions.DevelopmentProvider} in Development)."),
            };
        }));
        return services;
    }
}

/// <summary>
/// Rule 7 / SEC-1: the Api and Worker refuse to start outside Development and Testing with the development sender
/// (which never delivers), and refuse an unknown provider or an incomplete DLT configuration everywhere.
/// </summary>
internal sealed class SmsOptionsValidator(IHostEnvironment environment) : IValidateOptions<SmsOptions>
{
    public ValidateOptionsResult Validate(string? name, SmsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var provider = options.Provider.Trim();
        switch (provider)
        {
            case "" or SmsOptions.DevelopmentProvider when !(environment.IsDevelopment() || environment.IsEnvironment("Testing")):
                return ValidateOptionsResult.Fail(
                    $"{SmsOptions.SectionName}:Provider must be {SmsOptions.DltProvider} in the {environment.EnvironmentName} environment; the development SMS sender never delivers messages.");
            case "" or SmsOptions.DevelopmentProvider:
                return ValidateOptionsResult.Success;
            case SmsOptions.DltProvider:
                var missing = DltSmsSender.MissingSettings(options.Dlt);
                return missing.Count == 0
                    ? ValidateOptionsResult.Success
                    : ValidateOptionsResult.Fail($"The DLT SMS provider is not configured; set {string.Join(", ", missing)}.");
            default:
                return ValidateOptionsResult.Fail(
                    $"{SmsOptions.SectionName}:Provider '{provider}' is unknown; use {SmsOptions.DltProvider} (or {SmsOptions.DevelopmentProvider} in Development).");
        }
    }
}
