# Avalonia viewer implementation plan

Status: **planned; implementation has not started**. All checkboxes below are initially unchecked. This document is the implementation handoff, not a claim that an Avalonia project already exists.

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
| F01 | Browse/type paths; load `.xisf`, `.xish`, `.fit`, `.fits`, `.fts`; meaningful errors | Each extension, missing file, corrupt header, no convertible image | Pending |
| F02 | Multi-image selection, labels, lazy XISF materialization, session reset/unload | Two-image fixture; no second-image decode before selection; unload/reload | Pending |
| F03 | Full-resolution Gray/RGB/Lab and alpha display, supported sample types, bounds/layout/endian/orientation/DPI | Shared BGRA byte equality with the pre-extraction baseline | Pending |
| F04 | Linear/auto stretch, linked/unlinked channels, target background, clipping, Apply/Enter/focus behavior | Compare controls, validation and pixel output | Pending |
| F05 | Fit-to-viewport initial display; pointer-anchored wheel zoom 1–64 in steps of 1.2; left-drag pan and capture release | Headless gestures plus real DPI/scaling smoke tests | Pending |
| F06 | Image-first presentation, busy state, status/error feedback and disabled editing during operations | Staged-load observations, overlapping actions, failure recovery | Pending |
| F07 | Raster import PNG/JPEG/TIFF/BMP/GIF; frame zero, RGB/RGBA, identifier, DPI; reject unsupported formats | Small independent decoder fixtures, Windows WPF differential checks, OS codec matrix | Pending |
| F08 | Complete information tree: file/document/global/image metadata, tables, ancillary data, XML and FITS HDU cards/comments | Compare shared tree output/order and expand/scroll behavior | Pending |
| F09 | Image ID/type/color/orientation/offset/UUID/resolution editing | Save/reload each field and malformed values | Pending |
| F10 | Image property add/edit/remove, scalar/vector/matrix types and dimensions; retained format/comment fields | Parser boundary tests; commit grids before save; serialized declared types | Pending |
| F11 | FITS keyword add/edit/remove; quoting/comments and repeated ordered HISTORY/COMMENT | Save/reload and comparison of ordered cards | Pending |
| F12 | CFA fields; RGB gamma/sRGB/x/y chromaticities and derived luminance; display-function editing | Save/reload and scientific render regression | Pending |
| F13 | ICC profile attachment and thumbnail import without alpha | Byte/profile equality, thumbnail dimensions/sample format, reload | Pending |
| F14 | First image table identifier/caption/fields/rows using existing text syntax | Save/reload values; invalid field/row counts; editor limitations documented | Pending |
| F15 | Document creation metadata and document properties editing | Serialized types/values, writer policy and reload | Pending |
| F16 | All current compression/checksum options, shuffle, creator setting and defaults | Selected options recorded in output and checked by reader | Pending |
| F17 | Default byte-identical copy for monolithic XISF, ignores editor settings, same-file no-op | Whole-file hashes including multi-image/unknown/signature content; same-source safety | Pending |
| F18 | Re-encoded selected-image XISF; distributed `.xish` + sibling `.xisb` | Reload selected image and metadata; unchanged source; output failure tests | Pending |
| F19 | Selected-image FITS save with checksums and visible conversion omissions | CHECKSUM/DATASUM verification; loss findings; complex-image rejection | Pending |
| F20 | Reload after save and explicit validation; XISF/FITS diagnostics with severity/code/message | Independent invalid fixtures; compare findings and UI states | Pending |
| F21 | Astronomy metadata overview and missing-value display | Compare public utility results, units, observer and exposure values | Pending |
| F22 | Sampled channel statistics, sample limit, normalize flag and all current columns | Same options/results including finite/non-finite samples | Pending |
| F23 | Histogram selected channel/bins (maximum 65,536), rows/range/underflow/overflow/non-finite counts | Same histogram values and invalid-input errors | Pending |
| F24 | Interleaved region and channel reads; count and first 64 values | Compare arrays and preview formatting; out-of-bounds errors | Pending |
| F25 | FITS WCS summary, pixel→sky/sky→pixel, reciprocal field updates, unavailable state | Same numeric results and zero-based coordinates | Pending |
| F26 | Native dialogs, overwrite/extension behavior, focus/tab navigation and usable grid/tree scrolling | Interactive smoke checklist on supported OSes | Pending |
| F27 | Standalone publication, release/download/run instructions, CI test and doc coverage | Clean-machine Windows launch and per-RID smoke evidence | Pending |

The current WPF editor has limits: complex/binary128/unavailable properties are not fully text-editable; only the first image table is edited; pipe-delimited cells cannot escape literal pipes. Preserve and document these limits. The full library's feature set is broader than either viewer. If a migration exposes destructive handling of unsupported metadata or another defect, fix the shared behavior for both applications with regressions rather than using the defect as the parity target.

## Ordered implementation phases

Each phase should be a reviewable commit with a green applicable build/test set. Keep WPF working throughout. Add tests with the functionality rather than postponing all testing until the end. Update this file with completed checkboxes, evidence paths, selected versions, deviations, and the next action.

### 1. Freeze the baseline and resolve platform spikes

- [ ] Recheck every WPF event handler/control against F01–F27; record defaults, supported edits, commit/discard behavior, and error paths.
- [ ] Build/test existing solution and capture representative render hashes, save/reload results, metadata trees, utility values and load-stage timings under ignored `artifacts/avalonia-baseline/`.
- [ ] Verify compatible stable Avalonia desktop/theme/grid/headless packages, licensing, diagnostics changes, and .NET 10 support.
- [ ] Prototype framebuffer transfer (BGRA, alpha, stride, DPI), editable-grid row addition/deletion and commit, metadata-tree performance, and native local-path dialogs.
- [ ] Run raster import compatibility spikes for PNG/JPEG/TIFF/BMP/GIF; choose decoder(s) and record platform gaps with a concrete implementation path.

Exit: baseline evidence exists; dependencies and difficult platform features have tested approaches. No unresolved codec is treated as optional parity.

### 2. Extract platform-independent display rendering

Depends on phase 1.

- [ ] Move the numeric/display part of `XisfWindowsExtensions` into core `Imaging`, including options, orientation and DPI helpers.
- [ ] Keep `ToBitmap`, `ToBitmapSource`, and `ToBgra32Pixels` Windows APIs as wrappers over the same renderer; avoid duplicate image-sized buffers.
- [ ] Add core render tests and retain Windows adapter tests, including all orientations, layouts/endians, RGB/Lab/alpha, non-finite values and stored display functions.
- [ ] Compare against frozen baseline hashes and measure allocation changes; update API docs and any type forwarding.

Exit: WPF output matches baseline, the renderer builds/tests without Windows, and no UI framework has entered core.

### 3. Extract shared viewer behavior and adopt it in WPF

Depends on phase 2.

- [ ] Add `XISFLib.Viewer.Core` and its tests to the solution.
- [ ] Extract session loading/selection, row models, property/table codecs, metadata-tree construction, save mode selection, diagnostics and utility orchestration.
- [ ] Introduce testable async operation state and framework service interfaces. Stage editor parsing before mutation; keep buffer ownership/source lifetime explicit.
- [ ] Adapt WPF handlers incrementally to the shared view models/services. Preserve control names or update existing reflection-based benchmarks alongside them.
- [ ] Test byte-copy versus re-encode, selected-image scope, conversion-loss presentation, failed operation recovery and editor commit/discard rules.
- [ ] Handle OS-specific path identity safely; do not carry Windows case-insensitive comparisons into Linux same-file logic. Never overwrite a source through an alias accidentally.

Exit: WPF consumes the shared behavior with equivalent output and performance. Scientific logic/editor parsing is not copied into a second frontend.

### 4. Add the Avalonia project and application shell

Depends on phase 3.

- [ ] Create `XISFLib.AvaloniaViewer` with desktop `Program.cs`, `App.axaml`, `MainWindow.axaml`, selected theme and compiled bindings.
- [ ] Add solution entries and references to viewer core/core library; verify there is no WPF, WinForms, `System.Drawing.Common`, or `XISFLib.Windows` dependency in the portable frontend.
- [ ] Build the sidebar and six tabs, compose dialog/error/scheduler services, wire status and command enablement.
- [ ] Add `XISFLib.AvaloniaViewer.Tests`, the actual selected headless package/theme, and a startup/binding test. Confirm xUnit package compatibility with the repository.

Exit: both applications build and start; the Avalonia shell binds to shared state. Empty tabs are still marked pending, not completed features.

### 5. Implement load, preview, image navigation and viewport

Depends on phase 4.

- [ ] Wire F01–F06 with the shared loader, lazy selection and BGRA renderer.
- [ ] Transfer rows into Avalonia bitmap storage with explicit alpha mode/stride/DPI and deterministic disposal.
- [ ] Port pointer anchoring, zoom clamp/step, pan capture/loss/release and viewport reset/resize behavior.
- [ ] Present the bitmap before metadata/editor work; guard session replacement/window closure and suppress obsolete results.
- [ ] Test malformed files, unsupported render types, multi-image files, failed loads and operation recovery. Preserve useful diagnostics even if an image cannot be displayed.

Exit: astronomy files display with verified pixel parity and responsive UI; F01–F06 have evidence.

### 6. Migrate information and diagnostics views

Depends on phase 5.

- [ ] Bind complete shared metadata trees, XML nodes, FITS HDU headers, comments, tables and ancillary values.
- [ ] Preserve expanded defaults/order and empty-state behavior; verify virtualization or incremental population with large trees rather than assuming WPF settings port directly.
- [ ] Display both diagnostic types and explicit validation results, including conversion omissions after saves.

Exit: F08 and the validation portion of F20 are verified, with no full tree build blocking the first preview.

### 7. Migrate all image/document editors

Depends on phase 6.

- [ ] Implement image attributes, property/FITS grids, document metadata/properties, and row add/remove/commit behavior.
- [ ] Implement CFA, working-space, display-function, ICC and first-table controls; use shared parsing and validation.
- [ ] Preserve matrix dimensions, format/comments, type aliases and current unsupported-editor rules. Explicitly commit focused grid cells before taking a save snapshot.
- [ ] Verify all edited fields through serialized outputs, not only bindings; check invalid edits do not partially corrupt the live session.

Exit: F09–F12, F14 and F15 pass applicable tests; attachment/thumbnail integration completes in phase 8.

### 8. Implement raster import and thumbnail/profile workflows

Depends on phases 1 and 7.

- [ ] Implement the chosen raster-decoder services, native codec dependencies and native-dialog filters.
- [ ] Match frame zero, UInt8 RGB/RGBA, actual transparency detection, thumbnail alpha omission, identifier and DPI behavior.
- [ ] Verify decoded sample results against WPF for independent small fixtures; explicitly investigate color-profile/EXIF/rounding differences.
- [ ] Attach ICC/profile bytes, import thumbnail images, and check resource disposal and background execution for large imports.
- [ ] Close the per-OS codec matrix for the five required formats; do not announce full cross-platform parity prematurely.

Exit: F07/F13 are verified on Windows; portability gaps and their implementation status are explicit.

### 9. Implement every writer/save/reload mode

Depends on phase 8.

- [ ] Wire writer configuration and defaults, including all compression/checksum values and shuffle.
- [ ] Match byte-identical copy availability/default, ignored-editor warning and same-source behavior; verify complete source hashes.
- [ ] Wire selected-image re-encoding, distributed pair paths, FITS checksums and displayed conversion losses using shared save orchestration.
- [ ] Match extension/overwrite/cancel dialog behavior, reload the saved document, and refresh sessions/editors/previews/diagnostics coherently.
- [ ] Exercise same-path save, existing destination, denied access, serialization failure and distributed partial-output contracts. Apply any safety fixes to both viewers.

Exit: F16–F20 pass with byte/hash/numeric evidence and documented preservation limits.

### 10. Migrate all utilities

Depends on phase 9.

- [ ] Bind astronomy overview/missing values and statistics controls/results.
- [ ] Bind histogram settings/limits/rows and region/channel sampling previews.
- [ ] Bind FITS WCS summary, both coordinate directions, reciprocal fields and unavailable states.
- [ ] Reuse existing core calculations; keep expensive reads/calculations off the UI thread and compare results with the same options/cultures.

Exit: F21–F25 are verified. Native astrometry UI would be a separate enhancement added to both viewers, not an invented parity requirement.

### 11. Complete desktop, portability and performance verification

Depends on phase 10.

- [ ] Execute every parity row on Windows, including keyboard/focus, scrolling, pointer capture, dialogs and DPI/scaling.
- [ ] Run Avalonia-specific build/tests and real desktop smoke runs on Linux/macOS; build those projects individually because the existing whole solution includes Windows projects.
- [ ] Check file paths/case, distributed relative resources, Unicode, codec/native-library loading and bitmap resource disposal on each OS.
- [ ] Add an Avalonia staged-load benchmark alongside `--wpf-load`. Measure parsing, materialization, rendering, bitmap assignment and all-tabs-ready separately; neither assignment nor headless tests prove physical screen presentation.
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
| 1 | Not started | Planning source inventory complete; no packages/projects added | Freeze baseline and run compatibility spikes |
| 2–12 | Not started | Follow dependency order above | Begin after predecessor exit criteria |

For each implementation turn, update the relevant phase/feature status, tests run, actual package versions, unresolved issues and next concrete action. Store experimental/generated artifacts under `artifacts/`; commit durable small test fixtures and authored docs normally.

## References checked during planning

- [Project application behavior](../applications.md), [architecture](../architecture.md), [I/O contracts](../io-and-limitations.md), [performance](../performance.md), [typed metadata](../metadata-properties.md).
- [Avalonia 12 release](https://avaloniaui.net/blog/avalonia-12/) and [Avalonia 12 breaking changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes): choose current compatible packages and migration APIs.
- [Avalonia storage provider](https://docs.avaloniaui.net/docs/services/storage/storage-provider): native desktop dialogs and storage semantics.
- [Avalonia WriteableBitmap API](https://api-docs.avaloniaui.net/docs/T_Avalonia_Media_Imaging_WriteableBitmap): explicit framebuffer access and lifetime.
- [Avalonia headless xUnit testing](https://docs.avaloniaui.net/docs/testing/headless-xunit): framework-specific test setup, dispatcher and isolation requirements.
- [Microsoft MVVM Toolkit](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/): framework-independent MVVM support.
- [ImageSharp project and license](https://github.com/SixLabors/ImageSharp): evaluate decoder capabilities and current license eligibility before selecting the dependency.
