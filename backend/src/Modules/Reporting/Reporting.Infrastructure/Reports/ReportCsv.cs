using System.Globalization;
using System.Text;

namespace FreshOrbit.Reporting.Infrastructure.Reports;

/// <summary>
/// CSV format of a report (REP-R12): snake_case header; money as <c>_paise</c> integers followed by <c>_inr</c> decimal
/// strings; text cells starting with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c> prefixed with <c>'</c>; a final TOTAL row.
/// </summary>
public static class ReportCsv
{
    public static string Header(ReportDefinition report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return Line(report.Columns.SelectMany(c => c.Kind == ColumnKind.Money ? new[] { c.Name, InrName(c.Name) } : [c.Name]));
    }

    public static string Row(ReportDefinition report, IReadOnlyList<object?> values)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(values);
        var cells = new List<string>(report.Columns.Count + 4);
        for (var i = 0; i < report.Columns.Count; i++)
        {
            AddCells(cells, report.Columns[i], values[i]);
        }

        return Line(cells);
    }

    public static string TotalRow(ReportDefinition report, IReadOnlyDictionary<string, object?> totals)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(totals);
        var cells = new List<string>(report.Columns.Count + 4);
        for (var i = 0; i < report.Columns.Count; i++)
        {
            var column = report.Columns[i];
            if (i == 0)
            {
                cells.Add("TOTAL");
                continue;
            }

            AddCells(cells, column, totals.TryGetValue(column.Name, out var value) ? value : null);
        }

        return Line(cells);
    }

    /// <summary><c>123450</c> → <c>1234.50</c>; <c>-5</c> → <c>-0.05</c>.</summary>
    public static string Inr(long paise)
    {
        var sign = paise < 0 ? "-" : string.Empty;
        var abs = Math.Abs((decimal)paise);
        return sign + (abs / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string InrName(string name) => name.EndsWith("_paise", StringComparison.Ordinal) ? name[..^"_paise".Length] + "_inr" : name + "_inr";

    private static void AddCells(List<string> cells, ReportColumn column, object? value)
    {
        switch (column.Kind)
        {
            case ColumnKind.Money:
                cells.Add(value is long paise ? paise.ToString(CultureInfo.InvariantCulture) : string.Empty);
                cells.Add(value is long rupees ? Inr(rupees) : string.Empty);
                break;
            case ColumnKind.Date:
                cells.Add(value is DateOnly date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty);
                break;
            case ColumnKind.Text:
                cells.Add(Neutralize(value as string ?? string.Empty));
                break;
            default:
                cells.Add(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    /// <summary>Prefixes formula-looking text with <c>'</c> so spreadsheets do not evaluate it.</summary>
    private static string Neutralize(string text)
        => text.Length > 0 && text[0] is '=' or '+' or '-' or '@' ? "'" + text : text;

    private static string Line(IEnumerable<string> cells)
    {
        var builder = new StringBuilder();
        var first = true;
        foreach (var cell in cells)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            if (cell.AsSpan().IndexOfAny(",\"\r\n") >= 0)
            {
                builder.Append('"').Append(cell.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
            }
            else
            {
                builder.Append(cell);
            }
        }

        return builder.Append("\r\n").ToString();
    }
}
