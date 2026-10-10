using System.Diagnostics;
using System.IO;
using System.Text.Json;
using XISFLib;

internal static class FitsPerformanceBenchmarks
{
    public static void Run(string[] args)
    {
        int iterations = args.Length > 0 ? int.Parse(args[0]) : 5;
        if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(args));
        var results = new List<object>();
        foreach (string path in Directory.EnumerateFiles("Examples", "*.fits", SearchOption.AllDirectories).Order())
        {
            Measure(path, "Load", () => GC.KeepAlive(FitsReader.Load(path)));
            var document = FitsReader.Load(path);
            var image = document.Images.First();
            Console.WriteLine($"  {string.Join('x', image.Dimensions)}, BITPIX {image.BitPix}, BSCALE {image.BScale}, BZERO {image.BZero}, checksums {document.Hdus.Any(h => h.ChecksumValid.HasValue || h.DataChecksumValid.HasValue)}");
            Measure(path, "Convert to XISF", () => GC.KeepAlive(FitsConverter.ToXisf(document, new() { AxisInterpretation = FitsAxisInterpretation.RgbOrRgba, LossPolicy = ConversionLossPolicy.Allow })));
            var converted = FitsConverter.ToXisf(document, new() { AxisInterpretation = FitsAxisInterpretation.RgbOrRgba, LossPolicy = ConversionLossPolicy.Allow });
            Measure(path, "Render", () => GC.KeepAlive(converted[0].ToBgra32Pixels(XisfStretchOptions.Auto)));
        }

        const int count = 4 * 1024 * 1024;
        float[] floats = Enumerable.Range(0, count).Select(i => i / (float)count).ToArray();
        ushort[] unsigned = Enumerable.Range(0, count).Select(i => (ushort)i).ToArray();
        Measure("synthetic", "Encode Float32", () => GC.KeepAlive(FitsImage.FromPhysicalSamples(floats, [2048, 2048])));
        Measure("synthetic", "Encode UInt16", () => GC.KeepAlive(FitsImage.FromPhysicalSamples(unsigned, [2048, 2048])));
        var scaled = FitsImage.FromRaw(FitsImage.FromPhysicalSamples(unsigned, [2048, 2048]).ReadRawData(), 16, [2048, 2048], 0.5, 500, -32768);
        Measure("synthetic", "Scaled conversion", () => GC.KeepAlive(FitsConverter.ToXisf(scaled, new() { AxisInterpretation = FitsAxisInterpretation.RgbOrRgba, LossPolicy = ConversionLossPolicy.Allow })));
        var checksumDocument = new FitsDocument([FitsHdu.CreateImage(FitsImage.FromPhysicalSamples(floats, [2048, 2048]), true)]);
        Measure("synthetic", "Write checksums", () => { using var stream = new MemoryStream(); FitsWriter.Write(stream, checksumDocument, new() { WriteChecksums = true }); });
        using var checkedStream = new MemoryStream();
        FitsWriter.Write(checkedStream, checksumDocument, new() { WriteChecksums = true });
        byte[] checkedFile = checkedStream.ToArray();
        Measure("synthetic", "Read checksums", () => { using var stream = new MemoryStream(checkedFile, false); GC.KeepAlive(FitsReader.Load(stream)); });
        if (args.Length > 1) File.WriteAllText(args[1], JsonSerializer.Serialize(new { Runtime = Environment.Version.ToString(), Environment.ProcessorCount, Iterations = iterations, Results = results }, new JsonSerializerOptions { WriteIndented = true }));

        void Measure(string file, string operation, Action action)
        {
            action();
            var times = new double[iterations];
            var allocations = new long[iterations];
            for (int i = 0; i < iterations; i++)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long before = GC.GetTotalAllocatedBytes(true);
                var watch = Stopwatch.StartNew(); action(); watch.Stop();
                times[i] = watch.Elapsed.TotalMilliseconds;
                allocations[i] = GC.GetTotalAllocatedBytes(true) - before;
            }
            Array.Sort(times); Array.Sort(allocations);
            Console.WriteLine($"{Path.GetFileName(file)} {operation}: {times[iterations / 2]:F2} ms, {allocations[iterations / 2] / 1048576d:F2} MiB");
            results.Add(new { File = file, Operation = operation, MedianMilliseconds = times[iterations / 2], MedianAllocatedBytes = allocations[iterations / 2] });
        }
    }
}
