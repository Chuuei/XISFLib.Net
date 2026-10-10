using System.Text;

namespace XISFLib.Tests;

public class ReviewRegressionTests
{
    [Fact]
    public void SavingLoadedDocumentToItsSourcePreservesPixels()
    {
        string path = Path.GetTempFileName();
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new ushort[] { 17, 65535 }, [2, 1], 1)]);
            XisfWriter.Write(path, XisfReader.Load(path));
            Assert.Equal(new ushort[] { 17, 65535 }, XisfReader.Load(path).Images[0].GetSamples<ushort>());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FailedWriteLeavesExistingDestinationIntact()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "original");
            Assert.Throws<ArgumentException>(() => XisfWriter.Write(path, new XisfImageData[] { null! }));
            Assert.Equal("original", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("2,2147483647")]
    [InlineData("2,1:0,1")]
    public void SubblockOutputCannotExceedDeclaredTotal(string subblocks)
    {
        using MemoryStream input = Xml($"<Image geometry=\"1:1\" sampleFormat=\"UInt8\" location=\"inline:hex\" compression=\"lz4:1\" subblocks=\"{subblocks}\">1000</Image>");
        Assert.Throws<XisfFormatException>(() => XisfReader.Load(input).Images[0].ReadRawData());
    }

    [Fact]
    public void MonolithicStreamBufferingIsBounded()
    {
        using MemoryStream input = new();
        XisfWriter.Write(input, [XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1)]);
        input.Write(new byte[4096]);
        input.Position = 0;
        Assert.Throws<XisfFormatException>(() => XisfReader.Load(input,
            options: new XisfReaderOptions { MaximumBufferedInputBytes = 1024 }));
    }

    [Fact]
    public void UrlPrefixPreservesTheScheme()
    {
        using MemoryStream input = Xml("<Image geometry=\"1:1\" sampleFormat=\"UInt8\" location=\"url(file:///unused)\" />");
        XisfImage image = XisfReader.Load(input, options: new XisfReaderOptions { AllowNetworkResources = true }).Images[0];
        // File URLs are filesystem resources and require the filesystem policy, independently of HTTP access.
        Assert.Contains("External file", Assert.Throws<XisfFormatException>(() => image.ReadRawData()).Message);
    }

    [Fact]
    public void Unsigned64BitFitsRoundTripPreservesBoundaryValues()
    {
        ulong[] samples = [0, 1, (1UL << 53) + 1, (1UL << 63) + 1, ulong.MaxValue];
        FitsImage original = FitsImage.FromPhysicalSamples(samples, [samples.Length, 1]);
        XisfImageData converted = FitsConverter.ToXisf(original);
        Assert.Equal(original.ReadRawData(), FitsConverter.FromXisf([converted]).Images.Single().ReadRawData());
        using MemoryStream stream = new();
        XisfWriter.Write(stream, [converted]);
        stream.Position = 0;
        Assert.Equal(original.ReadRawData(), FitsConverter.FromXisf(XisfReader.Load(stream).Images).Images.Single().ReadRawData());
    }

    [Fact]
    public async Task PreCancelledLoadsDoNotOpenFiles()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => XisfReader.LoadAsync("missing.xisf", cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FitsReader.LoadAsync("missing.fits", cancellationToken: cancellation.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringInputReadingStopsLoadAndLeavesStreamOpen(bool fits)
    {
        using CancellationTokenSource cancellation = new();
        byte[] bytes;
        if (fits)
        {
            using MemoryStream output = new();
            FitsWriter.Write(output, new FitsDocument([FitsHdu.CreateImage(FitsImage.FromPhysicalSamples(new byte[] { 42 }, [1, 1]), true)]));
            bytes = output.ToArray();
        }
        else
        {
            using MemoryStream input = Xml("<Image geometry=\"1:1\" sampleFormat=\"UInt8\" location=\"inline:hex\">2a</Image>");
            bytes = input.ToArray();
        }
        using CancelAfterReadStream stream = new(bytes, cancellation);
        if (fits)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FitsReader.LoadAsync(stream, cancellationToken: cancellation.Token));
        else
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => XisfReader.LoadAsync(stream, cancellationToken: cancellation.Token));
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(9007199254740993L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue + 1)]
    public void InexactSigned64BitFitsConversionFailsExplicitly(long sample)
    {
        FitsImage image = FitsImage.FromPhysicalSamples(new[] { sample }, [1, 1]);
        Assert.Throws<NotSupportedException>(() => FitsConverter.ToXisf(image));
    }

    [Fact]
    public void ValidSubblocksDecodeWithinTheDeclaredBudget()
    {
        // Each hand-encoded LZ4 block contains one literal byte.
        using MemoryStream input = Xml("<Image geometry=\"2:1\" sampleFormat=\"UInt8\" location=\"inline:hex\" compression=\"lz4:2\" subblocks=\"2,1:2,1\">102a102b</Image>");
        Assert.Equal(new byte[] { 42, 43 }, XisfReader.Load(input).Images[0].ReadRawData());
    }

    [Fact]
    public async Task CancelledFitsWritePreservesDestination()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "original");
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FitsWriter.WriteAsync(path,
                new FitsDocument([FitsHdu.CreatePrimary()]), cancellationToken: cancellation.Token));
            Assert.Equal("original", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FailedFitsWritePreservesDestination()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "original");
            Assert.Throws<ArgumentNullException>(() => FitsWriter.Write(path, null!));
            Assert.Equal("original", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    private sealed class CancelAfterReadStream(byte[] bytes, CancellationTokenSource cancellation) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, count);
            cancellation.Cancel();
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            int read = base.Read(buffer);
            cancellation.Cancel();
            return read;
        }
    }

    private static MemoryStream Xml(string body) => new(Encoding.UTF8.GetBytes($"<xisf version=\"1.0\">{body}</xisf>"));
}
