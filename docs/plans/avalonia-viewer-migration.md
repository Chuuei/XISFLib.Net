# Avalonia viewer implementation plan

Status: **all planned functionality implemented on 2026-10-11; portability and interactive verification remain open**. Both viewers use shared application services. Automated Windows evidence is recorded below; Linux/macOS CI execution, native-dialog/DPI interaction and long-session/cold-load measurements are not claimed complete. Checkboxes track requirements including verification, so unfinished checks are retained explicitly.

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

Each phase should be a reviewable commit with a green applicable build/test set. Keep WPF working throughout. Add tests with the functionality rather than postponing all testing until the end. Update this file with completed checkboxes, evidence paths, selected versions, deviations, and the next action.

### 1. Freeze the baseline and resolve platform spikes

- [ ] Recheck every WPF event handler/control against F01–F27; record defaults, supported edits, commit/discard behavior, and error paths.
- [ ] Build/test existing solution and capture representative render hashes, save/reload results, metadata trees, utility values and load-stage timings under ignored `artifacts/avalonia-baseline/`.
- [x] Verify compatible stable Avalonia desktop/theme/grid/headless packages, licensing, diagnostics changes, and .NET 10 support.
- [ ] Prototype framebuffer transfer (BGRA, alpha, stride, DPI), editable-grid row addition/deletion and commit, metadata-tree performance, and native local-path dialogs.
- [ ] Run raster import compatibility spikes for PNG/JPEG/TIFF/BMP/GIF; choose decoder(s) and record platform gaps with a concrete implementation path.

Exit: baseline evidence exists; dependencies and difficult platform features have tested approaches. No unresolved codec is treated as optional parity.

### 2. Extract platform-independent display rendering

Depends on phase 1.

- [x] Move the numeric/display part of `XisfWindowsExtensions` into core `Imaging`, including options, orientation and DPI helpers.
- [x] Keep `ToBitmap`, `ToBitmapSource`, and `ToBgra32Pixels` Windows APIs as wrappers over the same renderer; avoid duplicate image-sized buffers.
- [x] Add core render tests and retain Windows adapter tests, including all orientations, layouts/endians, RGB/Lab/alpha, non-finite values and stored display functions.
- [ ] Compare against frozen baseline hashes and measure allocation changes; update API docs and any type forwarding.

Exit: WPF output matches baseline, the renderer builds/tests without Windows, and no UI framework has entered core.

### 3. Extract shared viewer behavior and adopt it in WPF

Depends on phase 2.

- [x] Add `XISFLib.Viewer.Core` and its tests to the solution.
- [ ] Extract session loading/selection, row models, property/table codecs, metadata-tree construction, save mode selection, diagnostics and utility orchestration.
- [ ] Introduce testable async operation state and framework service interfaces. Stage editor parsing before mutation; keep buffer ownership/source lifetime explicit.
- [ ] Adapt WPF handlers incrementally to the shared view models/services. Preserve control names or update existing reflection-based benchmarks alongside them.
- [ ] Test byte-copy versus re-encode, selected-image scope, conversion-loss presentation, failed operation recovery and editor commit/discard rules.
- [ ] Handle OS-specific path identity safely; do not carry Windows case-insensitive comparisons into Linux same-file logic. Never overwrite a source through an alias accidentally.

Exit: WPF consumes the shared behavior with equivalent output and performance. Scientific logic/editor parsing is not copied into a second frontend.

### 4. Add the Avalonia project and application shell

Depends on phase 3.

- [x] Create `XISFLib.AvaloniaViewer` with desktop `Program.cs`, `App.axaml`, `MainWindow.axaml`, selected theme and compiled bindings.
- [x] Add solution entries and references to viewer core/core library; verify there is no WPF, WinForms, `System.Drawing.Common`, or `XISFLib.Windows` dependency in the portable frontend.
- [ ] Build the sidebar and six tabs, compose dialog/error/scheduler services, wire status and command enablement.
- [x] Add `XISFLib.AvaloniaViewer.Tests`, the actual selected headless package/theme, and a startup/binding test. Confirm xUnit package compatibility with the repository.

Exit: both applications build and start; the Avalonia shell binds to shared state. Empty tabs are still marked pending, not completed features.

### 5. Implement load, preview, image navigation and viewport

Depends on phase 4.

- [ ] Wire F01–F06 with the shared loader, lazy selection and BGRA renderer.
- [x] Transfer rows into Avalonia bitmap storage with explicit alpha mode/stride/DPI and deterministic disposal.
- [ ] Port pointer anchoring, zoom clamp/step, pan capture/loss/release and viewport reset/resize behavior.
- [ ] Present the bitmap before metadata/editor work; guard session replacement/window closure and suppress obsolete results.
- [ ] Test malformed files, unsupported render types, multi-image files, failed loads and operation recovery. Preserve useful diagnostics even if an image cannot be displayed.

Exit: astronomy files display with verified pixel parity and responsive UI; F01–F06 have evidence.

### 6. Migrate information and diagnostics views

Depends on phase 5.

- [x] Bind complete shared metadata trees, XML nodes, FITS HDU headers, comments, tables and ancillary values.
- [ ] Preserve expanded defaults/order and empty-state behavior; verify virtualization or incremental population with large trees rather than assuming WPF settings port directly.
- [ ] Display both diagnostic types and explicit validation results, including conversion omissions after saves.

Exit: F08 and the validation portion of F20 are verified, with no full tree build blocking the first preview.

### 7. Migrate all image/document editors

Depends on phase 6.

- [x] Implement image attributes, property/FITS grids, document metadata/properties, and row add/remove/commit behavior.
- [ ] Implement CFA, working-space, display-function, ICC and first-table controls; use shared parsing and validation.
- [ ] Preserve matrix dimensions, format/comments, type aliases and current unsupported-editor rules. Explicitly commit focused grid cells before taking a save snapshot.
- [ ] Verify all edited fields through serialized outputs, not only bindings; check invalid edits do not partially corrupt the live session.

Exit: F09–F12, F14 and F15 pass applicable tests; attachment/thumbnail integration completes in phase 8.

### 8. Implement raster import and thumbnail/profile workflows

Depends on phases 1 and 7.

- [x] Implement the chosen raster-decoder services, native codec dependencies and native-dialog filters.
- [x] Match frame zero, UInt8 RGB/RGBA, actual transparency detection, thumbnail alpha omission, identifier and DPI behavior.
- [ ] Verify decoded sample results against WPF for independent small fixtures; explicitly investigate color-profile/EXIF/rounding differences.
- [x] Attach ICC/profile bytes and import thumbnails on workers; dispose decoder resources. Long-session native-memory checks remain in phase 11.
- [ ] Close the per-OS codec matrix for the five required formats; do not announce full cross-platform parity prematurely.

Exit: F07/F13 are verified on Windows; portability gaps and their implementation status are explicit.

### 9. Implement every writer/save/reload mode

Depends on phase 8.

- [x] Wire writer configuration and defaults, including all compression/checksum values and shuffle.
- [x] Match byte-identical copy availability/default, ignored-editor warning and same-source behavior; verify complete source hashes.
- [x] Wire selected-image re-encoding, distributed pair paths, FITS checksums and displayed conversion losses using shared save orchestration.
- [ ] Match extension/overwrite/cancel dialog behavior, reload the saved document, and refresh sessions/editors/previews/diagnostics coherently.
- [ ] Exercise same-path save, existing destination, denied access, serialization failure and distributed partial-output contracts. Apply any safety fixes to both viewers.

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

- [ ] Add an Avalonia Windows x64 self-contained publish profile and clean-machine launch smoke check. Verify actual native assets and single-file extraction behavior; keep trimming/AOT disabled initially unless separately validated.
- [ ] Add portable CI jobs for shared/viewer tests and app builds, plus per-RID packaging/smoke checks for supported OSes. Keep existing Windows/WPF build, audit, Pages and release behavior intact.
- [ ] Upload clearly named Avalonia artifacts. Extend tag releases with those assets while retaining WPF assets, existing release notes/prerelease behavior and narrowly scoped write permissions.
- [ ] Add run/publish instructions, native prerequisites and documented limitations to application/getting-started/contributing guides. Update architecture, AGENTS.md and any DocFX metadata for new public rendering APIs.
- [ ] Run the full applicable build/test/audit/DocFX checks; mark F01–F27 Done only with linked evidence. Review unresolved platform gaps explicitly.

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
| 3 | Implemented | WPF adopts shared loading/rendering/information/editor/raster/save/utility services. Async portable state has failure and closure guards. | Retain frontend-specific events and shared contracts |
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

Large-metadata follow-up (October 2026): both property grids now bind bounded previews and expose a paged full-value editor/export. Large arrays defer formatting and unchanged arrays preserve their original typed value; Information tree text is also bounded. WPF uses finite, recycling grid viewports. Regressions cover large-string Apply/save, arrays, Unicode page boundaries, cancellation and actual frontend bindings. A Windows native Avalonia probe exercised all three `Examples/tim-ray` files and scrolling to spline serialization rows; results and captures are under `artifacts/large-metadata`. This addresses oversized text layout, while the broader cross-platform/DPI/expanded-tree evidence gaps above remain open.


- [Project application behavior](../applications.md), [architecture](../architecture.md), [I/O contracts](../io-and-limitations.md), [performance](../performance.md), [typed metadata](../metadata-properties.md).
- [Avalonia 12 release](https://avaloniaui.net/blog/avalonia-12/) and [Avalonia 12 breaking changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes): choose current compatible packages and migration APIs.
- [Avalonia storage provider](https://docs.avaloniaui.net/docs/services/storage/storage-provider): native desktop dialogs and storage semantics.
- [Avalonia WriteableBitmap API](https://api-docs.avaloniaui.net/docs/T_Avalonia_Media_Imaging_WriteableBitmap): explicit framebuffer access and lifetime.
- [Avalonia headless xUnit testing](https://docs.avaloniaui.net/docs/testing/headless-xunit): framework-specific test setup, dispatcher and isolation requirements.
- [Microsoft MVVM Toolkit](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/): framework-independent MVVM support.
- [ImageSharp project and license](https://github.com/SixLabors/ImageSharp): evaluate decoder capabilities and current license eligibility before selecting the dependency.
