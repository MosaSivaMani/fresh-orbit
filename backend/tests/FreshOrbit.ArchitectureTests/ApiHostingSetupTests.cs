using AwesomeAssertions;
using FreshOrbit.Hosting;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Web.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FreshOrbit.ArchitectureTests;

/// <summary>
/// The Api's <c>Hosting:RunBackgroundJobsInApi</c> switch: refused outside the Demo environment, and in Demo it adds
/// the demo database guard first, the Worker's background jobs and the request-or-system actor. Production and
/// Staging register none of it.
/// </summary>
public sealed class ApiHostingSetupTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Development")]
    public async Task Background_jobs_in_the_api_are_refused_outside_demo(string environment)
    {
        using var host = BuildHost(environment, runBackgroundJobs: true);

        var start = () => host.StartAsync(Ct);

        var failure = (await start.Should().ThrowAsync<OptionsValidationException>()).Which;
        failure.OptionsType.Should().Be<ApiHostingOptions>();
        failure.Failures.Should().ContainSingle().Which.Should().Contain(ApiHostingOptions.RunBackgroundJobsInApiKey);
    }

    [Fact]
    public async Task Refused_switch_exits_with_code_one()
    {
        var exitCode = await BuildHost("Production", runBackgroundJobs: true).RunFreshOrbitAsync();

        exitCode.Should().Be(1);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Production_and_staging_add_nothing_and_start(string environment)
    {
        var builder = Builder(environment, runBackgroundJobs: false);
        var before = builder.Services.Count;

        builder.Services.AddFreshOrbitApiHosting(builder.Configuration, builder.Environment);

        // Only the Hosting options (options, validation, validator); no hosted service, no actor change.
        builder.Services.Should().NotContain(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType != null && d.ImplementationType.Namespace!.StartsWith("FreshOrbit", StringComparison.Ordinal));
        builder.Services.Should().NotContain(d => d.ServiceType == typeof(ICurrentActor));
        builder.Services.Count.Should().BeGreaterThan(before);
        using var host = builder.Build();
        await host.StartAsync(Ct);
        await host.StopAsync(Ct);
    }

    [Fact]
    public void Demo_without_the_switch_adds_only_the_database_guard()
    {
        var builder = Builder(DemoSetup.EnvironmentName, runBackgroundJobs: false);

        builder.Services.AddFreshOrbitApiHosting(builder.Configuration, builder.Environment);

        HostedServiceNames(builder.Services).Should().Equal("DemoDatabaseGuard");
        builder.Services.Should().NotContain(d => d.ServiceType == typeof(ICurrentActor));
    }

    [Fact]
    public void Demo_with_the_switch_adds_the_guard_first_then_the_workers_jobs_and_the_system_actor_outside_requests()
    {
        var builder = Builder(DemoSetup.EnvironmentName, runBackgroundJobs: true);
        builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();

        builder.Services.AddFreshOrbitApiHosting(builder.Configuration, builder.Environment);

        var hosted = HostedServiceNames(builder.Services);
        hosted[0].Should().Be("DemoDatabaseGuard");
        hosted.Should().Contain("RecurringJobScheduler").And.Contain("OutboxDispatcherService");
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(ICurrentActor))
            .Which.ImplementationType!.Name.Should().Be("RequestOrSystemActor");
    }

    [Fact]
    public void Demo_environment_name_is_demo()
    {
        DemoSetup.EnvironmentName.Should().Be("Demo");
        new HostApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Demo", DisableDefaults = true }).Environment.IsDemo().Should().BeTrue();
        new HostApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Production", DisableDefaults = true }).Environment.IsDemo().Should().BeFalse();
    }

    private static List<string> HostedServiceNames(IServiceCollection services)
        => [.. services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Namespace?.StartsWith("FreshOrbit", StringComparison.Ordinal) == true)
            .Select(d => d.ImplementationType!.Name)];

    private static HostApplicationBuilder Builder(string environment, bool runBackgroundJobs)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment, DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ApiHostingOptions.RunBackgroundJobsInApiKey] = runBackgroundJobs ? "true" : "false",
        });
        builder.Services.AddLogging();
        return builder;
    }

    private static IHost BuildHost(string environment, bool runBackgroundJobs)
    {
        var builder = Builder(environment, runBackgroundJobs);
        builder.Services.AddFreshOrbitApiHosting(builder.Configuration, builder.Environment);
        return builder.Build();
    }
}
