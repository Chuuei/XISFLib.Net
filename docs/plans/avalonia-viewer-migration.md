# Avalonia viewer implementation plan

Status: **Avalonia is the sole desktop viewer; the WPF sample was retired on 2026-10-11.** This document preserves the migration baseline, implementation history and evidence. WPF project paths and commands below are historical, not current run instructions. Use [applications](../applications.md) and [getting started](../getting-started.md) for the current viewer. Outstanding native-dialog/DPI, Linux/macOS, cold-load and long-session verification remains explicit.

Prepared on 2026-10-10 against repository HEAD `a7d788c1a6995b0b753a7b578d67d48e0ec21666` and the current WPF sources. Re-inventory functionality if the WPF application changes before implementation.

## Goal and scope

Add `XISFLib.AvaloniaViewer` to `XISFLib.Net.slnx`, retain the WPF application, and achieve feature parity between both viewers. Parity means equivalent user actions, data interpretation, save behavior, validation findings, and available functionality. Native styling and file dialogs may differ; scientific results and preservation contracts must agree.

Windows is the first comparison platform because the existing application is Windows-only. The new application should use a platform-independent .NET 10 desktop architecture and ultimately run on Windows, Linux, and macOS. Mobile/web are outside this plan. Full cross-platform parity requires explicit evidence, including raster codecs; a successful Windows build is insufficient.

Retain the WPF single-file release and Pages workflow. Deliver Avalonia release artifacts too, beginning with self-contained Windows x64 publication. Introduce other runtime artifacts after verifying the corresponding OS and native dependencies. No phase may silently remove a WPF feature to make migration easier.

Scope update requested by the user on 2026-10-10: JPEG XR (`.wdp`/`.jxr`) support is removed from the current WPF viewer and excluded from Avalonia feature parity. Raster import requirements are PNG, JPEG, TIFF, BMP, and GIF only.

## Findings from the existing implementation

| Source | What needs migration |
| --- | --- |
| `XISFLib.WpfViewer/MainWindow.xaml` | File/display sidebar; Image, Information, Utilities, Image metadata, Writer, and Diagnostics tabs; grids, trees, pointer interactions |
| `MainWindow.xaml.cs` | Loading, image selection, lazy materialization, rendering, stretch parsing, zoom/pan, metadata tree construction, busy/error handling |
| `MainWindow.Workbench.cs` | Import/unload, editor rows and parsing, image/document metadata, ICC/thumbnail attachment, first-table editing, save modes, reload/validation |
| `MainWindow.Utilities.cs` | Astronomy overview, statistics, histogram, sample access, FITS WCS transforms |
| `XISFLib.Windows/XisfWindowsExtensions.cs` | Shared scientific display algorithms currently mixed with WPF/System.Drawing bitmap creation |
| `XISFLib.Windows/XisfStretchOptions.cs` | Framework-independent options currently located in a Windows-only assembly |
| `XISFLib.Windows.Tests/XisfWindowsExtensionsTests.cs` | Existing rendering correctness coverage to preserve and extend |
| `XISFLib.Benchmarks/` | Existing WPF load-to-bitmap benchmark and rendering/allocation measurements |

The WPF application references `XISFLib.Windows`, uses `Microsoft.Win32` dialogs, WPF `BitmapDecoder`, `BitmapSource`, `FormatConvertedBitmap`, dispatcher calls, and mouse capture. Copying its code-behind into an Avalonia project would retain Windows dependencies and duplicate behavior. Extract shared application logic and display algorithms before wiring the new UI.

Important baseline distinctions:

- XISF materializes the first image on load; remaining images are materialized on first selection. FITS is retained as a document and converted with explicit RGB/RGBA axis interpretation and allowed conversion losses for this workbench.
- The first image is assigned before metadata-tree construction, validation, and editor initialization complete. Editing stays disabled while the operation is busy.
- Ordinary re-encoded saves write the selected image, not every original image/HDU. Byte-identical monolithic XISF copy preserves the entire file and ignores editor changes.
- Utilities expose the FITS linear/TAN WCS helper. They do not currently expose the separate native XISF astrometric evaluator.
- The renderer supports stored display functions, but the current viewer sidebar exposes linear versus automatic stretch. Backend capability is not an existing additional UI toggle.
- Raster import supports PNG/JPEG/TIFF/BMP/GIF. It reads frame zero, converts to UInt8 RGB/RGBA, preserves decoded DPI, and imports thumbnails without alpha. Decoder/color-management differences need verification.

## Target architecture

| Project/component | Role and dependencies |
| --- | --- |
| `XISFLib` | Existing codecs/models plus framework-independent BGRA display rendering in `Imaging`; no UI framework or raster-decoder dependency |
| `XISFLib.Windows` | Existing Windows bitmap adapters delegating to shared rendering; preserve current extension APIs |
| `XISFLib.Viewer.Core` (new, non-packable, `net10.0`) | Shared session state, commands/view models, editor parsing, metadata-tree models, save/utility orchestration, service interfaces; references core library |
| `XISFLib.WpfViewer` | Existing views plus WPF adapters for dialogs, raster import, dispatching, bitmap presentation; references shared viewer logic and Windows adapter |
| `XISFLib.AvaloniaViewer` (new, non-packable, `net10.0`) | Avalonia desktop entry point, AXAML views and platform services; references shared viewer logic and core library, never `XISFLib.Windows` |
| `XISFLib.Viewer.Core.Tests` (new) | Framework-independent xUnit tests for commands, session behavior, editor parsing and save modes |
| `XISFLib.AvaloniaViewer.Tests` (new) | Avalonia headless binding/input tests and bitmap-adapter tests; real desktop smoke tests supplement these |

Keep the new viewer's MVVM state framework-independent. Use `CommunityToolkit.Mvvm` for notifications/async commands if the version selected in phase 1 passes restore/auditing. Do not copy commands, editor parsing, or scientific algorithms into each frontend. Restrict view code-behind to framework-specific events, focus, pointer capture, bitmap lifetime, and dialog integration.

Suggested shared components: `ViewerSession`, `WorkbenchViewModel`, `ImageEditorState`, `WriterSettings`, `MetadataTreeBuilder`, `PropertyEditorCodec`, `TableEditorCodec`, `ViewerFileService`, and `UtilityService`. Separate dialog, raster decoder, and UI scheduling/presentation interfaces. Implement only abstractions with actual consumers in the two viewers; avoid a general plugin framework.

Shared session state retains the parsed XISF/FITS document, source identity, selected index, materialized images, editor drafts, and operation state. Define session replacement and operation generations so an obsolete render/load result cannot replace a newer selection or an unloaded session. Match existing editor commit/discard behavior explicitly before changing it in both frontends.

The display API should return dimensions, stride, DPI, and an owned BGRA buffer without framework objects. Preserve normalization, color conversion, stretching, orientation, rounding, parallel thresholds, and alpha exactly. Move `XisfStretchOptions` to the core library with its namespace/name unchanged; preserve source usage in Windows adapters and consider a type forwarder for previously compiled consumers. Enable narrowly scoped unsafe code in core only if the extracted parallel renderer still requires pinning. Document any assembly/API changes.

Avalonia should create/update an explicitly formatted `WriteableBitmap` from those bytes, respecting framebuffer row stride and straight versus premultiplied alpha. Compute pixels on a worker thread and marshal presentation to the UI thread; do not assume WPF's `Freeze` semantics exist. Dispose replaced bitmaps and shut down outstanding work safely.

## Dependency and portability decisions

Avalonia 12 is available at planning time, so do not build the plan around obsolete Avalonia 11 examples. During phase 1, select and pin a current stable, mutually compatible version set for desktop, theme, editable grid, and headless-test packages. Verify actual package/API availability and .NET 10 support. Record selected versions and licenses here; do not use floating versions or copy a stale template unchanged. In particular, Avalonia 12 changed diagnostics packaging.

Use the desktop storage-provider API for open/save dialogs. The core library's path-backed lazy blocks, relative distributed blocks, and atomic path writes need real local paths. Resolve local storage paths explicitly. If a provider yields only a stream, either stage a complete usable local resource set with a clear lifetime or report that this path-only workbench operation is unavailable; do not lose relative resources or silently load a truncated/bounded input. Such a limitation must remain visible in parity tracking.

Choose a raster decoder through a compatibility spike, not by assuming Avalonia's bitmap loader covers every required codec. ImageSharp is a candidate for PNG/JPEG/TIFF/BMP/GIF, subject to its current license terms and compatibility tests. Record supported formats by OS and keep unresolved required codecs open until they are implemented/tested or the user explicitly changes scope. Preserve TIFF variants, alpha, DPI, and frame-zero behavior. Reject unsupported formats even when selected through an all-files dialog filter.

## Feature-parity checklist

Each row needs implementation and verification evidence. Mark **Done** only after both. Add rows if the baseline changes. Do not treat a visual screenshot as proof of serialization or numeric parity.

| ID | Required behavior | Verification | Status |
| --- | --- | --- | --- |
| F01 | Browse/type paths; load `.xisf`, `.xish`, `.fit`, `.fits`, `.fts`; meaningful errors | Each extension, missing file, corrupt header, no convertible image | Implemented; extension/error tests pass; native browse interaction pending |
| F02 | Multi-image selection, labels, lazy XISF materialization, session reset/unload | Two-image fixture; no second-image decode before selection; unload/reload | Done (automated Windows evidence) |
| F03 | Full-resolution Gray/RGB/Lab and alpha display, supported sample types, bounds/layout/endian/orientation/DPI | Shared BGRA byte equality with the pre-extraction baseline | Done (independent baseline and bitmap tests) |
| F04 | Linear/auto stretch, linked/unlinked channels, target background, clipping, Apply/Enter/focus behavior | Compare controls, validation and pixel output | Implemented; Apply/toggle/focus handlers; full keyboard desktop check pending |
| F05 | Fit-to-viewport initial display; pointer-anchored wheel zoom 1–64 in steps of 1.2; left-drag pan and capture release | Headless gestures plus real DPI/scaling smoke tests | Implemented; headless gestures pass; real DPI pending |
| F06 | Image-first presentation, busy state, status/error feedback and disabled editing during operations | Staged-load observations, overlapping actions, failure recovery | Done (staged-load/state tests) |
| F07 | Raster import PNG/JPEG/TIFF/BMP/GIF; frame zero, RGB/RGBA, identifier, DPI; reject unsupported formats | Small independent decoder fixtures, Windows WPF differential checks, OS codec matrix | Windows independent codec checks pass; Linux/macOS execution pending |
| F08 | Complete information tree: file/document/global/image metadata, tables, ancillary data, XML and FITS HDU cards/comments | Compare shared tree output/order and expand/scroll behavior | Implemented; desktop scrolling pending |
| F09 | Image ID/type/color/orientation/offset/UUID/resolution editing | Save/reload each field and malformed values | Done (shared serialization/atomicity tests) |
| F10 | Image property add/edit/remove, scalar/vector/matrix types and dimensions; retained format/comment fields | Parser boundary tests; commit grids before save; serialized declared types | Done (serialization and active-cell tests) |
| F11 | FITS keyword add/edit/remove; quoting/comments and repeated ordered HISTORY/COMMENT | Save/reload and comparison of ordered cards | Done (ordered-card serialization tests) |
| F12 | CFA fields; RGB gamma/sRGB/x/y chromaticities and derived luminance; display-function editing | Save/reload and scientific render regression | Done (shared serialization/render tests) |
| F13 | ICC profile attachment and thumbnail import without alpha | Byte/profile equality, thumbnail dimensions/sample format, reload | Done (Windows attachment/thumbnail tests) |
| F14 | First image table identifier/caption/fields/rows using existing text syntax | Save/reload values; invalid field/row counts; editor limitations documented | Done (table serialization/preservation tests) |
| F15 | Document creation metadata and document properties editing | Serialized types/values, writer policy and reload | Done (document serialization/active-cell saves) |
| F16 | All current compression/checksum options, shuffle, creator setting and defaults | Selected options recorded in output and checked by reader | Done (all writer-option round trips) |
| F17 | Default byte-identical copy for monolithic XISF, ignores editor settings, same-file no-op | Whole-file hashes including multi-image/unknown/signature content; same-source safety | Done (whole-file hash and same-path tests) |
| F18 | Re-encoded selected-image XISF; distributed `.xish` + sibling `.xisb` | Reload selected image and metadata; unchanged source; output failure tests | Done (selected-image/distributed reload tests) |
| F19 | Selected-image FITS save with checksums and visible conversion omissions | CHECKSUM/DATASUM verification; loss findings; complex-image rejection | Done (checksums, omissions and rejection tests) |
| F20 | Reload after save and explicit validation; XISF/FITS diagnostics with severity/code/message | Independent invalid fixtures; compare findings and UI states | Done (validation and native save/reload probe) |
| F21 | Astronomy metadata overview and missing-value display | Compare public utility results, units, observer and exposure values | Done (overview/missing-value tests) |
| F22 | Sampled channel statistics, sample limit, normalize flag and all current columns | Same options/results including finite/non-finite samples | Done (shared statistics and bound-result tests) |
| F23 | Histogram selected channel/bins (maximum 65,536), rows/range/underflow/overflow/non-finite counts | Same histogram values and invalid-input errors | Done (histogram/limit tests) |
| F24 | Interleaved region and channel reads; count and first 64 values | Compare arrays and preview formatting; out-of-bounds errors | Done (region/channel result tests) |
| F25 | FITS WCS summary, pixel→sky/sky→pixel, reciprocal field updates, unavailable state | Same numeric results and zero-based coordinates | Done (WCS reciprocal/zero-origin tests) |
| F26 | Native dialogs, overwrite/extension behavior, focus/tab navigation and usable grid/tree scrolling | Interactive smoke checklist on supported OSes | Implemented; full native dialog/DPI/scrolling checklist pending |
| F27 | Standalone publication, release/download/run instructions, CI test and doc coverage | Clean-machine Windows launch and per-RID smoke evidence | Windows single-file native probe passed; Linux/macOS CI execution pending |

The current WPF editor has limits: complex/binary128/unavailable properties are not fully text-editable; only the first image table is edited; pipe-delimited cells cannot escape literal pipes. Preserve and document these limits. The full library's feature set is broader than either viewer. If a migration exposes destructive handling of unsupported metadata or another defect, fix the shared behavior for both applications with regressions rather than using the defect as the parity target.

## Ordered implementation phases

This is the current implementation checklist, reconciled against the source and evidence logs below. Checked items are implemented or have the specifically described evidence; unchecked items state the remaining work explicitly. Historical slice logs describe the state at that time and are superseded by this checklist. The F01–F27 table still governs full parity certification.

No missing WPF user action is currently identified by this audit. Both frontends now bind WorkbenchViewModel/ViewerUtilityState and share session ownership, drafts, commands, busy/status state and diagnostics. PropertyValueEditorState shares paged edits and ViewerViewportState shares zoom/pan geometry. Dialogs, grid commits, bitmap ownership and UI scheduling remain frontend handlers/delegates rather than a common interface hierarchy.

### 1. Freeze the baseline and resolve platform spikes

- [x] Record the WPF feature inventory, defaults and editor/save contracts in F01–F27 and the application guide; implemented controls and handlers exist for the corresponding Avalonia operations.
- [x] Build/test the solution; retain independent render hashes and shared save/tree/utility regressions, plus native load-stage reports under `artifacts/avalonia-baseline/`.
- [x] Verify compatible stable Avalonia desktop/theme/grid/headless packages, licensing, diagnostics changes, and .NET 10 support.
- [x] Implement and test framebuffer transfer, editable-grid commits and metadata-tree content/defaults. Native local-path dialog handlers are implemented.
- [x] Select Magick.NET-Q16-AnyCPU 15.0.0 and run Windows independent PNG/JPEG/TIFF/BMP/GIF compatibility checks; document decoder limits and per-OS gaps.
- [ ] Complete native-dialog, expanded-tree and per-OS codec interaction verification (phases 8 and 11).

Exit: baseline evidence exists; dependencies and difficult platform features have tested approaches. No unresolved codec is treated as optional parity.

### 2. Extract platform-independent display rendering

Depends on phase 1.

- [x] Move the numeric/display part of `XisfWindowsExtensions` into core `Imaging`, including options, orientation and DPI helpers.
- [x] Keep `ToBitmap`, `ToBitmapSource`, and `ToBgra32Pixels` Windows APIs as wrappers over the same renderer; avoid duplicate image-sized buffers.
- [x] Add core render tests and retain Windows adapter tests, including all orientations, layouts/endians, RGB/Lab/alpha, non-finite values and stored display functions.
- [x] Compare against 288 frozen pre-extraction render hashes; update rendering API documentation and retain the XisfStretchOptions type forwarder.
- [x] Record warm rendering/load allocation measurements in the benchmark reports.
- [ ] Complete cold-load and long-session managed/native-memory comparisons in phase 11.

Exit: WPF output matches baseline, the renderer builds/tests without Windows, and no UI framework has entered core.

### 3. Extract shared viewer behavior and adopt it in WPF

Depends on phase 2.

- [x] Add `XISFLib.Viewer.Core` and its tests to the solution.
- [x] Extract ViewerSession, editor rows/codecs, metadata-tree building, ViewerFileService and utility services/state. Both frontends use WorkbenchViewModel for load/selection/diagnostics orchestration.
- [x] Add testable async operation state, closure/generation guards and presentation/yield delegates; stage parsing before mutation and document ownership/source lifetime. Frontend dialog handlers replace the originally suggested interface hierarchy.
- [x] Adapt WPF to the same WorkbenchViewModel and ViewerUtilityState as Avalonia, retaining native handlers for dialogs, grid commits and presentation. Existing benchmark control names remain usable.
- [x] Test copy/re-encode, selected-image scope, conversion findings, failure recovery and editor commit/discard contracts in shared and frontend regressions.
- [x] Implement OS-specific path comparison and staged copy publication that replaces destination aliases rather than writing through them.
- [ ] Verify case-sensitive paths and source/destination alias behavior on Linux/macOS runners (phase 11).

Exit: WPF consumes the shared behavior with equivalent output and performance. Scientific logic/editor parsing is not copied into a second frontend.

### 4. Add the Avalonia project and application shell

Depends on phase 3.

- [x] Create `XISFLib.AvaloniaViewer` with desktop `Program.cs`, `App.axaml`, `MainWindow.axaml`, selected theme and compiled bindings.
- [x] Add solution entries and references to viewer core/core library; verify there is no WPF, WinForms, `System.Drawing.Common`, or `XISFLib.Windows` dependency in the portable frontend.
- [x] Build the sidebar and all six populated tabs; wire frontend dialogs, error feedback, UI scheduling, status and command enablement.
- [x] Add `XISFLib.AvaloniaViewer.Tests`, the actual selected headless package/theme, and a startup/binding test. Confirm xUnit package compatibility with the repository.

Exit: both applications build and start; the Avalonia shell binds to shared state. Empty tabs are still marked pending, not completed features.

### 5. Implement load, preview, image navigation and viewport

Depends on phase 4.

- [x] Wire F01–F06 loading, lazy selection, display/stretch controls and BGRA rendering; native keyboard/dialog/DPI certification remains in phase 11.
- [x] Transfer rows into Avalonia bitmap storage with explicit alpha mode/stride/DPI and deterministic disposal.
- [x] Port pointer-anchored zoom, clamp/step, pan capture/loss/release and viewport reset; the Image uses Stretch="Uniform" to fit the viewport as it resizes. Headless gestures are covered.
- [x] Present the bitmap before metadata/editor work; guard session replacement/window closure and suppress obsolete results.
- [x] Add malformed-input, unsupported-render, multi-image, failed-load and recovery regressions; retain the previous session/diagnostics on failed loads.

Exit: astronomy files display with verified pixel parity and responsive UI; F01–F06 have evidence.

### 6. Migrate information and diagnostics views

Depends on phase 5.

- [x] Bind complete shared metadata trees, XML nodes, FITS HDU headers, comments, tables and ancillary values.
- [x] Preserve metadata order, expansion defaults and empty states; test collapsed large branches and bound displayed text. Native tim-ray property-grid scrolling is measured.
- [ ] Verify expanded large-tree scrolling/population on each desktop OS; collapsed-tree tests do not establish expanded-tree virtualization.
- [x] Display XISF/FITS findings and explicit validation results, including conversion omissions after saves.

Exit: F08 and the validation portion of F20 are verified, with no full tree build blocking the first preview.

### 7. Migrate all image/document editors

Depends on phase 6.

- [x] Implement image attributes, property/FITS grids, document metadata/properties, and row add/remove/commit behavior.
- [x] Implement CFA, working-space, display-function, ICC and first-table controls with shared parsing and validation.
- [x] Preserve matrix dimensions, format/comments, type aliases and unsupported originals; commit focused cells before save snapshots. At the user's request the image-property grid exposes only ID, Type, Value and Inspect, matching WPF; retained dimensions/format/comments are not extra image-grid columns.
- [x] Verify edited fields through shared serialized-output regressions and frontend active-cell tests; malformed edits are staged before live mutation.

Exit: F09–F12, F14 and F15 pass applicable tests; attachment/thumbnail integration completes in phase 8.

### 8. Implement raster import and thumbnail/profile workflows

Depends on phases 1 and 7.

- [x] Implement the chosen raster-decoder services, native codec dependencies and native-dialog filters.
- [x] Match frame zero, UInt8 RGB/RGBA, actual transparency detection, thumbnail alpha omission, identifier and DPI behavior.
- [x] Compare independent WIC fixtures with the shared decoder for channels, alpha, DPI, frame zero and high-bit TIFF rounding; document that universal TIFF/ICC/EXIF preservation is not promised by either viewer.
- [x] Attach ICC/profile bytes and import thumbnails on workers; dispose decoder resources. Long-session native-memory checks remain in phase 11.
- [ ] Execute the codec/native-library matrix on Linux/macOS and representative real TIFF/ICC/EXIF variants; do not announce full cross-platform parity prematurely.

Exit: F07/F13 are verified on Windows; portability gaps and their implementation status are explicit.

### 9. Implement every writer/save/reload mode

Depends on phase 8.

- [x] Wire writer configuration and defaults, including all compression/checksum values and shuffle.
- [x] Match byte-identical copy availability/default, ignored-editor warning and same-source behavior; verify complete source hashes.
- [x] Wire selected-image re-encoding, distributed pair paths, FITS checksums and displayed conversion losses using shared save orchestration.
- [x] Implement native extension/filter choices, overwrite prompts and cancellation handling; reload saved output and refresh sessions/editors/previews/diagnostics.
- [x] Test same-path copy, existing-output preservation, failed serialization, selected-image/distributed reload and failure recovery. Both viewers use the same save service; distributed pairs retain the documented nontransactional contract.
- [ ] Exercise native extension selection, cancel/overwrite prompts, denied-access dialogs and distributed partial-output failures interactively on supported OSes.

Exit: F16–F20 pass with byte/hash/numeric evidence and documented preservation limits.

### 10. Migrate all utilities

Depends on phase 9.

- [x] Bind astronomy overview/missing values and statistics controls/results.
- [x] Bind histogram settings/limits/rows and region/channel sampling previews.
- [x] Bind FITS WCS summary, both coordinate directions, reciprocal fields and unavailable states.
- [x] Reuse existing core calculations; run Avalonia calculations and WPF statistics/histograms/channel reads on workers. WPF region reads now also run on a worker.

Exit: F21–F25 are verified. Native astrometry UI would be a separate enhancement added to both viewers, not an invented parity requirement.

### 11. Complete desktop, portability and performance verification

Depends on phase 10.

- [ ] Execute every parity row on Windows, including keyboard/focus, scrolling, pointer capture, dialogs and DPI/scaling.
- [ ] Run Avalonia-specific build/tests and real desktop smoke runs on Linux/macOS; build those projects individually because the existing whole solution includes Windows projects.
- [ ] Check file paths/case, distributed relative resources, Unicode, codec/native-library loading and bitmap resource disposal on each OS.
- [x] Add an Avalonia native staged-load benchmark alongside `--wpf-load`, measuring parsing, materialization, rendering, bitmap transfer/assignment and all-tabs-ready. Assignment is not physical screen presentation.
- [ ] Compare warm and cold cases and managed/native memory over repeated load/unload/stretch cycles. Investigate material regressions; record method/results rather than asserting machine-specific timing thresholds in unit tests.
- [ ] Rerun independent corpus validation where relevant, keeping original files unchanged.

Exit: F26 and all claimed OS parity have real evidence; no unresolved major performance/resource regression is hidden.

### 12. Publish, document and close the parity audit

Depends on phase 11.

- [x] Add the Avalonia Windows x64 self-contained publish profile; verify single-executable output and local native extraction/launch. Trimming/AOT remain disabled.
- [ ] Verify launch on a clean machine without an SDK; a local standalone run on the development machine is not that evidence.
- [x] Configure portable CI tests, per-RID publication and native probes; retain existing Windows/WPF build, audit, Pages and release rules.
- [x] Configure named Avalonia artifact uploads and tag-release ZIP assets alongside WPF, preserving notes/prerelease rules and permission scope.
- [ ] Obtain successful remote matrix/artifact/release execution evidence; editing the workflow is not proof that GitHub ran it successfully.
- [x] Update application/getting-started/contributing guides, prerequisites, limitations, architecture, AGENTS.md and DocFX rendering API metadata.
- [x] Run applicable local Release build/tests/audit/strict DocFX checks; evidence is in the implementation logs and durable regressions.
- [ ] Close outstanding F-row verification and review remaining platform gaps before declaring full parity certification complete.

Exit: both viewers are usable and distributed, and feature parity is demonstrated for each supported platform rather than inferred from shared code.

## Validation strategy and completion rule

Use three layers: shared unit tests for application behavior and scientific output, Avalonia headless tests for bindings/commands/input, and real desktop tests for dialogs/render backend/native dependencies/DPI. Use independent small fixtures for imports and malformed formats; keep large corpus/performance runs separate from normal unit tests.

Before extracting rendering, retain an independent baseline reference/hashes. Comparing two wrappers that now call the same renderer proves adapter agreement but cannot detect a shared algorithm regression. Use byte equality for display buffers, numeric/type/dimension comparisons for edited output, complete-file hashes for byte-identical copying, and diagnostics/conversion-finding comparisons for preservation behavior. Screenshots supplement these checks and need not be pixel-identical across native themes.

Completion requires every F-row to be Done with evidence, both viewers' applicable checks passing, documented editor/save limits matching, unchanged WPF release availability, and no unacknowledged OS-specific feature gaps. If the user changes scope, record the exact change here; elapsed implementation time or difficulty does not waive parity.

## Implementation log

| Phase | State | Evidence/decisions | Next action |
| --- | --- | --- | --- |
| 1 | Implemented; interactive baseline partial | Independent render baseline, editor/codec/utility/save regressions and native tab renders exist. | Complete native dialog/DPI inventory |
| 2 | Implemented | Independent hashes, wrappers/type forwarding, native bitmap and staged allocation measurements exist. | Long-session/native allocation checks remain in phase 11 |
| 3 | Implemented | Both frontends bind the same workbench/utility state, editor drafts and orchestration. Async portable state has failure and closure guards. | Retain frontend-specific events and shared contracts |
| 4 | Implemented | All six tabs, local-path dialogs, commands/status and headless tests are wired. | Interactive platform checklist |
| 5 | Implemented; desktop verification partial | Loading/selection/stretch/unload, alpha/stride/orientation/DPI and pointer gesture tests pass. | Full native keyboard/DPI interaction |
| 6 | Implemented; desktop verification partial | Complete shared tree, explicit validation and conversion findings after reload. Collapsed large-tree tests pass. | Expanded large-tree native scroll verification |
| 7 | Implemented | Image/document/FITS/attribute/table/ICC/thumbnail controls, shared staged parsing and active-cell serialization tests. | Full native interaction checklist |
| 8 | Implemented; portability verification partial | Magick.NET 15.0.0; independent WIC comparisons for five formats, alpha, frame zero, DPI and high-bit TIFF. | Execute Linux/macOS codec tests and document variant gaps |
| 9 | Implemented | All options/modes, selected-image scope, staging, whole-file hashes, reload, conversion findings and failure tests. | Native save-dialog/overwrite/cancel checks |
| 10 | Implemented | Overview/statistics/histogram/region/channel/WCS controls use shared calculations; reciprocal/origin/limit tests. | Native interaction checklist |
| 11 | Automation implemented; verification partial | Windows standalone backend probe, six reviewed renders and native staged benchmarks; portable CI probes prepared. | Execute other OS runners, dialog/DPI/scroll checks, cold/native-memory measurements |
| 12 | Implementation complete; audit closure pending | Single-executable profiles and three-RID ZIP/release workflow; guides and evidence updated. | Do not certify F26/F27 or full OS parity until remaining checks run |

### First implementation slice: 2026-10-10

The initial shell/loading integration overlaps phase 1 spikes and phase 3 extraction to verify framework integration early. This does not waive their remaining exit criteria. No F-row is yet marked Done: rendering has independent byte evidence, but complete user-action and desktop evidence remains outstanding.

Selected packages: Avalonia.Desktop, Avalonia.Themes.Fluent and Avalonia.Headless.XUnit **12.1.4**; Avalonia.Controls.DataGrid **12.1.2**; CommunityToolkit.Mvvm **8.4.2**. Avalonia/MVVM dependencies use MIT licensing. No raster decoder has been selected or installed. Avalonia 12 headless requires xUnit v3: its test project uses **3.2.2** and OutputType Exe, while existing/shared tests remain xUnit v2. Real Skia/HarfBuzz with UseHeadlessDrawing=false is necessary for framebuffer tests; the default dummy headless bitmap does not establish alpha-format behavior.

Durable evidence:

- `XISFLib.Tests/Fixtures/RenderingBaseline.json` and `DisplayRendererTests.cs`: 288 pre-extraction SHA-256 hashes for Gray/RGB/Lab plus alpha, eight orientations, two layouts, both byte orders, three stretch modes and non-finite samples. Original binaries generated the hashes before extraction.
- `XISFLib.Viewer.Core.Tests`: lazy multi-image materialization, FITS channel interpretation, selection/unload, failed-load preservation, operation recovery and closure guards.
- `XISFLib.AvaloniaViewer.Tests`: real Skia bitmap transfer and actual window load/unload through shared state.
- Windows CI builds the full solution and runs all four test projects. WPF release/Pages behavior remains unchanged.

Ignored experiment evidence is under `artifacts/avalonia-baseline/`: reference binaries/harness, `wpf-load.json`, `wpf-load-after.json`, and `desktop-startup.json`. Startup confirms a running Windows process reaching input idle; it is not an interactive visual/DPI test. Load timings use one warm iteration and are insufficient for a performance conclusion. Dedicated allocation, cold-load and native memory measurements remain pending.

Next implementation: finish codec/grid/tree/dialog spikes, extract the shared information tree and editor row/parsing models, adopt them in WPF, then bind the Avalonia information/editor views. Retain the existing scientific and preservation contracts.

Checks for this slice: Release solution build passed without warnings; `dotnet test XISFLib.Net.slnx -c Release --no-restore` passed 212 tests (182 core, 24 Windows, 4 shared viewer, 2 Avalonia). `docfx docfx.json --warningsAsErrors` passed with zero warnings/errors. Solution dependency auditing found no vulnerable packages. These counts record this implementation, not permanent completion criteria.

For each implementation turn, update the relevant phase/feature status, tests run, actual package versions, unresolved issues and next concrete action. Store experimental/generated artifacts under `artifacts/`; commit durable small test fixtures and authored docs normally.

### Information and diagnostics slice: 2026-10-11

Extracted the existing WPF information builder and MetadataNode into Viewer.Core, preserving labels, root order, expansion defaults, property types/format/comments, tables, ancillary image data, complete XML and FITS card order/comments. WPF initial loads and reload-after-save delegate to it. Avalonia now displays the same tree, with an unloaded empty state and explicit validation command. FITS validation displays reader findings, matching WPF; it does not reread the source file. XISF validation reruns the parsed-document validator.

Tree/validation work starts after preview presentation and dispatcher yielding. The tree is assigned as one property update; failed loads retain the previous tree and unload clears it. Closure guards suppress late results. Information construction does not materialize additional image pixel buffers.

Evidence: ViewerMetadataTests covers independent XISF XML metadata/ancillary/unknown-extension contents, FITS repeated-card order/comments, staged publication, validation, failed loads and unload. ViewerSessionTests checks that information construction leaves second-image materialization lazy. Avalonia ViewerTests checks bindings, validation/unload, default expansion and expansion of a tree containing 2,000 properties with fewer than 100 initial containers. This is evidence for collapsed initial branches, not a claim that every expanded branch is virtualized or desktop scrolling is verified.

F08 implementation/content evidence is present; interactive expansion/scroll smoke testing remains pending, so its Done status is withheld. F20 initial/explicit validation is present; reload/save/conversion findings remain pending. Codec/grid/native-dialog spikes and editor extraction remain open. Next: extract shared editor row models and property/table parsing, test commit/invalid-input behavior, then adopt in WPF and Avalonia.

Checks: Release solution build passed without warnings; all 216 tests passed (182 core, 24 Windows, 7 shared viewer, 3 Avalonia). DocFX passed with zero warnings/errors. The actual WPF load harness loaded slow.xisf and all four FITS examples successfully; timing evidence is in ignored `artifacts/avalonia-baseline/wpf-load-information.json`. This single warm iteration is a functional smoke check, not a performance conclusion.

### Shared property/FITS editor slice: 2026-10-11

Moved PropertyEditorRow and FitsKeywordEditorRow into Viewer.Core. Extracted ViewerEditorCodec for typed scalar/vector/matrix parsing, row formatting and first-table field/cell syntax. WPF delegates to it, and Avalonia's Image metadata tab supports property/FITS add/edit/remove and Apply. Grids commit active cells/rows before applying. Apply affects the working image only; saving is pending. Applied edits stay with each image; selection discards unapplied rows, matching WPF. The Information tree/validator still describe the parsed source.

Safety improvements apply to both frontends: unsupported/unavailable properties are preserved as read-only original values/XML instead of being converted to display strings. New unsupported text types and duplicate property IDs are rejected. Property/FITS parsing completes before replacing live metadata. WPF stages all image fields (including late CFA/display/table parsing) and document rows before mutation during save. Table field definitions reject extra unescaped pipe segments. Existing text-editor scope remains limited; explicitly removing a read-only row removes the property.

Evidence: ViewerEditorTests covers declared scalar types, exact Int64 matrix/UInt64 serialization with dimensions/format/comments, ordered FITS commentary serialization, rejected invalid/duplicate rows without partial mutation, unsupported complex and opaque XML preservation, table parsing and applied/discarded selection behavior. Avalonia's active-cell test edits a real DataGrid TextBox, clicks Apply, verifies the working property and scientific samples, and checks that complex rows cannot enter edit mode. WorkbenchEditorTests runs the actual WPF handlers on STA threads with malformed UUID, CFA and table inputs, verifying unchanged image ID/properties/ancillary data/pixels.

F10/F11 have initial implementation and serialization/grid evidence; full save/desktop workflow evidence remains pending. F09/F12/F13/F14/F15 controls remain pending in Avalonia; F14 parsing is shared but its UI has not migrated. No row is marked fully Done by this slice. Next: migrate image attributes, resolution, CFA, RGB working space and display-function controls using shared staged edit models, then document/table editors and attachments.

Checks for this slice: Release solution build passed without warnings; all 231 tests passed (182 core, 27 Windows, 18 shared viewer, 4 Avalonia). Strict DocFX generation passed with zero warnings/errors. The actual WPF load/editor path successfully loaded slow.xisf and all four FITS examples; ignored evidence is `artifacts/avalonia-baseline/wpf-load-editors.json` (one warm iteration, not a performance conclusion). No new package dependencies were introduced. Windows tests now reference WpfViewer for direct editor regressions.

### Image attribute and ancillary editor slice: 2026-10-11

Added shared ImageEditorFields and parsed ImageAttributeEdit snapshots for identifier/type/color/orientation/offset/UUID, resolution, CFA, RGB working space and display functions. WPF delegates its staging to the same parser. Avalonia exposes expandable controls and Apply refreshes the preview. All property/attribute parsing and stretch-option parsing finish before committing edits. Pixel buffers, dimensions and scientific sample storage stay unchanged.

Validation follows supported writer contracts: finite nonnegative offset, version-4 UUID, legal orientation/color-channel combinations, paired positive finite resolution/unit, valid CFA dimensions/symbols, core RGB primary validation/derived luminance, and five finite display vectors of four components. Image-type aliases are canonicalized. Working-space and display names are retained. Blank optional sections remove metadata. Editor values are formatted back from committed values; unapplied values are discarded on image switching. Stored display-function editing does not change the selected automatic/linear preview mode.

Evidence: ImageAttributeEditorTests checks serialization of attributes/UUID/resolution/CFA/RGB/display, exact unchanged sample storage, malformed input without mutation, removal of optional metadata, orientation preview and cross-editor atomicity. The actual Avalonia attribute-control test changes text boxes, clicks Apply and verifies rotated bitmap geometry; invalid CFA input retains both image metadata and the previous preview. Existing WPF malformed-field tests continue to exercise the shared parser.

F09/F12 controls and shared serialization evidence are implemented; full desktop/save evidence remains pending. Phase 7 remains partial. Next: document property/creation-metadata controls and first-table editing, followed by ICC/thumbnail/raster workflows and save orchestration. Codec/grid/native-dialog spikes and full native desktop verification remain open.

Checks: Release solution build passed with zero warnings/errors. All 241 tests passed (182 core, 27 Windows, 27 shared viewer, 5 Avalonia). Strict DocFX generation passed without warnings/errors. WPF loaded slow.xisf and all four FITS examples successfully; ignored timing evidence is `artifacts/avalonia-baseline/wpf-load-attributes.json`. One warm iteration establishes functional smoke coverage, not a performance conclusion. No package dependencies changed.

### Document and first-table editor slice: 2026-10-11

Added DocumentEditorState/DocumentEditorSnapshot for atomic document metadata/property parsing and creator settings. Avalonia's Writer tab now provides document grids, add/remove and Apply with active-cell commits. Applying is in memory; save orchestration remains pending. Document edits survive image selection and reset on load/unload, while the creator setting persists. Initial metadata rows exclude creation time and creator application; serialization follows writer policy. WPF saves use the same parser and selected-image content builder, without materializing other images or promising whole-container preservation.

TableEditorFields now supplies first-table controls to both viewers. All table parsing finishes before image mutation. Editable replacements retain the original comment and subsequent tables. Blank identifiers leave existing tables unchanged. Tables with unsupported/array/null cells or text that cannot round-trip through pipe-delimited syntax are read-only and retain the original table. Scalar timestamp formatting now preserves fractional seconds and offsets.

Evidence: DocumentAndTableEditorTests covers document types/matrix dimensions, output creation policy, timestamp precision, failed-parse snapshot retention, table comments/subsequent tables, unsupported and unrepresentable content preservation, invalid-table atomicity and unload. Avalonia's document/table test commits an actual active DataGrid TextBox, then edits table controls and verifies typed cells and unchanged scientific pixels. Existing WPF malformed-field tests still pass.

F14/F15 have controls, shared serialization and grid evidence; complete save/desktop evidence remains pending. Phase 7 is partial. Next: ICC/profile and thumbnail workflows, with raster-decoder evaluation and import implementation, then shared save orchestration. Native dialog/platform and expanded-tree interaction evidence remain open.

Checks: Release solution build passed with zero warnings/errors. All 251 tests passed (182 core, 27 Windows, 36 shared viewer, 6 Avalonia). Strict DocFX generation passed with zero warnings/errors. WPF successfully loaded slow.xisf and all four FITS examples; ignored evidence is `artifacts/avalonia-baseline/wpf-load-document-tables.json`. One warm iteration is a functional smoke check, not a performance conclusion. No dependencies changed.

### Remaining functionality, publication and audit: 2026-10-11

The user authorized automatic progression through every remaining stage. All planned viewer operations are now implemented. No WPF feature was removed to reach parity; JPEG XR remains excluded by the earlier scope decision. This log distinguishes completed implementation from native and OS verification that cannot be established from a Windows-only workspace.

Raster/attachments: Magick.NET-Q16-AnyCPU **15.0.0** (Apache-2.0, ImageMagick 7.1.2-33 and bundled native notices) is the shared application decoder. It reads only signature-approved PNG/JPEG/TIFF/BMP/GIF frame zero, exports UInt8 RGB/RGBA with actual alpha detection and decoded DPI, and omits alpha/ID/ICC/resolution for thumbnails. Both viewers run import and attachment reads on workers. Independent WIC encoders/decoder verify channel/layout/color/DPI/frame-zero samples, transparent PNG and high-bit TIFF (at most one UInt8 rounding step for the latter); decoder-generated cases supplement that independent evidence. These tests do not establish every TIFF/ICC/EXIF variant. Complete package notices, toolkit licenses/notices and Avalonia/Skia licenses are embedded and readable from the UI.

Saves: ViewerFileService is used by both frontends. It implements atomic staged byte-copy with OS-specific path comparison, signature-based monolithic eligibility, selected-image monolithic/distributed output and checksummed FITS with returned conversion findings. Replacement does not write through destination symlinks/hardlinks into the source. Avalonia commits all active grids before re-encoding, stages document and image drafts, reloads/presents the saved result and restores default copy eligibility. Byte-copy bypasses invalid/unapplied drafts. Distributed pair output retains its documented nontransactional contract. Native picker overwrite prompts are explicit; local paths are required.

Utilities: ViewerUtilityService shares overview, statistics, histogram/limits and sample formatting with WPF. Avalonia binds all results/options through ViewerUtilityState, runs large operations on workers and suppresses publication after closure or session replacement. WCS uses the existing FITS helper and zero-based origins; reciprocal field updates are tested. Native XISF solving/evaluation UI was not added because it is outside the WPF parity baseline.

Durable regression evidence: ViewerWorkflowTests covers all five codecs, alpha/DPI/frame-zero/thumbnail rules, byte-copy hashes/default/same-path, selected-image/document/attachment/distributed saves, checksums/omissions, complex FITS rejection, output failure/recovery, every compression/checksum combination, import/session/utilities, WCS origins/reciprocal fields and all FITS extensions. RasterCompatibilityTests provides six independent Windows codec cases. Avalonia ViewerTests adds an actual focused document-cell save, utility-button binding/click/results and pointer zoom bounds/pan/capture release. Existing scientific baseline and malformed-field tests remain green.

Publication: both Windows profiles produce exactly one self-contained executable, with native self-extraction and trimming disabled. Linux x64 and macOS arm64 cross-publication also produces one executable each. Cross-publication proves packaging, not native execution. CI adds a three-OS portable test/publish/probe matrix, requires its success before tag releases, and uploads named Avalonia ZIP assets alongside the retained WPF executable. Pages deployment rules, v-tag/prerelease behavior, release notes and permission scope are unchanged. Other OS native execution awaits CI; macOS builds are unsigned and ZIP extraction may require chmod.

Local native evidence: the standalone Windows Avalonia `--smoke-test` probe loaded a scientific image, ran utilities, saved/reloaded XISF and FITS and rendered all six tabs successfully. The Writer and Utilities captures were inspected. Probe JSON/screenshots are under ignored `artifacts/avalonia-smoke/`. Native `--benchmark` records parse/materialization/render/bitmap transfer/assignment/all-tabs-ready and managed allocation data; three warm iterations loaded slow.xisf and all four FITS examples successfully in both native Avalonia and the actual WPF harness. Reports are under `artifacts/avalonia-baseline/avalonia-load-complete.json` and `wpf-load-complete.json`. Short warmed runs do not establish cold I/O, physical screen presentation or long-session native memory behavior.

Final local checks: Release solution build passed with zero warnings/errors; all **277 tests** passed (182 scientific core, 33 Windows, 54 shared viewer, 8 Avalonia). Strict DocFX passed with zero warnings/errors, including the newly published migration-audit page. Transitive dependency auditing reported no vulnerable packages. Both Windows single-file publications passed output checks, and the final standalone Avalonia native probe succeeded; Linux/macOS cross-publication passed single-file output checks. Experimental outputs remain ignored; no example originals were changed.

Audit still open: run the Linux/macOS CI jobs and native dialog/overwrite/cancel/focus/tab/DPI/expanded-tree checklist below, then complete cold-load/native-memory measurements. No missing viewer control or save mode remains intentionally deferred. Full cross-platform feature parity is not certified until these evidence gaps close.

### Native interaction checklist (per supported OS)

- Open each astronomy extension and a distributed header through the picker; type Unicode/local paths and cancel without replacing the session.
- Import PNG/JPEG/TIFF/BMP/GIF through filters and All files; verify frame zero, palette/gray/alpha/DPI and representative real TIFF/ICC/EXIF variants.
- Edit focused image/document/FITS cells, use Tab/Enter, add/remove rows, scroll grids and expand/scroll large metadata branches.
- Exercise stretch toggles, Enter/focus Apply, fit-to-viewport, pointer-anchored wheel zoom and captured drag/release at 100%, 150% and 200% scaling.
- Save each mode through the native picker, test selected extension, cancel/overwrite prompts, same-path and denied-access cases; inspect conversion findings after FITS output.
- Launch the downloaded standalone asset without an SDK; confirm native extraction, repeated load/unload stability, shutdown during worker work and third-party notices.

## References checked during planning

### 2026-10-11: Automatic star deconvolution and spike attenuation

Finish & share now has enabled-by-default Sharpen stars and Reduce star spikes switches. Choices persist across loads; changing either invalidates the reviewed picture. A bounded tile detector finds round resolved stars in eligible linear data. Isolated unsaturated stars receive five damped Richardson–Lucy iterations in small per-channel patches using a limited estimated Gaussian PSF, noise floors, update/peak limits and feathered signal masks. Saturated/non-finite/transparent apertures, close blends, hot pixels and broad wings are excluded. Linear corrections are mapped into display deltas after cosmetic smoothing and before orientation; originals remain unchanged. New stages retain progress and cancellation.

Spike attenuation groups structured bright cores and considers at most 32 distinct bright stars. Transverse measurements and narrow angular contrast identify supported opposite rays; round halos, cores and neighboring resolved stars are protected. Up to three pairs receive partial feathered attenuation. Complex starbursts and broad optical halos remain, and filaments aligned through bright stars can still be ambiguous. Both effects can be disabled without expert PSF settings. Methods, limits and primary deconvolution references are in [applications](../applications.md#finish--share).

Evidence: independent synthetic cases verify measured stellar narrowing above background noise in both storage layouts/byte orders, exact distant background/source preservation, horizontal/rotated spike attenuation with retained cores/neighbors, and rejection of saturation, hot pixels, smooth extended emission, broad halos and close blends. Shared tests (101), Avalonia tests (12), Release solution build with zero warnings/errors, native preview/export smoke and strict DocFX passed. The headless test verifies actual checkbox bindings and invalidated save/preview state; native controls were visually inspected under `artifacts/finish-stars-smoke/`. Full-resolution Horsehead and M31 outputs under `artifacts/finish-preview-probe/` were regenerated: approximately 4,450 and 5,640 stars restored; supported rays around two Horsehead bright stars attenuated. Horsehead creation measured about 11.2 seconds locally (roughly one extra second for star processing); M31 measurements varied around 11–15 seconds with concurrent validation. Timings are illustrative, not assertions. The Alnitak crop retains its core, companion, broad halo and much of the starburst. These checks do not certify calibrated PSFs, flux conservation, universal spike/filament separation or recovery of clipped detail.

### 2026-10-11: Finish/export progress and PNG encoding performance

Creation and PNG/JPEG export now show immediate stage text and a visible progress bar. Measurable scan/render/encoder stages use stage-local progress; native multipass filters remain indeterminate. Worker callbacks are throttled, marshalled through the caller's UI context and suppressed after completion/cancellation/closure. The same Cancel button handles creation and export; existing reviewed pixels remain available after cancelled recreation, and cancelled publication preserves the destination.

PNG previously inherited JPEG quality 95, which ImageMagick interprets as PNG compression level 9 with adaptive filtering. Export now explicitly uses level 3, preserving exact PNG pixels, orientation/DPI and staged publication. JPEG remains quality 95. A same-machine Release probe of the 6,238 × 4,180 Horsehead finish measured PNG export at 71.31 seconds before and 2.76 seconds after (about 26× faster), with output about 14.5% larger; JPEG measured 0.73 seconds before and 0.68 seconds after. Creation took 10.57 seconds, including roughly 6.4 seconds selective blur and 2.3 seconds sharpening. Timings include warm/local conditions and do not guarantee other hardware/destinations. Measurements and exact PNG pixel verification are under `artifacts/finish-preview-probe/`.

Evidence: Release solution build passed without warnings/errors; shared viewer tests (95), Avalonia tests (12), native creation/PNG/JPEG progress captures and strict DocFX passed. Regressions cover visible progress during both operations, busy/cancel cleanup, reported stages, preserved previous previews, and cancellation just before publication without replacing the destination or leaving a staging file. Native progress screenshots under `artifacts/finish-progress-smoke/` were visually inspected. Scientific samples, image appearance and fixture files are unchanged.

### 2026-10-11: Darker finishing and conservative gradient reduction

The default finished stretch now targets a 0.10 sky instead of 0.16. Linear scientific frames receive an automatic per-channel plane estimate from bounded 12 × 12 tile medians, with iterative two-sided clipping to reject bright halos/emission and dark clouds. Coverage, retained-tile, quadrant and residual-scatter checks skip unreliable estimates; only 80% of a supported slope is subtracted before recalculating the stretch. Raster/already-developed exposure is retained. The summary reports applied reduction. Source pixels, metadata and fixture PNGs remain untouched. Methods and extended-emission/curved-gradient limitations are in [applications](../applications.md#finish--share).

Validation: full-resolution Horsehead and M31 sharing copies were regenerated and their thumbnails visually inspected under `artifacts/finish-preview-probe/`. Horsehead's broad left-to-right glow is reduced while the dark silhouette, Flame Nebula and Alnitak remain visible. M31 receives the darker stretch without triggering gradient correction. Independent synthetic RGB gradients with a dark cloud, emission and an extreme star cover planar/interleaved storage and byte orders, unchanged source bytes and raster bypass; a flat-sky nebula/star case verifies no correction and dark background. Release solution build passed without warnings/errors. All four suites passed (182 core, 30 Windows, 93 shared viewer, 12 Avalonia), as did native PNG/JPEG smoke and strict DocFX. Outputs were isolated because the user's open viewer locks its normal DLL; the core suite was rerun from standard output because its two example-fixture tests assume that location. These scene checks do not establish universal separation of diffuse astronomical signal from background, nor removal of optical halos.

### 2026-10-11: High-background linear finishing correction

The Horsehead FITS exposure exposed a brightness-only classification defect: channel medians near 0.344 caused exposure retention despite a narrow linear distribution, washing out faint nebular detail. Finishing now stretches positive narrow high-background distributions (mean robust channel sigma below 10% of mean channel median), recentering their display samples to avoid inverted-image adaptation. Low-background processing remains unchanged; constant frames, broad bright distributions and raster imports retain exposure. The scientific source is unchanged.

Validation: the full-resolution corrected Horsehead preview reveals the silhouette and Flame Nebula; regenerated M31 reviewed BGRA pixels have the identical SHA-256 to the user-approved PNG. Original FITS and user PNG files in Examples were preserved; comparison outputs remain under `artifacts/finish-preview-probe/`. Three synthetic regressions cover 0.344/0.7 linear backgrounds, positive faint detail, source preservation, raster exposure retention and a broad developed distribution. Shared tests (90) and Avalonia tests (12) passed, as did native PNG/JPEG smoke and strict DocFX validation. An isolated Release solution build under `artifacts/finish-validation/` passed without warnings/errors because the open user viewer locked its normal output DLL. These two scene checks validate this correction, not universal automatic scene classification or photometric color calibration.

### 2026-10-11: Novice image finishing and raster export

Added Finish & share with a sidebar entry, one-click processing, a separate reviewable preview and PNG/JPEG save actions. ViewerFinishState serializes worker creation/export with workbench commands, handles cancellation/closure and clears finished artifacts on image changes. ViewerFinishService conservatively crops empty borders, stretches dark astronomical data, balances dark RGB backgrounds, adapts gentle cosmetic filter thresholds to display noise and exports the reviewed opaque 8-bit image through staged publication. Source samples/metadata and unapplied drafts remain unchanged; loaded/imported source paths cannot be overwritten. Orientation/DPI and UI-thread bitmap disposal are preserved. Methods, automatic assumptions and unsupported input are described in [applications](../applications.md#finish--share).

Evidence: Release solution build passed without warnings/errors, and all four suites passed (182 core, 30 Windows, 87 shared, 12 Avalonia). Regressions cover crop-before-stretch, negative backgrounds, exposure retention, flat/non-finite input, color balance, noise reduction/star detail, byte order/layout, orientation/DPI, alpha flattening, exact PNG preview pixels, JPEG dimensions, failure/cancellation publication and original-path protection. The headless test verifies separate native preview ownership and save-button lifecycle. Windows native smoke creates/exports finished PNG/JPEG and captures all eight tabs; the Finish & share capture was visually inspected at 1450 × 900 (`artifacts/finish-smoke/`). Independent local probes processed NINA TestImage_Jelly and a 6248 × 4176 Meridian NGC3718 red exposure; exported previews were visually inspected under `artifacts/finish-preview-probe/`. These checks do not certify calibrated color, every aesthetic scene, complex stacking borders, native save dialogs, DPI transitions or other operating systems.

### 2026-10-11: Image quality statistics

Added a seventh tab, Statistics, with worker-based, cancellable selected-channel quality analysis in Viewer.Core. It exposes star candidate count/density, saturated peaks, moment FWHM/eccentricity, peak SNR estimates, robust neighbor-difference noise, background/variation, percentile contrast, clipping, finite counts and channel summaries. RGB defaults to green; metadata/selection/reload changes invalidate results while display stretch preserves them. Memory is bounded by samples and overlapping tiles; no full-image analysis copy/cache is retained. Methods and scientific limitations are documented in [applications](../applications.md).

Evidence: Release solution build passed without warnings/errors. All four suites passed (182 core, 30 Windows, 73 shared, 11 Avalonia). Independent synthetic regressions cover known Gaussian noise/widths, elongated stars, threshold changes, hot pixels, tile boundaries, saturation, borders, non-finite data, constant images, layouts/endian order, cancellation and state invalidation. The new headless test verifies actual tab bindings and finite grids. The Windows native smoke probe detects three injected stars and captures all seven tabs; the Statistics capture was visually inspected at 1450 × 900. Reports/captures are under ignored `artifacts/quality-smoke/`. These checks do not establish catalog completeness, calibrated photometry, broad/crowded-star accuracy or cross-platform/native-dialog/DPI certification.

### 2026-10-11: Desktop visual refresh

Avalonia now uses a compact light slate theme with teal accents, a top file toolbar, a 286-pixel sidebar with grouped document/preview settings, and a full-width status footer. Buttons retain content-sized widths; numeric preview fields are compact, and metadata forms have bounded widths. The dark image canvas has a new empty state. The paged property dialog uses the same typography and button hierarchy. All six tabs and existing command/binding contracts remain available.

Writer tables retain finite 200-pixel viewports inside a scrolling page so short windows do not collapse them. Property columns have minimum widths and document type/shape/format/comment columns have explicit desktop widths, allowing grid scrolling rather than compressing fields to unreadable sizes.

Validation: Release Avalonia/test build passed with no warnings or errors; all 10 Avalonia tests passed. Windows native smoke probes loaded, calculated utilities, saved/reloaded XISF and FITS, and captured all six tabs at 1450 × 900 and 1050 × 650. Captures were visually inspected, including the compact Writer layout. Reports/captures are under ignored `artifacts/avalonia-redesign/`. Build validation used `-p:UsedAvaloniaProducts=` to skip the telemetry task, whose log directory is outside the workspace sandbox. Native dialog, DPI and other-OS verification gaps remain open.

Large-metadata follow-up (October 2026): both property grids now bind bounded previews and expose a paged full-value editor/export. Large arrays defer formatting and unchanged arrays preserve their original typed value; Information tree text is also bounded. WPF uses finite, recycling grid viewports. Regressions cover large-string Apply/save, arrays, Unicode page boundaries, cancellation and actual frontend bindings. A Windows native Avalonia probe exercised all three `Examples/tim-ray` files and scrolling to spline serialization rows; results and captures are under `artifacts/large-metadata`. This addresses oversized text layout, while the broader cross-platform/DPI/expanded-tree evidence gaps above remain open.


- [Project application behavior](../applications.md), [architecture](../architecture.md), [I/O contracts](../io-and-limitations.md), [performance](../performance.md), [typed metadata](../metadata-properties.md).
- [Avalonia 12 release](https://avaloniaui.net/blog/avalonia-12/) and [Avalonia 12 breaking changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes): choose current compatible packages and migration APIs.
- [Avalonia storage provider](https://docs.avaloniaui.net/docs/services/storage/storage-provider): native desktop dialogs and storage semantics.
- [Avalonia WriteableBitmap API](https://api-docs.avaloniaui.net/docs/T_Avalonia_Media_Imaging_WriteableBitmap): explicit framebuffer access and lifetime.
- [Avalonia headless xUnit testing](https://docs.avaloniaui.net/docs/testing/headless-xunit): framework-specific test setup, dispatcher and isolation requirements.
- [Microsoft MVVM Toolkit](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/): framework-independent MVVM support.
- [ImageSharp project and license](https://github.com/SixLabors/ImageSharp): evaluate decoder capabilities and current license eligibility before selecting the dependency.

### 2026-10-11: Consolidate both frontends on shared state

WPF now binds the same WorkbenchViewModel and ViewerUtilityState as Avalonia. Removed its duplicate session fields, editor population/parsing wrappers, loading/rendering/save orchestration, busy/diagnostic state and utility handlers. Native controls, dialogs, cell commits, frozen WPF bitmaps and disposable Avalonia bitmaps remain frontend adapters. Creator names identify each application; writer defaults are shared.

PropertyValueEditorState centralizes bounded page navigation, staged edits and full-text reconstruction. ViewerViewportState centralizes pointer-anchored zoom, bounds and captured-pan geometry. Tests cover staged export/acceptance, unchanged typed-array preservation and viewport geometry. WPF regressions now exercise shared state through real bindings and the save entry point, including active-cell commits, invalid drafts, byte-copy bypass and failed-load recovery. The hidden WPF benchmark initializes bindings before setting the path and invoking the actual load handler.

Checks: all 291 tests passed (182 core, 36 Windows, 63 shared, 10 Avalonia). The WPF load probe successfully loaded slow.xisf and all four FITS examples; evidence is artifacts/consolidation/wpf-load.json. The standalone Windows Avalonia smoke probe passed, including saves/utilities and all six tab captures; evidence is artifacts/consolidation/avalonia-smoke.json. These checks do not certify native dialogs, DPI, physical screen timing or other operating systems.

### 2026-10-11: Retire the WPF sample

Removed XISFLib.WpfViewer from source and the solution, its frontend-only regression suite, and its load benchmark. Scientific Windows bitmap adapters and their tests remain in XISFLib.Windows. Avalonia and Viewer.Core retain editor, session, save, utility, paging and viewport behavior and regression coverage. The native Avalonia --benchmark probe replaces the removed WPF load harness.

CI publishes/releases only Avalonia ZIPs for Windows x64, Linux x64 and macOS arm64. Windows solution testing, library packaging, dependency auditing, Pages deployment, version-tag/prerelease rules and release-note preservation remain unchanged. Current documentation and agent instructions describe Avalonia as the sole viewer; preceding WPF comparisons are historical evidence.

### 2026-10-11: Noisy mosaic finishing

The supplied IC_434_mosaic_wizardstack_992f.fits exposed colored grain and incomplete mosaic corners. Eligible noisy scientific images now receive three-scale B3-spline wavelet shrinkage in luminance/color differences, with stronger chroma suppression and robust noise estimates at each scale. Bounded halo tiles preserve convolution support; significant coefficients remain intact. This suppresses visible noise but may suppress very faint texture. A default-enabled Reduce noise switch permits review without the additional pass. Progress/cancellation and original-buffer preservation remain intact.

Scientific mosaic cropping now finds a central populated rectangle on a bounded coverage grid, retaining at least 70% of each dimension and field area before the existing tidy trim. The IC 434 output removes both missing corners (880 × 2,706). Coarse sampling can miss small holes; insufficient coverage retains the existing conservative edge crop. This does not alter source geometry or astrometry.

Release checks: solution build completed without warnings; 105 shared-viewer and 12 real-Skia Avalonia tests passed. Independent noisy Gray/RGB fixtures verify lower scatter, retained background/dust/star signal, tile-boundary brightness, source preservation, cancellation and mosaic cropping. Native smoke succeeded at artifacts/finish-noise-smoke/report.json, including the additional switch. Full-resolution comparison outputs are under artifacts/finish-noise; IC 434 finished in approximately 1.3–1.8 seconds in warmed local runs. Horsehead and M31 skipped additional denoising through the low-noise gate (approximately 10.4–10.8 seconds); mosaic coverage cleanup still applies. These are local measurements, not timing guarantees. Strict DocFX generation passed. Original examples remain unchanged.

### 2026-10-11: Irregular mosaic crop and finished-preview navigation

The original bounding-frame retention limit rejected a useful crop of slow.xisf, leaving jagged blank mosaic edges. The central largest populated rectangle is now checked against sampled populated coverage (at least 60%), with independent bounding-frame area (35%) and axis (50%) safety floors before tidy trimming. Negative nonzero science samples count as content. The full-resolution slow.xisf output under artifacts/finish-noise/slow-finished.png is 2,222 × 3,578; visual review confirms removal of the black mosaic border with the visible nebula retained. Local warmed finishing decreased from about 8.3 to 4.5 seconds because fewer pixels require processing. This remains a sampled coverage crop, not subject recognition; it may discard valid peripheral sky, and small gaps may escape sampling.

The finished preview now reuses the normal preview's pointer handlers with an independent ViewerViewportState, clipped transforms and pointer capture. Scroll zooms around the pointer; left dragging pans. Publication/clearing resets the finished view only, and navigation never changes exported pixels.

Release build passed without warnings. Shared regressions (107) and real-Skia Avalonia tests (13) passed, including irregular coverage/negative samples and both preview surfaces' zoom limits, pointer capture, independent transforms and recreate/unload resets. Original examples remain unchanged. Native smoke succeeded at artifacts/finish-crop-navigation-smoke/report.json; strict DocFX generation passed with no warnings.

### 2026-10-15: Optional Bayer reconstruction

The sidebar now offers explicit debayering for single-channel two-dimensional UInt8/16/32/64 and Float32/64 Gray images. Native 2 × 2 CFA metadata takes precedence over FITS BAYERPAT with integer XBAYROFF/YBAYROFF. Missing or invalid metadata requires a manual RGGB/BGGR/GRBG/GBRG choice; detected patterns can be overridden. Patterns refer to stored rows before display orientation.

ViewerDebayerService performs bilinear interpolation on a worker with row cancellation checks. Existing samples retain their exact storage bits; integer means use UInt128 sums and round down. Output retains geometry, sample type, bounds, byte order, orientation and resolution. The session selects a separate RGB working image and retains the original mosaic. Selection refreshes editors, quality and finishing state, discards unapplied image drafts, and disables whole-file copy for derived results. Saving writes selected RGB data, and Finish & share can use it. Source-only opaque extensions and stale CFA/ICC/thumbnail/display/UUID/FITS storage hints are excluded from the derived result; limitations are documented in applications.md.

Validation: Release solution build passed with the user's revised global.json settings (default installation selected .NET 10 RC1). All four suites passed: 182 library, 30 Windows adapter, 118 shared-viewer and 14 real-Skia Avalonia cases. New fixtures cover all four Bayer patterns, odd dimensions/edges, byte order, non-finite samples, UInt64 means above 2^53, metadata precedence/offsets, explicit manual choice, cancellation, RGB save/reload, original-file preservation and UI selection back to the original. Strict DocFX validation passed. The existing native smoke probe succeeded at artifacts/debayer-smoke/report.json; it checks general viewer integration, while the new debayer controls are exercised headlessly. Independent real-camera debayer color certification remains unverified. NuGet auditing required updating System.Security.Cryptography.Xml (and its transitive Pkcs dependency) to 10.0.13; auditing remains enabled.
