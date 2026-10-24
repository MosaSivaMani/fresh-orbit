using AwesomeAssertions;
using FreshOrbit.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FreshOrbit.ArchitectureTests;

/// <summary>E2E-2: the Api and Worker hosts exit with code 1, not a crash, when start-up validation refuses configuration.</summary>
public sealed class HostRunnerTests
{
    private sealed class SampleOptions
    {
        public string? Value { get; set; }
    }

    [Fact]
    public async Task Refused_configuration_exits_with_code_one()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddOptions<SampleOptions>().Validate(o => o.Value is not null, "Value is required.").ValidateOnStart();

        var exitCode = await builder.Build().RunFreshOrbitAsync();

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task Clean_shutdown_exits_with_code_zero()
    {
        var host = Host.CreateApplicationBuilder().Build();
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(lifetime.StopApplication);

        var exitCode = await host.RunFreshOrbitAsync();

        exitCode.Should().Be(0);
    }
}
