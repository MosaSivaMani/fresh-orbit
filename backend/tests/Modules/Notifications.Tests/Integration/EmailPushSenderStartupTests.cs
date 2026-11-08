using AwesomeAssertions;
using FreshOrbit.E1Testing;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Infrastructure.Senders;
using FreshOrbit.Notifications.Infrastructure.Senders.Fcm;
using FreshOrbit.Notifications.Tests.Unit;
using FreshOrbit.SharedKernel.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog.Events;

namespace FreshOrbit.Notifications.Tests.Integration;

/// <summary>
/// BE-4 / L-19 (D322) and BE-3 / L-20 (D325): outside Development, Testing and Demo the hosts refuse to start when
/// e-mail or push would go to a development sender, or when SMTP or FCM is incomplete. DISABLED is explicit and
/// logged Critical at start. The refusals name the setting, never a secret.
/// </summary>
public sealed class EmailPushSenderStartupTests(E1Database database) : IAsyncLifetime
{
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

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Host_refuses_email_without_an_smtp_host_outside_development(string environment)
    {
        var failure = await EmailStartFailure(environment, []);

        failure.Failures.Should().ContainSingle().Which.Should().Contain("Notifications:Smtp:Host");
    }

    [Fact]
    public async Task Host_refuses_the_development_email_sender_named_outside_development()
    {
        var failure = await EmailStartFailure("Production", new() { ["Notifications:Email:Provider"] = "DEVELOPMENT", ["Notifications:Smtp:Host"] = "smtp.example.test" });

        failure.Failures.Should().ContainSingle().Which.Should().Contain("Notifications:Smtp:Host");
    }

    [Fact]
    public async Task Host_refuses_smtp_without_a_sender_address()
    {
        var failure = await EmailStartFailure("Production", new() { ["Notifications:Email:Provider"] = "SMTP", ["Notifications:Smtp:Host"] = "smtp.example.test", ["Notifications:Smtp:FromAddress"] = "" });

        failure.Failures.Should().ContainSingle().Which.Should().Contain("Notifications:Smtp:FromAddress").And.NotContain("Notifications:Smtp:Host");
    }

    [Fact]
    public async Task Host_refuses_an_unknown_email_provider()
    {
        var failure = await EmailStartFailure("Testing", new() { ["Notifications:Email:Provider"] = "CARRIER_PIGEON" });

        failure.Failures.Should().ContainSingle().Which.Should().Contain("Notifications:Email:Provider");
    }

    [Theory]
    [InlineData("Development", null, null, typeof(LoggingEmailSender))]
    [InlineData("Testing", null, null, typeof(LoggingEmailSender))]
    [InlineData("Demo", null, null, typeof(LoggingEmailSender))]
    [InlineData("Development", null, "localhost", typeof(SmtpEmailSender))]
    [InlineData("Production", null, "smtp.example.test", typeof(SmtpEmailSender))]
    [InlineData("Production", "SMTP", "smtp.example.test", typeof(SmtpEmailSender))]
    [InlineData("Production", "DISABLED", null, typeof(DisabledEmailSender))]
    public async Task Host_starts_with_a_valid_email_configuration_and_uses_its_sender(string environment, string? provider, string? host, Type expected)
    {
        using var app = SenderHost(environment, new() { ["Notifications:Email:Provider"] = provider, ["Notifications:Smtp:Host"] = host });

        await app.StartAsync(Ct);
        app.Services.GetRequiredService<IEmailSender>().Should().BeOfType(expected);
        await app.StopAsync(Ct);
    }

    [Theory]
    [InlineData("Production", "")]
    [InlineData("Production", "DEVELOPMENT")]
    [InlineData("Staging", " ")]
    public async Task Host_refuses_the_development_push_sender_outside_development(string environment, string provider)
    {
        var failure = await PushStartFailure(environment, new() { ["Notifications:Push:Provider"] = provider });

        failure.Failures.Should().ContainSingle().Which.Should().Contain("Notifications:Push:Provider");
    }

    [Fact]
    public async Task Host_refuses_an_incomplete_fcm_configuration_without_echoing_the_key()
    {
        var failure = await PushStartFailure("Production", new()
        {
            ["Notifications:Push:Provider"] = "FCM",
            ["Notifications:Push:Fcm:ServiceAccountJson"] = """{"client_email":"push@example.iam.gserviceaccount.com","private_key":"not-a-secret-but-not-a-key"}""",
        });

        failure.Failures.Should().ContainSingle().Which.Should()
            .Contain("Notifications:Push:Fcm:ProjectId").And.Contain("Notifications:Push:Fcm:ServiceAccountJson")
            .And.NotContain("not-a-secret-but-not-a-key");
    }

    [Fact]
    public async Task Host_refuses_an_unknown_push_provider()
    {
        var failure = await PushStartFailure("Testing", new() { ["Notifications:Push:Provider"] = "APNS" });

        failure.Failures.Should().ContainSingle().Which.Should().Contain("Notifications:Push:Provider");
    }

    [Theory]
    [InlineData("Development", "", typeof(LoggingPushSender))]
    [InlineData("Testing", "DEVELOPMENT", typeof(LoggingPushSender))]
    [InlineData("Demo", "", typeof(LoggingPushSender))]
    [InlineData("Production", "DISABLED", typeof(DisabledPushSender))]
    [InlineData("Production", "FCM", typeof(FcmPushSender))]
    public async Task Host_starts_with_a_valid_push_configuration_and_uses_its_sender(string environment, string provider, Type expected)
    {
        var settings = new Dictionary<string, string?> { ["Notifications:Push:Provider"] = provider };
        if (provider == "FCM")
        {
            settings["Notifications:Push:Fcm:ProjectId"] = FcmPushSenderTests.ProjectId;
            settings["Notifications:Push:Fcm:ServiceAccountJson"] = FcmPushSenderTests.ServiceAccountJson();
        }

        using var app = SenderHost(environment, settings);

        await app.StartAsync(Ct);
        app.Services.GetRequiredService<IPushSender>().Should().BeOfType(expected);
        await app.StopAsync(Ct);
    }

    [Fact]
    public async Task Production_with_disabled_email_and_push_starts_and_logs_each_as_critical()
    {
        var settings = new Dictionary<string, string?>(SmsSenderStartupTests.FullDlt)
        {
            ["Notifications:Email:Provider"] = "DISABLED",
            ["Notifications:Smtp:Host"] = "",
            ["Notifications:Push:Provider"] = "DISABLED",
        };
        var factory = Create("Production", settings);

        using var client = factory.CreateClient();

        var critical = factory.Logs.Where(e => e.Level == LogEventLevel.Fatal).Select(e => e.Properties.TryGetValue("FailureCode", out var code) ? code.ToString().Trim('"') : null).ToList();
        critical.Should().Contain("EMAIL_DISABLED").And.Contain("PUSH_DISABLED");
    }

    private SmsHostFactory Create(string environment, Dictionary<string, string?> settings)
    {
        var factory = new SmsHostFactory(database, environment, settings);
        _factories.Add(factory);
        return factory;
    }

    private static IHost SenderHost(string environment, Dictionary<string, string?> settings, bool email = true, bool push = true)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment, DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton<IClock, SystemClock>();
        if (email)
        {
            builder.Services.AddFreshOrbitEmail();
        }

        if (push)
        {
            builder.Services.AddFreshOrbitPush();
        }

        return builder.Build();
    }

    private static Task<OptionsValidationException> EmailStartFailure(string environment, Dictionary<string, string?> settings)
        => StartFailure<EmailOptions>(SenderHost(environment, settings, push: false));

    private static Task<OptionsValidationException> PushStartFailure(string environment, Dictionary<string, string?> settings)
        => StartFailure<PushOptions>(SenderHost(environment, settings, email: false));

    private static async Task<OptionsValidationException> StartFailure<TOptions>(IHost host)
    {
        using (host)
        {
            var start = () => host.StartAsync(Ct);

            var failure = (await start.Should().ThrowAsync<OptionsValidationException>()).Which;
            failure.OptionsType.Should().Be<TOptions>();
            return failure;
        }
    }
}
