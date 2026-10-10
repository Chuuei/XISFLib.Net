# Architecture

## Projects

| Project | Responsibility |
| --- | --- |
| `XISFLib` | Platform-independent XISF/FITS models, I/O, conversion, validation, image utilities, astronomy metadata, and WCS |
| `XISFLib.Windows` | Windows Forms/WPF rendering and display stretching |
| `XISFLib.Convert` | Command-line batch XISF-to-FITS conversion and metadata mapping |
| `XISFLib.WpfViewer` | Interactive library workbench and reference application |
| `XISFLib.Benchmarks` | Timing/allocation measurements using example images |
| `XISFLib.Tests` | Core format, validation, security, utility, conversion, and batch-converter tests |
| `XISFLib.Windows.Tests` | Windows adapter and rendering tests |

`Directory.Build.props` sets the C# language version and NuGet vulnerability-audit policy. `global.json` selects the SDK. `XISFLib.Net.slnx` groups the projects. The GitHub Actions workflow restores, builds, tests, packs, audits dependencies, and uploads package artifacts on Windows. `Examples` supplies representative files, including benchmark inputs.

## XISF read/write path

`XisfReader` distinguishes a monolithic file by its binary signature, or accepts a distributed XML header. It validates the fixed header, decodes UTF-8, parses XML with restricted settings, enforces limits, resolves references, and constructs the parsed document model.

`XisfDataSource` provides bounded attachment access. `XisfDataBlock` interprets inline, embedded, attached, path, and URL locations; verifies digests; decompresses zlib/LZ4 blocks and subblocks; and reverses byte shuffling. Path-backed unshuffled zlib attachments have a streaming decode path. `XisfImage` checks decoded geometry and converts samples or channel layout.

`XisfWriter` builds XML and serialized attachments from editable images. Writing a loaded document copies the XML and rebuilds attachments from the original source. The writer stabilizes attachment offsets against serialized header length, writes the fixed header, XML, and attachments, or emits a distributed XML/XISB pair. Signing must occur after final attachment-location calculation.

`XisfValidation` supplies conformance diagnostics beyond parse-time checks. The XML remains available for inspection and preservation of extensions; parsed model properties are snapshots and do not synchronize with arbitrary XML edits.

## FITS read/write path

`FitsReader` reads 2880-byte header blocks and 80-character cards until END, identifies HDU types, calculates payload sizes, enforces limits, reads padding, validates checksums, and decodes supported image/table content. Tolerant and strict modes share diagnostics but differ in rejecting conformance errors.

`FitsWriter` rebuilds structural cards from the content model, retains nonstructural cards, serializes header strings, pads payloads, and optionally writes FITS checksums. `FitsDocument.cs` contains the HDU/header/image/table models and checksum support.

`FitsConverter` interprets FITS storage conventions and optional channel axes. Standard unsigned storage has typed conversion paths; other supported storage is represented through physical floating-point samples. Conversion retains supported astronomy metadata but is not a lossless container-to-container transformation.

## Applications and utilities

`XisfImageUtilities` provides extraction, sampled statistics, and histograms. `AstronomyMetadata` normalizes common properties/keywords and implements supported WCS transforms. Windows rendering reads these models and applies display normalization/stretching independently of scientific sample storage.

The viewer's main window is split across code-behind files for core UI behavior, the editing workbench, and utility views. The converter keeps its argument handling and batch processing in `BatchConversion` so its behavior can be tested independently of the console entry point. The benchmark harness measures allocations and elapsed time rather than asserting performance thresholds.

The internal `AtomicFile` helper stages single-file output in the destination directory. `CancellationReadStream` adds cancellation checkpoints to synchronous input reads without taking ownership of the underlying stream. `CancellationStringReader` adds checkpoints while parsing the buffered XISF XML text.
