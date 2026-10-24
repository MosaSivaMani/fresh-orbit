using System.Text.RegularExpressions;
using FreshOrbit.Notifications.Application.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Notifications.Infrastructure.Senders.Dlt;

/// <summary>
/// One SMS in TRAI DLT terms, ready for any DLT-registered provider (E2E-1). Every field is already checked by
/// <see cref="DltSmsSender"/>; gateways only translate it into their API.
/// </summary>
/// <param name="NationalNumber">The 10-digit Indian mobile number (no +91).</param>
/// <param name="SenderId">The DLT-registered header.</param>
/// <param name="PrincipalEntityId">The DLT principal entity id of the business.</param>
/// <param name="DltTemplateId">The DLT content template id registered for <paramref name="TemplateCode"/>.</param>
/// <param name="TemplateCode">Our template code (for logs only).</param>
/// <param name="Text">The full text: the DLT-approved text with each <c>{#var#}</c> filled in.</param>
/// <param name="Variables">The values of the <c>{#var#}</c> slots in DLT-approved order, for gateways that take variables.</param>
public sealed record DltSms(
    string NationalNumber,
    string SenderId,
    string PrincipalEntityId,
    string DltTemplateId,
    string TemplateCode,
    string Text,
    IReadOnlyList<string> Variables);

/// <summary>
/// The HTTP adapter of one DLT-registered SMS provider. Returns the provider's message id on acceptance and throws
/// <see cref="SmsDeliveryException"/> for every failure (unreachable, timeout, refused key, rejected message). It never
/// logs the text, the number or the API key.
/// </summary>
public interface IDltSmsGateway
{
    /// <summary>The <c>Notifications:Sms:Dlt:Gateway</c> value that selects this adapter (UPPER_SNAKE).</summary>
    string Name { get; }

    Task<string?> SendAsync(DltSms sms, DltSmsOptions options, CancellationToken cancellationToken);
}

/// <summary>The provider adapters this build contains (<c>Notifications:Sms:Dlt:Gateway</c>).</summary>
public static class DltGateways
{
    public static readonly IReadOnlyList<string> Known = [Fast2SmsGateway.GatewayName];
}

/// <summary>
/// Every SMS template code the system can send (<c>notification-templates.json</c>, channel SMS). Each needs a DLT
/// content template id in <c>Notifications:Sms:Dlt:Templates</c>, or the hosts refuse to start (rule 7): operators
/// block an SMS whose template is not registered. A test keeps this list equal to the reference data.
/// </summary>
public static class DltTemplateCodes
{
    public static readonly IReadOnlyList<string> All =
    [
        "OTP_LOGIN",
        "OTP_VERIFY_PHONE",
        "ORDER_CONFIRMED",
        "PAYMENT_RECEIVED",
        "PAYMENT_FAILED",
        "SUBSTITUTION_PROPOSED",
        "ORDER_OUT_FOR_DELIVERY",
        "ORDER_DELIVERED",
        "DELIVERY_FAILED",
        "ORDER_CANCELLED",
        "REFUND_INITIATED",
        "REFUND_SUCCEEDED",
        "COMPLAINT_RESOLVED",
    ];
}

/// <summary>The checks on <c>Notifications:Sms:Dlt</c>, shared by the start-up validator and the sender.</summary>
public static partial class DltSmsSettings
{
    private const string Section = SmsOptions.SectionName + ":Dlt";

    /// <summary>The settings to fix, each named with its full key; empty when the configuration is complete.</summary>
    public static IReadOnlyList<string> Problems(DltSmsOptions dlt)
    {
        ArgumentNullException.ThrowIfNull(dlt);
        var problems = new List<string>();
        var gateway = dlt.Gateway?.Trim() ?? string.Empty;
        if (!DltGateways.Known.Contains(gateway, StringComparer.Ordinal))
        {
            problems.Add($"{Section}:Gateway (one of {string.Join(", ", DltGateways.Known)})");
        }

        if (!string.IsNullOrWhiteSpace(dlt.BaseUrl)
            && (!Uri.TryCreate(dlt.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            problems.Add($"{Section}:BaseUrl (https, or empty for the gateway's endpoint)");
        }

        if (string.IsNullOrWhiteSpace(dlt.ApiKey))
        {
            problems.Add($"{Section}:ApiKey");
        }

        if (!SenderIdPattern().IsMatch(dlt.SenderId?.Trim() ?? string.Empty))
        {
            problems.Add($"{Section}:SenderId (3 to 6 letters or digits)");
        }

        if (!IsDltId(dlt.PrincipalEntityId?.Trim()))
        {
            problems.Add($"{Section}:PrincipalEntityId (digits)");
        }

        if (dlt.TimeoutSeconds is < 1 or > 60)
        {
            problems.Add($"{Section}:TimeoutSeconds (1 to 60)");
        }

        foreach (var code in DltTemplateCodes.All)
        {
            if (!IsDltId(dlt.Templates.GetValueOrDefault(code)))
            {
                problems.Add($"{Section}:Templates:{code} (DLT template id, digits)");
            }
        }

        foreach (var (code, id) in dlt.Templates)
        {
            if (!DltTemplateCodes.All.Contains(code, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(id) && !IsDltId(id))
            {
                problems.Add($"{Section}:Templates:{code} (DLT template id, digits)");
            }
        }

        return problems;
    }

    /// <summary>DLT entity and content template ids are digit strings (19 digits in the examples we saw).</summary>
    public static bool IsDltId(string? value) => value is not null && DltIdPattern().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9]{3,6}$")]
    private static partial Regex SenderIdPattern();

    [GeneratedRegex("^[0-9]{10,25}$")]
    private static partial Regex DltIdPattern();
}

/// <summary>
/// The <c>DLT</c> <see cref="ISmsSender"/> (E2E-1): maps our template code to its DLT template id, checks the number is
/// an Indian mobile, and hands a <see cref="DltSms"/> to the configured <see cref="IDltSmsGateway"/>. Logs only the
/// template code and the last 2 digits of the phone (SEC-1). Every failure is an <see cref="SmsDeliveryException"/>.
/// </summary>
public sealed partial class DltSmsSender(IOptionsMonitor<SmsOptions> options, IEnumerable<IDltSmsGateway> gateways, ILogger<DltSmsSender> logger) : ISmsSender
{
    private readonly IReadOnlyList<IDltSmsGateway> _gateways = [.. gateways];

    public async Task<string?> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var dlt = options.CurrentValue.Dlt;
        var phoneEnd = LastTwo(message.Phone);
        var gatewayName = dlt.Gateway?.Trim() ?? string.Empty;
        try
        {
            if (DltSmsSettings.Problems(dlt).Count > 0)
            {
                throw new SmsDeliveryException(SmsFailureKind.NotConfigured);
            }

            var templateId = dlt.Templates.GetValueOrDefault(message.TemplateCode)?.Trim();
            if (!DltSmsSettings.IsDltId(templateId))
            {
                throw new SmsDeliveryException(SmsFailureKind.TemplateNotRegistered);
            }

            var national = NationalNumber(message.Phone) ?? throw new SmsDeliveryException(SmsFailureKind.RecipientUnsupported);
            var gateway = _gateways.FirstOrDefault(g => string.Equals(g.Name, gatewayName, StringComparison.Ordinal))
                ?? throw new SmsDeliveryException(SmsFailureKind.NotConfigured);
            var sms = new DltSms(national, dlt.SenderId!.Trim(), dlt.PrincipalEntityId!.Trim(), templateId!, message.TemplateCode, message.Text, message.Variables);
            var providerMessageId = await gateway.SendAsync(sms, dlt, cancellationToken);
            LogSent(message.TemplateCode, phoneEnd, gatewayName, providerMessageId ?? "-");
            return providerMessageId;
        }
        catch (SmsDeliveryException failure)
        {
            LogNotSent(message.TemplateCode, phoneEnd, gatewayName, failure.FailureCode, failure.ProviderStatus ?? "-", failure.InnerException?.GetType().Name ?? "-");
            throw;
        }
    }

    /// <summary><c>+91</c> followed by a 10-digit mobile number starting 6 to 9 → the 10 digits; anything else → null.</summary>
    public static string? NationalNumber(string? phone)
        => phone is not null && IndianMobile().Match(phone) is { Success: true } match ? match.Groups[1].Value : null;

    /// <summary>The last 2 digits only, e.g. <c>**10</c> (SEC-1).</summary>
    public static string LastTwo(string? phone) => phone is { Length: > 2 } ? "**" + phone[^2..] : "**";

    [GeneratedRegex(@"^\+91([6-9][0-9]{9})$")]
    private static partial Regex IndianMobile();

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS {TemplateCode} to {PhoneEnd} accepted by {Gateway} (provider id {ProviderMessageId})")]
    private partial void LogSent(string templateCode, string phoneEnd, string gateway, string providerMessageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SMS {TemplateCode} to {PhoneEnd} not sent by {Gateway}: {FailureCode} (provider status {ProviderStatus}, cause {Cause})")]
    private partial void LogNotSent(string templateCode, string phoneEnd, string gateway, string failureCode, string providerStatus, string cause);
}
