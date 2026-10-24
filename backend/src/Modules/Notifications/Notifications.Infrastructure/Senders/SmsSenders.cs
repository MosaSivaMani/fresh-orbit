using System.Collections.Concurrent;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Infrastructure.Senders.Dlt;
using FreshOrbit.SharedKernel.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Notifications.Infrastructure.Senders;

/// <summary>Configuration section <c>Notifications:Sms</c> (SEC-1, E2E-1).</summary>
public sealed class SmsOptions
{
    public const string SectionName = "Notifications:Sms";

    /// <summary>Development-only sender: keeps messages in memory, never sends.</summary>
    public const string DevelopmentProvider = "DEVELOPMENT";

    /// <summary>A TRAI DLT-registered SMS provider (<see cref="DltSmsSender"/> through an <see cref="IDltSmsGateway"/>).</summary>
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
    /// <summary>The provider adapter (<see cref="DltGateways"/>), e.g. <c>FAST2SMS</c>.</summary>
    public string? Gateway { get; set; }

    /// <summary>Provider API base URL (HTTPS). Empty = the gateway's documented public endpoint.</summary>
    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>The DLT-registered sender id (header), 3 to 6 letters or digits.</summary>
    public string? SenderId { get; set; }

    /// <summary>The DLT principal entity id of the business (digits).</summary>
    public string? PrincipalEntityId { get; set; }

    /// <summary>Seconds to wait for the provider before the send counts as failed (1 to 60).</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Our SMS template code → the DLT content template id registered for its text (digits). Every code of
    /// <see cref="DltTemplateCodes.All"/> is required at start-up.
    /// </summary>
    public Dictionary<string, string?> Templates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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

/// <param name="DltTemplateId">The id configured in <c>Notifications:Sms:Dlt:Templates</c> for the code, if any.</param>
public sealed record DevelopmentSms(string Phone, string Text, string? DltTemplateId, DateTimeOffset SentAt);

/// <summary>
/// Development and Testing SMS sender: keeps each message in <see cref="DevelopmentSmsOutbox"/> (read it with
/// <c>GET /api/v1/dev/sms?phone=…</c>) and logs only the masked phone and the length, never the text (SEC-1).
/// </summary>
public sealed partial class DevelopmentSmsSender(DevelopmentSmsOutbox outbox, IOptionsMonitor<SmsOptions> options, IClock clock, ILogger<DevelopmentSmsSender> logger) : ISmsSender, IDevelopmentOnlySender
{
    public Task<string?> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var dltTemplateId = options.CurrentValue.Dlt.Templates.GetValueOrDefault(message.TemplateCode);
        outbox.Add(new DevelopmentSms(message.Phone, message.Text, string.IsNullOrWhiteSpace(dltTemplateId) ? null : dltTemplateId, clock.UtcNow));
        if (logger.IsEnabled(LogLevel.Information))
        {
            var maskedPhone = PhoneMasking.Mask(message.Phone);
            LogSms(maskedPhone, message.TemplateCode, message.Text.Length);
        }

        return Task.FromResult<string?>(null);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "DEV SMS {TemplateCode} to {Phone} ({Length} characters) kept in the development outbox")]
    private partial void LogSms(string phone, string templateCode, int length);
}

public static class SmsSenderRegistration
{
    /// <summary>
    /// Registers the <see cref="ISmsSender"/> named by <c>Notifications:Sms:Provider</c> (read when first resolved, so
    /// hosts and tests can override configuration; a sender registered earlier wins), the DLT gateways and their
    /// HTTP clients. The settings are validated at host start, before any hosted service runs.
    /// </summary>
    public static IServiceCollection AddFreshOrbitSms(this IServiceCollection services)
    {
        services.AddOptions<SmsOptions>().BindConfiguration(SmsOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SmsOptions>, SmsOptionsValidator>());
        services.TryAddSingleton<DevelopmentSmsOutbox>();

        // One adapter per provider (E2E-1). Each uses a named client from IHttpClientFactory, so handlers rotate.
        services.AddHttpClient(Fast2SmsGateway.HttpClientName)
            .ConfigureHttpClient((provider, client) =>
            {
                var timeout = provider.GetRequiredService<IOptions<SmsOptions>>().Value.Dlt.TimeoutSeconds;
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeout, 1, 60));
            });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDltSmsGateway, Fast2SmsGateway>());

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
/// Rule 7 / SEC-1 / E2E-1: the Api and Worker refuse to start outside Development and Testing with the development
/// sender (which never delivers), and refuse an unknown provider or an incomplete DLT configuration (including a
/// missing DLT template id for any SMS template code) everywhere.
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
                var problems = DltSmsSettings.Problems(options.Dlt);
                return problems.Count == 0
                    ? ValidateOptionsResult.Success
                    : ValidateOptionsResult.Fail($"The DLT SMS provider is not configured; set {string.Join(", ", problems)}.");
            default:
                return ValidateOptionsResult.Fail(
                    $"{SmsOptions.SectionName}:Provider '{provider}' is unknown; use {SmsOptions.DltProvider} (or {SmsOptions.DevelopmentProvider} in Development).");
        }
    }
}
