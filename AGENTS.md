# Coding agent guide

This file applies to the entire repository. Keep it current when architecture, public contracts, tooling, or workflows change. Use source code and the linked guides to resolve details; do not treat historical benchmark results or test counts as permanent requirements.

## Project intent

XISFLib.Net reads, writes, validates, and converts XISF and FITS astronomy data. Priorities are full supported-specification behavior, scientific fidelity, maximum practical performance, and an API that is easy to use and hard to misuse. Performance changes must preserve precision, metadata, checksums, validation, resource limits, and failure behavior.

The public API is still evolving. When changing it, update affected callers, samples, tests, XML documentation, and migration guidance together. Keep user-facing examples on the recommended API.

There is no shell-extension project. The old shell extension was removed; do not reintroduce it into documentation, dependencies, solution entries, or packaging.

Raster import in the viewers is limited to PNG, JPEG, TIFF, BMP, and GIF. Keep the Avalonia parity plan aligned with this scope.

## Project map

| Location | Responsibility |
| --- | --- |
| `XISFLib/` | Platform-independent `net10.0` library: XISF/FITS models, codecs, validation, conversion, imaging utilities, astronomy |
| `XISFLib.Windows/` | `net10.0-windows` WPF/Windows Forms bitmap adapters, frozen bitmap sources |
| `XISFLib.Convert/` | Batch XISF-to-FITS console application; testable logic in `BatchConversion.cs` |
| `XISFLib.WpfViewer/` | WPF workbench/sample; `MainWindow.xaml.cs`, `.Workbench.cs`, and `.Utilities.cs` divide responsibilities |
| `XISFLib.Benchmarks/` | Elapsed-time and allocation measurements, including the actual WPF load path |
| `XISFLib.Tests/` | xUnit core, malformed-input, numeric, conversion, astronomy, metadata, and converter tests |
| `XISFLib.Windows.Tests/` | xUnit rendering and Windows adapter tests |
| `XISFLib.Viewer.Core/` | Shared session loader, lazy selection and initial async workbench state |
| `XISFLib.AvaloniaViewer/` | Avalonia desktop frontend under development |
| `XISFLib.Viewer.Core.Tests/` | Shared session/state regressions |
| `XISFLib.AvaloniaViewer.Tests/` | xUnit v3 headless tests with real Skia bitmap backend |
| `Examples/` | Large independent/user-supplied fixtures, provenance, validation hashes, metadata inventory |
| `docs/` | Authored Markdown guides; DocFX generates `api/` and `_site/` |
| `.github/workflows/ci.yml` | Windows build/test/package, documentation, viewer publication, releases, Pages |

Core source folders are `Xisf`, `Fits`, `Imaging`, `Astronomy`, and `Internal`. Public library types retain the single `XISFLib` namespace. Keep Windows/UI dependencies out of the core library.

Useful implementation entry points:

- `XisfReader`, `XisfDataSource`, `XisfDataBlock`, `XisfImage`, `XisfValidation`.
- `XisfWriter` partial files: properties, blocks, layout, signing, and async entry points.
- `FitsDocument.cs`: FITS header/HDU/image/table models. `FitsReader.Cards.cs` and `.Tables.cs`: parsing. `FitsChecksum.cs`: shared checksums. `FitsConverter.cs`: format conversion.
- `ApiContracts` and `XisfNumericTypes`: shared value validation and type mappings.
- `XisfImageUtilities`, `AstronomyMetadata`, `CelestialWorldCoordinateSystem`, `XisfAstrometricSolution`.
- `XisfDisplayRenderer.cs`: portable sample-to-display rendering; `XisfWindowsExtensions.cs`: Windows adapters. `Internal/AtomicFile.cs`: staged single-file publication.

## Build and validation

Use the repository's `global.json`: currently .NET SDK `10.0.401`, latest-feature roll-forward, no prerelease SDKs. C# 14 is configured in `Directory.Build.props`. The complete solution and Windows tests require Windows; the core library is platform independent.

Run from the repository root:

```powershell
dotnet restore XISFLib.Net.slnx
dotnet build XISFLib.Net.slnx -c Release --no-restore
dotnet test XISFLib.Tests/XISFLib.Tests.csproj -c Release --no-build
dotnet test XISFLib.Windows.Tests/XISFLib.Windows.Tests.csproj -c Release --no-build
dotnet test XISFLib.Viewer.Core.Tests/XISFLib.Viewer.Core.Tests.csproj -c Release --no-build
dotnet test XISFLib.AvaloniaViewer.Tests/XISFLib.AvaloniaViewer.Tests.csproj -c Release --no-build
docfx docfx.json --warningsAsErrors
```

Use `--no-build` only after building the changes. Both library projects generate XML documentation and make missing/malformed public documentation an error. NuGet auditing covers transitive dependencies down to low severity; `NU1901` through `NU1904` are errors. Fix dependency problems rather than suppressing auditing.

Packaging and application commands:

```powershell
dotnet pack XISFLib/XISFLib.csproj -c Release --no-build --output artifacts
dotnet pack XISFLib.Windows/XISFLib.Windows.csproj -c Release --no-build --output artifacts
dotnet run --project XISFLib.WpfViewer
dotnet run --project XISFLib.Convert -- "D:\Astro\Captures" 4
dotnet publish XISFLib.WpfViewer/XISFLib.WpfViewer.csproj -c Release -p:PublishProfile=WinX64
```

Normal builds/tests do not require DocFX. CI currently installs DocFX 2.81.0. Validate documentation changes with the warnings-as-errors command when available.

## Contracts to preserve

### Models and ownership

- Parsed XISF models are read-only views. XML inspection returns detached copies. `EditXml` returns a reparsed document; mutating an inspection copy does not edit the document.
- Use `XisfDocumentData`/`XisfImageData` for editable content. Content belongs in these models, not writer options. Options are snapshotted when the operation begins.
- Public buffers/arrays must respect existing ownership contracts. Typed property setters/getters copy arrays. Internal owned-buffer paths avoid duplicate full-image allocations; do not expose mutable backing storage publicly.
- Path-backed XISF loads lazily reopen source files for blocks and retain no open handle. Keep sources unchanged and available through lazy access. Materializing image pixels does not necessarily detach opaque metadata/table blocks from their sources.
- Monolithic XISF stream loads buffer input for later absolute-offset block access, even for seekable streams. FITS loads materialize HDU payloads and supported content.
- Caller-supplied streams remain open, including on failure or cancellation.

### Numeric and layout behavior

- Preserve checked conversions, overflow detection, byte order, planar/interleaved ordering, complex component order, non-finite values, and floating-point representations where the operation promises exact storage.
- `FromArray(T[,])` uses `[height, width]`; `FromArray(T[,,])` uses `[height, width, channel]`. Explicitly distinguish scientific axes from channel axes.
- Scientific FITS dimensions are preserved by default. Color interpretation requires explicit `FitsAxisInterpretation.RgbOrRgba` where applicable.
- FITS physical samples apply `BSCALE`, `BZERO`, and integer `BLANK`. Standard unsigned conversions have an exact typed route, including UInt64 above `2^53`. Do not route that path through double.
- Signed 64-bit conversions that would silently lose precision are rejected. Scaled physical-double conversions have documented floating-point limits. Standard FITS image HDUs cannot represent complex pixels; reject unsupported conversion.
- `XisfFloat128`/`XisfComplex128` preserve binary128 storage and decimal parsing without passing through double. They are storage types, not a quadruple-precision arithmetic engine.

### I/O, validation, and preservation

- Validate sizes and use checked arithmetic before allocating. Preserve malformed/truncated-input checks and decompression limits.
- XISF defaults disable network/general external-path resources; header-relative resources are bounded to the header directory. XML DTDs and resolution remain disabled. Resource policy is not a filesystem sandbox against symlinks/mounts.
- Verify XISF digests over serialized bytes before decompression. Preserve FITS `CHECKSUM`/`DATASUM` semantics and strict/tolerant diagnostics. An optimization must not bypass checks.
- Single-file path writers stage output in the destination directory, then replace it only after successful serialization. Existing files survive serialization failure/cancellation. Stream writes and distributed two-file output are not transactional.
- Async APIs execute synchronous work on worker threads with documented cancellation checkpoints; do not claim fully asynchronous I/O or interruptibility during every CPU/codec operation.
- Preserve unknown extensions, unsupported property representations, ordered FITS cards, and repeated commentary when the relevant save path promises preservation. Re-encoding is distinct from a byte-identical copy.
- Cross-format conversion is not universally lossless. Report omissions and reject them by default unless `ConversionLossPolicy.Allow` is selected. Do not quietly broaden lossy defaults.
- XISF signatures are detached whole-root signatures. Sign after attachment locations stabilize. Verification establishes integrity against an embedded key; signer trust belongs to the application.

## Specifications, astronomy, and metadata

The XISF audit targets XISF 1.0 Revision 1, document version 1.01, September 2026. The file-format version is still `1.0`. FITS support targets FITS 4.0. For specification changes, consult primary references and distinguish format requirements from optional application conventions:

- [XISF specification](https://pixinsight.com/doc/docs/XISF-1.0-spec/XISF-1.0-spec.html)
- [FITS standard](https://fits.gsfc.nasa.gov/fits_standard.html)
- [NASA keyword dictionaries](https://fits.gsfc.nasa.gov/fits_dictionary.html)

Native XISF astrometry and FITS WCS are separate APIs. Native XISF uses pixel centers `(column + 0.5, row + 0.5)`; the FITS convenience API exposes zero-based .NET coordinates while FITS reference pixels are one-based. Never mix their coordinate origins or synthesize a native solution from FITS cards.

Native solutions support seven projections, projective transformations, six spline kernels, both stored directions, revision negotiation, and layer fallback. Unknown major revisions are preserved but not evaluated. Legacy `PCL:AstrometricSolution:*` properties are metadata, not inputs to the Revision 1 evaluator. Geometry-changing edits require consistent model transformation or explicit solution removal. Evaluation does not fit/re-solve models or convert celestial reference systems.

Typed metadata catalogs:

- `XisfKnownProperties`: 159 reserved keys, with specified serialization types, units, and required array shapes. Exposure is `Instrument:ExposureTime`, Float32: use `30f`, not the obsolete `Observation:ExposureTime` example.
- `XisfAdditionalProperties`: observed application metadata, including PCL noise/signal/signature/legacy astrometry and PixInsight processing history, plus library FITS storage hints. Do not infer unverified shapes or units.
- `FitsKnownKeywords`: `Standard`, `Common`, `Astrophotography`, `Examples`, and `Wcs` groups. Native `FitsHeader` supports typed access; `XisfFitsKeyword.FromValue(key, value)` encodes the same definitions for XISF.
- Every key exposes `Source`, `SourceReference`, CLR type, and optional unit. Maintain the distinction between specification, published convention, observed examples, and library convention. Keep read-only `All` catalogs synchronized with fixed keys.
- FITS typed integer reads reject fractional/non-finite/out-of-range values. Dates remain strings; numeric RA/DEC and textual RA/DEC are separate representations. Numeric `APERTURE` in examples differs from NASA's string aperture-name convention. Units are descriptive, not automatic conversion instructions.
- Typed `Set` rejects repeated FITS `HISTORY`/`COMMENT`; use `Add`. Preserve structural-header guards. Native FITS values are CLR values; XISF-carried FITS values are textual and require proper quoting/commentary fields.

See [typed metadata](docs/metadata-properties.md) for full access contracts and provenance. Keep custom string-based metadata access available.

## Performance and WPF

- Prefer matching numeric types/layouts, spans, bulk copies, SIMD with scalar fallbacks, and direct decoding/rendering into final buffers. Avoid boxing per sample and full normalized/reordered arrays just for display.
- Do not introduce hidden image-sized caches or remove defensive public ownership to improve benchmarks. Repeated parsed reads decode/checksum again; reuse an owned editable image when appropriate.
- Load/decode/render away from the UI thread. WPF bitmap sources are frozen before cross-thread use. Assign the image before expensive metadata/editor population and allow dispatcher rendering; retain busy/cancellation guards and eventual validation completion.
- Display stretching is non-destructive, alpha is not stretched, and color/orientation/DPI behavior must remain consistent. Preserve exact statistics and transform behavior rather than replacing them with unreviewed approximations.
- Measure Release builds, allocations, and representative files on the same machine/runtime. Warm caches and bitmap-assignment timings are not cold-load or physical screen-presentation guarantees. Avoid machine-dependent timing assertions in tests.

```powershell
dotnet run --project XISFLib.Benchmarks -c Release -- --performance 7 artifacts/performance.json
dotnet run --project XISFLib.Benchmarks -c Release -- --fits-performance 5 artifacts/fits-performance.json
dotnet run --project XISFLib.Benchmarks -c Release -- --wpf-load 5 artifacts/wpf-load.json
```

## Tests and example files

Use focused regressions for observable behavior. Format changes need independently constructed fixtures as well as reader/writer round trips; matching implementations can share a defect. Cover relevant numeric boundaries, byte orders/layouts, invalid input, checksums, metadata preservation, and failure contracts. Keep ordinary tests independent of network access and large external datasets.

`Examples` currently contains 63 XISF files and four FITS files, including very large images. Binary astronomy files are tracked with Git LFS; CI checks out LFS content. Preserve originals and provenance; write experiments/outputs under ignored `artifacts/`. Do not replace original fixtures with re-encoded or repaired files. Corpus hashes and diagnostics are documented in `Examples/README.md`, `validation.json`, and `fits-validation.json`; metadata identifiers/types are in `metadata-inventory.json`. The corpus lacks an independent real-world September 2026 native distortion solution, so generated tests do not establish universal interoperability.

## Documentation, publishing, and CI

Document public APIs with useful XML summaries and parameter/return contracts. Include units, index origins, ownership, exceptions, precision, and source lifetime where relevant. Document positional record parameters. Update authored Markdown and `docs/toc.yml` when adding guides.

Do not edit or commit generated `docs/api`, `docs/_site`, `bin`, `obj`, or `artifacts`. Change C# XML comments or authored Markdown instead.

The `WinX64` publish profile produces exactly `artifacts/wpf/win-x64/XISFLib.WpfViewer.exe`, self-contained with .NET. Native libraries self-extract at runtime. Trimming is disabled for WPF; symbols are embedded/excluded to keep publication to one executable.

CI validates all four test suites, core packaging, dependency auditing, strict DocFX generation, and single-executable publication. Default-branch successful builds/manual runs deploy GitHub Pages; pull requests validate without deployment. Version tags beginning with `v` publish a GitHub release with the viewer executable; hyphenated tags are prereleases. Reruns replace the executable asset while preserving release notes. Do not broaden deployment triggers or write permissions unintentionally.

## Further reading

The initial Avalonia shell, shared loading/state and portable renderer now exist. Editors, import, saves and utilities remain pending. Continue extracting shared behavior rather than duplicating WPF algorithms. Rendering now lives in core `XisfDisplayRenderer`; `XisfWindowsExtensions` supplies Windows adapters. Avalonia desktop/theme/headless are pinned to 12.1.4, DataGrid to 12.1.2, CommunityToolkit.Mvvm to 8.4.2. Avalonia headless tests use xUnit v3 3.2.2, OutputType Exe, and Skia/HarfBuzz with UseHeadlessDrawing=false. CI covers all four test suites; Avalonia publication and Linux/macOS verification remain pending.

- [Avalonia viewer migration plan](docs/plans/avalonia-viewer-migration.md): future project creation, shared-code extraction, ordered implementation phases and feature-parity checklist. Initial projects are implemented; remaining functionality and evidence are tracked in the plan. Update its progress/evidence as work proceeds.

- [Contributing](CONTRIBUTING.md): development, packaging, documentation, CI.
- [API guide](docs/api-guide.md) and [migration](docs/api-migration.md): recommended public usage.
- [Architecture](docs/architecture.md): implementation boundaries.
- [I/O contracts](docs/io-and-limitations.md): ownership, failure, resource, cancellation, precision limits.
- [Specification coverage](docs/specification-compliance.md) and [astrometry](docs/astrometry.md): supported behavior and evidence limits.
- [Performance](docs/performance.md) and [applications](docs/applications.md): benchmark methodology and viewer/converter behavior.
