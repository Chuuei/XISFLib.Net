using ImageMagick;
using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public class ViewerFinishTests
{
    private sealed class ProgressReceiver(Action<FinishProgress> callback) : IProgress<FinishProgress>
    {
        public void Report(FinishProgress update) => callback(update);
    }

    [Fact]
    public void ProgressDescribesCreationAndExportAndCancellationBeforePublicationPreservesDestination()
    {
        var updates = new List<FinishProgress>();
        var progress = new ProgressReceiver(updates.Add);
        var picture = ViewerFinishService.Create(XisfImageData.FromSamples(
            Enumerable.Range(0, 1024).Select(i => (ushort)(1000 + i * 4)).ToArray(), [32, 32], 1), false, default, progress);
        Assert.Contains(updates, u => u.Stage == "Stretching image" && u.Percentage == 0);
        Assert.Contains(updates, u => u.Stage == "Smoothing background noise");
        Assert.Equal("Preparing finished preview", updates[^1].Stage);
        Assert.All(updates.Where(u => u.Percentage is not null), u => Assert.InRange(u.Percentage!.Value, 0, 100));
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            updates.Clear(); ViewerFinishService.Save(path, picture, default, progress);
            Assert.Contains(updates, u => u.Stage == "Encoding PNG");
            Assert.Equal("Publishing saved image", updates[^1].Stage);
            byte[] original = File.ReadAllBytes(path);
            using var cancellation = new CancellationTokenSource();
            var cancelProgress = new ProgressReceiver(update =>
            {
                if (update.Stage == "Publishing saved image") cancellation.Cancel();
            });
            Assert.Throws<OperationCanceledException>(() => ViewerFinishService.Save(path, picture, cancellation.Token, cancelProgress));
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.*.tmp"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ProcessingStateIsVisibleDuringCreationAndSavingAndClearsAfterCancellation()
    {
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf"), export = source + ".jpg";
        try
        {
            XisfWriter.Write(source, [XisfImageData.FromSamples(new byte[] { 10, 20 }, [2, 1], 1)]);
            var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = source };
            await vm.LoadCommand.ExecuteAsync(null);
            var operations = new List<string>();
            vm.Finish.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ViewerFinishState.IsProcessing) || !vm.Finish.IsProcessing) return;
                operations.Add(vm.Finish.ProgressText);
                Assert.True(vm.Finish.IsIndeterminate);
                Assert.False(vm.Finish.CanSave);
            };
            await vm.Finish.CreateCommand.ExecuteAsync(null);
            Assert.False(vm.Finish.IsProcessing); Assert.True(vm.Finish.CanSave);
            await vm.SaveFinishedAsync(export);
            Assert.False(vm.Finish.IsProcessing); Assert.True(vm.Finish.CanSave);
            Assert.Equal(new[] { "Preparing finished image…", "Preparing JPEG export…" }, operations);
            vm.Finish.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewerFinishState.IsProcessing) && vm.Finish.IsProcessing)
                    vm.Finish.CancelCommand.Execute(null);
            };
            await vm.Finish.CreateCommand.ExecuteAsync(null);
            Assert.False(vm.Finish.IsProcessing); Assert.False(vm.IsBusy);
            Assert.Contains("cancelled", vm.Status);
            Assert.True(vm.Finish.CanSave); // Retain the previous reviewed picture when recreation is cancelled.
        }
        finally { File.Delete(source); File.Delete(export); }
    }

    [Theory]
    [InlineData(XisfPixelLayout.Planar, false)]
    [InlineData(XisfPixelLayout.Interleaved, true)]
    public void GradientReductionPreservesDarkCloudEmissionAndAnExtremeStar(XisfPixelLayout layout, bool bigEndian)
    {
        const int width = 240, height = 192;
        var random = new Random(17);
        var samples = new float[width * height * 3];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        for (int c = 0; c < 3; c++)
        {
            double background = 0.344 + c * 0.002 + (0.008 + c * 0.002) * (x / (double)width - 0.5)
                + 0.003 * (y / (double)height - 0.5) + (random.NextDouble() - 0.5) * 0.002;
            double star = 0.6 * Math.Exp(-((x - 175d) * (x - 175) + (y - 105d) * (y - 105)) / 50);
            double cloud = x >= 95 && x < 125 && y >= 80 && y < 115 ? -0.006 : 0;
            double emission = x >= 95 && x < 125 && y >= 50 && y < 70 ? 0.012 : 0;
            int pixel = y * width + x;
            samples[layout == XisfPixelLayout.Planar ? c * width * height + pixel : pixel * 3 + c] = (float)(background + star + cloud + emission);
        }
        var image = XisfImageData.FromSamples(samples, [width, height], 3, layout, bigEndian);
        byte[] original = image.RawData.ToArray();
        var result = ViewerFinishService.Create(image);
        Assert.Contains("conservative gradient reduction", result.Summary);
        double leftSky = MeanRegion(result, 15, 20, 20, 20), rightSky = MeanRegion(result, 205, 20, 20, 20);
        Assert.InRange(Math.Abs(leftSky - rightSky), 0, 45);
        double sky = MeanRegion(result, 95, 130, 30, 20), cloudLevel = MeanRegion(result, 100, 85, 20, 25);
        Assert.True(cloudLevel < sky - 10, $"Dark cloud must remain darker than sky: {cloudLevel} vs {sky}.");
        Assert.True(MeanRegion(result, 100, 53, 20, 14) > sky + 60);
        Assert.InRange((int)result.Pixels[(105 * width + 175) * 4], 230, 255);
        Assert.Equal(original, image.RawData.ToArray());
        Assert.DoesNotContain("gradient reduction", ViewerFinishService.Create(image, true).Summary);
    }

    [Fact]
    public void IsolatedNebulaAndStarDoNotTriggerGradientReductionInFlatSky()
    {
        const int width = 192, height = 192;
        var random = new Random(18);
        var samples = new float[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            samples[y * width + x] = 0.02f + (float)((random.NextDouble() - 0.5) * 0.001
                + 0.06 * Math.Exp(-((x - 65d) * (x - 65) + (y - 110d) * (y - 110)) / 180)
                + 0.9 * Math.Exp(-((x - 140d) * (x - 140) + (y - 50d) * (y - 50)) / 20));
        var result = ViewerFinishService.Create(XisfImageData.FromSamples(samples, [width, height], 1));
        Assert.DoesNotContain("gradient reduction", result.Summary);
        Assert.InRange(MeanRegion(result, 10, 10, 30, 30), 15, 35);
    }

    [Theory]
    [InlineData(0.344f)]
    [InlineData(0.7f)]
    public void NarrowLinearSignalWithHighBackgroundGetsStretchedWithoutInvertingDetail(float background)
    {
        const int width = 128, height = 96;
        var random = new Random(42);
        var samples = new float[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            samples[y * width + x] = background + (float)((random.NextDouble() - 0.5) * 0.004) + (x >= 50 && x < 80 && y >= 30 && y < 60 ? 0.015f : 0);
        var image = XisfImageData.FromSamples(samples, [width, height], 1);
        var original = image.RawData.ToArray();
        var result = ViewerFinishService.Create(image);
        Assert.Contains("Automatic stretch", result.Summary);
        double sky = MeanRegion(result, 5, 5, 25, 25), signal = MeanRegion(result, 55, 35, 20, 20);
        Assert.InRange(sky, 20, 60);
        Assert.True(signal - sky > 60, $"Expected resolved faint detail, got background {sky} and signal {signal}.");
        Assert.Equal(original, image.RawData.ToArray());
        var imported = ViewerFinishService.Create(image, true);
        Assert.Contains("Existing exposure retained", imported.Summary);
        Assert.InRange(MeanRegion(imported, 5, 5, 25, 25), background * 255 - 2, background * 255 + 2);
    }

    [Fact]
    public void BroadAlreadyDevelopedScientificImageKeepsItsExposure()
    {
        float[] samples = Enumerable.Range(0, 10000).Select(i => 0.1f + (i % 100) / 100f * 0.7f).ToArray();
        var result = ViewerFinishService.Create(XisfImageData.FromSamples(samples, [100, 100], 1));
        Assert.Contains("Existing exposure retained", result.Summary);
    }

    private static double MeanRegion(FinishedPicture image, int x, int y, int width, int height)
    {
        double sum = 0;
        for (int row = y; row < y + height; row++)
        for (int col = x; col < x + width; col++) sum += image.Pixels[(row * image.Width + col) * 4];
        return sum / (width * height);
    }

    [Fact]
    public void BlankBordersAreRemovedBeforeStretchWithoutModifyingTheOriginal()
    {
        const int width = 80, height = 70;
        var samples = new ushort[width * height];
        for (int y = 10; y < height - 10; y++)
        for (int x = 10; x < width - 10; x++) samples[y * width + x] = (ushort)(1000 + (x * 7 + y * 11) % 100);
        samples[35 * width + 40] = 40000;
        var image = XisfImageData.FromSamples(samples, [width, height], 1);
        image.Properties["Test:Original"] = "retained";
        byte[] original = image.RawData.ToArray();
        var result = ViewerFinishService.Create(image);
        Assert.Equal(60, result.Width); Assert.Equal(50, result.Height);
        Assert.Contains("Automatic stretch", result.Summary);
        Assert.InRange(result.Pixels.Where((_, i) => i % 4 == 0).Average(b => (double)b), 20, 70);
        Assert.Equal(original, image.RawData.ToArray()); Assert.Equal("retained", image.Properties["Test:Original"]);
        Assert.All(result.Pixels.Where((_, i) => i % 4 == 3), alpha => Assert.Equal(255, alpha));
    }

    [Fact]
    public void LargePopulatedFrameOnlyGetsSmallEdgeTidyAndNegativeSkyIsNotBlank()
    {
        var image = XisfImageData.FromSamples(Enumerable.Repeat(0.3f, 300 * 280).ToArray(), [300, 280], 1);
        var result = ViewerFinishService.Create(image);
        Assert.Equal(294, result.Width); Assert.Equal(276, result.Height);
        Assert.Contains("Existing exposure retained", result.Summary);
        float[] negative = Enumerable.Repeat(-0.1f, 80 * 70).ToArray(); negative[35 * 80 + 40] = 0.8f;
        result = ViewerFinishService.Create(XisfImageData.FromSamples(negative, [80, 70], 1));
        Assert.Equal(80, result.Width); Assert.Equal(70, result.Height);
    }

    [Fact]
    public void ImportedRasterKeepsItsExposureAndFlatBlackImagesKeepTheirFrame()
    {
        var dark = XisfImageData.FromSamples(Enumerable.Repeat((byte)20, 25).ToArray(), [5, 5], 1);
        var result = ViewerFinishService.Create(dark, true);
        Assert.Contains("Existing exposure retained", result.Summary);
        Assert.InRange((int)result.Pixels[0], 19, 21);
        result = ViewerFinishService.Create(XisfImageData.FromSamples(new byte[400], [20, 20], 1));
        Assert.Equal(20, result.Width); Assert.Equal(20, result.Height);
        Assert.All(result.Pixels.Where((_, i) => i % 4 != 3), b => Assert.Equal(0, b));
    }

    [Theory]
    [InlineData(XisfPixelLayout.Planar, false)]
    [InlineData(XisfPixelLayout.Planar, true)]
    [InlineData(XisfPixelLayout.Interleaved, false)]
    [InlineData(XisfPixelLayout.Interleaved, true)]
    public void ColorLayoutEndianAndOrientationMatchTheDisplayContract(XisfPixelLayout layout, bool bigEndian)
    {
        ushort[] samples = layout == XisfPixelLayout.Planar ? [50000, 10000, 20000, 40000, 30000, 60000] : [50000, 20000, 30000, 10000, 40000, 60000];
        var image = XisfImageData.FromSamples(samples, [2, 1], 3, layout, bigEndian);
        image.Orientation = "90;flip"; image.Resolution = new(100, 150, "inch");
        var result = ViewerFinishService.Create(image, true);
        var expected = XisfDisplayRenderer.Render(image);
        Assert.Equal(expected.Width, result.Width); Assert.Equal(expected.Height, result.Height);
        Assert.Equal(expected.Pixels, result.Pixels);
        Assert.Equal(150, result.DpiX); Assert.Equal(100, result.DpiY);
    }

    [Fact]
    public void TransparentBordersAreCroppedAndAlphaIsFlattenedOnBlack()
    {
        var rgba = new byte[10 * 10 * 4];
        for (int y = 1; y < 9; y++)
        for (int x = 1; x < 9; x++)
        {
            int i = (y * 10 + x) * 4; rgba[i] = 200; rgba[i + 1] = 100; rgba[i + 2] = 50; rgba[i + 3] = 128;
        }
        var result = ViewerFinishService.Create(XisfImageData.FromSamples(rgba, [10, 10], 4, XisfPixelLayout.Interleaved), true);
        Assert.Equal(8, result.Width); Assert.Equal(8, result.Height);
        Assert.InRange((int)result.Pixels[2], 98, 105); Assert.Equal(255, result.Pixels[3]);
    }

    [Fact]
    public void PngMatchesReviewedPixelsAndJpegHasTheSameDimensions()
    {
        string directory = Path.Combine(Path.GetTempPath(), "finish-test-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        try
        {
            var result = ViewerFinishService.Create(XisfImageData.FromSamples(Enumerable.Range(0, 1024).Select(i => (ushort)(1000 + i * 4)).ToArray(), [32, 32], 1));
            foreach (string extension in new[] { ".png", ".jpg", ".jpeg" })
            {
                string path = Path.Combine(directory, "output" + extension); ViewerFinishService.Save(path, result);
                using var saved = new MagickImage(path);
                Assert.Equal((uint)result.Width, saved.Width); Assert.Equal((uint)result.Height, saved.Height);
                Assert.Equal(8u, saved.Depth);
                if (extension == ".png") { using var pixels = saved.GetPixels(); Assert.Equal(result.Pixels, pixels.ToByteArray(PixelMapping.BGRA)); }
                else Assert.Equal(MagickFormat.Jpeg, saved.Format);
            }
            string existing = Path.Combine(directory, "existing.png"); File.WriteAllText(existing, "original");
            Assert.Throws<OperationCanceledException>(() => ViewerFinishService.Save(existing, result, new CancellationToken(true)));
            Assert.Equal("original", File.ReadAllText(existing));
            Assert.Throws<ArgumentException>(() => ViewerFinishService.Save(existing, result with { Pixels = [0] }));
            Assert.Equal("original", File.ReadAllText(existing));
            Assert.Throws<NotSupportedException>(() => ViewerFinishService.Save(Path.Combine(directory, "bad.gif"), result));
            Assert.DoesNotContain(Directory.GetFiles(directory), p => p.EndsWith(".tmp"));
        }
        finally { foreach (string file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
    }

    [Fact]
    public void UnavailableAndUnsupportedInputAndCancellationAreExplicit()
    {
        var nan = XisfImageData.FromSamples(Enumerable.Repeat(float.NaN, 64).ToArray(), [8, 8], 1);
        var result = ViewerFinishService.Create(nan);
        Assert.All(result.Pixels.Where((_, i) => i % 4 != 3), b => Assert.Equal(0, b));
        Assert.Throws<OperationCanceledException>(() => ViewerFinishService.Create(nan, false, new CancellationToken(true)));
        var complex = XisfImageData.FromSamples(new System.Numerics.Complex[] { new(1, 2) }, [1, 1], 1);
        Assert.Throws<NotSupportedException>(() => ViewerFinishService.Create(complex));
        var volume = XisfImageData.FromSamples(new byte[] { 1 }, [1, 1, 1], 1);
        Assert.Throws<NotSupportedException>(() => ViewerFinishService.Create(volume));
        var cfa = XisfImageData.FromSamples(new byte[] { 1, 2, 3, 4 }, [2, 2], 1);
        cfa.ColorFilterArray = new("RGGB", 2, 2, "Bayer");
        Assert.Contains("debayered", Assert.Throws<NotSupportedException>(() => ViewerFinishService.Create(cfa)).Message);
    }

    [Fact]
    public void AutomaticBackgroundBalanceReducesCastInDarkRgbData()
    {
        var samples = new float[32 * 32 * 3];
        for (int i = 0; i < 32 * 32; i++) { samples[i * 3] = 0.05f; samples[i * 3 + 1] = 0.1f; samples[i * 3 + 2] = 0.15f; }
        var image = XisfImageData.FromSamples(samples, [32, 32], 3, XisfPixelLayout.Interleaved);
        var picture = ViewerFinishService.Create(image);
        Assert.Contains("background color balance", picture.Summary);
        Assert.InRange(Math.Abs(picture.Pixels[0] - picture.Pixels[1]), 0, 1);
        Assert.InRange(Math.Abs(picture.Pixels[2] - picture.Pixels[1]), 0, 1);
    }

    [Fact]
    public void CosmeticCleanupReducesBackgroundGrainAndPreservesBrightStarDetail()
    {
        var random = new Random(42);
        byte[] values = Enumerable.Range(0, 128 * 128).Select(_ => (byte)Math.Clamp(Math.Round(80 + 5 * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble())), 0, 255)).ToArray();
        values[90 * 128 + 90] = 255;
        var picture = ViewerFinishService.Create(XisfImageData.FromSamples(values, [128, 128], 1), true);
        double[] cleaned = Enumerable.Range(0, 64 * 64).Select(i => (double)picture.Pixels[((i / 64) * 128 + i % 64) * 4]).ToArray();
        double mean = cleaned.Average(), sigma = Math.Sqrt(cleaned.Average(v => (v - mean) * (v - mean)));
        Assert.InRange(mean, 79, 81); Assert.InRange(sigma, 0, 4.5);
        Assert.InRange((int)picture.Pixels[(90 * 128 + 90) * 4], 220, 255);
    }

    [Fact]
    public async Task WorkbenchFinishingDoesNotApplyDraftsAndInvalidatesOnImageChanges()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf"), export = path + ".png";
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new byte[] { 10, 20 }, [2, 1], 1), XisfImageData.FromSamples(new byte[] { 40 }, [1, 1], 1)]);
            FinishedPicture? presented = null;
            var vm = new WorkbenchViewModel(_ => Task.CompletedTask) { FilePath = path };
            vm.Finish.Present = picture => presented = picture;
            Assert.False(vm.Finish.CanSave); Assert.False(vm.Finish.CreateCommand.CanExecute(null));
            await vm.LoadCommand.ExecuteAsync(null); vm.ImageEditor.Id = "unfinished draft";
            await vm.Finish.CreateCommand.ExecuteAsync(null);
            Assert.NotNull(presented); Assert.True(vm.Finish.CanSave); Assert.Null(vm.SelectedImage!.Id);
            var picture = presented; await vm.ApplyCommand.ExecuteAsync(null); Assert.Same(picture, presented);
            await vm.SaveFinishedAsync(export); Assert.True(File.Exists(export)); Assert.Equal(path, vm.FilePath);
            vm.SelectedImageIndex = 1; await vm.SelectImageCommand.ExecuteAsync(null); Assert.Null(presented); Assert.False(vm.Finish.HasPicture);
            await vm.Finish.CreateCommand.ExecuteAsync(null);
            await vm.ApplyMetadataCommand.ExecuteAsync(null); Assert.Null(presented);
            await vm.Finish.CreateCommand.ExecuteAsync(null); await vm.UnloadCommand.ExecuteAsync(null); Assert.False(vm.Finish.HasPicture);
        }
        finally { File.Delete(path); File.Delete(export); }
    }

    [Fact]
    public async Task RasterOriginalCannotBeOverwrittenByFinishedExport()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            using (var image = new MagickImage(MagickColors.Red, 4, 4)) image.Write(path);
            byte[] original = File.ReadAllBytes(path);
            var vm = new WorkbenchViewModel(_ => Task.CompletedTask); await vm.ImportAsync(path);
            await vm.Finish.CreateCommand.ExecuteAsync(null); await vm.SaveFinishedAsync(path);
            Assert.Contains("different filename", vm.Status); Assert.Equal(original, File.ReadAllBytes(path));
            Assert.True(vm.Finish.CanSave);
        }
        finally { File.Delete(path); }
    }
}
