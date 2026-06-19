using SkiaSharp;

// Fresh app-icon batch v2 — a DIFFERENT direction from the </> code-mark.
// Theme: the literal meaning of "tittle" = the small typographic mark (the dot over i/j),
// plus punctuation / lettermarks. Emits 12 transparent 512 PNGs + one contact sheet into
// arg[0] (default plans/icons-v2). Reuses the glossy treatment of the finalized icon.

const int S = 512;
var outDir = args.Length > 0 ? args[0] : Path.Combine("..", "..", "plans", "icons-v2");
Directory.CreateDirectory(outDir);

SKColor C(uint hex) => new SKColor((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);

SKPath Centered(SKPath path, float target)
{
    var b = path.Bounds;
    float s = target / Math.Max(b.Width, b.Height);
    var m = SKMatrix.CreateScaleTranslation(s, s, 256 - b.MidX * s, 256 - b.MidY * s);
    var p = new SKPath(); path.Transform(m, p); return p;
}
SKPath Rot(SKPath src, float deg, float cx, float cy)
{ var m = SKMatrix.CreateRotationDegrees(deg, cx, cy); var o = new SKPath(); src.Transform(m, o); return o; }

void Gloss(SKCanvas c, SKPath path, SKColor top, SKColor bot)
{
    var b = path.Bounds;
    using (var sh = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 130), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 22) })
    { c.Save(); c.Translate(0, 16); c.DrawPath(path, sh); c.Restore(); }
    using (var p = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(b.MidX, b.Top), new SKPoint(b.MidX, b.Bottom), new[] { top, bot }, null, SKShaderTileMode.Clamp) })
        c.DrawPath(path, p);
    c.Save(); c.ClipPath(path, SKClipOperation.Intersect, true);
    using (var g = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(b.MidX, b.Top), new SKPoint(b.MidX, b.Top + b.Height * 0.52f), new[] { new SKColor(255, 255, 255, 175), new SKColor(255, 255, 255, 0) }, null, SKShaderTileMode.Clamp) })
        c.DrawRect(b, g);
    c.Restore();
    using (var r = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = new SKColor(255, 255, 255, 105) })
    { c.Save(); c.ClipPath(path, SKClipOperation.Intersect, true); c.DrawPath(path, r); c.Restore(); }
}

SKPath RRect(float l, float t, float r, float b, float rad)
{ var p = new SKPath(); p.AddRoundRect(new SKRect(l, t, r, b), rad, rad); return p; }

SKPath Drop(float cx, float cy, float r, float elong)
{
    var d = new SKPath();
    d.MoveTo(cx, cy - r * elong);
    d.CubicTo(cx + r, cy - r * 0.4f, cx + r, cy + r, cx, cy + r);
    d.CubicTo(cx - r, cy + r, cx - r, cy - r * 0.4f, cx, cy - r * elong);
    d.Close(); return d;
}

// concept name -> (path builder, top, bot)
var concepts = new (string name, Func<SKPath> build, uint top, uint bot)[]
{
    // 1. tittle-i: a lowercase i — the dot (the "tittle") oversized. The literal name.
    ("01-tittle-i", () => { var p = RRect(228, 236, 284, 404, 28); p.AddCircle(256, 150, 48); return p; }, 0x818CF8, 0x4338CA),
    // 2. jot: a single ink jot / comma (a rotated drop).
    ("02-jot", () => Rot(Drop(256, 250, 92, 1.7f), 28, 256, 256), 0xFB7185, 0xE11D48),
    // 3. t-lower: a clean lowercase t lettermark.
    ("03-t-lower", () => { var p = RRect(232, 130, 282, 392, 25); p.AddPath(RRect(194, 206, 322, 252, 23)); return p; }, 0x5EEAD4, 0x0D9488),
    // 4. T-tile: a bold uppercase T.
    ("04-T-cap", () => { var p = RRect(146, 150, 366, 208, 28); p.AddPath(RRect(228, 208, 284, 388, 24)); return p; }, 0xFCD34D, 0xF59E0B),
    // 5. colon: a typographic colon (two dots).
    ("05-colon", () => { var p = new SKPath(); p.AddCircle(256, 178, 52); p.AddCircle(256, 334, 52); return p; }, 0x7DD3FC, 0x0284C7),
    // 6. dot-line: a dot above a baseline — the mark over the text.
    ("06-dot-line", () => { var p = new SKPath(); p.AddCircle(256, 158, 60); p.AddPath(RRect(150, 322, 362, 372, 25)); return p; }, 0xC4B5FD, 0x7C3AED),
    // 7. droplet: an ink drop (writing).
    ("07-droplet", () => Drop(256, 268, 96, 1.7f), 0x67E8F9, 0x0891B2),
    // 8. quotes: closing curly quotes (two commas).
    ("08-quotes", () => { var a = Rot(Drop(214, 210, 52, 1.5f), 200, 214, 210); var b = Rot(Drop(300, 210, 52, 1.5f), 200, 300, 210); a.AddPath(b); return a; }, 0xF9A8D4, 0xDB2777),
    // 9. therefore: a three-dot mark (∴) — text/logic.
    ("09-therefore", () => { var p = new SKPath(); p.AddCircle(256, 150, 44); p.AddCircle(186, 332, 44); p.AddCircle(326, 332, 44); return p; }, 0xFDBA74, 0xEA580C),
    // 10. asterisk: a typographic asterisk (three rounded bars).
    ("10-asterisk", () => { var bar = RRect(106, 228, 406, 284, 28); var p = new SKPath(); p.AddPath(bar); p.AddPath(Rot(bar, 60, 256, 256)); p.AddPath(Rot(bar, 120, 256, 256)); return p; }, 0xFDE68A, 0xD97706),
    // 11. ellipsis: three dots in a row (…).
    ("11-ellipsis", () => { var p = new SKPath(); p.AddCircle(150, 256, 46); p.AddCircle(256, 256, 46); p.AddCircle(362, 256, 46); return p; }, 0x93C5FD, 0x3B82F6),
    // 12. i-beam: a text cursor (I-beam) — editing.
    ("12-ibeam", () => { var p = RRect(238, 150, 274, 362, 16); p.AddPath(RRect(202, 140, 310, 178, 18)); p.AddPath(RRect(202, 334, 310, 372, 18)); return p; }, 0xBEF264, 0x65A30D),
};

void SavePng(SKBitmap bmp, string file)
{ using var img = SKImage.FromBitmap(bmp); using var d = img.Encode(SKEncodedImageFormat.Png, 100); using var fs = File.OpenWrite(file); d.SaveTo(fs); }

var tiles = new List<(string name, SKBitmap bmp)>();
foreach (var c in concepts)
{
    var bmp = new SKBitmap(S, S);
    using (var cv = new SKCanvas(bmp))
    {
        cv.Clear(SKColors.Transparent);
        Gloss(cv, Centered(c.build(), 360), C(c.top), C(c.bot));
    }
    SavePng(bmp, Path.Combine(outDir, c.name + ".png"));
    tiles.Add((c.name, bmp));
}
Console.WriteLine($"wrote {tiles.Count} concept PNGs");

// contact sheet — 4 cols x 3 rows, dark rounded tiles + labels
int cols = 4, rows = 3, tileW = 330, tileH = 360, pad = 0;
using var sheet = new SKBitmap(cols * tileW, rows * tileH);
using (var cv = new SKCanvas(sheet))
{
    cv.Clear(C(0x0E0F13));
    using var label = new SKPaint { IsAntialias = true, Color = SKColors.White, TextSize = 30, TextAlign = SKTextAlign.Center, Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) };
    for (int i = 0; i < tiles.Count; i++)
    {
        int cx = (i % cols) * tileW, cy = (i / cols) * tileH;
        var tileRect = new SKRect(cx + 22, cy + 22, cx + tileW - 22, cy + tileH - 74);
        using (var tp = new SKPaint { IsAntialias = true, Color = C(0x1A1C22) })
            cv.DrawRoundRect(tileRect, 40, 40, tp);
        float ico = 216;
        var dst = new SKRect(cx + tileW / 2f - ico / 2, cy + 40, cx + tileW / 2f + ico / 2, cy + 40 + ico);
        cv.DrawBitmap(tiles[i].bmp, dst);
        cv.DrawText(tiles[i].name, cx + tileW / 2f, cy + tileH - 30, label);
    }
}
SavePng(sheet, Path.Combine(outDir, "_contact-sheet-v2.png"));
Console.WriteLine("wrote _contact-sheet-v2.png");
Console.WriteLine("done -> " + Path.GetFullPath(outDir));
