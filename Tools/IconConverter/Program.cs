using SkiaSharp;

if (args.Length < 2)
{
    Console.WriteLine("Usage: IconConverter <input.png> <output.ico>");
    Console.WriteLine("  Generates 16x16, 32x32, 48x48 sizes from the input PNG.");
    return 1;
}

string inputPath = args[0];
string outputPath = args[1];

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input file not found: {inputPath}");
    return 1;
}

int[] sizes = { 16, 32, 48 };

using var inputStream = File.OpenRead(inputPath);
using var originalBmp = SKBitmap.Decode(inputStream);
if (originalBmp == null)
{
    Console.Error.WriteLine("Failed to decode PNG.");
    return 1;
}

var dibDataList = new (int size, byte[] dib)[sizes.Length];

for (int i = 0; i < sizes.Length; i++)
{
    int size = sizes[i];
    using var resized = originalBmp.Resize(new SKSizeI(size, size), SKFilterQuality.High);
    dibDataList[i] = (size, BuildDib(resized));
}

using var outStream = File.Create(outputPath);
using var writer = new BinaryWriter(outStream);

// ICO header
writer.Write((ushort)0);   // reserved
writer.Write((ushort)1);   // type: ICO
writer.Write((ushort)sizes.Length);

// Directory entries + data
uint offset = (uint)(6 + sizes.Length * 16);
uint[] offsets = new uint[sizes.Length];
offsets[0] = offset;
for (int i = 1; i < sizes.Length; i++)
    offsets[i] = offsets[i - 1] + (uint)dibDataList[i - 1].dib.Length;

for (int i = 0; i < sizes.Length; i++)
{
    var (size, dib) = dibDataList[i];
    writer.Write((byte)(size >= 256 ? 0 : size));
    writer.Write((byte)(size >= 256 ? 0 : size));
    writer.Write((byte)0);     // color count
    writer.Write((byte)0);     // reserved
    writer.Write((ushort)1);   // planes
    writer.Write((ushort)32);  // bpp
    writer.Write((uint)dib.Length);
    writer.Write(offsets[i]);
}

foreach (var (_, dib) in dibDataList)
    writer.Write(dib);

Console.WriteLine($"Created: {outputPath} ({sizes.Length} sizes, BMP DIB)");
return 0;

static byte[] BuildDib(SKBitmap bmp)
{
    int w = bmp.Width;
    int h = bmp.Height;

    int xorRowBytes = w * 4;
    int xorSize = xorRowBytes * h;
    int andRowBytes = ((w + 31) / 32) * 4;
    int andSize = andRowBytes * h;

    var ms = new MemoryStream();
    var bw = new BinaryWriter(ms);

    // BITMAPINFOHEADER
    bw.Write((uint)40);       // biSize
    bw.Write((int)w);         // biWidth
    bw.Write((int)(h * 2));   // biHeight (XOR + AND)
    bw.Write((ushort)1);      // biPlanes
    bw.Write((ushort)32);     // biBitCount
    bw.Write((uint)0);        // biCompression = BI_RGB
    bw.Write((uint)(xorSize + andSize)); // biSizeImage
    bw.Write((int)0);         // biXPelsPerMeter
    bw.Write((int)0);         // biYPelsPerMeter
    bw.Write((uint)0);        // biClrUsed
    bw.Write((uint)0);        // biClrImportant

    // XOR rows: BGRA, bottom-up
    for (int row = h - 1; row >= 0; row--)
        for (int col = 0; col < w; col++)
        {
            var pixel = bmp.GetPixel(col, row);
            bw.Write(pixel.Blue);
            bw.Write(pixel.Green);
            bw.Write(pixel.Red);
            bw.Write(pixel.Alpha);
        }

    // AND mask: all zeros for 32bpp
    for (int i = 0; i < andSize; i++)
        bw.Write((byte)0);

    bw.Flush();
    return ms.ToArray();
}
