using System.Globalization;
using System.Text;
using System.Text.Json;
using FreshOrbit.Pricing.Application.Contracts;
using FreshOrbit.Pricing.Application.Schedules;
using FreshOrbit.Pricing.Domain.Periods;
using FreshOrbit.Pricing.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Serialization;
using Microsoft.AspNetCore.Http;

namespace FreshOrbit.Pricing.Infrastructure.Endpoints;

/// <summary>
/// Reads a bulk upload body (PRC-R13): JSON <see cref="BulkPriceRequest"/>, or <c>text/csv</c> with the header
/// <c>kind,location_code,sku,unit_price_paise,compare_at_paise,preparation_code,charge_paise,charge_basis,valid_from</c>
/// (any order; <c>validFrom</c> and <c>replaceFuture</c> for the file come from the query string).
/// </summary>
internal static class BulkPriceReader
{
    public static readonly string[] CsvColumns =
        ["kind", "location_code", "sku", "unit_price_paise", "compare_at_paise", "preparation_code", "charge_paise", "charge_basis", "valid_from"];

    public static async Task<BulkPriceRequest> ReadAsync(HttpRequest request, DateTimeOffset? validFrom, bool? replaceFuture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContentType?.StartsWith("text/csv", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return new BulkPriceRequest(validFrom, replaceFuture, ParseCsv(text));
        }

        try
        {
            var body = await JsonSerializer.DeserializeAsync<BulkPriceRequest>(request.Body, FreshOrbitJson.Options, cancellationToken)
                ?? throw new BadHttpRequestException("Send a JSON body { validFrom?, replaceFuture?, rows: [...] } or text/csv.");
            return body with { ValidFrom = body.ValidFrom ?? validFrom, ReplaceFuture = body.ReplaceFuture ?? replaceFuture };
        }
        catch (JsonException exception)
        {
            throw new BadHttpRequestException($"The JSON body could not be read: {exception.Message}");
        }
    }

    public static List<BulkPriceRow?> ParseCsv(string text)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count < 2)
        {
            throw new RefusalException(new FieldError("rows", PricingErrors.ValidationFailed, "Send a header line and at least one row."));
        }

        if (lines.Count - 1 > BulkPriceService.MaxRows)
        {
            throw new RefusalException(new FieldError("rows", PricingErrors.ValidationFailed, $"Send at most {BulkPriceService.MaxRows} rows."));
        }

        var header = SplitLine(lines[0]).Select(h => h.Trim()).ToList();
        var unknown = header.Where(h => !CsvColumns.Contains(h, StringComparer.OrdinalIgnoreCase)).ToList();
        if (unknown.Count > 0 || !header.Contains("kind", StringComparer.OrdinalIgnoreCase))
        {
            throw new RefusalException(new FieldError("header", PricingErrors.ValidationFailed, $"Use the columns {string.Join(',', CsvColumns)}."));
        }

        var errors = new List<FieldError>();
        var rows = new List<BulkPriceRow?>();
        for (var i = 1; i < lines.Count; i++)
        {
            var index = i - 1;
            var cells = SplitLine(lines[i]);
            string? Cell(string name)
            {
                var position = header.FindIndex(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
                var value = position >= 0 && position < cells.Count ? cells[position].Trim() : null;
                return string.IsNullOrEmpty(value) ? null : value;
            }

            long? Long(string name, string field)
            {
                var value = Cell(name);
                if (value is null)
                {
                    return null;
                }

                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    return number;
                }

                errors.Add(new FieldError($"rows[{index}].{field}", PricingErrors.ValidationFailed, "Enter a whole number of paise."));
                return null;
            }

            BulkRowKind? kind = EnumCodes.TryToEnum<BulkRowKind>(Cell("kind")?.ToUpperInvariant(), out var parsedKind) ? parsedKind : null;
            ChargeBasis? basis = null;
            if (Cell("charge_basis") is { } basisText)
            {
                if (EnumCodes.TryToEnum<ChargeBasis>(basisText.ToUpperInvariant(), out var parsedBasis))
                {
                    basis = parsedBasis;
                }
                else
                {
                    errors.Add(new FieldError($"rows[{index}].chargeBasis", PricingErrors.ValidationFailed, "Use PER_KG, PER_ITEM or FLAT."));
                }
            }

            DateTimeOffset? rowFrom = null;
            if (Cell("valid_from") is { } fromText)
            {
                if (DateTimeOffset.TryParse(fromText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var from))
                {
                    rowFrom = from;
                }
                else
                {
                    errors.Add(new FieldError($"rows[{index}].validFrom", PricingErrors.ValidationFailed, "Enter an ISO 8601 time, e.g. 2026-10-24T00:30:00Z."));
                }
            }

            rows.Add(new BulkPriceRow(
                kind,
                Cell("location_code"),
                Cell("sku"),
                Long("unit_price_paise", "unitPricePaise"),
                Long("compare_at_paise", "compareAtPaise"),
                Cell("preparation_code"),
                Long("charge_paise", "chargePaise"),
                basis,
                rowFrom));
        }

        return errors.Count > 0 ? throw new RefusalException(errors) : rows;
    }

    /// <summary>Splits a CSV line; double quotes enclose fields with commas, <c>""</c> is a literal quote.</summary>
    private static List<string> SplitLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}
