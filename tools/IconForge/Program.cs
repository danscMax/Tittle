using SkiaSharp;

// Finalize the chosen app icon: concept #22 — emerald glossy </> on a transparent background.
// Emits tittle.png (512, transparent) + a multi-size PNG-framed tittle.ico into arg[0]
// (default src/Tittle/Assets). The .ico embeds PNG entries (Vista+), per the icon pipeline.

const int S = 512;
var outDir = args.Length > 0 ? args[0] : Path.Combine("..", "..", "src", "Tittle", "Assets");
Directory.CreateDirectory(outDir);

SKColor C(uint hex) => new SKColor((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);

SKPath Centered(SKPath path, float target)
{
    var b = path.Bounds;
    float s = target / Math.Max(b.Width, b.Height);
    var m = SKMatrix.CreateScaleTranslation(s, s, 256 - b.MidX * s, 256 - b.MidY * s);
    var p = new SKPath(); path.Transform(m, p); return p;
}
SKPath StrokeFill(SKPath raw, float w)
{ using var sp = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = w, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round }; var o = new SKPath(); sp.GetFillPath(raw, o); return o; }

void Gloss(SKCanvas c, SKPath path, SKColor top, SKColor bot)
{
    var b = path.Bounds;
    using (var sh = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 130), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 22) })
    { c.Save(); c.Translate(0, 18); c.DrawPath(path, sh); c.Restore(); }
    using (var p = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(b.MidX, b.Top), new SKPoint(b.MidX, b.Bottom), new[] { top, bot }, null, SKShaderTileMode.Clamp) })
        c.DrawPath(path, p);
    c.Save(); c.ClipPath(path, SKClipOperation.Intersect, true);
    using (var g = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(b.MidX, b.Top), new SKPoint(b.MidX, b.Top + b.Height * 0.52f), new[] { new SKColor(255, 255, 255, 180), new SKColor(255, 255, 255, 0) }, null, SKShaderTileMode.Clamp) })
        c.DrawRect(b, g);
    c.Restore();
    using (var r = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = new SKColor(255, 255, 255, 110) })
    { c.Save(); c.ClipPath(path, SKClipOperation.Intersect, true); c.DrawPath(path, r); c.Restore(); }
}

void DrawIcon(SKCanvas c)
{
    c.Clear(SKColors.Transparent);
    using var raw = new SKPath();
    raw.MoveTo(150, 150); raw.LineTo(70, 256); raw.LineTo(150, 362);    // <
    raw.MoveTo(362, 150); raw.LineTo(442, 256); raw.LineTo(362, 362);   // >
    raw.MoveTo(300, 140); raw.LineTo(212, 372);                          // /
    Gloss(c, Centered(StrokeFill(raw, 46), 400), C(0x6EE7B7), C(0x059669));
}

// master 512
using var master = new SKBitmap(S, S);
using (var cv = new SKCanvas(master)) DrawIcon(cv);

void SavePng(SKBitmap bmp, string file)
{ using var img = SKImage.FromBitmap(bmp); using var d = img.Encode(SKEncodedImageFormat.Png, 100); using var fs = File.OpenWrite(file); d.SaveTo(fs); }

SavePng(master, Path.Combine(outDir, "tittle.png"));
Console.WriteLine("wrote tittle.png (512, transparent)");

// multi-size PNG-framed .ico
int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
var pngs = new List<byte[]>();
foreach (var sz in sizes)
{
    using var resized = master.Resize(new SKImageInfo(sz, sz), SKFilterQuality.High);
    using var img = SKImage.FromBitmap(resized);
    using var d = img.Encode(SKEncodedImageFormat.Png, 100);
    pngs.Add(d.ToArray());
}
using (var fs = new FileStream(Path.Combine(outDir, "tittle.ico"), FileMode.Create))
using (var bw = new BinaryWriter(fs))
{
    bw.Write((short)0); bw.Write((short)1); bw.Write((short)sizes.Length);   // ICONDIR
    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        int sz = sizes[i];
        bw.Write((byte)(sz >= 256 ? 0 : sz)); bw.Write((byte)(sz >= 256 ? 0 : sz)); // w,h (0 == 256)
        bw.Write((byte)0); bw.Write((byte)0);                                       // colors, reserved
        bw.Write((short)1); bw.Write((short)32);                                    // planes, bpp
        bw.Write(pngs[i].Length); bw.Write(offset);                                 // bytesInRes, offset
        offset += pngs[i].Length;
    }
    foreach (var p in pngs) bw.Write(p);
}
Console.WriteLine($"wrote tittle.ico ({string.Join(",", sizes)})");
Console.WriteLine("done");
