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

```csharp
ushort[] pixels = [0, 100, 200, ushort.MaxValue];
XisfImageData created = XisfImageData.FromSamples(pixels, [2, 2], 1);
created.Id = "light";
created.Properties["Observation:ExposureTime"] = 30.0;
XisfWriter.Write("new.xisf", [created], new XisfWriterOptions
{
    Compression = XisfCompression.Zlib,
    Shuffle = true,
    Checksum = XisfChecksum.Sha256
});

XisfImageData editable = XisfImageData.FromImage(image);
editable.FitsKeywords.Add(new XisfFitsKeyword("OBJECT", "M31", "Target"));
XisfWriter.Write("edited.xisf", [editable]);
```

`XisfImageData` owns its pixels and exposes editable known metadata. `FromImage` copies decoded data and known metadata; it is not a mechanism for preserving unknown XML extensions byte for byte. For a loaded document, `XisfWriter.Write(path, document)` rebuilds attachments while retaining its XML. File-path writes stage output before replacing the destination, allowing a loaded document to be saved to its source path.

## Read and convert FITS

```csharp
FitsDocument fits = FitsReader.Load("capture.fits");
IReadOnlyList<XisfImageData> images = FitsConverter.ToXisf(fits);
XisfWriter.Write("converted.xisf", images);

FitsDocument converted = FitsConverter.FromXisf(images);
FitsWriter.Write("converted.fits", converted,
    new FitsWriterOptions { WriteChecksums = true });
```

Use `FitsConverter.ConvertFitsToXisf` or `ConvertXisfToFits` for the corresponding file-to-file operations. Review [conversion limits](io-and-limitations.md#conversion-and-numeric-precision) when exact sample preservation matters.

## Run the applications

```powershell
dotnet run --project XISFLib.WpfViewer
dotnet run --project XISFLib.Convert -- "D:\Images" 4
dotnet run --project XISFLib.Benchmarks -- "D:\Images\Examples"
```

The viewer loads, renders, inspects, edits, validates, and saves files. Its byte-identical copy mode preserves the original monolithic file; re-encoding applies editor changes.

The batch converter processes `.xisf` files directly inside the specified folder, with optional concurrency (default eight). It creates `.fits` files, refuses existing outputs, and returns exit code zero on success, one for conversion/enumeration failures, and two for invalid arguments.

The benchmark project measures elapsed time and allocation for the supplied `slow.xisf` and `slow.fits` examples. It is a diagnostic harness; compare results on the same runtime, machine, and input files.
