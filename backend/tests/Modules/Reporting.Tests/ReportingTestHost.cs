using System.Net.Http.Json;
using System.Text.Json;
using FreshOrbit.Reporting.Infrastructure.Jobs;
using FreshOrbit.Reporting.Infrastructure.Persistence;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(FreshOrbit.Reporting.Tests.ReportingDatabase))]

namespace FreshOrbit.Reporting.Tests;

/// <summary>
/// One Postgres for the assembly. Reporting reads other modules' tables, which are written by other engineers, so the
/// fixture replaces them with minimal tables holding exactly the columns documented in docs/database (the contract the
/// view SQL is written against), seeds the FO-260001 dataset (docs/product/reporting.md §6) and creates the views.
/// </summary>
public sealed class ReportingDatabase : PostgresFixture
{
    protected override IEnumerable<IModuleModel> AdditionalModuleModels => [new ReportingSourcesModel()];
}

/// <summary>Test-only model: minimal source tables, the seeded dataset, then the Reporting view SQL again.</summary>
public sealed class ReportingSourcesModel : IModuleModel
{
    public string Schema => "reporting_test";

    public IEnumerable<string> DatabaseObjectsSql => [SourceTablesSql, ReportingDataset.SeedSql, .. new ReportingModuleModel().DatabaseObjectsSql];

    public void Configure(ModelBuilder modelBuilder)
    {
    }

    private const string SourceTablesSql = """
        CREATE SCHEMA IF NOT EXISTS locations;
        CREATE SCHEMA IF NOT EXISTS catalog;
        CREATE SCHEMA IF NOT EXISTS ordering;
        CREATE SCHEMA IF NOT EXISTS inventory;
        CREATE SCHEMA IF NOT EXISTS delivery;
        CREATE SCHEMA IF NOT EXISTS payments;
        DROP TABLE IF EXISTS locations.cities, locations.fulfillment_locations, catalog.products, catalog.product_variants,
            ordering.orders, ordering.order_items, ordering.complaints, inventory.stock_items, inventory.stock_movements,
            delivery.delivery_slots, payments.refunds, payments.reconciliation_issues CASCADE;
        CREATE TABLE locations.cities (id uuid PRIMARY KEY, code text, name text, time_zone text NOT NULL);
        CREATE TABLE locations.fulfillment_locations (id uuid PRIMARY KEY, city_id uuid NOT NULL, code text, name text);
        CREATE TABLE catalog.products (id uuid PRIMARY KEY, category_id uuid, name text NOT NULL);
        CREATE TABLE catalog.product_variants (id uuid PRIMARY KEY, product_id uuid NOT NULL, sku text NOT NULL, name text NOT NULL, stock_unit text NOT NULL);
        CREATE TABLE ordering.orders (
            id uuid PRIMARY KEY, order_number text, customer_id uuid NOT NULL, city_id uuid, fulfillment_location_id uuid NOT NULL,
            status text NOT NULL, payment_method text, slot_start_at timestamptz,
            items_paise bigint NOT NULL DEFAULT 0, preparation_paise bigint NOT NULL DEFAULT 0, discount_paise bigint NOT NULL DEFAULT 0,
            delivery_fee_paise bigint NOT NULL DEFAULT 0, tax_paise bigint NOT NULL DEFAULT 0, total_paise bigint NOT NULL, final_total_paise bigint,
            placed_at timestamptz, delivered_at timestamptz, cancelled_at timestamptz,
            created_at timestamptz NOT NULL DEFAULT clock_timestamp(), modified_at timestamptz);
        CREATE TABLE ordering.order_items (
            id uuid PRIMARY KEY, order_id uuid NOT NULL, line_no integer, product_id uuid NOT NULL, variant_id uuid NOT NULL,
            product_name text NOT NULL, variant_name text NOT NULL, sku text NOT NULL, preparation_code text,
            ordered_quantity integer NOT NULL, max_chargeable_quantity integer, packed_quantity integer, final_total_paise bigint,
            status text NOT NULL DEFAULT 'ACTIVE');
        CREATE TABLE ordering.complaints (id uuid PRIMARY KEY, order_id uuid NOT NULL, status text NOT NULL);
        CREATE TABLE inventory.stock_items (
            id uuid PRIMARY KEY, fulfillment_location_id uuid NOT NULL, variant_id uuid NOT NULL,
            on_hand integer NOT NULL, reserved integer NOT NULL DEFAULT 0, low_stock_threshold integer NOT NULL DEFAULT 0);
        CREATE TABLE inventory.stock_movements (
            id uuid PRIMARY KEY, stock_item_id uuid NOT NULL, batch_id uuid, kind text NOT NULL, quantity integer NOT NULL,
            cost_paise bigint NOT NULL, reason_code text, reference_type text, reference_id uuid, occurred_at timestamptz NOT NULL,
            created_at timestamptz NOT NULL DEFAULT clock_timestamp());
        CREATE TABLE delivery.delivery_slots (
            id uuid PRIMARY KEY, fulfillment_location_id uuid NOT NULL, slot_date date NOT NULL, start_at timestamptz NOT NULL,
            end_at timestamptz NOT NULL, capacity integer NOT NULL, booked_count integer NOT NULL DEFAULT 0, status text NOT NULL DEFAULT 'OPEN');
        CREATE TABLE payments.refunds (
            id uuid PRIMARY KEY, payment_id uuid, order_id uuid NOT NULL, fulfillment_location_id uuid NOT NULL, amount_paise bigint NOT NULL,
            reason_code text NOT NULL, method text NOT NULL DEFAULT 'ORIGINAL', status text NOT NULL, succeeded_at timestamptz,
            created_at timestamptz NOT NULL DEFAULT clock_timestamp(), modified_at timestamptz);
        CREATE TABLE payments.reconciliation_issues (id uuid PRIMARY KEY, kind text NOT NULL, status text NOT NULL, fulfillment_location_id uuid);
        """;
}

/// <summary>Fresh Api per test plus helpers to run the refresh job and read reports.</summary>
public abstract class ReportingTestBase(ReportingDatabase database) : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = new(database);

    protected ReportingDatabase Database => database;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected HttpClient Reports(IReadOnlyCollection<Guid>? locations = null, bool all = false)
        => Factory.CreateStaffClient(["reports.view"], locations, all);

    protected async Task<RefreshResult> RefreshAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RefreshReportsJob>().RunAsync(Ct);
    }

    protected async Task ExecuteAsync(string sql)
    {
        await using var db = Database.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(sql, Ct);
    }

    protected static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GET {url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        }

        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Clone();
    }

    protected static async Task<string?> CodeAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
