using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Hosting;
using FreshOrbit.Migrations;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web.Modules;
using FreshOrbit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace FreshOrbit.SampleData.Tests;

/// <summary>
/// The free public demo (Demo environment, docs/runbooks/render-demo.md) on real Postgres: the Migrations host claims
/// only an empty database and refuses any other before touching it; the Demo Api refuses a database the demo did not
/// claim, and on its own database runs the Worker's background jobs, the development SMS outbox and the media
/// placeholders. Testing keeps none of the demo behaviour.
/// </summary>
public sealed class DemoEnvironmentTests(SampleDatabase database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_host_claims_an_empty_database_and_runs_again_on_it()
    {
        var connectionString = await CreateDatabaseAsync();

        (await DemoDatabase.InspectAsync(connectionString, Ct)).Should().Be(DemoDatabaseState.Empty);
        (await RunMigrationsAsync(connectionString, DemoSetup.EnvironmentName)).Should().Be(0);
        (await RunMigrationsAsync(connectionString, DemoSetup.EnvironmentName)).Should().Be(0);

        (await DemoDatabase.InspectAsync(connectionString, Ct)).Should().Be(DemoDatabaseState.Demo);
        (await ScalarAsync(connectionString, "SELECT count(*) FROM platform.__ef_migrations_history")).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Migrations_host_refuses_a_database_with_tables_and_no_marker_before_touching_it()
    {
        var connectionString = await CreateDatabaseAsync("CREATE TABLE public.real_customers (id integer)");

        var exitCode = await RunMigrationsAsync(connectionString, DemoSetup.EnvironmentName);

        exitCode.Should().Be(5);
        (await DemoDatabase.InspectAsync(connectionString, Ct)).Should().Be(DemoDatabaseState.NotDemo);
        (await ScalarAsync(connectionString, "SELECT count(*) FROM pg_catalog.pg_namespace WHERE nspname = 'platform'")).Should().Be(0, "nothing is migrated");
    }

    [Fact]
    public async Task Production_migrations_never_mark_the_database()
    {
        var connectionString = await CreateDatabaseAsync();

        (await RunMigrationsAsync(connectionString, Environments.Production)).Should().Be(0);

        (await DemoDatabase.InspectAsync(connectionString, Ct)).Should().Be(DemoDatabaseState.NotDemo);
    }

    [Fact]
    public async Task Demo_api_refuses_to_start_on_a_database_the_demo_did_not_claim()
    {
        // The fixture's database: every module's tables, no demo marker (like a production database).
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = DemoSetup.EnvironmentName, DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{FreshOrbitDbContextOptions.ConnectionStringName}"] = database.ConnectionString,
        });
        builder.Services.AddLogging();
        builder.Services.AddFreshOrbitApiHosting(builder.Configuration, builder.Environment);

        var exitCode = await builder.Build().RunFreshOrbitAsync();

        exitCode.Should().Be(1);
        (await DemoDatabase.InspectAsync(database.ConnectionString, Ct)).Should().Be(DemoDatabaseState.NotDemo);
    }

    [Fact]
    public async Task Demo_api_runs_background_jobs_dev_sms_and_media_placeholders_on_its_own_database()
    {
        var connectionString = await CreateDatabaseAsync();
        (await RunMigrationsAsync(connectionString, DemoSetup.EnvironmentName)).Should().Be(0);
        await using var factory = new DemoApiFactory(database, connectionString);
        using var client = factory.CreateClient();

        (await client.GetAsync("/health", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        // The Worker's background work runs inside the Api; background scopes act as the system actor.
        var hosted = factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Name).ToList();
        hosted.Should().Contain("DemoDatabaseGuard").And.Contain("RecurringJobScheduler").And.Contain("OutboxDispatcherService");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ICurrentActor>().ActorType.Should().Be(ActorType.System);
        }

        // OTPs go to the development outbox, readable by testers.
        var phone = "+919" + Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        (await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone }, Ct)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var outbox = await client.GetAsync($"/api/v1/dev/sms?phone={Uri.EscapeDataString(phone)}", Ct);
        outbox.StatusCode.Should().Be(HttpStatusCode.OK);
        (await outbox.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetArrayLength().Should().Be(1);

        // An image whose file is gone (restart on the free host) is drawn again in the requested format.
        var image = await client.GetAsync("/media/products/0199a1b2-0000-7000-8000-000000000001/0199a1b2-0000-7000-8000-000000000002/640.webp", Ct);
        image.StatusCode.Should().Be(HttpStatusCode.OK);
        image.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        var bytes = await image.Content.ReadAsByteArrayAsync(Ct);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("RIFF");
        var banner = await client.GetAsync("/media/sample/banners/gone.png", Ct);
        banner.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await client.GetAsync("/media/products/x/original", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound, "only image extensions are drawn");
    }

    [Fact]
    public async Task Testing_api_has_no_demo_media_placeholders()
    {
        await using var factory = new ApiFactory(database);
        using var client = factory.CreateClient();

        var image = await client.GetAsync("/media/products/x/y/640.webp", Ct);

        image.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<string> CreateDatabaseAsync(string? sql = null)
    {
        var name = $"fo_demo_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync(Ct);
        }

        var connectionString = new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = name }.ConnectionString;
        if (sql is not null)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(Ct);
        }

        return connectionString;
    }

    private static async Task<long> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The Migrations host's wiring (Program.cs) in the given environment, without sample data.</summary>
    private static async Task<int> RunMigrationsAsync(string connectionString, string environmentName)
    {
        var mediaRoot = Path.Combine(Path.GetTempPath(), "freshorbit-demo-tests", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{FreshOrbitDbContextOptions.ConnectionStringName}"] = connectionString,
                ["FileStorage:RootPath"] = mediaRoot,
            })
            .Build();
        var environment = new HostingEnvironment { EnvironmentName = environmentName, ApplicationName = "FreshOrbit.Migrations", ContentRootPath = mediaRoot, ContentRootFileProvider = new NullFileProvider() };
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton<ICurrentActor>(SystemActor.Instance);
        services.AddFreshOrbitPersistence();
        services.AddFreshOrbitModules(configuration, Modules.All);
        await using var provider = services.BuildServiceProvider();
        try
        {
            var runner = new MigrationRunner(provider.GetRequiredService<IServiceScopeFactory>(), configuration, environment, NullLogger<MigrationRunner>.Instance);
            return await runner.RunAsync(new MigrationCommand(LoadSample: false, PurgeSample: false, CreateAdmin: null), Ct);
        }
        finally
        {
            if (Directory.Exists(mediaRoot))
            {
                Directory.Delete(mediaRoot, recursive: true);
            }
        }
    }

    /// <summary>The real Api in the Demo environment on the given (claimed) database, background jobs on.</summary>
    private sealed class DemoApiFactory(SampleDatabase database, string connectionString) : ApiFactory(database)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment(DemoSetup.EnvironmentName);
            builder.UseSetting($"ConnectionStrings:{FreshOrbitDbContextOptions.ConnectionStringName}", connectionString);
            builder.UseSetting(ApiHostingOptions.RunBackgroundJobsInApiKey, "true");
            builder.UseSetting(BackgroundJobsSetup.WorkerCountKey, "1");
            builder.UseSetting("FileStorage:RootPath", Path.Combine(Path.GetTempPath(), "freshorbit-demo-api-tests", Guid.NewGuid().ToString("N")));
        }
    }
}
