using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Catalog.Infrastructure.Persistence;
using FreshOrbit.Catalog.Infrastructure.Seeding;
using FreshOrbit.Pricing.Infrastructure.Seeding;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace FreshOrbit.Catalog.Tests.Support;

/// <summary>
/// Test-only stand-in for Inventory's <c>inventory.stock_items</c> (owned by E3, not on this branch): creates the
/// table only when it does not exist, then re-creates <c>catalog.storefront_offers</c> so the view joins it.
/// Its schema name is only a unique key for the model list; it maps no entities.
/// </summary>
public sealed class StockItemsTestModel : IModuleModel
{
    public const string CreateTableSql = """
        CREATE SCHEMA IF NOT EXISTS inventory;
        CREATE TABLE IF NOT EXISTS inventory.stock_items (
            id uuid PRIMARY KEY,
            fulfillment_location_id uuid NOT NULL,
            variant_id uuid NOT NULL,
            stock_unit varchar(40) NOT NULL,
            on_hand integer NOT NULL,
            reserved integer NOT NULL,
            low_stock_threshold integer NOT NULL,
            created_at timestamptz NOT NULL,
            created_by varchar(100),
            modified_at timestamptz,
            modified_by varchar(100));
        """;

    public string Schema => "test_stock_items";

    public IEnumerable<string> DatabaseObjectsSql => [CreateTableSql, CatalogModuleModel.StorefrontOffersViewSql];

    public void Configure(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
    }
}

/// <summary>The test database for Catalog and Pricing: every module plus <see cref="StockItemsTestModel"/>.</summary>
public class StorefrontDatabase : PostgresFixture
{
    private readonly SemaphoreSlim _referenceLock = new(1, 1);
    private bool _referenceLoaded;

    protected override IEnumerable<IModuleModel> AdditionalModuleModels => [new StockItemsTestModel()];

    public static string ReferenceDataPath
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "data", "reference");
                if (Directory.Exists(Path.Combine(candidate, "catalog")))
                {
                    return candidate;
                }
            }

            throw new DirectoryNotFoundException("backend/data/reference was not found above the test output directory.");
        }
    }

    /// <summary>Runs this branch's reference loaders (preparation methods, tax categories) once per database.</summary>
    public async Task EnsureReferenceDataAsync(IServiceProvider services)
    {
        await _referenceLock.WaitAsync();
        try
        {
            if (_referenceLoaded)
            {
                return;
            }

            await using var scope = services.CreateAsyncScope();
            foreach (var loader in scope.ServiceProvider.GetServices<IReferenceDataLoader>().Where(l => l is PreparationMethodsLoader or TaxCategoriesLoader).OrderBy(l => l.Order))
            {
                await loader.LoadAsync(new ReferenceDataContext(ReferenceDataPath), CancellationToken.None);
            }

            _referenceLoaded = true;
        }
        finally
        {
            _referenceLock.Release();
        }
    }
}

/// <summary>An ApiFactory whose file storage writes to a temporary directory the test can inspect.</summary>
public sealed class StorefrontApiFactory(StorefrontDatabase database) : ApiFactory(database)
{
    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), "freshorbit-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("FileStorage:RootPath", MediaRoot);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(MediaRoot))
        {
            Directory.Delete(MediaRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>Builds served locations, products, prices and stock through the real endpoints (plus SQL for stock and city status).</summary>
public abstract class StorefrontTestBase(StorefrontDatabase database) : IAsyncLifetime
{
    public const string TaxCategory = "SEAFOOD_FRESH_CHILLED";
    public const string UntaxedCategory = "SEAFOOD_DRIED_SALTED";

    private static int _pincodeSeed = Random.Shared.Next(200_000, 800_000);
    private HttpClient? _admin;

    protected StorefrontApiFactory Factory { get; } = new(database);

    protected StorefrontDatabase Database => database;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Staff with catalog, pricing and locations permissions on all locations.</summary>
    protected HttpClient Admin => _admin ??= Factory.CreateStaffClient(["catalog.manage", "pricing.manage", "locations.manage", "cities.activate"], allLocations: true);

    public async ValueTask InitializeAsync()
    {
        await Database.EnsureReferenceDataAsync(Factory.Services);
        await EnsureTaxRuleAsync(TaxCategory, 500);
    }

    public async ValueTask DisposeAsync()
    {
        _admin?.Dispose();
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    protected static string UniqueSku(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(Ct);
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    protected static async Task<string?> CodeOfAsync(HttpResponseMessage response) => (await ReadJsonAsync(response)).GetProperty("code").GetString();

    protected static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, object? body = null, string? ifMatch = null, bool ifNoneMatchAny = false, string? pincode = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue(ifMatch.StartsWith('"') ? ifMatch : $"\"{ifMatch}\""));
        }

        if (ifNoneMatchAny)
        {
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);
        }

        if (pincode is not null)
        {
            request.Headers.Add("X-Pincode", pincode);
        }

        return await client.SendAsync(request, Ct);
    }

    protected async Task<JsonElement> OkAsync(HttpMethod method, string url, object? body = null, string? ifMatch = null, bool ifNoneMatchAny = false)
    {
        var response = await SendAsync(Admin, method, url, body, ifMatch, ifNoneMatchAny);
        var json = await ReadJsonAsync(response);
        response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} answered {(int)response.StatusCode} {json}");
        return json;
    }

    protected async Task<JsonElement> GetPublicAsync(string url, string? pincode = null)
    {
        using var client = Factory.CreateClient();
        var response = await SendAsync(client, HttpMethod.Get, url, pincode: pincode);
        var json = await ReadJsonAsync(response);
        response.IsSuccessStatusCode.Should().BeTrue($"GET {url} answered {(int)response.StatusCode} {json}");
        return json;
    }

    /// <summary>A tax rule for the category from now (unchanged when one with this rate is already current).</summary>
    protected Task<JsonElement> EnsureTaxRuleAsync(string category, int rateBp)
        => OkAsync(HttpMethod.Post, "/api/v1/admin/pricing/tax-rules", new { taxCategoryCode = category, rateBp });

    /// <summary>An ACTIVE city with an ACTIVE location and a service area serving one new pincode.</summary>
    protected async Task<ServedLocation> CreateServedLocationAsync()
    {
        var city = await OkAsync(HttpMethod.Post, "/api/v1/admin/cities", new { code = $"CITY_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", name = "Test City", stateCode = "TN", timeZone = "Asia/Kolkata" });
        var cityId = city.GetProperty("id").GetGuid();
        var code = $"LOC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var details = new
        {
            cityId,
            code,
            name = "Test Store",
            addressLine = "1 Beach Road",
            pincode = "600041",
            phone = "+914400000000",
            latitude = 12.98m,
            longitude = 80.26m,
            hasColdStorage = true,
            fssaiLicenseNo = "12345678901234",
        };
        var location = await OkAsync(HttpMethod.Post, "/api/v1/admin/fulfillment-locations", details);
        var locationId = location.GetProperty("id").GetGuid();
        var withHours = await OkAsync(HttpMethod.Put, $"/api/v1/admin/fulfillment-locations/{locationId}/operating-hours", new
        {
            days = new[] { new { dayOfWeek = "MONDAY", opensAt = "07:00", closesAt = "21:00" } },
        }, withHoursVersion(location));
        await OkAsync(HttpMethod.Put, $"/api/v1/admin/fulfillment-locations/{locationId}", new
        {
            details.name,
            details.addressLine,
            details.pincode,
            details.phone,
            details.latitude,
            details.longitude,
            details.hasColdStorage,
            details.fssaiLicenseNo,
            status = "ACTIVE",
        }, withHoursVersion(withHours));
        var area = await OkAsync(HttpMethod.Post, "/api/v1/admin/service-areas", new { cityId, fulfillmentLocationId = locationId, name = "Adyar", deliveryFeePaise = 3900, freeDeliveryAbovePaise = 49900, minOrderPaise = 29900 });
        var pincode = Interlocked.Increment(ref _pincodeSeed).ToString(CultureInfo.InvariantCulture);
        await OkAsync(HttpMethod.Post, $"/api/v1/admin/service-areas/{area.GetProperty("id").GetGuid()}/pincodes", new { pincodes = new[] { pincode } }, withHoursVersion(area));
        await ExecuteSqlAsync($"UPDATE locations.cities SET status = 'ACTIVE' WHERE id = '{cityId}'");
        return new ServedLocation(cityId, locationId, code, pincode);

        static string withHoursVersion(JsonElement json) => json.GetProperty("version").GetString()!;
    }

    protected async Task<Guid> CreateCategoryAsync(string? name = null, Guid? parentId = null)
    {
        var category = await OkAsync(HttpMethod.Post, "/api/v1/admin/categories", new { parentId, name = name ?? "Fish", slug = Unique("cat"), sortOrder = 10 });
        return category.GetProperty("id").GetGuid();
    }

    /// <summary>A DRAFT product; returns its JSON (id, version, …).</summary>
    protected Task<JsonElement> CreateProductAsync(Guid categoryId, string name, string[]? localNames = null, string taxCategory = TaxCategory, string? description = null, int? featuredRank = null)
        => OkAsync(HttpMethod.Post, "/api/v1/admin/products", new
        {
            categoryId,
            name,
            slug = Unique("product"),
            localNames = localNames ?? [],
            speciesScientificName = (string?)null,
            description,
            sourceType = "WILD",
            sourceRegion = "Kasimedu, Chennai",
            storageInstructions = "Keep at 0-4 °C.",
            hsnCode = "0302",
            taxCategoryCode = taxCategory,
            featuredRank,
        });

    protected static object KgVariant(string sku, string name = "Medium", params (string Code, int YieldBp, bool IsDefault)[] preparations) => new
    {
        sku,
        name,
        sellUnit = "KG",
        minOrder = 250,
        maxOrder = 5000,
        orderStep = 250,
        preparations = (preparations.Length == 0 ? [("WHOLE", 10000, false), ("CURRY_CUT", 6500, true)] : preparations)
            .Select(p => new { code = p.Code, yieldBp = p.YieldBp, isDefault = p.IsDefault })
            .ToArray(),
    };

    /// <summary>A PNG of the given size (solid colour) generated with SkiaSharp.</summary>
    protected static byte[] Png(int width = 900, int height = 900)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(30, 120, 200));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    protected async Task<HttpResponseMessage> UploadImageAsync(Guid productId, byte[] bytes, string fileName = "photo.png", string? altText = "Seer fish steaks")
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "file", fileName);
        if (altText is not null)
        {
            content.Add(new StringContent(altText), "altText");
        }

        return await Admin.PostAsync($"/api/v1/admin/products/{productId}/images", content, Ct);
    }

    /// <summary>A published product with one KG variant (WHOLE + default CURRY_CUT) and one image.</summary>
    protected async Task<PublishedProduct> CreatePublishedProductAsync(Guid categoryId, string name, string[]? localNames = null, string taxCategory = TaxCategory, int? featuredRank = null, string? description = null)
    {
        var product = await CreateProductAsync(categoryId, name, localNames, taxCategory, description, featuredRank);
        var productId = product.GetProperty("id").GetGuid();
        var sku = UniqueSku("SKU");
        var withVariant = await OkAsync(HttpMethod.Post, $"/api/v1/admin/products/{productId}/variants", KgVariant(sku), product.GetProperty("version").GetString());
        var upload = await UploadImageAsync(productId, Png());
        upload.IsSuccessStatusCode.Should().BeTrue(await upload.Content.ReadAsStringAsync(Ct));
        var published = await OkAsync(HttpMethod.Post, $"/api/v1/admin/products/{productId}/publish", null, upload.Headers.ETag!.Tag);
        var variant = withVariant.GetProperty("variants")[0];
        return new PublishedProduct(productId, published.GetProperty("slug").GetString()!, variant.GetProperty("id").GetGuid(), sku, published.GetProperty("version").GetString()!);
    }

    /// <summary>Price and charges for every preparation of the variant at the location (first schedule: If-None-Match: *).</summary>
    protected async Task PriceAsync(Guid locationId, Guid variantId, long unitPricePaise, long? compareAtPaise = null, params string[] preparationCodes)
    {
        await OkAsync(HttpMethod.Put, $"/api/v1/admin/pricing/locations/{locationId}/variants/{variantId}/price", new { unitPricePaise, compareAtPaise }, ifNoneMatchAny: true);
        foreach (var code in preparationCodes.Length == 0 ? ["WHOLE", "CURRY_CUT"] : preparationCodes)
        {
            await OkAsync(HttpMethod.Put, $"/api/v1/admin/pricing/locations/{locationId}/variants/{variantId}/preparations/{code}/charge", new { chargePaise = 2000, chargeBasis = "PER_KG" }, ifNoneMatchAny: true);
        }
    }

    protected Task AddStockAsync(Guid locationId, Guid variantId, int onHand, int reserved = 0, int lowStockThreshold = 1000)
        => ExecuteSqlAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"INSERT INTO inventory.stock_items (id, fulfillment_location_id, variant_id, stock_unit, on_hand, reserved, low_stock_threshold, created_at) VALUES ('{Guid.CreateVersion7()}', '{locationId}', '{variantId}', 'GRAM', {onHand}, {reserved}, {lowStockThreshold}, now())"));

    protected async Task ExecuteSqlAsync(string sql)
    {
        await using var db = Database.CreateDbContext();
#pragma warning disable EF1002 // Test-only SQL built from ids and numbers the test created.
        await db.Database.ExecuteSqlRawAsync(sql, Ct);
#pragma warning restore EF1002
    }

    protected sealed record ServedLocation(Guid CityId, Guid LocationId, string LocationCode, string Pincode);

    protected sealed record PublishedProduct(Guid ProductId, string Slug, Guid VariantId, string Sku, string Version);
}
