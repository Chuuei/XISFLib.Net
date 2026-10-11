# Architecture

## Projects

| Project | Responsibility |
| --- | --- |
| `XISFLib` | Platform-independent XISF/FITS models, I/O, conversion, validation, image utilities, astronomy metadata, and WCS |
| `XISFLib.Windows` | Windows Forms/WPF adapters over portable rendering |
| `XISFLib.Viewer.Core` | Shared workbench/session state, commands, editors, paging, viewport geometry, save operations, raster decoding and utilities |
| `XISFLib.AvaloniaViewer` | Portable desktop workbench and native publication/load probes |
| `XISFLib.Viewer.Core.Tests` | Session, editor, save, import, utility and failure-recovery tests |
| `XISFLib.AvaloniaViewer.Tests` | Headless binding/input/active-cell tests and real Skia bitmap transfer |
| `XISFLib.Convert` | Command-line batch XISF-to-FITS conversion and metadata mapping |
| `XISFLib.WpfViewer` | Interactive library workbench and reference application |
| `XISFLib.Benchmarks` | Timing/allocation measurements using example images |
| `XISFLib.Tests` | Core format, validation, security, utility, conversion, and batch-converter tests |
| `XISFLib.Windows.Tests` | Windows adapter and rendering tests |

`Directory.Build.props` sets the C# language version and NuGet vulnerability-audit policy. `global.json` selects the SDK. `XISFLib.Net.slnx` groups the projects. The GitHub Actions workflow restores, builds, tests, packs the core library, audits dependencies, and uploads package artifacts on Windows. It also builds DocFX with warnings treated as errors and deploys the documentation to GitHub Pages after successful default-branch builds. `Examples` supplies representative files, including benchmark inputs.

## XISF read/write path

Source files are grouped under `Xisf`, `Fits`, `Imaging`, `Astronomy` and `Internal`. Public types retain the single `XISFLib` namespace so consumers need one import. Reader/writer settings have their own files; document content is represented by document models rather than stored in options.

`XisfReader` distinguishes a monolithic file by its binary signature, or accepts a distributed XML header. It validates the fixed header, decodes UTF-8, parses XML with restricted settings, enforces limits, resolves references, and constructs the parsed document model.

`XisfDataSource` provides bounded attachment access. `XisfDataBlock` interprets inline, embedded, attached, path, and URL locations; verifies digests; decompresses zlib, LZ4, LZ4HC and Zstandard blocks and subblocks; and reverses byte shuffling. Path-backed unshuffled zlib attachments have a streaming decode path. `XisfImage` checks decoded geometry and converts samples or channel layout.

`XisfWriter` builds XML and serialized attachments from editable images. Writing a loaded document copies the XML and rebuilds attachments from the original source. The writer stabilizes attachment offsets against serialized header length, writes the fixed header, XML, and attachments, or emits a distributed XML/XISB pair. Signing must occur after final attachment-location calculation.

`XisfValidation` supplies conformance diagnostics beyond parse-time checks. The XML remains available for inspection and preservation of extensions; parsed models are read-only and expose detached XML copies. Advanced EditXml reparses the edited header into consistent typed views. XisfDocumentData contains editable content separately from writer settings.

## FITS read/write path

`FitsReader` reads 2880-byte header blocks and 80-character cards until END, identifies HDU types, calculates payload sizes, enforces limits, reads padding, validates checksums, and decodes supported image/table content. Tolerant and strict modes share diagnostics but differ in rejecting conformance errors.

`FitsWriter` rebuilds structural cards from the content model, retains nonstructural cards, serializes header strings, writes payloads followed by separate padding, and optionally writes FITS checksums. `FitsDocument.cs` contains the HDU/header/image/table models; the shared checksum implementation is in `FitsChecksum.cs`.

`FitsConverter` interprets FITS storage conventions and optional channel axes. Standard unsigned storage has typed conversion paths; other supported storage is represented through physical floating-point samples. Conversion retains supported astronomy metadata but is not a lossless container-to-container transformation.

## Applications and utilities

`ImageEditorFields` provides the shared text snapshot for image attributes, resolution, CFA, RGB working space and display functions. Parsing produces an `ImageAttributeEdit` before mutation, validates the supported writer rules, derives RGB luminance through the core color API and preserves working-space/display-function names. Both frontends use it. Applying edits changes metadata only; pixel buffers and geometry remain untouched. Avalonia refreshes its preview after Apply and replaces the text snapshot with formatted committed values.

`ViewerEditorCodec` shares property/FITS row conversion, typed text parsing and first-table parsing. Unsupported properties retain their original objects/XML through read-only rows. New unsupported text types and duplicate IDs are rejected. Both frontends commit active grid cells before taking parsed snapshots. Both stage document rows and image fields before save mutation; byte-copy mode skips editors entirely.

`DocumentEditorState` stages document metadata/properties and creator settings into a `DocumentEditorSnapshot`; failed parsing retains the previous applied snapshot. Both frontends use its parser. `CreateContent` includes the selected working image only, without materializing other images; output creation metadata remains governed by writer policy. `TableEditorFields` shares first-table text snapshots and preserves unsupported or unrepresentable tables unchanged. Editable table replacement retains the original comment, and callers retain all subsequent tables.

Core `XisfDisplayRenderer` returns an owned straight-alpha BGRA32 frame without Windows dependencies. Windows and Avalonia adapters transfer that frame into bitmap storage. `XisfStretchOptions` now lives in core; the Windows assembly forwards its previous type identity. Scientific samples remain unchanged.

Both frontends bind the same `WorkbenchViewModel`. It owns the `ViewerSession`, editor drafts, image selection, load/render/save orchestration, diagnostics, busy/status state and obsolete-result suppression after closure. `ViewerSession` supplies XISF/FITS loading and lazy XISF materialization. Access is serialized by the application worker. `ViewerMetadataBuilder` supplies the ordered information tree, including XISF XML and ordered FITS cards. The view model builds this tree and validates on a worker after preview presentation; immutable node data is published together. Bitmap presentation and dispatcher yielding are frontend callbacks. WPF creates and freezes its bitmap on a worker; Avalonia transfers and disposes native bitmap storage on the UI thread. Dialogs, active-cell commits and native input events remain frontend responsibilities.

`PropertyValueEditorState` owns bounded page navigation, export reconstruction and staged edits for both property dialogs. Accepting applies changes to the editor row; closing leaves it untouched. `ViewerViewportState` shares pointer-anchored zoom, limits and pan geometry. Frontends translate native pointer events and apply the resulting transforms and capture state. These shared models are application components, not part of the packaged scientific library API.

`ViewerFileService` selects byte-copy, monolithic/distributed selected-image XISF and checksummed FITS output. Copy mode verifies monolithic eligibility by signature, stages publication and uses OS-specific path comparison. It replaces destination aliases rather than streaming into them, preserving source bytes. It returns conversion findings for frontend diagnostics; session reload/presentation follows successful output. Distributed output retains the library's two-file failure contract.

`ViewerRasterDecoder` restricts signatures to five raster formats and uses Magick.NET-Q16-AnyCPU 15.0.0 only in the application layer. It imports frame zero into UInt8 interleaved RGB/RGBA and creates thumbnails without alpha. Both frontends invoke it on workers; the scientific core has no raster-decoder dependency. `ViewerUtilityService` shares overview, statistics, histogram and sample formatting; both frontends bind `ViewerUtilityState` commands and results, which suppress obsolete worker results. Native WCS calculations remain core APIs.

The explicit Avalonia `--smoke-test` command starts a real desktop backend, exercises saves/utilities and captures all tabs. `--benchmark` records staged native load timings and allocations. Neither mode runs during ordinary startup. CI runs portable project tests and publication probes separately from the Windows solution build, retaining WPF and Pages workflows.

Writer implementation is divided into property serialization (`XisfWriter.Properties.cs`), block encoding (`XisfWriter.Blocks.cs`), attachment layout (`XisfWriter.Layout.cs`), signing (`XisfWriter.Signing.cs`) and async entry points (`XisfWriter.Async.cs`). FITS card and table parsing live in `FitsReader.Cards.cs` and `FitsReader.Tables.cs`; checksums live in `FitsChecksum.cs`. `ApiContracts` centralizes enum/value validation and `XisfNumericTypes` supplies shared sample/property mappings.

`XisfImageUtilities` provides extraction, sampled statistics, and histograms. `AstronomyMetadata` normalizes common properties/keywords and implements supported WCS transforms. Windows rendering reads these models and applies display normalization/stretching independently of scientific sample storage.

`XisfAstrometricSolution` evaluates native XISF projections, projective transforms and spline distortion, with revision negotiation and layer fallback. It is independent of the FITS WCS helper. `XisfFloat128` and the complex storage types preserve numeric representations; `XisfColorSpace`, `XisfDisplayFunctions` and `XisfPropertyFormatter` implement color, display and presentation rules.

WPF's main window separates native presentation/input (`MainWindow.xaml.cs`) from dialog and grid integration (`MainWindow.Workbench.cs`). Its utilities bind shared commands directly; there is no separate utility code-behind. Avalonia uses equivalent native adapters in its main window. The converter keeps its argument handling and batch processing in `BatchConversion` so its behavior can be tested independently of the console entry point. The benchmark harness measures allocations and elapsed time rather than asserting performance thresholds.

The internal `AtomicFile` helper stages single-file output in the destination directory. `CancellationReadStream` adds cancellation checkpoints to synchronous input reads without taking ownership of the underlying stream. `CancellationStringReader` adds checkpoints while parsing the buffered XISF XML text.
