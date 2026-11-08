using System.Diagnostics.Metrics;
using AwesomeAssertions;
using FreshOrbit.Hosting;
using FreshOrbit.Hosting.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace FreshOrbit.ArchitectureTests;

/// <summary>
/// BE-8 / D332 / D333: OpenTelemetry is registered only when <c>Telemetry:OtlpEndpoint</c> is set, an invalid endpoint
/// refuses start-up, and the application's own instruments are published on the <c>FreshOrbit</c> meter.
/// </summary>
public sealed class TelemetrySetupTests
{
    private const string JobId = "identity.purge-retention";

    // A local address that refuses at once, so the exporters' flush on dispose never waits on DNS.
    private const string Endpoint = "http://127.0.0.1:4318";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Without_an_endpoint_no_OpenTelemetry_provider_is_registered()
    {
        using var provider = Build(endpoint: null).BuildServiceProvider();

        provider.GetService<TracerProvider>().Should().BeNull();
        provider.GetService<MeterProvider>().Should().BeNull();
        provider.GetRequiredService<FreshOrbitMetrics>().Should().NotBeNull();
    }

    [Fact]
    public void With_an_endpoint_traces_and_metrics_providers_are_registered()
    {
        using var provider = Build(Endpoint).BuildServiceProvider();

        provider.GetService<TracerProvider>().Should().NotBeNull();
        provider.GetService<MeterProvider>().Should().NotBeNull();
    }

    [Theory]
    [InlineData("alloy:4318")]
    [InlineData("ftp://alloy:4318")]
    [InlineData("not a url")]
    public async Task An_invalid_endpoint_refuses_start_up(string endpoint)
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Production", DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [TelemetrySetup.OtlpEndpointKey] = endpoint });
        builder.Services.AddFreshOrbitTelemetry(builder.Configuration, "fresh-orbit-test", web: false);
        using var host = builder.Build();

        var start = () => host.StartAsync(Ct);

        var failure = (await start.Should().ThrowAsync<OptionsValidationException>()).Which;
        failure.OptionsType.Should().Be<TelemetryOptions>();
        failure.Failures.Should().ContainSingle().Which.Should().Contain(TelemetrySetup.OtlpEndpointKey);
    }

    [Theory]
    [InlineData("http://alloy:4318", "http://alloy:4318/")]
    [InlineData("https://otel.example.test/otlp", "https://otel.example.test/otlp/")]
    [InlineData("https://otel.example.test/otlp/", "https://otel.example.test/otlp/")]
    public void Endpoint_is_normalised_so_signal_paths_append(string value, string expected)
    {
        TelemetryOptions.TryParseEndpoint(value, out var endpoint).Should().BeTrue();

        endpoint!.AbsoluteUri.Should().Be(expected);
        new Uri(endpoint, "v1/traces").AbsoluteUri.Should().Be(expected + "v1/traces");
    }

    [Fact]
    public void The_outbox_and_job_instruments_are_published_and_report_values()
    {
        using var provider = Build(endpoint: null).BuildServiceProvider();
        var metrics = provider.GetRequiredService<FreshOrbitMetrics>();
        var measurements = new List<(string Name, double Value, object? JobId)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == FreshOrbitMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => measurements.Add((instrument.Name, value, JobIdOf(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => measurements.Add((instrument.Name, value, JobIdOf(tags))));
        listener.Start();

        metrics.SetOutboxSnapshot(pending: 7, oldestPendingAge: TimeSpan.FromSeconds(42), deadLettersLastDay: 2);
        metrics.RecordJobRun(JobId, TimeSpan.FromMilliseconds(250), failed: true);
        listener.RecordObservableInstruments();

        measurements.Should().Contain(m => m.Name == "freshorbit.outbox.pending" && m.Value == 7);
        measurements.Should().Contain(m => m.Name == "freshorbit.outbox.oldest_pending_age" && m.Value == 42);
        measurements.Should().Contain(m => m.Name == "freshorbit.outbox.dead_letters_24h" && m.Value == 2);
        measurements.Should().Contain(m => m.Name == "freshorbit.jobs.runs" && m.Value == 1 && Equals(m.JobId, JobId));
        measurements.Should().Contain(m => m.Name == "freshorbit.jobs.failures" && m.Value == 1 && Equals(m.JobId, JobId));
        measurements.Should().Contain(m => m.Name == "freshorbit.jobs.duration" && m.Value > 0 && Equals(m.JobId, JobId));
    }

    [Fact]
    public void The_outbox_sampler_runs_only_with_telemetry_on()
    {
        HostedServiceNames(JobMetrics(endpoint: null)).Should().NotContain("OutboxMetricsSampler");
        HostedServiceNames(JobMetrics(Endpoint)).Should().Contain("OutboxMetricsSampler");
    }

    private static ServiceCollection Build(string? endpoint)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFreshOrbitTelemetry(Configuration(endpoint), "fresh-orbit-test", web: true);
        return services;
    }

    private static ServiceCollection JobMetrics(string? endpoint)
    {
        var services = new ServiceCollection();
        services.AddFreshOrbitJobMetrics(Configuration(endpoint));
        return services;
    }

    private static IConfiguration Configuration(string? endpoint)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [TelemetrySetup.OtlpEndpointKey] = endpoint }).Build();

    private static List<string> HostedServiceNames(IServiceCollection services)
        => [.. services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType is not null).Select(d => d.ImplementationType!.Name)];

    private static object? JobIdOf(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == "job.id")
            {
                return tag.Value;
            }
        }

        return null;
    }
}
