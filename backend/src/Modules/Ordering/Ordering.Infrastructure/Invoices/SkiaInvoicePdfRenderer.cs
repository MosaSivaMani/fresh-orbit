using System.Globalization;
using FreshOrbit.Ordering.Application.Invoices;
using SkiaSharp;

namespace FreshOrbit.Ordering.Infrastructure.Invoices;

/// <summary>
/// Lays out a GST invoice on A4 pages with SkiaSharp's PDF backend (D345: MIT, already in the build for product images,
/// no new native dependency). Inter Regular and Bold (SIL OFL 1.1, <c>Fonts/Inter-OFL.txt</c>) are embedded in this
/// assembly and subset into each PDF, so the file prints the same everywhere and needs no system fonts. Lines that do
/// not fit continue on the next page under a repeated table header. Draws only the server's figures (rule 4).
/// </summary>
public sealed class SkiaInvoicePdfRenderer : IInvoicePdfRenderer, IDisposable
{
    private const float PageWidth = 595.28f;
    private const float PageHeight = 841.89f;
    private const float Margin = 36f;
    private const float ContentWidth = PageWidth - (2 * Margin);
    private const float TableFontSize = 7.5f;
    private const float TableLineHeight = 10f;
    private const float CellPadding = 3f;
    private const float FooterHeight = 24f;

    /// <summary>Table columns: header, width (points, summing to the content width), right-aligned.</summary>
    private static readonly (string Header, float Width, bool Right)[] Columns =
    [
        ("#", 14f, false),
        ("Description", 128f, false),
        ("HSN/SAC", 42f, false),
        ("Qty", 44f, true),
        ("Rate (₹)", 66f, true),
        ("Taxable (₹)", 56f, true),
        ("GST", 28f, true),
        ("CGST (₹)", 44f, true),
        ("SGST (₹)", 44f, true),
        ("Total (₹)", 57f, true),
    ];

    private static readonly SKColor Ink = SKColor.Parse("#1F2933");
    private static readonly SKColor Muted = SKColor.Parse("#52606D");
    private static readonly SKColor Rule = SKColor.Parse("#CBD2D9");
    private static readonly SKColor HeaderFill = SKColor.Parse("#EEF2F5");

    private readonly SKTypeface _regular;
    private readonly SKTypeface _bold;

    public SkiaInvoicePdfRenderer()
    {
        _regular = LoadTypeface("Inter-Regular.ttf");
        _bold = LoadTypeface("Inter-Bold.ttf");
    }

    /// <summary>True when both embedded fonts have a glyph for every character of <paramref name="text"/>.</summary>
    public bool CanPrint(string text)
    {
        using var regular = new SKFont(_regular);
        using var bold = new SKFont(_bold);
        return regular.ContainsGlyphs(text) && bold.ContainsGlyphs(text);
    }

    public byte[] Render(InvoicePdfContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var output = new MemoryStream();
        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"{InvoicePdfContent.Title} {content.InvoiceNumber}",
            Author = content.Seller.LegalName,
            Creator = "Fresh Orbit",
            Producer = "Fresh Orbit",
        };
        using (var document = SKDocument.CreatePdf(output, metadata)
            ?? throw new InvalidOperationException("The PDF backend is not available in this SkiaSharp build."))
        {
            using var page = new PageWriter(document, this);
            DrawHeader(page, content);
            DrawTable(page, content);
            DrawTotals(page, content);
            page.Finish();
            document.Close();
        }

        return output.ToArray();
    }

    public void Dispose()
    {
        _regular.Dispose();
        _bold.Dispose();
    }

    private static SKTypeface LoadTypeface(string fileName)
    {
        var name = $"FreshOrbit.Ordering.Invoices.Fonts.{fileName}";
        using var stream = typeof(SkiaInvoicePdfRenderer).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The embedded font {name} is missing from the Ordering assembly.");
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data)
            ?? throw new InvalidOperationException($"The embedded font {name} could not be loaded.");
    }

    private static void DrawHeader(PageWriter page, InvoicePdfContent content)
    {
        page.Text(InvoicePdfContent.Title.ToUpperInvariant(), Margin, page.Y + 16, 16f, bold: true);
        page.Text("Original for recipient", Margin + ContentWidth, page.Y + 16, 8f, color: Muted, right: true);
        page.Y += 28;

        var top = page.Y;
        var half = (ContentWidth / 2) - 8;
        var left = Margin;
        var right = Margin + (ContentWidth / 2) + 8;

        // Seller (left) and invoice facts (right).
        var y = top;
        y = page.Paragraph(content.Seller.LegalName, left, y, half, 10f, bold: true);
        y = page.Paragraph(content.Seller.Address, left, y, half, 8.5f);
        y = page.Paragraph($"GSTIN: {content.Seller.Gstin}", left, y, half, 8.5f);
        y = page.Paragraph($"FSSAI licence no.: {content.Seller.FssaiLicenseNo}", left, y, half, 8.5f);

        var facts = new (string Label, string Value)[]
        {
            ("Invoice no.", content.InvoiceNumber),
            ("Invoice date", content.InvoiceDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)),
            ("Order no.", content.OrderNumber),
            ("Place of supply", $"{content.PlaceOfSupply.Name} ({content.PlaceOfSupply.Code})"),
            ("Reverse charge", "No"),
        };
        var factY = top;
        foreach (var (label, value) in facts)
        {
            page.Text(label, right, factY + 9, 8.5f, color: Muted);
            page.Text(value, right + 80, factY + 9, 8.5f, bold: true);
            factY += 12;
        }

        page.Y = Math.Max(y, factY) + 10;

        // Buyer.
        page.Text("Billed and shipped to", left, page.Y + 9, 8.5f, color: Muted);
        page.Y += 12;
        page.Y = page.Paragraph(content.Buyer.Name, left, page.Y, ContentWidth, 9.5f, bold: true);
        foreach (var line in content.Buyer.AddressLines)
        {
            page.Y = page.Paragraph(line, left, page.Y, ContentWidth, 8.5f);
        }

        page.Y += 12;
    }

    private static void DrawTable(PageWriter page, InvoicePdfContent content)
    {
        page.TableHeader();
        foreach (var line in content.Lines)
        {
            var descriptionLines = page.Wrap(line.Description, Columns[1].Width - (2 * CellPadding), TableFontSize, bold: false);
            var height = (descriptionLines.Count * TableLineHeight) + (2 * CellPadding);
            if (!page.Fits(height))
            {
                page.NewPage();
                page.TableHeader();
            }

            var cells = new[]
            {
                line.LineNo.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                line.HsnCode,
                line.Quantity,
                line.Rate,
                IndianRupees.Format(line.TaxablePaise),
                InvoicePdfContent.RateBpText(line.TaxRateBp),
                IndianRupees.Format(line.CgstPaise),
                IndianRupees.Format(line.SgstPaise),
                IndianRupees.Format(line.TotalPaise),
            };
            var x = Margin;
            for (var i = 0; i < Columns.Length; i++)
            {
                if (i == 1)
                {
                    var lineY = page.Y + CellPadding;
                    foreach (var text in descriptionLines)
                    {
                        page.Text(text, x + CellPadding, lineY + 7.5f, TableFontSize);
                        lineY += TableLineHeight;
                    }
                }
                else
                {
                    var textX = Columns[i].Right ? x + Columns[i].Width - CellPadding : x + CellPadding;
                    page.Text(cells[i], textX, page.Y + CellPadding + 7.5f, TableFontSize, right: Columns[i].Right);
                }

                x += Columns[i].Width;
            }

            page.Y += height;
            page.HorizontalRule(page.Y);
        }
    }

    private static void DrawTotals(PageWriter page, InvoicePdfContent content)
    {
        var rows = new (string Label, long Paise, bool Bold)[]
        {
            ("Taxable value", content.TaxablePaise, false),
            ("CGST", content.CgstPaise, false),
            ("SGST", content.SgstPaise, false),
            ("IGST", content.IgstPaise, false),
            ("Invoice total", content.TotalPaise, true),
        };
        const float RowHeight = 13f;
        var needed = (rows.Length * RowHeight) + 60;
        if (!page.Fits(needed))
        {
            page.NewPage();
        }

        page.Y += 8;
        var labelX = Margin + ContentWidth - 200;
        var valueX = Margin + ContentWidth - CellPadding;
        foreach (var (label, paise, bold) in rows)
        {
            if (bold)
            {
                page.HorizontalRule(page.Y + 1, labelX);
                page.Y += 3;
            }

            page.Text(label, labelX, page.Y + 9.5f, bold ? 10f : 8.5f, bold: bold);
            page.Text($"₹ {IndianRupees.Format(paise)}", valueX, page.Y + 9.5f, bold ? 10f : 8.5f, bold: bold, right: true);
            page.Y += RowHeight;
        }

        page.Y += 14;
        page.Y = page.Paragraph("Line totals include any preparation charge and are after discount. GST is included in the prices.", Margin, page.Y, ContentWidth, 7.5f, Muted);
        page.Y = page.Paragraph("This is a computer-generated invoice.", Margin, page.Y, ContentWidth, 7.5f, Muted);
    }

    /// <summary>Draws on the current page and turns pages; tracks the vertical position <see cref="Y"/>.</summary>
    private sealed class PageWriter : IDisposable
    {
        private readonly SKDocument _document;
        private readonly SkiaInvoicePdfRenderer _renderer;
        private readonly SKPaint _paint = new() { IsAntialias = true };
        private SKCanvas _canvas;
        private int _pageNumber = 1;

        public PageWriter(SKDocument document, SkiaInvoicePdfRenderer renderer)
        {
            _document = document;
            _renderer = renderer;
            _canvas = document.BeginPage(PageWidth, PageHeight);
            Y = Margin;
        }

        public float Y { get; set; }

        public bool Fits(float height) => Y + height <= PageHeight - Margin - FooterHeight;

        public void NewPage()
        {
            DrawPageNumber();
            _document.EndPage();
            _pageNumber++;
            _canvas = _document.BeginPage(PageWidth, PageHeight);
            Y = Margin;
        }

        public void Finish()
        {
            DrawPageNumber();
            _document.EndPage();
        }

        public void Text(string text, float x, float baseline, float size, bool bold = false, SKColor? color = null, bool right = false)
        {
            using var font = new SKFont(bold ? _renderer._bold : _renderer._regular, size);
            _paint.Color = color ?? Ink;
            _canvas.DrawText(text, x, baseline, right ? SKTextAlign.Right : SKTextAlign.Left, font, _paint);
        }

        /// <summary>Draws wrapped text from the top <paramref name="y"/>; returns the y below it.</summary>
        public float Paragraph(string text, float x, float y, float width, float size, SKColor? color = null, bool bold = false)
        {
            var lineHeight = size * 1.35f;
            foreach (var line in Wrap(text, width, size, bold))
            {
                Text(line, x, y + size, size, bold, color);
                y += lineHeight;
            }

            return y;
        }

        /// <summary>
        /// Splits <paramref name="text"/> into lines no wider than <paramref name="width"/>: at line breaks, then at
        /// spaces (a word longer than the width is cut).
        /// </summary>
        public List<string> Wrap(string text, float width, float size, bool bold)
        {
            using var font = new SKFont(bold ? _renderer._bold : _renderer._regular, size);
            var lines = new List<string>();
            foreach (var paragraph in text.Split('\n'))
            {
                var current = string.Empty;
                foreach (var word in paragraph.TrimEnd('\r').Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = current.Length == 0 ? word : $"{current} {word}";
                    if (font.MeasureText(candidate) <= width)
                    {
                        current = candidate;
                        continue;
                    }

                    if (current.Length > 0)
                    {
                        lines.Add(current);
                    }

                    current = word;
                    while (current.Length > 1 && font.MeasureText(current) > width)
                    {
                        var cut = current.Length - 1;
                        while (cut > 1 && font.MeasureText(current[..cut]) > width)
                        {
                            cut--;
                        }

                        lines.Add(current[..cut]);
                        current = current[cut..];
                    }
                }

                if (current.Length > 0)
                {
                    lines.Add(current);
                }
            }

            if (lines.Count == 0)
            {
                lines.Add(string.Empty);
            }

            return lines;
        }

        public void TableHeader()
        {
            var height = TableLineHeight + (2 * CellPadding);
            using (var fill = new SKPaint { Color = HeaderFill, Style = SKPaintStyle.Fill })
            {
                _canvas.DrawRect(Margin, Y, ContentWidth, height, fill);
            }

            var x = Margin;
            foreach (var (header, width, right) in Columns)
            {
                Text(header, right ? x + width - CellPadding : x + CellPadding, Y + CellPadding + 7.5f, TableFontSize, bold: true, right: right);
                x += width;
            }

            Y += height;
            HorizontalRule(Y);
        }

        public void HorizontalRule(float y, float startX = Margin)
        {
            using var line = new SKPaint { Color = Rule, StrokeWidth = 0.5f, Style = SKPaintStyle.Stroke };
            _canvas.DrawLine(startX, y, Margin + ContentWidth, y, line);
        }

        public void Dispose() => _paint.Dispose();

        private void DrawPageNumber()
            => Text(string.Create(CultureInfo.InvariantCulture, $"Page {_pageNumber}"), Margin + ContentWidth, PageHeight - Margin, 7.5f, color: Muted, right: true);
    }
}
