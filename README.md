# XISFLib.Net

`XISFLib` is a `net10.0` library for reading, writing, and converting Extensible Image Serialization Format (XISF) 1.0 and FITS astrophotography files.

## Usage

```csharp
using XISFLib;

XisfDocument document = XisfReader.Load("capture.xisf");
XisfImage image = document.Images[0];

// Values in planar channel order, converted to a common .NET type.
float[] planar = image.GetSamples<float>();

// Values in pixel-interleaved order and mapped through the image bounds.
double[] normalized = image.GetNormalizedSamples(XisfPixelLayout.Interleaved);

foreach (XisfFitsKeyword keyword in image.FitsKeywords)
    Console.WriteLine($"{keyword.Name} = {keyword.Value}");
```

For untrusted inputs, configure resource access and limits explicitly. `LoadAsync` supports cancellation:

```csharp
XisfReaderOptions readerOptions = new()
{
    AllowNetworkResources = false,
    AllowExternalFilePaths = false,
    MaximumDataBlockSize = 512 * 1024 * 1024
};
XisfDocument document = await XisfReader.LoadAsync("capture.xisf", readerOptions, cancellationToken);
IReadOnlyList<XisfDiagnostic> diagnostics = document.Validate();
```

Network and arbitrary external-path blocks are disabled by default. Header-relative distributed blocks remain enabled but cannot escape the header directory. `XisfReaderOptions` also bounds XML bytes/characters/depth/elements, image and property counts, pixels, table cells, compressed/decompressed block sizes, and decompression ratios. Increase limits deliberately for trusted exceptionally large datasets.

### Regions, statistics, and astronomy metadata

Two-dimensional real images support channel and rectangular reads without allocating a converted full-image array. The same APIs work on loaded `XisfImage` and editable `XisfImageData` instances:

```csharp
float[] tile = image.ReadRegion<float>(x: 100, y: 100, width: 512, height: 512);
float[] red = image.ReadChannel<float>(0);
IReadOnlyList<XisfChannelStatistics> statistics = image.CalculateStatistics(
    new XisfStatisticsOptions { MaximumSamplesPerChannel = 500_000 });
XisfHistogram histogram = image.CalculateHistogram(channel: 0, binCount: 4096);

AstronomyImageMetadata metadata = AstronomyMetadata.From(image);
if (CelestialWorldCoordinateSystem.TryCreate(image.FitsKeywords, out var wcs))
{
    (double rightAscension, double declination) = wcs.PixelToSky(1000, 750);
    (double x, double y) = wcs.SkyToPixel(rightAscension, declination);
}
```

Statistics include finite/non-finite counts, extrema, mean, sample standard deviation, median, and MAD. Large images are deterministically sampled according to `MaximumSamplesPerChannel`; histograms process all pixels. The unified metadata view recognizes common FITS and XISF fields for target, observer, instrument, telescope, filter, exposure, gain, offset, binning, temperature, and observation time. WCS supports linear `CD` matrices or `PC`/`CDELT` and TAN projection in zero-based .NET pixel coordinates.

### Writing images

Create an image from a one-dimensional array or `ReadOnlyMemory<T>` in planar or interleaved order:

```csharp
ushort[] pixels = [100, 120, 140, 160, 180, 200];
XisfImageData outputImage = XisfImageData.FromSamples(
    pixels, dimensions: [2, 1], channelCount: 3,
    pixelStorage: XisfPixelLayout.Interleaved);
outputImage.ColorSpace = "RGB";
outputImage.FitsKeywords.Add(new XisfFitsKeyword("OBJECT", "M31", "Target"));

XisfWriter.Write("capture.xisf", [outputImage], new XisfWriterOptions
{
    Compression = XisfCompression.Zlib,
    Shuffle = true,
    Checksum = XisfChecksum.Sha256,
    CreatorApplication = "My application"
});
```

`XisfImageData.FromArray(T[,])` accepts conventional `[height, width]` grayscale arrays. `FromArray(T[,,])` accepts `[height, width, channel]` interleaved images. `FromRaw` accepts already serialized XISF sample bytes, while `FromSamples` supports `byte`, `ushort`, `uint`, `ulong`, `float`, `double`, and `System.Numerics.Complex`. Writers target either a file path or any writable `Stream`.

Use `XisfWriter.WriteDistributed("capture.xish", "capture.xisb", images)` for indexed distributed output. A loaded document can be edited through `XisfDocument.Xml` and saved with `XisfWriter.Write`; unknown extension elements and data blocks are retained. `XisfWriter.WriteSigned` creates a monolithic file with a verifiable enveloped RSA XML signature.

### FITS files and conversion

`FitsReader` and `FitsWriter` work with paths or streams and retain every HDU, ordered header card, duplicate `COMMENT`/`HISTORY` entry, and raw extension payload. Images expose stored bytes and physical values after `BSCALE`, `BZERO`, and `BLANK` processing. FITS headers support standard cards, `HIERARCH`, long-string `CONTINUE` records, and `CHECKSUM`/`DATASUM` verification. Image extensions, ASCII tables, binary tables, and unknown extensions are preserved; binary table decoding includes logical, bit, integer, floating-point, complex, text, and `P`/`Q` variable-length fields.

```csharp
FitsDocument fits = FitsReader.Load("capture.fits");
FitsImage fitsImage = fits.Images.First();
double[] physicalPixels = fitsImage.GetPhysicalSamples();

// Direct file conversion; WCS, HISTORY, COMMENT, and other FITS cards are retained.
FitsConverter.ConvertFitsToXisf("capture.fits", "capture.xisf",
    new XisfWriterOptions { Compression = XisfCompression.Zlib });
FitsConverter.ConvertXisfToFits("capture.xisf", "capture.fits",
    new FitsWriterOptions { WriteChecksums = true });
```

FITS integer `BITPIX` images, standard unsigned `BZERO` conventions, floating-point images, arbitrary-dimensional cubes, and common three- or four-plane color cubes are converted. Original `BITPIX`, scaling, and blank-value metadata are stored as XISF properties so a FITS → XISF → FITS round trip can reconstruct the original storage. Standard FITS image HDUs have no complex pixel type, so complex XISF images are rejected rather than silently losing their imaginary components.

### Windows Forms and WPF

Reference the `XISFLib.Windows` project or package from a `net10.0-windows` application. It provides extension methods for loaded images:

```csharp
using Bitmap windowsFormsImage = image.ToBitmap();
BitmapSource wpfImage = image.ToBitmapSource();

// Apply a non-destructive screen stretch for typical linear astrophotography data.
BitmapSource stretched = image.ToBitmapSource(XisfStretchOptions.Auto);
BitmapSource metadataStretch = image.ToBitmapSource(XisfStretchOptions.DisplayFunction);
```

Both methods normalize samples through the XISF image bounds and support grayscale, grayscale with alpha, RGB, RGBA, and CIELab images. They apply image orientation and use XISF's default 72 DPI when no resolution is present. Passing `XisfStretchOptions.Auto` applies a median and median-absolute-deviation screen stretch; `XisfStretchOptions.DisplayFunction` applies stored display metadata. RGB channels are linked by default to preserve color balance, alpha is never stretched, and `TargetBackground`, `ShadowsClipping`, and `LinkChannels` can be customized with a new options instance. Omitting the options retains a linear rendering.

`ToBgra32Pixels()` accepts the same stretch options and exposes the converted pixel buffer for custom Windows UI controls. WPF bitmap sources are frozen for safe use across UI threads, and XISF resolution metadata is applied when available.

The solution also includes `XISFLib.WpfViewer`, a WPF library workbench and example implementation. It can browse and load `.xisf`, `.xish`, `.fit`, `.fits`, or `.fts` files, import PNG/JPEG/TIFF/BMP/GIF images, edit document and image properties, FITS keywords, tables and ancillary image metadata, configure compression/checksums/shuffling, and write FITS or monolithic/distributed XISF output. Its Utilities tab demonstrates unified astronomy metadata, sampled channel statistics, full histograms, region/channel reads, and bidirectional pixel/sky WCS transforms by calling the same public APIs shown above. For a loaded monolithic `.xisf`, **Byte-identical copy** is enabled by default: saving copies the complete source unchanged, including every image, unknown extension, signature, data-block representation, and metadata item. Clear that option to apply editor changes or writer settings; re-encoding intentionally rebuilds the selected image and can change XML ordering, creation metadata, block locations, compression, checksums, and file size. Saved files are reloaded immediately and shown with XISF or FITS conformance diagnostics. It also supports multi-image selection, comparison of linear display with configurable automatic stretching, mouse-wheel zooming, and left-button drag panning. Run it with `dotnet run --project XISFLib.WpfViewer`.

Large file loading is performed away from the UI thread. Path-backed zlib XISF attachments are checksum-verified while streaming directly into their final decoded buffer, FITS conversion reuses its source pixel storage when possible, and WPF rendering reads samples directly without allocating full normalized or reordered arrays. Additional images in multi-image XISF documents are decoded on first selection.

To reproduce the large-image stage timings with `Examples/slow.xisf` and `Examples/slow.fits`, run:

```powershell
dotnet run --project .\XISFLib.Benchmarks\XISFLib.Benchmarks.csproj -c Release -- .\Examples
```

The document exposes typed global/image properties, tables, FITS keywords, ICC profiles, thumbnails, RGB working spaces, display functions, color-filter arrays, and resolution metadata. `XisfDocument.Xml` retains the complete XML header, including application-defined extension elements.

## Supported storage and samples

- Monolithic `.xisf` files and distributed `.xish` headers with generic or indexed `.xisb` data files
- Attached, embedded, inline Base64/Base16, local-path, and URL data blocks
- zlib, LZ4, and LZ4HC decompression, including byte shuffling and compression subblocks
- SHA-1, SHA-256, SHA-512, SHA3-256, and SHA3-512 block verification
- UInt8/16/32/64, Float32/64, and Complex32/64 image samples
- All required scalar, vector, and matrix property types and aliases, including complex collections and optional Int128/UInt128 values
- Little- and big-endian data with planar/interleaved conversion
- Monolithic and indexed distributed writing with zlib/LZ4/LZ4HC compression, byte shuffling, checksums, conformant scalar/vector/matrix properties, tables, FITS keywords, ICC profiles, thumbnails, CFA, display, working-space, and resolution metadata
- Shared core objects through `uid`/`Reference`, one-dimensional images, image offsets, orientation, and UUIDs
- Structured conformance diagnostics through `XisfDocument.Validate()`, including image attributes, FITS names, RGB working spaces, and ICC profile headers
- W3C XML digital-signature verification and RSA signature creation
- FITS 4.0 80-byte cards and 2880-byte records, primary/image/table/unknown HDUs, physical scaling and nulls, long and hierarchical keywords, WCS-preserving metadata conversion, standard unsigned integer conventions, and FITS checksums