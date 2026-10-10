using System.Diagnostics;
using System.IO;
using XISFLib;

if (args.FirstOrDefault() == "--wpf-load")
{
    WpfLoadBenchmarks.Run(args.Skip(1).ToArray());
    return;
}

if (args.FirstOrDefault() == "--performance")
{
    PerformanceBenchmarks.Run(args.Skip(1).ToArray());
    return;
}

string examples = args.Length == 0
    ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Examples"))
    : Path.GetFullPath(args[0]);

Console.WriteLine($"Runtime: {Environment.Version}; processor count: {Environment.ProcessorCount}");
Console.WriteLine($"Examples: {examples}");
Console.WriteLine();

string xisfPath = Path.Combine(examples, "slow.xisf");
XisfDocument? xisf = null;
XisfImageData? editableXisf = null;
Measure("XISF parse", () => xisf = XisfReader.Load(xisfPath));
XisfImage benchmarkImage = xisf!.Images[0];
Console.WriteLine($"  {string.Join('x', benchmarkImage.Dimensions)}x{benchmarkImage.ChannelCount} {benchmarkImage.SampleFormat} {benchmarkImage.PixelStorage}; {benchmarkImage.Xml.Attribute("compression")?.Value ?? "uncompressed"}; {benchmarkImage.Xml.Attribute("checksum")?.Value.Split(':')[0] ?? "no checksum"}");
Measure("XISF raw decode", () => Consume(xisf!.Images[0].ReadRawData()));
Measure("XISF BGRA auto-stretch", () => Consume(xisf!.Images[0].ToBgra32Pixels(XisfStretchOptions.Auto)));
Measure("XISF editable model", () => editableXisf = XisfImageData.FromImage(xisf!.Images[0]));
Measure("XISF editable BGRA", () => Consume(editableXisf!.ToBgra32Pixels(XisfStretchOptions.Auto)));
xisf = null;

Console.WriteLine();
string fitsPath = Path.Combine(examples, "slow.fits");
FitsDocument? fits = null;
IReadOnlyList<XisfImageData>? converted = null;
Measure("FITS parse", () => fits = FitsReader.Load(fitsPath));
Measure("FITS to XISF model", () => converted = FitsConverter.ToXisf(fits!));
Measure("FITS converted BGRA", () => Consume(converted![0].ToBgra32Pixels(XisfStretchOptions.Auto)));
GC.KeepAlive(converted);

static void Consume(byte[] data)
{
    if (data.Length > 0)
        GC.KeepAlive(data[^1]);
}

static void Measure(string name, Action action)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    long allocatedBefore = GC.GetTotalAllocatedBytes(true);
    long memoryBefore = GC.GetTotalMemory(true);
    Stopwatch stopwatch = Stopwatch.StartNew();
    action();
    stopwatch.Stop();
    long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
    long retained = GC.GetTotalMemory(false) - memoryBefore;
    Console.WriteLine($"{name,-26} {stopwatch.Elapsed.TotalSeconds,8:F3} s  allocated {allocated / 1048576d,9:F1} MiB  retained {retained / 1048576d,9:F1} MiB");
}
