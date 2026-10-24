using SkiaSharp;

namespace FreshOrbit.Catalog.Infrastructure.Seeding;

/// <summary>The silhouette drawn on a sample placeholder.</summary>
public enum PlaceholderShape
{
    Fish,
    Prawn,
    Crab,
}

/// <summary>
/// Draws the sample placeholder images (images.json sizes) with SkiaSharp: a soft sand background, a pale teal disc and
/// a simple seafood silhouette in the brand teal (design tokens <c>seaGreen</c> on <c>highlightSubtle</c>). No text:
/// product names belong to the page, not the picture. Owner photographs replace these through the admin endpoints.
/// </summary>
public static class SamplePlaceholderRenderer
{
    private static readonly SKColor Sand = SKColor.Parse("#FBF0DF");
    private static readonly SKColor SandDeep = SKColor.Parse("#F5E2C4");
    private static readonly SKColor Disc = SKColor.Parse("#E0F2EF");
    private static readonly SKColor Teal = SKColor.Parse("#0E9384");
    private static readonly SKColor TealDeep = SKColor.Parse("#0B7D70");

    /// <summary>The shape for a product or category: prawns, scampi and lobster as a prawn; crabs as a crab; else a fish.</summary>
    public static PlaceholderShape ShapeFor(string categorySlug, string? productSlug = null)
    {
        var text = $"{categorySlug} {productSlug}";
        if (text.Contains("prawn", StringComparison.Ordinal) || text.Contains("shrimp", StringComparison.Ordinal)
            || text.Contains("scampi", StringComparison.Ordinal) || text.Contains("lobster", StringComparison.Ordinal))
        {
            return PlaceholderShape.Prawn;
        }

        return text.Contains("crab", StringComparison.Ordinal) ? PlaceholderShape.Crab : PlaceholderShape.Fish;
    }

    /// <summary>Encoded image bytes (PNG or WebP) of the given size.</summary>
    public static byte[] Render(int width, int height, PlaceholderShape shape, SKEncodedImageFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        DrawBackground(canvas, width, height);

        // A wide banner puts the motif in its right third; square and landscape images centre it.
        var wide = width >= height * 2;
        var size = wide ? height * 0.78f : Math.Min(width, height) * 0.62f;
        var center = wide ? new SKPoint(width * 0.76f, height * 0.5f) : new SKPoint(width * 0.5f, height * 0.5f);
        using (var disc = new SKPaint { Color = Disc, IsAntialias = true })
        {
            canvas.DrawCircle(center, size * 0.62f, disc);
        }

        DrawBubbles(canvas, center, size);
        canvas.Save();
        canvas.Translate(center);
        canvas.Scale(size);
        switch (shape)
        {
            case PlaceholderShape.Prawn:
                DrawPrawn(canvas);
                break;
            case PlaceholderShape.Crab:
                DrawCrab(canvas);
                break;
            default:
                DrawFish(canvas);
                break;
        }

        canvas.Restore();
        if (wide)
        {
            DrawWaves(canvas, width, height);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(format, 90)
            ?? throw new InvalidOperationException($"{format} encoding is not available in this SkiaSharp build.");
        return data.ToArray();
    }

    private static void DrawBackground(SKCanvas canvas, int width, int height)
    {
        using var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(width, height), [Sand, SandDeep], SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(0, 0, width, height, paint);
    }

    private static void DrawBubbles(SKCanvas canvas, SKPoint center, float size)
    {
        using var paint = new SKPaint { Color = Teal.WithAlpha(70), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = size * 0.012f };
        canvas.DrawCircle(center.X + (size * 0.42f), center.Y - (size * 0.40f), size * 0.035f, paint);
        canvas.DrawCircle(center.X + (size * 0.50f), center.Y - (size * 0.50f), size * 0.022f, paint);
        canvas.DrawCircle(center.X + (size * 0.45f), center.Y - (size * 0.58f), size * 0.015f, paint);
    }

    private static void Draw(SKCanvas canvas, SKPathBuilder builder, SKPaint paint)
    {
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
    }

    private static void DrawWaves(SKCanvas canvas, int width, int height)
    {
        using var paint = new SKPaint { Color = Teal.WithAlpha(60), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = height * 0.012f, StrokeCap = SKStrokeCap.Round };
        for (var line = 0; line < 3; line++)
        {
            var y = height * (0.68f + (line * 0.09f));
            using var path = new SKPathBuilder();
            path.MoveTo(width * 0.06f, y);
            for (var x = 0; x < 4; x++)
            {
                var start = width * (0.06f + (x * 0.09f));
                path.QuadTo(start + (width * 0.0225f), y - (height * 0.035f), start + (width * 0.045f), y);
                path.QuadTo(start + (width * 0.0675f), y + (height * 0.035f), start + (width * 0.09f), y);
            }

            Draw(canvas, path, paint);
        }
    }

    /// <summary>A fish facing right in a unit box centred on the origin.</summary>
    private static void DrawFish(SKCanvas canvas)
    {
        using var fill = new SKPaint { Color = Teal, IsAntialias = true };
        using var body = new SKPathBuilder();
        body.MoveTo(0.44f, 0.02f);
        body.CubicTo(0.32f, -0.22f, -0.04f, -0.27f, -0.27f, -0.05f);
        body.LineTo(-0.46f, -0.21f);
        body.QuadTo(-0.40f, 0.0f, -0.46f, 0.21f);
        body.LineTo(-0.27f, 0.05f);
        body.CubicTo(-0.04f, 0.27f, 0.32f, 0.24f, 0.44f, 0.02f);
        body.Close();
        Draw(canvas, body, fill);

        using var fins = new SKPaint { Color = TealDeep, IsAntialias = true };
        using var dorsal = new SKPathBuilder();
        dorsal.MoveTo(-0.06f, -0.20f);
        dorsal.QuadTo(0.02f, -0.36f, 0.16f, -0.21f);
        dorsal.Close();
        Draw(canvas, dorsal, fins);
        using var pelvic = new SKPathBuilder();
        pelvic.MoveTo(0.02f, 0.19f);
        pelvic.QuadTo(0.06f, 0.30f, 0.14f, 0.18f);
        pelvic.Close();
        Draw(canvas, pelvic, fins);

        using var detail = new SKPaint { Color = Sand, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.018f, StrokeCap = SKStrokeCap.Round };
        using var gill = new SKPathBuilder();
        gill.MoveTo(0.21f, -0.12f);
        gill.QuadTo(0.15f, 0.0f, 0.21f, 0.12f);
        Draw(canvas, gill, detail);
        using var eye = new SKPaint { Color = Sand, IsAntialias = true };
        canvas.DrawCircle(0.31f, -0.04f, 0.028f, eye);
    }

    /// <summary>A curled prawn: a smooth tapered body round an arc, shell bands, legs, a tail fan and two antennae.</summary>
    private static void DrawPrawn(SKCanvas canvas)
    {
        // The body follows a circle: head on the right (angle 15°), over the top, down to the tail lower left (250°).
        const float Radius = 0.25f;
        const float HeadAngle = 15f;
        const float TailAngle = 250f;
        const int Steps = 48;
        static SKPoint At(float degrees, float radius)
        {
            var a = degrees * MathF.PI / 180f;
            return new SKPoint(radius * MathF.Cos(a), 0.03f - (radius * MathF.Sin(a)));
        }

        static float HalfWidth(float t) => 0.15f - (0.105f * t);

        using var body = new SKPathBuilder();
        for (var i = 0; i <= Steps; i++)
        {
            var t = (float)i / Steps;
            var point = At(HeadAngle + ((TailAngle - HeadAngle) * t), Radius + HalfWidth(t));
            if (i == 0)
            {
                body.MoveTo(point);
            }
            else
            {
                body.LineTo(point);
            }
        }

        for (var i = Steps; i >= 0; i--)
        {
            var t = (float)i / Steps;
            body.LineTo(At(HeadAngle + ((TailAngle - HeadAngle) * t), Radius - HalfWidth(t)));
        }

        body.Close();
        using var fill = new SKPaint { Color = Teal, IsAntialias = true };

        // A rounded head end.
        canvas.DrawCircle(At(HeadAngle, Radius), HalfWidth(0f), fill);
        Draw(canvas, body, fill);

        // Tail fan.
        var tail = At(TailAngle, Radius);
        using var fan = new SKPathBuilder();
        fan.MoveTo(tail);
        fan.LineTo(tail.X + 0.16f, tail.Y + 0.10f);
        fan.QuadTo(tail.X + 0.12f, tail.Y + 0.18f, tail.X + 0.03f, tail.Y + 0.19f);
        fan.Close();
        using var deep = new SKPaint { Color = TealDeep, IsAntialias = true };
        Draw(canvas, fan, deep);

        // Shell bands across the body.
        using var bands = new SKPaint { Color = Sand.WithAlpha(210), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.012f, StrokeCap = SKStrokeCap.Round };
        for (var i = 1; i <= 5; i++)
        {
            var t = 0.22f + (i * 0.12f);
            var angle = HeadAngle + ((TailAngle - HeadAngle) * t);
            var half = HalfWidth(t) * 0.8f;
            canvas.DrawLine(At(angle, Radius - half), At(angle, Radius + half), bands);
        }

        // Short legs along the inner edge of the front body.
        using var legs = new SKPaint { Color = TealDeep, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.012f, StrokeCap = SKStrokeCap.Round };
        for (var i = 0; i < 5; i++)
        {
            var t = 0.12f + (i * 0.07f);
            var angle = HeadAngle + ((TailAngle - HeadAngle) * t);
            var inner = Radius - HalfWidth(t);
            canvas.DrawLine(At(angle, inner + 0.01f), At(angle - 4f, inner - 0.045f), legs);
        }

        // Antennae sweep back over the body from the head.
        var head = At(HeadAngle, Radius + 0.05f);
        using var antenna = new SKPaint { Color = TealDeep, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.012f, StrokeCap = SKStrokeCap.Round };
        using var first = new SKPathBuilder();
        first.MoveTo(head);
        first.CubicTo(0.55f, -0.10f, 0.40f, -0.50f, -0.05f, -0.52f);
        Draw(canvas, first, antenna);
        using var second = new SKPathBuilder();
        second.MoveTo(head);
        second.CubicTo(0.60f, 0.05f, 0.58f, -0.38f, 0.22f, -0.50f);
        Draw(canvas, second, antenna);

        using var eye = new SKPaint { Color = Sand, IsAntialias = true };
        var eyeAt = At(HeadAngle + 4f, Radius + 0.06f);
        canvas.DrawCircle(eyeAt, 0.022f, eye);
    }

    /// <summary>A crab seen from above: round shell, two claws and four legs a side.</summary>
    private static void DrawCrab(SKCanvas canvas)
    {
        using var legs = new SKPaint { Color = TealDeep, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.035f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        for (var side = -1; side <= 1; side += 2)
        {
            for (var i = 0; i < 4; i++)
            {
                using var leg = new SKPathBuilder();
                var y = 0.02f + (i * 0.06f);
                leg.MoveTo(side * 0.18f, y);
                leg.LineTo(side * (0.36f + (i * 0.02f)), y - 0.04f + (i * 0.03f));
                leg.LineTo(side * (0.44f + (i * 0.01f)), y + 0.08f + (i * 0.03f));
                Draw(canvas, leg, legs);
            }

            using var arm = new SKPathBuilder();
            arm.MoveTo(side * 0.16f, -0.10f);
            arm.QuadTo(side * 0.34f, -0.20f, side * 0.30f, -0.32f);
            Draw(canvas, arm, legs);
            using var claw = new SKPaint { Color = Teal, IsAntialias = true };
            canvas.DrawOval(new SKRect((side * 0.30f) - 0.08f, -0.44f, (side * 0.30f) + 0.08f, -0.28f), claw);
            using var notch = new SKPaint { Color = Disc, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.02f, StrokeCap = SKStrokeCap.Round };
            canvas.DrawLine(side * 0.30f, -0.44f, side * 0.30f, -0.36f, notch);
        }

        using var shell = new SKPaint { Color = Teal, IsAntialias = true };
        canvas.DrawOval(new SKRect(-0.26f, -0.16f, 0.26f, 0.20f), shell);
        using var eyes = new SKPaint { Color = Sand, IsAntialias = true };
        canvas.DrawCircle(-0.07f, -0.09f, 0.022f, eyes);
        canvas.DrawCircle(0.07f, -0.09f, 0.022f, eyes);
    }
}
