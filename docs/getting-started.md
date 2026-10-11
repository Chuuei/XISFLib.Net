# Getting started

## Requirements and build

Use the .NET 10 SDK selected by `global.json`. Building the complete solution requires Windows because it includes WPF and Windows Forms projects. The core library and its consumers can run on supported non-Windows .NET platforms.

```powershell
dotnet restore XISFLib.Net.slnx
dotnet build XISFLib.Net.slnx -c Release --no-restore
dotnet test XISFLib.Tests/XISFLib.Tests.csproj -c Release --no-build
dotnet test XISFLib.Windows.Tests/XISFLib.Windows.Tests.csproj -c Release --no-build
```

From another project, reference `XISFLib/XISFLib.csproj`, or use the `XISFLib` NuGet package when available in your configured feed. Add `XISFLib.Windows` only for Windows rendering. Both assemblies expose types in the `XISFLib` namespace.

## Read an image

```csharp
using XISFLib;

XisfDocument document = XisfReader.Load("capture.xisf");
XisfImage image = document.Images[0];
ushort[] samples = image.GetSamples<ushort>();
Console.WriteLine($"{image.Dimensions[0]} x {image.Dimensions[1]}, {image.ChannelCount} channels");
```

Choose a sample type appropriate for the source. Checked conversion can throw when a value does not fit the requested type. Use `GetComplexSamples()` for complex images. Typed reads default to planar channel order, regardless of the source layout.

File-path XISF loading reads the XML header first. Pixel blocks are read later, so keep the source file available until all required data has been copied. Stream loading retains monolithic input in memory, subject to `MaximumBufferedInputBytes`. Caller-supplied streams remain open.

## Create or edit an image

Known FITS keywords also support typed access, for example `hdu.Header.Set(FitsKnownKeywords.Common.EXPTIME, 30.0)`. Additional PixInsight and acquisition properties are available through `XisfAdditionalProperties`. See [typed metadata](metadata-properties.md) for examples and convention differences.

```csharp
ushort[] pixels = [0, 100, 200, ushort.MaxValue];
XisfImageData created = XisfImageData.FromSamples(pixels, [2, 2], 1);
created.Id = "light";
created.Properties.Set(XisfKnownProperties.Instrument.ExposureTime, 30.0f);
XisfWriter.Write("new.xisf", [created], new XisfWriterOptions
{
    Compression = XisfCompression.Zlib,
    Shuffle = true,
    Checksum = XisfChecksum.Sha256
});

XisfImageData editable = XisfImageData.FromImage(image);
editable.FitsKeywords.Add(new XisfFitsKeyword("OBJECT", "'M31'", "Target"));
XisfWriter.Write("edited.xisf", [editable]);
```

`XisfImageData` owns its pixels and exposes editable known metadata. `FromImage` copies decoded data and known metadata; it is not a mechanism for preserving unknown XML extensions byte for byte. For a loaded document, `XisfWriter.Write(path, document)` rebuilds attachments while retaining its XML. File-path writes stage output before replacing the destination, allowing a loaded document to be saved to its source path.

Predefined properties have typed keys grouped by specification namespace:

```csharp
created.Properties.Set(XisfKnownProperties.Observation.Object.Name, "M31");
created.Properties.Set(XisfKnownProperties.Instrument.Camera.XBinning, 2);
created.Properties.Set(XisfKnownProperties.Observation.Time.Start, DateTimeOffset.UtcNow);

float exposureSeconds = created.Properties.GetRequiredValue(XisfKnownProperties.Instrument.ExposureTime);
if (image.Properties.TryGetValue(XisfKnownProperties.Instrument.ExposureTime, out float loadedExposure))
    Console.WriteLine(loadedExposure);
```

The exposure property is `Instrument:ExposureTime`, a Float32 value in seconds. The earlier `Observation:ExposureTime` example used a custom identifier, not the specification's exposure property. Typed keys infer the CLR type for reads and enforce the declared type for writes. See [predefined properties](api-guide.md#predefined-properties) for array ownership, missing values, creation metadata and astrometric properties. String identifiers remain available for custom properties.

## Read and convert FITS

For complete document editing, use `document.ToEditable()`, edit its images/global properties/tables, and write the resulting `XisfDocumentData`. Parsed XML is an inspection copy; advanced `EditXml` returns a reparsed document. See [API contracts](api-migration.md) for preservation and migration details.

```csharp
FitsDocument fits = FitsReader.Load("capture.fits");
IReadOnlyList<XisfImageData> images = FitsConverter.ToXisf(fits);
XisfWriter.Write("converted.xisf", images);

FitsDocument converted = FitsConverter.FromXisf(images, ConversionLossPolicy.Allow);
FitsWriter.Write("converted.fits", converted,
    new FitsWriterOptions { WriteChecksums = true });
```

Use `FitsConverter.ConvertFitsToXisf` or `ConvertXisfToFits` for the corresponding file-to-file operations. Review [conversion limits](io-and-limitations.md#conversion-and-numeric-precision) when exact sample preservation matters.

## Run the applications

```powershell
dotnet run --project XISFLib.WpfViewer -c Release
dotnet run --project XISFLib.AvaloniaViewer -c Release
dotnet run --project XISFLib.Convert -- "D:\Images" 4
dotnet run --project XISFLib.Benchmarks -c Release -- "D:\Images\Examples"
```

Both viewers load, render, inspect, edit, validate, and save files. Byte-identical copy preserves the original monolithic file; re-encoding applies editor changes and writes the selected image.

To publish the viewer as a standalone executable for Windows x64:

```powershell
dotnet publish XISFLib.WpfViewer/XISFLib.WpfViewer.csproj -c Release -p:PublishProfile=WinX64
dotnet publish XISFLib.AvaloniaViewer/XISFLib.AvaloniaViewer.csproj -c Release -p:PublishProfile=WinX64
```

Outputs are `artifacts/wpf/win-x64/XISFLib.WpfViewer.exe` and `artifacts/avalonia/win-x64/XISFLib.AvaloniaViewer.exe`. Each includes .NET and application dependencies; no .NET installation is required on the destination computer. Native libraries are extracted by .NET at startup. Ordinary `dotnet build` remains a development build; use `dotnet publish` to produce a bundled executable.

CI retains `wpf-viewer-win-x64` and adds `avalonia-viewer-win-x64`, `avalonia-viewer-linux-x64` and `avalonia-viewer-osx-arm64` ZIP artifacts. Pushing a `v*` tag, such as `v1.0.0`, creates a release with generated notes and all viewer assets after the Windows and portable jobs pass. Hyphenated tags create prereleases; reruns replace assets while retaining notes. See [application deployment](applications.md#avalonia-viewer) for native prerequisites and platform verification limits.

The batch converter processes `.xisf` files directly inside the specified folder, with optional concurrency (default eight). It creates `.fits` files, refuses existing outputs, and returns exit code zero on success, one for conversion/enumeration failures, and two for invalid arguments.

The benchmark project measures elapsed time and allocation for the supplied `slow.xisf` and `slow.fits` examples. It also has `--performance`, `--fits-performance`, and `--wpf-load` modes for synthetic operations, FITS processing, and end-to-end viewer loading. See the [performance guide](performance.md) for commands and measurement limits, and the [application guide](applications.md) for viewer and converter behavior.
