using System.Globalization;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Hosting;
using FreshOrbit.Migrations;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.SharedKernel.Web.Modules;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

[assembly: AssemblyFixture(typeof(FreshOrbit.SampleData.Tests.SampleDatabase))]

namespace FreshOrbit.SampleData.Tests;

/// <summary>An empty database for the sample-data run (one per assembly).</summary>
public sealed class SampleDatabase : PostgresFixture;

/// <summary>
/// The whole sample set through the real loaders (wired like the Migrations host): counts per module, idempotence,
/// the storefront serving priced in-stock products for a sample pincode, the D55 purge refusal and the purge.
/// </summary>
public sealed class SampleDataTests(SampleDatabase database) : IAsyncDisposable
{
    private const string SamplePincode = "530017";

    /// <summary>Rows each sample table holds after one load into an empty database (docs/product/sample-data.md §1).</summary>
    private static readonly Dictionary<string, int> Expected = new(StringComparer.Ordinal)
    {
        ["settings.business_profile"] = 1,
        ["locations.cities"] = 1,
        ["locations.fulfillment_locations"] = 1,
        ["locations.location_closures"] = 1,
        ["locations.service_areas"] = 3,
        ["locations.pincodes"] = 20,
        ["identity.users"] = 5,
        ["identity.staff_members"] = 2,
        ["customers.customers"] = 3,
        ["customers.addresses"] = 5,
        ["catalog.categories"] = 8,
        ["catalog.products"] = 26,
        ["catalog.product_variants"] = 28,
        ["catalog.product_images"] = 26,
        ["pricing.tax_rules"] = 4,
        ["pricing.variant_prices"] = 28,
        ["pricing.preparation_charges"] = 67,
        ["inventory.stock_items"] = 28,
        ["inventory.batches"] = 29,
        ["inventory.stock_movements"] = 29,
        ["delivery.slot_templates"] = 21,
        ["delivery.riders"] = 3,
        ["promotions.coupons"] = 2,
        ["promotions.promotions"] = 2,
    };

    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), "freshorbit-sample-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sample_set_loads_once_serves_the_storefront_and_purges()
    {
        await using var services = BuildServices();
        await RunReferenceLoadersAsync(services);

        await LoadSampleAsync(services);

        var counts = await CountAsync(Expected.Keys);
        counts.Should().BeEquivalentTo(Expected);
        var tracked = await TrackedCountsAsync();
        foreach (var (table, count) in Expected)
        {
            tracked.GetValueOrDefault(table).Should().Be(count, $"every {table} row is a tracked sample row");
        }

        (await ScalarAsync("SELECT count(*) FROM inventory.cold_chain_logs")).Should().BeGreaterThanOrEqualTo(2);
        tracked.GetValueOrDefault("inventory.cold_chain_logs").Should().Be((int)await ScalarAsync("SELECT count(*) FROM inventory.cold_chain_logs"));
        (await ScalarAsync("SELECT count(*) FROM locations.cities WHERE status = 'ACTIVE'")).Should().Be(1);
        (await ScalarAsync("SELECT count(*) FROM catalog.products WHERE status = 'ACTIVE'")).Should().Be(26);
        (await ScalarAsync("SELECT count(*) FROM catalog.product_variants WHERE serves IS NOT NULL")).Should().Be(28, "every sample variant states its serving facts (CAT-R21)");
        (await ScalarAsync("SELECT count(*) FROM catalog.product_variants WHERE pieces_min IS NOT NULL")).Should().Be(21);
        (await ScalarAsync("SELECT count(DISTINCT slot_date) FROM delivery.delivery_slots")).Should().Be(7, "slots cover the next 7 days");
        (await ScalarAsync("SELECT count(*) FROM inventory.stock_items WHERE on_hand > 0")).Should().Be(28, "on-hand is the fold of the RECEIPT movements");
        File.Exists(Path.Combine(_mediaRoot, "sample", "banners", "prawn-week.webp")).Should().BeTrue();
        (await ScalarAsync("SELECT count(*) FROM promotions.promotions WHERE link_type = 'CATEGORY' AND link_target = (SELECT id::text FROM catalog.categories WHERE slug = 'prawns-shrimp')")).Should().Be(1);

        // Idempotent: a second run inserts nothing.
        var recordsBefore = await ScalarAsync("SELECT count(*) FROM platform.sample_records");
        var slotsBefore = await ScalarAsync("SELECT count(*) FROM delivery.delivery_slots");
        await LoadSampleAsync(services);
        (await CountAsync(Expected.Keys)).Should().BeEquivalentTo(Expected);
        (await ScalarAsync("SELECT count(*) FROM platform.sample_records")).Should().Be(recordsBefore);
        (await ScalarAsync("SELECT count(*) FROM delivery.delivery_slots")).Should().Be(slotsBefore);

        await AssertStorefrontAsync();

        // D55: a booked slot of a sample template blocks the purge and nothing is deleted.
        await ExecuteAsync("UPDATE delivery.delivery_slots SET booked_count = 1 WHERE id = (SELECT id FROM delivery.delivery_slots ORDER BY start_at DESC LIMIT 1)");
        var refused = async () => await PurgeAsync(services);
        (await refused.Should().ThrowAsync<SamplePurgeRefusedException>()).Which.Blockers.Should().ContainSingle(b => b.StartsWith("delivery.delivery_slots", StringComparison.Ordinal));
        (await ScalarAsync("SELECT count(*) FROM platform.sample_records")).Should().Be(recordsBefore);
        await ExecuteAsync("UPDATE delivery.delivery_slots SET booked_count = 0");

        await PurgeAsync(services);
        (await CountAsync(Expected.Keys)).Values.Should().AllSatisfy(c => c.Should().Be(0));
        (await ScalarAsync("SELECT count(*) FROM platform.sample_records")).Should().Be(0);
        (await ScalarAsync("SELECT count(*) FROM delivery.delivery_slots")).Should().Be(0);
        (await ScalarAsync("SELECT count(*) FROM inventory.cold_chain_logs")).Should().Be(0);
    }

    [Fact]
    public async Task Sample_and_purge_are_refused_in_production_before_the_database_is_touched()
    {
        // An empty provider: any database access would throw, so a clean refusal proves nothing ran.
        await using var services = new ServiceCollection().BuildServiceProvider();
        var environment = new HostingEnvironment { EnvironmentName = Environments.Production, ApplicationName = "FreshOrbit.Migrations", ContentRootPath = _mediaRoot, ContentRootFileProvider = new NullFileProvider() };
        var runner = new MigrationRunner(services.GetRequiredService<IServiceScopeFactory>(), new ConfigurationBuilder().Build(), environment, NullLogger<MigrationRunner>.Instance);

        (await runner.RunAsync(new MigrationCommand(LoadSample: true, PurgeSample: false, CreateAdmin: null), Ct)).Should().Be(2);
        (await runner.RunAsync(new MigrationCommand(LoadSample: false, PurgeSample: true, CreateAdmin: null), Ct)).Should().Be(2);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_mediaRoot))
        {
            Directory.Delete(_mediaRoot, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    private static string DataPath(string kind)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "data", kind);
            if (File.Exists(Path.Combine(candidate, "sample-set.json")) || Directory.Exists(Path.Combine(candidate, "catalog")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"backend/data/{kind} was not found above the test output directory.");
    }

    /// <summary>
    /// The load time: 00:00 UTC of today, i.e. 05:30 in the sample city (UTC+05:30). DEL-R9 skips a slot whose cut-off
    /// has passed, so with the real clock today has no slot after 15:00 local (the last slot's cut-off) and only six
    /// dates get slots. At 05:30 the 11:00 slot (cut-off 09:00) is still open, so all seven horizon dates have slots.
    /// It is at most a day before the real time, well inside the 30 h best-before of the sample batches, so the
    /// storefront (real clock) still sees them in stock.
    /// </summary>
    private static DateTimeOffset LoadTime() => new(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);

    /// <summary>Services wired like the Migrations host (system actor, a fixed clock, every module, file storage in a temp folder).</summary>
    private ServiceProvider BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{FreshOrbitDbContextOptions.ConnectionStringName}"] = database.ConnectionString,
                ["FileStorage:RootPath"] = _mediaRoot,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = Environments.Development, ApplicationName = "FreshOrbit.Migrations", ContentRootPath = _mediaRoot, ContentRootFileProvider = new NullFileProvider() });
        services.AddSingleton<ICurrentActor>(SystemActor.Instance);
        services.AddSingleton<IClock>(new TestClock(LoadTime()));
        services.AddFreshOrbitPersistence();
        services.AddFreshOrbitModules(configuration, Modules.All);

        return services.BuildServiceProvider();
    }

    private static async Task RunReferenceLoadersAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        foreach (var loader in scope.ServiceProvider.GetServices<IReferenceDataLoader>().OrderBy(l => l.Order))
        {
            await loader.LoadAsync(new ReferenceDataContext(DataPath("reference")), Ct);
        }
    }

    private static async Task LoadSampleAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var context = new SampleDataContext(DataPath("sample"), scope.ServiceProvider.GetRequiredService<ISampleRecordTracker>(), scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        foreach (var loader in scope.ServiceProvider.GetServices<ISampleDataLoader>().OrderBy(l => l.Order))
        {
            await loader.LoadAsync(context, Ct);
        }
    }

    private static async Task PurgeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SampleDataPurger>().PurgeAsync(Ct);
    }

    private async Task AssertStorefrontAsync()
    {
        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();
        using var response = await client.GetAsync($"/api/v1/products?pincode={SamplePincode}&pageSize=50", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.IsSuccessStatusCode.Should().BeTrue(body);
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("totalCount").GetInt32().Should().Be(26);
        foreach (var item in json.GetProperty("items").EnumerateArray())
        {
            item.GetProperty("fromPrice").ValueKind.Should().Be(JsonValueKind.Object, $"{item.GetProperty("slug")} is priced");
            item.GetProperty("fromPrice").GetProperty("unitPricePaise").GetInt64().Should().BePositive();
            item.GetProperty("availability").GetProperty("status").GetString().Should().BeOneOf("IN_STOCK", "LOW_STOCK");
            item.GetProperty("imageUrl").GetString().Should().NotBeNullOrEmpty();
        }
    }

    private async Task<Dictionary<string, int>> CountAsync(IEnumerable<string> tables)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            counts[table] = (int)await ScalarAsync($"SELECT count(*) FROM {table}");
        }

        return counts;
    }

    private async Task<Dictionary<string, int>> TrackedCountsAsync()
    {
        await using var db = database.CreateDbContext();
        return await db.Set<SampleRecord>().GroupBy(r => r.TableName).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, Ct);
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var db = database.CreateDbContext();
        await db.Database.OpenConnectionAsync(Ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(Ct), CultureInfo.InvariantCulture);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var db = database.CreateDbContext();
#pragma warning disable EF1002 // Test-only SQL with no user input.
        await db.Database.ExecuteSqlRawAsync(sql, Ct);
#pragma warning restore EF1002
    }
}
