using System.Runtime.ExceptionServices;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XISFLib.Viewer;

namespace XISFLib.Windows.Tests;

/// <summary>Independent WIC encoders/decoder compared with the portable importer.</summary>
public sealed class RasterCompatibilityTests
{
    [Theory]
    [InlineData("png")] [InlineData("jpg")] [InlineData("tif")] [InlineData("bmp")] [InlineData("gif")]
    public void PortableDecoderAgreesWithIndependentWicFrameZero(string format)
    {
        RunSta(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "." + format);
            try
            {
                BitmapEncoder encoder = format switch { "png" => new PngBitmapEncoder(), "jpg" => new JpegBitmapEncoder { QualityLevel = 100 },
                    "tif" => new TiffBitmapEncoder(), "bmp" => new BmpBitmapEncoder(), _ => new GifBitmapEncoder() };
                // Four distinct RGB pixels prevent a channel/layout defect from cancelling itself.
                byte[] bgra = [0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 32, 64, 128, 255];
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 144, 96, PixelFormats.Bgra32, null, bgra, 8)));
                if (format is "tif" or "gif") encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 72, 72, PixelFormats.Bgra32, null, new byte[] { 255, 255, 255, 255 }, 4)));
                using (var output = File.Create(path)) encoder.Save(output);
                using var input = File.OpenRead(path);
                var frame = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                var reference = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                var referenceBytes = new byte[16]; reference.CopyPixels(referenceBytes, 8, 0);
                var actual = ViewerRasterDecoder.Load(path);
                Assert.Equal(new[] { 2, 2 }, actual.Dimensions); Assert.Equal(3, actual.ChannelCount);
                var samples = actual.GetSamples<byte>(XisfPixelLayout.Interleaved);
                for (int i = 0; i < 4; i++) for (int c = 0; c < 3; c++)
                    Assert.InRange(Math.Abs(referenceBytes[i * 4 + 2 - c] - samples[i * 3 + c]), 0, format == "jpg" ? 3 : 0);
                if (format is "png" or "tif") { Assert.Equal(frame.DpiX, actual.Resolution!.Horizontal, 1); Assert.Equal(frame.DpiY, actual.Resolution.Vertical, 1); }
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public void SixteenBitTiffAndTransparentPngRetainTheWpfImportContract()
    {
        RunSta(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".tif");
            try
            {
                ushort[] rgb = [65535, 32768, 0, 8192, 16384, 49152];
                var source = BitmapSource.Create(2, 1, 144, 96, PixelFormats.Rgb48, null, rgb, 12);
                var encoder = new TiffBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source)); using (var output = File.Create(path)) encoder.Save(output);
                var expected = new byte[8]; new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0).CopyPixels(expected, 8, 0);
                var samples = ViewerRasterDecoder.Load(path).GetSamples<byte>(XisfPixelLayout.Interleaved);
                for (int i = 0; i < 2; i++) for (int c = 0; c < 3; c++) Assert.InRange(Math.Abs(expected[i * 4 + 2 - c] - samples[i * 3 + c]), 0, 1);
                byte[] rgba = [32, 64, 128, 128]; var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 72, 72, PixelFormats.Bgra32, null, rgba, 4)));
                using (var output = File.Create(path)) png.Save(output);
                var actual = ViewerRasterDecoder.Load(path); Assert.Equal(4, actual.ChannelCount);
                Assert.Equal(new byte[] { 128, 64, 32, 128 }, actual.GetSamples<byte>());
            }
            finally { File.Delete(path); }
        });
    }
    private static void RunSta(Action action)
    {
        Exception? error = null; var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
