# API contracts and migration

Typed metadata now includes `XisfAdditionalProperties` for observed application properties and `FitsKnownKeywords` for native FITS headers and XISF-carried FITS keyword creation. Existing string-based access remains supported. Each key exposes its source and unit; see [typed metadata](metadata-properties.md) for conversion rules and ambiguous names.

These changes replace ambiguous editing, layout and conversion behavior before the API is frozen. Format codecs, numeric precision guards and specification support remain in place.

## Edit a complete XISF document

```csharp
using XISFLib;

XisfDocument source = XisfReader.Load("capture.xisf");
XisfDocumentData editable = source.ToEditable();
editable.Images[0].Id = "processed";
editable.Images[0].Properties.Set(XisfKnownProperties.Instrument.ExposureTime, 30.0f);
editable.Images[0].FitsKeywords.Add(XisfFitsKeyword.FromValue("OBJECT", "M31"));
editable.Properties.Set("Processing:Description", "Reviewed image");
XisfWriter.Write("edited.xisf", editable, new XisfWriterOptions
{
    Compression = XisfCompression.Zstd,
    Shuffle = true,
    Checksum = XisfChecksum.Sha256
});
```

`XisfDocumentData` contains images, global properties, creation metadata, tables and extensions. These collections have moved out of `XisfWriterOptions`, which contains serialization and creation-time/application settings. Image-sequence writers remain convenient for files without document-level content. Distributed and signed writers accept editable documents too.

`ToEditable()` materializes pixels and copies supported metadata, retaining unsupported representations and extension elements for writing. Opaque or native astrometric metadata can still require the original source until saving finishes. New creation time/application metadata comes from writer settings. Rebuilding editable content removes original signatures; use `WriteSigned` to sign the final representation. Use the parsed-document writer when retaining the original XML structure and reference layout is essential.

## Parsed models and advanced XML edits

Parsed XML and detached signatures are inspection copies. Property/cell arrays are returned as owned copies; geometry and parsed collections expose read-only views. Modifying an inspection copy does not edit the document.

```csharp
XisfDocument updated = source.EditXml(xml =>
    xml.Root!.SetAttributeValue("id", "XISFRootElement_Custom"));
XisfWriter.Write("updated.xisf", updated);
```

`EditXml` reparses typed views, enforces reader limits, retains the original block source, and removes signatures. Block locations must still describe that source. It does not change the original document. `Sign` returns a signed copy; `WriteSigned` is preferred for serialized output because it signs the final attachment layout.

## Consistent sample layouts

`GetSamples<T>`, `GetComplexSamples`, `GetNormalizedSamples` and `ReadRegion<T>` default to **planar** channel order for parsed and editable images. Region reads previously defaulted to interleaved output; pass `XisfPixelLayout.Interleaved` to retain that behavior. `GetPlanarSamples<T>` and `GetInterleavedSamples<T>` make the choice explicit. Undefined enum values are rejected.

Factories copy sample buffers and validate geometry/layout before allocating encoded samples. Checked conversions preserve overflow behavior. Parsed region reads still decode the complete raw block; editable reads reuse owned storage.

## Explicit metadata access

`FitsHeader.GetValue<T>` is now `GetValueOrDefault<T>`:

```csharp
FitsDocument fits = FitsReader.Load("capture.fits");
FitsHeader header = fits.Hdus[0].Header;
if (header.TryGetValue<double>("EXPTIME", out double exposure))
    Console.WriteLine(exposure);
string target = header.GetRequiredValue<string>("OBJECT");
double gain = header.GetValueOrDefault<double>("GAIN", 1.0);
header.Set("OBJECT", "M31", "Target");
header.Add(new FitsHeaderCard("HISTORY", "Processed once"));
```

Try access returns false for absent, null or incompatible values. Required access throws with the keyword identified. Default access explicitly accepts a fallback, including zero when omitted. `Set` replaces matching cards; `Add` retains ordered duplicates such as HISTORY/COMMENT.

Parsed XISF dictionaries and editable property collections also provide `TryGetValue<T>` and `GetRequiredValue<T>`. `Properties.Set(id, value)` and `XisfPropertyData.FromValue(value)` infer and validate scalar/vector/matrix types immediately. Raw values and explicit type declarations remain available for advanced representations.

Use `XisfKnownProperties` to access predefined identifiers with inferred CLR types: `Properties.Set(XisfKnownProperties.Instrument.ExposureTime, 30.0f)` and `Properties.GetRequiredValue(XisfKnownProperties.Instrument.ExposureTime)`. The catalog covers all predefined properties in revision 1, including both distortion-model directions. The former `Observation:ExposureTime` example is corrected to `Instrument:ExposureTime` (Float32, seconds). Existing custom identifiers remain unchanged; the library does not rename stored properties automatically. See [predefined properties](api-guide.md#predefined-properties).

`XisfFitsKeyword.FromValue` handles FITS quoting, apostrophe escaping and commentary storage. Its raw constructor accepts encoded FITS text. `FitsHeaderCard` accepts CLR values directly.

## FITS image editing

`FitsHdu.Data` is now `ReadOnlyMemory<byte>` over the authoritative payload. Use `.Span` to read it or `.ToArray()` for an owned copy. `Image` and `Table` provide typed access alongside `Content`.

```csharp
FitsHdu hdu = fits.Hdus[0];
hdu.ReplaceImage(FitsImage.FromPhysicalSamples(new ushort[] { 100, 200 }, [2, 1]));
hdu.Header.Set("OBJECT", "M31");
FitsWriter.Write("edited.fits", fits, new FitsWriterOptions { WriteChecksums = true });
```

`ReplaceImage` updates pixels, geometry and scaling together, clears stale structural/checksum cards and preserves nonstructural metadata. Incompatible structural-header insertion, replacement or removal is rejected. CHECKSUM/DATASUM are generated by the writer. For opaque/table payloads, `ReplaceRawData` copies bytes and clears stale decoded content and checksum results. Decoded table rows are inspection data; the writer uses raw table storage.

Public construction of an image HDU from raw bytes requires matching BITPIX/NAXIS cards and creates the same authoritative image model. Prefer `CreateImage` with a validated `FitsImage` for new images.

## Conversion policies and reports

Scientific FITS dimensions are preserved by default. Explicit color interpretation replaces the old `TreatThirdAxisAsColorChannels` option:

```csharp
var converted = FitsConverter.ConvertToXisf(fits, new FitsConversionOptions
{
    AxisInterpretation = FitsAxisInterpretation.RgbOrRgba,
    LossPolicy = ConversionLossPolicy.Allow
});
foreach (ConversionFinding finding in converted.Findings)
    Console.WriteLine($"{finding.Effect}: {finding.Message}");
XisfWriter.Write("converted.xisf", converted.Value);
```

`ConvertToXisf` and `ConvertToFits` return values and findings classified as preserved, transformed or omitted. `HasLosses` identifies omissions. Default `ConversionLossPolicy.Reject` throws `ConversionLossException`, whose `Findings` explain the losses. Select `Allow` when those omissions are acceptable. An explicitly chosen color-axis interpretation is a transformation, not an omission.

Reports also cover custom floating-point normalization bounds and properties using the FITS: prefix other than the four supported storage hints. Store ordinary FITS cards in `FitsKeywords` to preserve them. Parsed XISF conversion inspects metadata before decoding pixels, then converts one image at a time.

`ToXisf` and `FromXisf` return only the converted value and enforce the same policies. File converters return reports and accept reader policies and writer settings. `ConvertToFits` accepts parsed and editable complete documents, reporting global metadata/signature losses where present; image-sequence conversion describes the supplied images only. Complex images remain unsupported by standard FITS image HDUs even when losses are allowed.

The viewer explicitly selects RGB/RGBA interpretation and displays FITS-save omissions in its diagnostics. The batch converter explicitly permits its documented omissions and adds its separate XISFMETA archive. Library consumers receive safe defaults.

## Options, async work and lifetime

Readers snapshot options at load invocation, including permissions governing later lazy XISF reads. Reusing or changing caller options does not change existing documents. Writers snapshot serialization options too. Do not modify editable content or caller streams during an operation.

XISF and FITS offer path/stream `WriteAsync`, running serialization on worker threads. Cancellation is cooperative: file writes check before work and before atomic publication; stream writes check before and after serialization and may leave partial output. Streams remain caller-owned. These APIs do not promise asynchronous decompression or interruption of every CPU operation. See [I/O contracts](io-and-limitations.md) for source lifetime and resource bounds.
