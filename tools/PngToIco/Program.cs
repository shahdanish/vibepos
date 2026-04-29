using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

// Converts app-icon.png → app.ico with multiple sizes for best quality
var pngPath = args.Length > 0 ? args[0] : "app-icon.png";
var icoPath = args.Length > 1 ? args[1] : "app.ico";

if (!File.Exists(pngPath))
{
    Console.WriteLine($"Not found: {pngPath}");
    return;
}

using var src = new Bitmap(pngPath);
var sizes = new[] { 16, 32, 48, 256 };

// Generate PNG data for each size (ICO supports embedded PNGs)
var pngDataList = new List<byte[]>();
foreach (var size in sizes)
{
    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
    g.DrawImage(src, 0, 0, size, size);
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    pngDataList.Add(ms.ToArray());
}

// Write ICO file with embedded PNG images
using var outStream = File.Create(icoPath);
using var writer = new BinaryWriter(outStream);

// ICO Header
writer.Write((ushort)0);              // Reserved
writer.Write((ushort)1);              // Type = ICO
writer.Write((ushort)sizes.Length);   // Number of images

// Calculate data offset: header(6) + directory entries(16 * count)
int dataOffset = 6 + (16 * sizes.Length);

// Directory entries
for (int i = 0; i < sizes.Length; i++)
{
    var s = sizes[i];
    writer.Write((byte)(s >= 256 ? 0 : s));   // Width (0 = 256)
    writer.Write((byte)(s >= 256 ? 0 : s));   // Height (0 = 256)
    writer.Write((byte)0);                      // Color palette
    writer.Write((byte)0);                      // Reserved
    writer.Write((ushort)1);                    // Color planes
    writer.Write((ushort)32);                   // Bits per pixel
    writer.Write((uint)pngDataList[i].Length);  // Image size
    writer.Write((uint)dataOffset);             // Image offset
    dataOffset += pngDataList[i].Length;
}

// Image data
foreach (var data in pngDataList)
{
    writer.Write(data);
}

Console.WriteLine($"Created {icoPath} ({sizes.Length} sizes: {string.Join(", ", sizes)})");
