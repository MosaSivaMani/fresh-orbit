using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Domain.Templates;
using FreshOrbit.Notifications.Infrastructure.Senders;
using FreshOrbit.Notifications.Infrastructure.Senders.Dlt;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Notifications.Tests.Unit;

/// <summary>SEC-1 and E2E-1: phone masking, DLT settings, template mapping, variable order and the DLT sender.</summary>
public sealed class SmsSenderUnitTests
{
    private const string OtpBody = "{{code}} is your {{businessName}} login code. It expires in {{expiresInMinutes}} minutes. Do not share it with anyone.";

    [Theory]
    [InlineData("+919876543210", "***3210")]
    [InlineData("12345", "***2345")]
    [InlineData("1234", "****")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Mask_keeps_only_the_last_four_digits(string? phone, string expected)
        => PhoneMasking.Mask(phone).Should().Be(expected);

    [Theory]
    [InlineData("+919876543210", "**10")]
    [InlineData("12", "**")]
    [InlineData(null, "**")]
    public void Dlt_logs_keep_only_the_last_two_digits(string? phone, string expected)
        => DltSmsSender.LastTwo(phone).Should().Be(expected);

    [Theory]
    [InlineData("+919876543210", "9876543210")]
    [InlineData("+916000000000", "6000000000")]
    [InlineData("+915876543210", null)]
    [InlineData("+14155550100", null)]
    [InlineData("919876543210", null)]
    [InlineData("+9198765432101", null)]
    public void Only_indian_mobiles_have_a_dlt_number(string phone, string? expected)
        => DltSmsSender.NationalNumber(phone).Should().Be(expected);

    [Fact]
    public void Ordered_values_follow_the_placeholders_of_the_text()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["expiresInMinutes"] = "5",
            ["businessName"] = "Fresh Orbit",
            ["code"] = "123456",
        };

        TemplateText.OrderedValues(OtpBody, values).Should().Equal("123456", "Fresh Orbit", "5");
        TemplateText.OrderedValues("{{a}} {{missing}} {{a}}", new Dictionary<string, string> { ["a"] = "x" }).Should().Equal("x", string.Empty, "x");
    }

    [Fact]
    public void Extract_values_recovers_the_variables_of_a_rendered_text()
    {
        var rendered = "123456 is your Fresh Orbit login code. It expires in 5 minutes. Do not share it with anyone.";

        TemplateText.ExtractValues(OtpBody, rendered).Should().Equal("123456", "Fresh Orbit", "5");
        TemplateText.ExtractValues(OtpBody, "Something else entirely.").Should().BeNull();
        TemplateText.ExtractValues("No variables (literal [regex] chars).", "No variables (literal [regex] chars).").Should().BeEmpty();
    }

    [Fact]
    public void Dlt_text_replaces_each_placeholder_with_var()
        => TemplateText.ToDltText(OtpBody).Should().Be("{#var#} is your {#var#} login code. It expires in {#var#} minutes. Do not share it with anyone.");

    [Fact]
    public void Complete_dlt_settings_have_no_problems()
        => DltSmsSettings.Problems(Configured()).Should().BeEmpty();

    [Fact]
    public void Dlt_settings_list_every_missing_value_by_key()
    {
        var problems = DltSmsSettings.Problems(new DltSmsOptions { BaseUrl = "http://insecure.example", ApiKey = "key", TimeoutSeconds = 0 });

        problems.Should().Contain(p => p.StartsWith("Notifications:Sms:Dlt:Gateway", StringComparison.Ordinal));
        problems.Should().Contain(p => p.StartsWith("Notifications:Sms:Dlt:BaseUrl", StringComparison.Ordinal));
        problems.Should().Contain(p => p.StartsWith("Notifications:Sms:Dlt:SenderId", StringComparison.Ordinal));
        problems.Should().Contain(p => p.StartsWith("Notifications:Sms:Dlt:PrincipalEntityId", StringComparison.Ordinal));
        problems.Should().Contain(p => p.StartsWith("Notifications:Sms:Dlt:TimeoutSeconds", StringComparison.Ordinal));
        problems.Should().NotContain(p => p.StartsWith("Notifications:Sms:Dlt:ApiKey", StringComparison.Ordinal));
        foreach (var code in DltTemplateCodes.All)
        {
            problems.Should().Contain(p => p.StartsWith($"Notifications:Sms:Dlt:Templates:{code} ", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Base_url_is_optional_but_must_be_https_when_set()
    {
        var withoutUrl = Configured();
        withoutUrl.BaseUrl = null;
        DltSmsSettings.Problems(withoutUrl).Should().BeEmpty();

        var plain = Configured();
        plain.BaseUrl = "http://sms.example.in";
        DltSmsSettings.Problems(plain).Should().ContainSingle().Which.Should().StartWith("Notifications:Sms:Dlt:BaseUrl");
    }

    [Theory]
    [InlineData("OTP_LOGIN", null)]
    [InlineData("OTP_LOGIN", "")]
    [InlineData("OTP_LOGIN", "not-digits")]
    [InlineData("ORDER_DELIVERED", "12")]
    public void A_missing_or_malformed_template_id_is_a_problem(string code, string? id)
    {
        var options = Configured();
        options.Templates[code] = id;

        DltSmsSettings.Problems(options).Should().ContainSingle().Which.Should().StartWith($"Notifications:Sms:Dlt:Templates:{code}");
    }

    [Fact]
    public void Template_codes_match_every_sms_template_of_the_reference_data()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ReferenceFile("notifications", "notification-templates.json")));
        var smsCodes = document.RootElement.GetProperty("templates").EnumerateArray()
            .Where(t => t.GetProperty("channel").GetString() == "SMS")
            .Select(t => t.GetProperty("code").GetString())
            .ToList();

        DltTemplateCodes.All.Should().BeEquivalentTo(smsCodes);
    }

    [Fact]
    public async Task Dlt_sender_maps_the_template_code_and_passes_variables_in_order()
    {
        var gateway = new RecordingGateway("req-1");
        var sender = Sender(Configured(), gateway);

        var id = await sender.SendAsync(new SmsMessage("+919876543210", "OTP_LOGIN", "123456 is your code", ["123456", "Fresh Orbit", "5"]), CancellationToken.None);

        id.Should().Be("req-1");
        var sent = gateway.Sent.Should().ContainSingle().Subject;
        sent.NationalNumber.Should().Be("9876543210");
        sent.DltTemplateId.Should().Be(TemplateId("OTP_LOGIN"));
        sent.SenderId.Should().Be("FRSHOR");
        sent.PrincipalEntityId.Should().Be("1101000000000000001");
        sent.TemplateCode.Should().Be("OTP_LOGIN");
        sent.Text.Should().Be("123456 is your code");
        sent.Variables.Should().Equal("123456", "Fresh Orbit", "5");
    }

    [Fact]
    public async Task Template_codes_are_looked_up_without_regard_to_case_of_the_configuration_key()
    {
        var options = Configured();
        options.Templates = new(StringComparer.OrdinalIgnoreCase) { ["otp_login"] = TemplateId("OTP_LOGIN") };
        foreach (var code in DltTemplateCodes.All.Skip(1))
        {
            options.Templates[code] = TemplateId(code);
        }

        var gateway = new RecordingGateway(null);
        await Sender(options, gateway).SendAsync(new SmsMessage("+919876543210", "OTP_LOGIN", "x", []), CancellationToken.None);

        gateway.Sent.Should().ContainSingle().Which.DltTemplateId.Should().Be(TemplateId("OTP_LOGIN"));
    }

    [Fact]
    public async Task Dlt_sender_refuses_an_unmapped_template_code_without_calling_the_gateway()
    {
        var gateway = new RecordingGateway(null);

        var failure = await Refusal(Sender(Configured(), gateway), new SmsMessage("+919876543210", "SOMETHING_NEW", "x", []));

        failure.Kind.Should().Be(SmsFailureKind.TemplateNotRegistered);
        failure.FailureCode.Should().Be("SMS_TEMPLATE_NOT_REGISTERED");
        failure.Code.Should().Be("SMS_PROVIDER_UNAVAILABLE");
        failure.IsRetryable.Should().BeFalse();
        gateway.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Dlt_sender_refuses_a_number_outside_india()
    {
        var failure = await Refusal(Sender(Configured(), new RecordingGateway(null)), new SmsMessage("+14155550100", "OTP_LOGIN", "x", []));

        failure.Kind.Should().Be(SmsFailureKind.RecipientUnsupported);
    }

    [Fact]
    public async Task Dlt_sender_refuses_to_send_when_not_configured()
    {
        var failure = await Refusal(Sender(new DltSmsOptions(), new RecordingGateway(null)), new SmsMessage("+919876543210", "OTP_LOGIN", "x", []));

        failure.Kind.Should().Be(SmsFailureKind.NotConfigured);
        failure.FailureCode.Should().Be("SMS_NOT_CONFIGURED");
    }

    [Fact]
    public void Only_outages_and_refused_keys_are_retried()
    {
        new SmsDeliveryException(SmsFailureKind.Unreachable).IsRetryable.Should().BeTrue();
        new SmsDeliveryException(SmsFailureKind.Unauthorized).IsRetryable.Should().BeTrue();
        new SmsDeliveryException(SmsFailureKind.Rejected).IsRetryable.Should().BeFalse();
        new SmsDeliveryException(SmsFailureKind.RecipientUnsupported).IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Development_outbox_keeps_the_newest_messages_per_phone()
    {
        var outbox = new DevelopmentSmsOutbox();
        for (var i = 0; i < DevelopmentSmsOutbox.Capacity + 5; i++)
        {
            outbox.Add(new DevelopmentSms("+919800000001", $"message {i}", null, DateTimeOffset.UnixEpoch.AddSeconds(i)));
        }

        var kept = outbox.For("+919800000001");

        kept.Should().HaveCount(DevelopmentSmsOutbox.Capacity);
        kept[0].Text.Should().Be($"message {DevelopmentSmsOutbox.Capacity + 4}");
        outbox.For("+919800000002").Should().BeEmpty();
    }

    /// <summary>A distinct 19-digit id per code.</summary>
    internal static string TemplateId(string code) => "11070000000000" + (1000 + DltTemplateCodes.All.ToList().IndexOf(code)).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(5, '0');

    internal static DltSmsOptions Configured()
    {
        var options = new DltSmsOptions
        {
            Gateway = Fast2SmsGateway.GatewayName,
            BaseUrl = "https://sms.example.in",
            ApiKey = "key",
            SenderId = "FRSHOR",
            PrincipalEntityId = "1101000000000000001",
        };
        foreach (var code in DltTemplateCodes.All)
        {
            options.Templates[code] = TemplateId(code);
        }

        return options;
    }

    internal static string ReferenceFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, "data", "reference", .. parts]);
            if (File.Exists(Path.Combine(directory.FullName, "FreshOrbit.slnx")) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("backend/data/reference was not found above the test output directory.");
    }

    private static async Task<SmsDeliveryException> Refusal(DltSmsSender sender, SmsMessage message)
    {
        var send = () => sender.SendAsync(message, CancellationToken.None);
        return (await send.Should().ThrowAsync<SmsDeliveryException>()).Which;
    }

    private static DltSmsSender Sender(DltSmsOptions dlt, IDltSmsGateway gateway)
        => new(new StaticOptionsMonitor(new SmsOptions { Provider = SmsOptions.DltProvider, Dlt = dlt }), [gateway], NullLogger<DltSmsSender>.Instance);

    private sealed class RecordingGateway(string? providerMessageId) : IDltSmsGateway
    {
        public List<DltSms> Sent { get; } = [];

        public string Name => Fast2SmsGateway.GatewayName;

        public Task<string?> SendAsync(DltSms sms, DltSmsOptions options, CancellationToken cancellationToken)
        {
            Sent.Add(sms);
            return Task.FromResult(providerMessageId);
        }
    }

    private sealed class StaticOptionsMonitor(SmsOptions value) : IOptionsMonitor<SmsOptions>
    {
        public SmsOptions CurrentValue => value;

        public SmsOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<SmsOptions, string?> listener) => null;
    }
}
