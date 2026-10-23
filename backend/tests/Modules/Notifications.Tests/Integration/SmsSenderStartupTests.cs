using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using FreshOrbit.E1Testing;
using FreshOrbit.Notifications.Infrastructure.Senders;
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
    private static readonly Dictionary<string, string?> FullDlt = new()
    {
        ["Notifications:Sms:Provider"] = "DLT",
        ["Notifications:Sms:Dlt:BaseUrl"] = "https://sms.example.in/api",
        ["Notifications:Sms:Dlt:ApiKey"] = "test-key",
        ["Notifications:Sms:Dlt:SenderId"] = "FRSHOR",
        ["Notifications:Sms:Dlt:PrincipalEntityId"] = "1101000000000000001",
    };

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
            .Contain("Notifications:Sms:Dlt:BaseUrl").And.Contain("Notifications:Sms:Dlt:SenderId")
            .And.Contain("Notifications:Sms:Dlt:PrincipalEntityId").And.NotContain("Notifications:Sms:Dlt:ApiKey");
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
    [InlineData("Production", "DLT")]
    public async Task Host_starts_with_a_valid_sms_configuration(string environment, string provider)
    {
        var settings = provider == "DLT" ? new Dictionary<string, string?>(FullDlt) : new() { ["Notifications:Sms:Provider"] = provider };
        using var host = SmsHost(environment, settings);

        await host.StartAsync(Ct);
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task Production_with_dlt_starts_fails_loudly_and_hides_the_dev_outbox()
    {
        var factory = Create("Production", FullDlt);
        using var client = factory.CreateClient();
        var phone = E1ApiFactory.NewPhone();

        var otp = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone }, Ct);
        var outbox = await client.GetAsync($"/api/v1/dev/sms?phone={Uri.EscapeDataString(phone)}", Ct);

        // The stub cannot deliver, so the request fails instead of pretending the code was sent.
        otp.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await otp.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().Should().Be("INTERNAL_ERROR");
        outbox.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

        var logs = factory.Logs.Select(e => e.RenderMessage(System.Globalization.CultureInfo.InvariantCulture)
            + " " + string.Join(" ", e.Properties.Select(p => p.Value.ToString()))).ToList();
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

    private SmsHostFactory Create(string environment, Dictionary<string, string?> settings)
    {
        var factory = new SmsHostFactory(database, environment, settings);
        _factories.Add(factory);
        return factory;
    }

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}

/// <summary>The real Api in a chosen environment with the module's own SMS sender and every log event captured.</summary>
public sealed class SmsHostFactory(E1Database database, string environment, IReadOnlyDictionary<string, string?> settings) : ApiFactory(database), ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _logs = new();

    public IReadOnlyCollection<LogEvent> Logs => _logs;

    public void Emit(LogEvent logEvent) => _logs.Enqueue(logEvent);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment(environment);
        builder.UseSetting("Serilog:MinimumLevel:Default", "Information");
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }

    protected override void ConfigureTestServices(IServiceCollection services) => services.AddSingleton<ILogEventSink>(this);
}
