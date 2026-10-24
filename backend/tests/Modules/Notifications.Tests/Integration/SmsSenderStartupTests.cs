using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using FreshOrbit.E1Testing;
using FreshOrbit.Hosting;
using FreshOrbit.Notifications.Infrastructure.Senders;
using FreshOrbit.Notifications.Infrastructure.Senders.Dlt;
using FreshOrbit.Notifications.Tests.Unit;
using FreshOrbit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog.Core;
using Serilog.Events;

namespace FreshOrbit.Notifications.Tests.Integration;

/// <summary>
/// SEC-1: outside Development/Testing the host refuses the development SMS sender and an incomplete DLT
/// configuration; the development sender never logs the text or the full phone number.
/// </summary>
public sealed partial class SmsSenderStartupTests(E1Database database) : IAsyncLifetime
{
    private static readonly Dictionary<string, string?> FullDlt = FullDltSettings();

    private readonly List<SmsHostFactory> _factories = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            await factory.DisposeAsync();
        }
    }

    // Refusals start a generic host with the module's own registration: WebApplicationFactory sometimes reports
    // ObjectDisposedException instead of the startup error when the Api entry point fails, which made them flaky.
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Host_refuses_the_development_sender_outside_development(string environment)
    {
        var failure = await StartFailure(environment, []);

        failure.Failures.Should().ContainSingle().Which.Should().Contain("Notifications:Sms:Provider");
    }

    [Fact]
    public async Task Host_refuses_an_empty_provider_outside_development()
    {
        var failure = await StartFailure("Production", new() { ["Notifications:Sms:Provider"] = " " });

        failure.Failures.Should().ContainSingle().Which.Should().Contain("Notifications:Sms:Provider");
    }

    [Fact]
    public async Task Host_refuses_an_incomplete_dlt_configuration()
    {
        var failure = await StartFailure("Staging", new() { ["Notifications:Sms:Provider"] = "DLT", ["Notifications:Sms:Dlt:ApiKey"] = "test-key" });

        failure.Failures.Should().ContainSingle().Which.Should()
            .Contain("Notifications:Sms:Dlt:Gateway").And.Contain("Notifications:Sms:Dlt:SenderId")
            .And.Contain("Notifications:Sms:Dlt:PrincipalEntityId").And.Contain("Notifications:Sms:Dlt:Templates:OTP_LOGIN")
            .And.NotContain("Notifications:Sms:Dlt:ApiKey").And.NotContain("Notifications:Sms:Dlt:BaseUrl");
    }

    [Theory]
    [InlineData("OTP_LOGIN")]
    [InlineData("ORDER_CONFIRMED")]
    public async Task Host_refuses_dlt_without_a_template_id_for_an_sms_template(string code)
    {
        var settings = new Dictionary<string, string?>(FullDlt) { [$"Notifications:Sms:Dlt:Templates:{code}"] = "" };

        var failure = await StartFailure("Production", settings);

        failure.Failures.Should().ContainSingle().Which.Should().Contain($"Notifications:Sms:Dlt:Templates:{code}")
            .And.NotContain("Notifications:Sms:Dlt:ApiKey");
    }

    [Fact]
    public async Task Refused_sms_configuration_exits_with_code_one()
    {
        var settings = new Dictionary<string, string?>(FullDlt) { ["Notifications:Sms:Dlt:Templates:OTP_LOGIN"] = null };
        settings.Remove("Notifications:Sms:Dlt:Templates:OTP_LOGIN");

        var exitCode = await SmsHost("Production", settings).RunFreshOrbitAsync();

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task Host_refuses_an_unknown_provider()
    {
        var failure = await StartFailure("Testing", new() { ["Notifications:Sms:Provider"] = "SOMETHING_ELSE" });

        failure.Failures.Should().ContainSingle().Which.Should().Contain("Notifications:Sms:Provider");
    }

    [Theory]
    [InlineData("Development", "DEVELOPMENT")]
    [InlineData("Testing", "")]
    [InlineData("Demo", "DEVELOPMENT")]
    [InlineData("Production", "DLT")]
    public async Task Host_starts_with_a_valid_sms_configuration(string environment, string provider)
    {
        var settings = provider == "DLT" ? new Dictionary<string, string?>(FullDlt) : new() { ["Notifications:Sms:Provider"] = provider };
        using var host = SmsHost(environment, settings);

        await host.StartAsync(Ct);
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task Production_with_dlt_sends_the_otp_through_the_gateway_and_hides_the_dev_outbox()
    {
        var handler = new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"return":true,"request_id":"e2e1-ok"}""", System.Text.Encoding.UTF8, "application/json"),
        });
        var factory = Create("Production", FullDlt, handler);
        using var client = factory.CreateClient();
        var phone = E1ApiFactory.NewPhone();

        var otp = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone }, Ct);
        var outbox = await client.GetAsync($"/api/v1/dev/sms?phone={Uri.EscapeDataString(phone)}", Ct);

        otp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        outbox.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Uri.Should().Be("https://sms.example.in/dev/bulkV2");
        request.Authorization.Should().Be("test-key");
        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.GetProperty("numbers").GetString().Should().Be(phone[3..]);
        body.RootElement.GetProperty("template_id").GetString().Should().Be(FullDlt["Notifications:Sms:Dlt:Templates:OTP_LOGIN"]);
        var code = SixDigits().Match(body.RootElement.GetProperty("message").GetString()!).Value;
        code.Should().HaveLength(6);

        var logs = RenderedLogs(factory);
        var lastTwo = "**" + phone[^2..];
        var national = phone[3..];
        logs.Should().Contain(l => l.Contains("OTP_LOGIN", StringComparison.Ordinal) && l.Contains(lastTwo, StringComparison.Ordinal));
        logs.Should().NotContain(l => l.Contains(national, StringComparison.Ordinal));
        logs.Should().NotContain(l => l.Contains(code, StringComparison.Ordinal));
        logs.Should().NotContain(l => l.Contains("test-key", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_otp_request_answers_503_when_the_gateway_fails(bool unreachable)
    {
        var handler = unreachable
            ? new FakeHandler((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("No such host is known.")))
            : new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"return":false,"status_code":995}""", System.Text.Encoding.UTF8, "application/json"),
            });
        var factory = Create("Production", FullDlt, handler);
        using var client = factory.CreateClient();
        var phone = E1ApiFactory.NewPhone();

        var otp = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone }, Ct);

        otp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await otp.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().Should().Be("SMS_PROVIDER_UNAVAILABLE");
        RenderedLogs(factory).Should().Contain(l => l.Contains(unreachable ? "SMS_UNREACHABLE" : "SMS_REJECTED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Development_sender_keeps_the_code_in_the_outbox_and_out_of_the_logs()
    {
        var factory = Create("Testing", []);
        using var client = factory.CreateClient();
        var phone = E1ApiFactory.NewPhone();

        var otp = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone }, Ct);
        var outbox = await client.GetAsync($"/api/v1/dev/sms?phone={Uri.EscapeDataString(phone)}", Ct);

        otp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        outbox.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await outbox.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var code = SixDigits().Match(messages[0].GetProperty("text").GetString()!).Value;
        code.Should().HaveLength(6);

        var logs = RenderedLogs(factory);
        var masked = "***" + phone[^4..];
        var digits = phone[1..];
        logs.Should().Contain(l => l.Contains(masked, StringComparison.Ordinal), "the send is still logged, masked");
        logs.Should().NotContain(l => l.Contains(digits, StringComparison.Ordinal));
        logs.Should().NotContain(l => l.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dev_outbox_accepts_an_unencoded_plus()
    {
        var factory = Create("Testing", []);
        using var client = factory.CreateClient();
        var phone = E1ApiFactory.NewPhone();
        await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone }, Ct);

        var outbox = await client.GetAsync($"/api/v1/dev/sms?phone={phone}", Ct);

        (await outbox.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetArrayLength().Should().Be(1);
    }

    private static IHost SmsHost(string environment, Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment, DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddFreshOrbitSms();
        return builder.Build();
    }

    private static async Task<OptionsValidationException> StartFailure(string environment, Dictionary<string, string?> settings)
    {
        using var host = SmsHost(environment, settings);

        var start = () => host.StartAsync(Ct);

        var failure = (await start.Should().ThrowAsync<OptionsValidationException>()).Which;
        failure.OptionsType.Should().Be<SmsOptions>();
        return failure;
    }

    private static Dictionary<string, string?> FullDltSettings()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Notifications:Sms:Provider"] = "DLT",
            ["Notifications:Sms:Dlt:Gateway"] = Fast2SmsGateway.GatewayName,
            ["Notifications:Sms:Dlt:BaseUrl"] = "https://sms.example.in",
            ["Notifications:Sms:Dlt:ApiKey"] = "test-key",
            ["Notifications:Sms:Dlt:SenderId"] = "FRSHOR",
            ["Notifications:Sms:Dlt:PrincipalEntityId"] = "1101000000000000001",
        };
        foreach (var code in DltTemplateCodes.All)
        {
            settings[$"Notifications:Sms:Dlt:Templates:{code}"] = SmsSenderUnitTests.TemplateId(code);
        }

        return settings;
    }

    private static List<string> RenderedLogs(SmsHostFactory factory)
        => [.. factory.Logs.Select(e => e.RenderMessage(System.Globalization.CultureInfo.InvariantCulture)
            + " " + string.Join(" ", e.Properties.Select(p => p.Value.ToString())))];

    private SmsHostFactory Create(string environment, Dictionary<string, string?> settings, FakeHandler? handler = null)
    {
        var factory = new SmsHostFactory(database, environment, settings, handler);
        _factories.Add(factory);
        return factory;
    }

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}

/// <summary>The real Api in a chosen environment with the module's own SMS sender and every log event captured.</summary>
public sealed class SmsHostFactory(E1Database database, string environment, IReadOnlyDictionary<string, string?> settings, HttpMessageHandler? smsHandler = null) : ApiFactory(database), ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _logs = new();

    public IReadOnlyCollection<LogEvent> Logs => _logs;

    public void Emit(LogEvent logEvent) => _logs.Enqueue(logEvent);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment(environment);
        builder.UseSetting("Serilog:MinimumLevel:Default", "Information");

        // Production refuses to start without a media root (Phase 3 file storage); these tests are about SMS only.
        builder.UseSetting("FileStorage:RootPath", Path.Combine(Path.GetTempPath(), "freshorbit-sms-host-tests"));
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<ILogEventSink>(this);
        if (smsHandler is not null)
        {
            // The real Fast2SMS adapter, with a fake transport: no network.
            services.AddHttpClient(Fast2SmsGateway.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => smsHandler);
        }
    }
}
