using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PublicSellUnit = FreshOrbit.Catalog.Public.SellUnit;

namespace FreshOrbit.Catalog.Infrastructure.Reads;

/// <summary>A product's best current offer at a location: the price rule of the product list (lowest stocked variant price).</summary>
public sealed record ProductOffer(long UnitPricePaise, long? CompareAtPaise, PublicSellUnit SellUnit, int Available);

/// <summary>
/// Batched reads of <c>catalog.storefront_offers</c> (D3) for other modules' cards (W5). Same rule as
/// <see cref="StorefrontReads.ListProductsAsync"/>: only variants with a stock item and a current price count; the
/// price is the lowest one, availability the largest available quantity.
/// </summary>
public static class StorefrontOffers
{
    private const string Sql = """
        WITH offer_rows AS (
            SELECT o.product_id, o.variant_id, o.sell_unit, o.unit_price_paise, o.compare_at_paise, o.available
            FROM catalog.storefront_offers o
            WHERE o.fulfillment_location_id = @location AND o.has_stock_item AND o.product_id = ANY(@ids)
              AND o.valid_from <= @at AND (o.valid_to IS NULL OR o.valid_to > @at)),
        best AS (SELECT DISTINCT ON (product_id) product_id, unit_price_paise, compare_at_paise, sell_unit FROM offer_rows ORDER BY product_id, unit_price_paise, variant_id),
        stock AS (SELECT product_id, max(available) AS available FROM offer_rows GROUP BY product_id)
        SELECT b.product_id, b.unit_price_paise, b.compare_at_paise, b.sell_unit, s.available
        FROM best b JOIN stock s ON s.product_id = b.product_id
        """;

    public static async Task<Dictionary<Guid, ProductOffer>> BestByProductAsync(FreshOrbitDbContext db, Guid[] productIds, Guid fulfillmentLocationId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(productIds);
        var result = new Dictionary<Guid, ProductOffer>();
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = Sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.Parameters.Add(new NpgsqlParameter("location", NpgsqlDbType.Uuid) { Value = fulfillmentLocationId });
            command.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = productIds });
            command.Parameters.Add(new NpgsqlParameter("at", NpgsqlDbType.TimestampTz) { Value = at.UtcDateTime });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result[reader.GetGuid(0)] = new ProductOffer(
                    reader.GetInt64(1),
                    reader.IsDBNull(2) ? null : reader.GetInt64(2),
                    reader.GetString(3).ToEnum<PublicSellUnit>(),
                    reader.IsDBNull(4) ? 0 : reader.GetInt32(4));
            }
        }
        finally
        {
            if (opened)
            {
                await db.Database.CloseConnectionAsync();
            }
        }

        return result;
    }
}
