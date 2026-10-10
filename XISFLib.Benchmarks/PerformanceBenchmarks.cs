using System.Diagnostics;
using System.IO;
using System.Text.Json;
using XISFLib;

internal static class PerformanceBenchmarks
{
    public static void Run(string[] args)
    {
        int iterations = args.Length > 0 ? int.Parse(args[0]) : 5;
        if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(args));
        var results = new List<object>();
        ushort[] integers = Enumerable.Range(0, 4 * 1024 * 1024).Select(i => (ushort)i).ToArray();
        float[] floats = Enumerable.Range(0, integers.Length).Select(i => (i % 65536) / 65535f).ToArray();
        Measure("UInt16 owned samples", () => GC.KeepAlive(XisfImageData.FromSamples(integers, [2048, 2048], 1)));
        Measure("Float32 owned samples", () => GC.KeepAlive(XisfImageData.FromSamples(floats, [2048, 2048], 1)));

        var raw = XisfImageData.FromSamples(floats, [2048, 2048], 1);
        XisfImage parsed = Encode(raw, new());
        Measure("Float32 typed read", () => GC.KeepAlive(parsed.GetSamples<float>()));
        var subblocks = Encode(raw, new() { Compression = XisfCompression.Zstd, Shuffle = true, CompressionSubblockSize = 65536 });
        Measure("Zstd shuffled subblocks", () => GC.KeepAlive(subblocks.ReadRawData()));
        var matrixImage = XisfImageData.FromSamples(new byte[] { 0 }, [1, 1], 1);
        matrixImage.Properties["Benchmark:Matrix"] = new double[1024, 1024];
        Measure("Float64 matrix write", () => { using var output = new MemoryStream(); XisfWriter.Write(output, [matrixImage]); GC.KeepAlive(output.Length); });

        var lab = XisfImageData.FromSamples(Enumerable.Range(0, 256 * 256 * 3).Select(i => i % 3 == 0 ? 0.6f : 0.5f).ToArray(), [256, 256], 3, XisfPixelLayout.Interleaved);
        lab.ColorSpace = "CIELab";
        Measure("Lab rendering", () => GC.KeepAlive(lab.ToBgra32Pixels()));
        var rotated = XisfImageData.FromSamples(new byte[1024 * 1024 * 3], [1024, 1024], 3);
        rotated.Orientation = "90;flip";
        Measure("Rotated RGB rendering", () => GC.KeepAlive(rotated.ToBgra32Pixels()));
        var channels = new[] { floats.Take(262144).Select(v => (double)v).ToArray() };
        Measure("Adaptive display", () => GC.KeepAlive(XisfDisplayFunctions.CreateAdaptive(channels)));

        string file = Path.Combine("Examples", "slow.xisf");
        if (File.Exists(file))
        {
            var image = XisfReader.Load(file).Images[0];
            Measure("Real RGB zlib decode", () => GC.KeepAlive(image.ReadRawData()));
            var editable = XisfImageData.FromImage(image);
            Measure("Real RGB adaptive render", () => GC.KeepAlive(editable.ToBgra32Pixels(XisfStretchOptions.Auto)));
        }
        if (args.Length > 1) File.WriteAllText(args[1], JsonSerializer.Serialize(new { Runtime = Environment.Version.ToString(), Environment.ProcessorCount, Iterations = iterations, Results = results }, new JsonSerializerOptions { WriteIndented = true }));

        void Measure(string name, Action action)
        {
            action(); // JIT and warm file caches; no output from this run contributes to the measurements.
            var times = new double[iterations];
            var allocations = new long[iterations];
            for (int i = 0; i < iterations; i++)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long before = GC.GetTotalAllocatedBytes(true);
                var watch = Stopwatch.StartNew();
                action();
                watch.Stop();
                times[i] = watch.Elapsed.TotalMilliseconds;
                allocations[i] = GC.GetTotalAllocatedBytes(true) - before;
            }
            Array.Sort(times); Array.Sort(allocations);
            double median = times[iterations / 2];
            long bytes = allocations[iterations / 2];
            Console.WriteLine($"{name,-27} {median,9:F2} ms {bytes / 1048576d,9:F2} MiB");
            results.Add(new { Name = name, MedianMilliseconds = median, MedianAllocatedBytes = bytes });
        }
    }

    private static XisfImage Encode(XisfImageData image, XisfWriterOptions options)
    {
        using var stream = new MemoryStream();
        XisfWriter.Write(stream, [image], options);
        stream.Position = 0;
        return XisfReader.Load(stream).Images[0];
    }
}
