param(
    [Parameter(Mandatory = $true)][string]$ManifestPath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src/InfiniteDao.Game/Assets'
$previews = Join-Path $root 'artifacts/verification/art'
$decoded = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$pairs = @($decoded | Sort-Object batch)
if ($pairs.Count -ne 25 -or (@($pairs.batch | Sort-Object -Unique).Count -ne 25) -or (@($pairs.path | Sort-Object -Unique).Count -ne 25)) {
    throw 'The production manifest must contain 25 distinct character pairs.'
}
for ($i = 0; $i -lt 25; $i++) {
    if ($pairs[$i].batch -ne $i -or !(Test-Path -LiteralPath $pairs[$i].path)) { throw "Missing pair $i" }
}
New-Item -ItemType Directory -Path $previews -Force | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class CultivatorPortraitPreparation
{
    public static Rectangle Bounds(Bitmap image)
    {
        int x0 = image.Width, y0 = image.Height, x1 = -1, y1 = -1;
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
            if (image.GetPixel(x, y).A > 20)
            { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
        if (x1 < x0) throw new InvalidOperationException("Empty character panel.");
        return Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
    }
    public static void Prepare(string source, int side, string output)
    {
        using (var pair = new Bitmap(source))
        {
            if (pair.RawFormat.Guid != ImageFormat.Png.Guid || pair.GetPixel(0, 0).A != 0)
                throw new InvalidOperationException("Source must be a PNG with true alpha.");
            int split = pair.Width / 2;
            var cell = new Rectangle(side * split, 0, side == 0 ? split : pair.Width - split, pair.Height);
            using (var character = pair.Clone(cell, PixelFormat.Format32bppArgb))
            using (var result = new Bitmap(512, 768, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(result))
            {
                var bounds = Bounds(character);
                if (bounds.Left <= 1 || bounds.Right >= character.Width - 1)
                    Console.WriteLine("REVIEW_EDGE: " + output);
                float scale = Math.Min(464f / bounds.Width, 720f / bounds.Height);
                float w = bounds.Width * scale, h = bounds.Height * scale;
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(character, new RectangleF((512 - w) / 2, 24 + (720 - h) / 2, w, h), bounds, GraphicsUnit.Pixel);
                result.Save(output, ImageFormat.Png);
            }
        }
    }
    public static void Contact(string directory, string output, int first, int count, int columns)
    {
        const int cw = 230, ch = 360;
        int rows = (count + columns - 1) / columns;
        using (var result = new Bitmap(columns * cw, rows * ch, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(result))
        using (var labelFont = new Font("Arial", 15, FontStyle.Bold))
        using (var brush = new SolidBrush(Color.FromArgb(38, 61, 66)))
        using (var pen = new Pen(Color.FromArgb(214, 210, 192)))
        {
            g.Clear(Color.FromArgb(244, 240, 229));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            for (int n = 0; n < count; n++)
            {
                int id = first + n, x = n % columns * cw, y = n / columns * ch;
                using (var portrait = new Bitmap(System.IO.Path.Combine(directory, "cultivator-" + id.ToString("00") + ".png")))
                    g.DrawImage(portrait, new Rectangle(x + 7, y + 4, 216, 324));
                g.DrawString(id.ToString("00"), labelFont, brush, x + 100, y + 330);
                g.DrawRectangle(pen, x, y, cw - 1, ch - 1);
            }
            result.Save(output, ImageFormat.Png);
        }
    }
}
'@

foreach ($pair in $pairs) {
    for ($side = 0; $side -lt 2; $side++) {
        $id = [int]$pair.batch * 2 + $side
        $destination = Join-Path $assets ('cultivator-{0:00}.png' -f $id)
        [CultivatorPortraitPreparation]::Prepare($pair.path, $side, $destination)
    }
}
[CultivatorPortraitPreparation]::Contact($assets, (Join-Path $previews 'PORTRAIT_CONTACT_SHEET.png'), 0, 50, 10)
for ($group = 0; $group -lt 5; $group++) {
    [CultivatorPortraitPreparation]::Contact($assets, (Join-Path $previews ('PORTRAIT_REVIEW_{0:00}.png' -f $group)), $group * 10, 10, 5)
}
Write-Output 'Prepared 50 transparent 512x768 character PNGs and six review sheets.'
