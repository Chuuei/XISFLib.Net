# API guide

## Documents and image storage

`XisfDocument` exposes XML, global properties, creation metadata, tables, and images. `XisfImage` represents a parsed image and decodes its data when requested. `XisfImageData` owns sample bytes and supports editing known metadata before writing.

Dimensions are ordered width, height, then additional axes. Channels are a separate count. A planar buffer contains every pixel of channel zero, followed by every pixel of channel one. An interleaved buffer stores every channel of the first pixel, then every channel of the next pixel. `GetSamples<T>` and `GetComplexSamples` can reorder either representation. Raw reads preserve the source layout and byte order.

`FromSamples` accepts a flat array or read-only memory and copies the samples. `FromArray` accepts arrays indexed `[height, width]` or `[height, width, channels]`. `FromRaw` copies encoded samples after validating geometry. Integer XISF image formats are unsigned; real and complex floating-point formats are also supported.

`FitsDocument` contains ordered `FitsHdu` values. The first HDU is primary; it may be empty. Each HDU has a `FitsHeader`, raw `Data`, optional decoded `Content`, and nullable checksum results. `FitsImage` retains big-endian storage and the `BITPIX`, `BSCALE`, `BZERO`, and `BLANK` conventions. `GetPhysicalSamples` applies scaling and maps integer blanks to NaN, returning doubles.

`FitsHeader` preserves card order and repeated keywords. `Find`, `FindFirst`, and `GetValue<T>` compare keywords without case sensitivity. `FitsHeaderCard` holds a typed value and optional comment. `FitsTable` exposes decoded rows and `FitsTableColumn` definitions, including format, unit, scaling, and null metadata. FITS writing uses retained raw table data; decoded rows are not an editable table serialization model.

## Metadata, properties, and tables

Image properties use case-sensitive identifiers. Ordinary CLR scalar values infer their XISF type; `XisfPropertyData` specifies a type and optional format/comment explicitly. Supported numeric arrays become vector or matrix properties. `XisfProperty` retains the parsed value and original XML. See the property serialization tests for supported CLR combinations.

`XisfFitsKeyword` preserves astronomy keyword values and comments. Ancillary metadata uses `XisfColorFilterArray`, `XisfRgbWorkingSpace`, `XisfDisplayFunction`, and `XisfResolution`. ICC profiles are byte buffers; thumbnails are images with their own storage and metadata.

`XisfTable` is the parsed representation. For new output, construct `XisfTableData`, populate `Fields` with `XisfTableFieldData`, and append rows with exactly one value per field. The field type must agree with the values being serialized. Vector and matrix cells use the same data-block serialization as properties. Table comment text is preserved.

`AstronomyMetadata.From` extracts a common `AstronomyImageMetadata` view from a parsed image, editable image, or FITS header. Missing values remain nullable. Exposure is in seconds, sensor temperature is in Celsius, and observation timestamps use `DateTimeOffset`.

## Validation, checksums, and signatures

`XisfDocument.Validate()` returns `XisfDiagnostic` values with a severity, stable code, message, and optional XML element. Header loading and pixel-block decoding also throw for malformed data or exceeded reader policies. Validation is not a substitute for accessing and verifying the required pixel blocks.

`FitsReaderOptions.ValidationMode` defaults to tolerant reading. Findings remain in `FitsDocument.Diagnostics`. Strict mode rejects error-level diagnostics. Each `FitsValidationDiagnostic` identifies its HDU and may identify a header card or keyword.

XISF block checksums are verified when blocks are read. XISF output defaults to SHA-256; `XisfChecksum` also selects other supported digests. SHA-3 uses a portable fallback when the native provider does not support it. FITS checksum output is opt-in through `WriteChecksums`. Missing FITS checksums yield null validation results.

`XisfWriter.Sign` modifies the current XML header. Rebuilding attachments can change that header, so use `WriteSigned` when producing signed serialized output. `VerifyXmlSignature` verifies the signature against an embedded key; applications must establish signer trust separately. Signatures are detached elements following the XISF root, and reference its complete contents. These APIs do not provide certificate-policy validation.

## Image utilities and celestial coordinates

`ReadRegion<T>` extracts a zero-based rectangle; `ReadChannel<T>` extracts a zero-based channel. Both operate on two-dimensional real images and accept parsed or editable models. They allocate only the converted requested output, but a parsed image still reads the entire raw block.

`CalculateStatistics` uses at most `MaximumSamplesPerChannel` values per channel. It reports sampled counts, finite/non-finite counts, extrema, mean, standard deviation, median, and median absolute deviation. Set `Normalize` to use declared sample bounds. These are estimates when sampling skips pixels.

`CalculateHistogram` examines every sample in the selected channel. `XisfHistogram` includes bin counts, underflow, overflow, and non-finite counts. Bounds default to the image bounds; specify them when a different numeric interval is needed.

`CelestialWorldCoordinateSystem.TryCreate` recognizes the supported FITS WCS keywords. `PixelToSky` accepts zero-based pixel coordinates and returns right ascension/declination in degrees; `SkyToPixel` returns zero-based coordinates. Reference-pixel properties retain FITS one-based values. The supported linear/CD and TAN transformations do not implement a complete astronomical WCS engine or arbitrary distortion models.

## Windows display adapters

Reference `XISFLib.Windows` from a Windows-targeted application. `ToBitmap` returns a disposable Windows Forms bitmap. `ToBitmapSource` returns a frozen WPF source, allowing cross-thread use. `ToBgra32Pixels` returns a BGRA byte buffer for custom rendering.

Passing no stretch options renders normalized linear samples. `XisfStretchOptions.Auto` computes an automatic stretch. `DisplayFunction` requests the stored display transformation when available. `TargetBackground`, `ShadowsClipping`, and `LinkChannels` control automatic stretching; this affects display output rather than stored scientific samples.

## Revision 1 APIs

See [native astrometry](astrometry.md) and the [coverage report](specification-compliance.md). `XisfColorSpace` provides D50 RGB/XYZ/normalized CIELab conversion; `XisfDisplayFunctions` evaluates stored transformations and creates adaptive ones. `XisfPropertyFormatter` interprets property presentation tokens without changing stored values.

`XisfWriterOptions.DataBlockStorage` selects attachment, embedded or inline storage, with `MaximumInlineBlockSize` controlling the threshold; images use embedded storage when inline is selected. `HexEncoding`, `CompressionSubblockSize`, `BlockAlignmentSize` and `StringsAsDataBlocks` configure representation. Empty collections always use empty inline blocks.

`XisfProperty.IsAvailable`, table-cell availability and document/image `UnavailableObjects` expose unsupported representations without hiding readable content. `Validate(XmlSchemaSet)` adds validation against an application-supplied schema to semantic diagnostics.
