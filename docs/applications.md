# Applications

The solution includes WPF and Avalonia workbenches, a batch converter, and benchmark harnesses. They demonstrate the public library APIs; reference `XISFLib` or `XISFLib.Windows` directly when integrating the library into another application.

Both viewers bind the same workbench and utility state. Loading, selection, editor drafts, saving, diagnostics and calculations follow one implementation in `Viewer.Core`. Property dialogs share staged paging, and zoom/pan share viewport geometry. Frontends provide native controls, dialogs, grid commits and bitmap handling. Writer defaults are shared: zlib, SHA-256 and byte shuffling; creator-application names identify the frontend.

## Avalonia viewer

Run with the .NET 10 SDK:

```powershell
dotnet run --project XISFLib.AvaloniaViewer -c Release
```

It supports XISF/FITS loading, lazy image selection, full-resolution previews, linear/automatic stretching, Apply, zoom/pan, unload, the information tree, validation, image/document editing, raster import, attachments, all save modes and utilities. Session loading, rendering, editor parsing, raster decoding, save operations and utility calculations are shared with WPF. The image is presented before metadata and validation; failed loads retain the previous session.

The Image metadata tab supports property and FITS keyword add/edit/remove and **Apply metadata edits**. Applying commits the active grid cell and stages parsing before changing the working image. Matrix rows/columns, declared types, formats/comments, and repeated FITS commentary are retained. Unsupported property types and unavailable values are preserved as read-only rows. New unsupported text types and duplicate property IDs are rejected. Explicitly removing a preserved row removes that property.

Expandable sections also edit identifier/type/color space/orientation, offset, version-4 UUID, resolution, CFA, RGB working space, and display function. RGB luminance coefficients are derived from the primaries on Apply. Blank optional sections remove their metadata. Both frontends use the same staged attribute parser; malformed values fail before changing the working image. Apply refreshes the preview, including orientation and DPI. Display-function editing stores its parameters; the automatic/linear preview controls retain their current stretch mode.

Switching images discards unapplied editor values; applied metadata stays with each working image. The Information tree and validation describe the loaded source until saving reloads the output. Loading another file or unloading discards working edits. Re-encoded saves also apply current editor drafts after committing active cells.

The first-image-table section edits scalar fields and rows using the shared syntax described below. Parsing finishes before any image edits are applied. Other tables and the first table's comment are retained. A blank table identifier leaves an existing table unchanged.

The Writer tab edits document metadata and properties, plus the creator-application setting. **Apply document edits** commits active cells and parses both grids before replacing the applied snapshot. Creation-time and creator-application properties are excluded from the initial metadata rows; output creation metadata follows writer policy. Document edits survive image switching and are cleared on load/unload. The creator setting persists. These controls prepare selected-image output; they do not imply preservation of every image or extension in the original container.

Both viewers import frame zero of PNG/JPEG/TIFF/BMP/GIF through Magick.NET-Q16-AnyCPU 15.0.0 into UInt8 RGB/RGBA. They detect actual transparency, retain decoded DPI and omit alpha/ID/resolution from imported thumbnails. Import does not promise preservation of high-bit raster storage, EXIF orientation, every TIFF variant or embedded color-profile rendering. Raster format signatures are restricted before invoking the decoder. ICC attachment preserves profile bytes subject to the XISF writer's header normalization/validation. Import and attachment reads run away from the UI thread.

The Utilities tab exposes astronomy metadata, sampled statistics, histograms (up to 65,536 bins), interleaved region/channel reads with a 64-value preview, and FITS WCS transforms using zero-based pixel coordinates. Heavy calculations run on workers; display stretching does not alter scientific samples. Writer settings and save modes follow the behavior below.

Windows native publication and all six tab renders have been smoke-tested. CI now tests and probes standalone builds on Windows x64, Linux x64 and macOS arm64 before publishing ZIP artifacts and attaching them to version-tag releases. Linux/macOS runner results and full interactive native-dialog/DPI/scrolling verification remain outstanding; the [migration audit](plans/avalonia-viewer-migration.md) records the evidence boundary. The WPF release asset and Pages deployment rules are retained.

Publish a self-contained Windows executable with `dotnet publish XISFLib.AvaloniaViewer/XISFLib.AvaloniaViewer.csproj -c Release -p:PublishProfile=WinX64`. It produces `artifacts/avalonia/win-x64/XISFLib.AvaloniaViewer.exe`; native libraries self-extract at startup. On Linux/macOS, extract the matching release ZIP, run `chmod +x XISFLib.AvaloniaViewer` if necessary, and launch it from a terminal. These builds are unsigned; OS download/security prompts may apply. Linux requires desktop X11/font libraries as described in [Avalonia's deployment guide](https://docs.avaloniaui.net/docs/deployment/linux), plus the native decoder dependencies tested by CI. Local file paths are required for lazy and distributed-resource access.

Magick.NET 15 uses [Apache-2.0](https://github.com/dlemstra/Magick.NET/blob/main/License.txt); its bundled native components have additional notices. Both single-file applications embed the package's complete notices and expose **Third-party notices**.

The implementation handoff is `docs/plans/avalonia-viewer-migration.md` in the repository, with evidence and remaining parity requirements.

## WPF workbench

Run on Windows with the .NET 10 SDK:

```powershell
dotnet run --project XISFLib.WpfViewer -c Release
```

Open `.xisf`, `.xish`, `.fit`, `.fits`, or `.fts` files and select an image in multi-image documents. Raster import accepts PNG, JPEG, TIFF, BMP, and GIF images. Linear and automatic stretched displays affect the preview only. Use the mouse wheel to zoom and drag with the left button to pan.

Loading and rendering run on worker threads. The first preview is presented before the metadata tree, validation and editor initialization finish. Additional XISF images are decoded when selected. See [performance](performance.md) for the measured loading stages and benchmark commands.

The editor exposes document/image properties, creation metadata, FITS keywords, image attributes, resolution, CFA, RGB working space, display functions, ICC profiles, thumbnails, and the first image table. Utilities demonstrate astronomy metadata, sampled statistics, histograms, region/channel extraction and FITS WCS coordinate conversion.

### Save behavior

| Mode | Output and preservation |
| --- | --- |
| Byte-identical copy | Available for loaded monolithic XISF and enabled by default. Copies the complete source, including all images, metadata, extensions and signatures. Editor changes and writer settings are not applied. Saving to the same path leaves the source unchanged. |
| Re-encoded XISF | Clear **Byte-identical copy**. Applies editor changes and compression/checksum/shuffle settings, then writes the selected image and configured document metadata. |
| Distributed XISF | Writes a `.xish` header and a sibling `.xisb` file for the selected image. |
| FITS | Writes the selected image as FITS with checksums. XISF compression settings do not apply. |

Re-encoding can change XML ordering, creation metadata, attachments and file size. It does not retain every unknown extension, original signature or other image/HDU. Saved output is reloaded and its conformance diagnostics are displayed. The library's loaded-document writer provides broader XML and opaque-block preservation; see [I/O contracts](io-and-limitations.md) and [specification coverage](specification-compliance.md#compatibility-and-preservation).

### Editor limits

Both viewers show single-line property previews of at most 256 value characters, with a character count for longer text. Selecting an image-property row shows its identifier above the grid. Use **Open…** to inspect or edit the complete value in 4,096-character pages, or export the full text as UTF-8. **Accept edits** stages the edited value in the row; use Apply or re-encoded Save to apply it. Closing the dialog discards its staged changes. Unsupported types remain read-only. A page accepts up to 8,192 characters; edit additional pages or use the library API for larger insertions.

Previews never replace property content. Large numeric arrays show an element count and are formatted on a worker only when opened; unchanged arrays retain their original typed values, shape, format and comment. The Information tree also uses bounded summaries. WPF property grids have a fixed viewport with recycling virtualization, and both viewers use fixed-height rows so serialized spline models cannot expand the scrolling layout.

The workbench's text editors cover fewer types than the library. Scalar editing supports strings, Boolean, integers through 64 bits, Float32/64 and timestamps. Numeric vector/matrix editing supports integers through 128 bits and Float32/64, with comma-separated values and explicit matrix dimensions. Complex and binary128 values and unavailable properties are preserved as read-only rows; new values require the typed library APIs. Use the typed library APIs to edit them, or byte-identical copy to preserve an unchanged monolithic file.

Table fields use one `id|type|label|format` definition per line; label and format are optional. Rows use one line per row with pipe-separated cells matching the fields. Both viewers edit only the first image table. Tables containing array/unsupported/null cells, delimiters, or cell text that this syntax cannot round-trip remain read-only and are preserved unchanged. Use the typed library APIs to edit them. Scalar timestamps retain fractional seconds and offsets.

## Batch XISF-to-FITS converter

```powershell
dotnet run --project XISFLib.Convert -c Release -- "D:\Astro\Captures" 4
```

The folder argument is required; the optional positive concurrency argument defaults to eight. Only `.xisf` files directly in that folder are processed, with case-insensitive extension matching. Each produces a sibling `.fits` file containing all its images. Existing outputs are refused, source files are retained, and failed conversions do not stop the remaining files. Progress messages may appear out of order. Lower concurrency for large files to reduce peak memory use.

| Exit code | Meaning |
| --- | --- |
| 0 | Success, including an empty input folder |
| 1 | Conversion or enumeration failure |
| 2 | Invalid arguments |

Existing FITS keywords take precedence. Missing standard cards are populated from native XISF astronomy properties, with image properties taking precedence over global properties. Center RA/DEC is stored in degrees; native astrometric models are not translated into FITS WCS.

An `XISFMETA` binary-table extension archives the original XML header and decoded global/image properties as UTF-8 XML in a `METADATA` byte column. This preserves Unicode and property arrays beyond ordinary FITS cards. It does not copy ancillary binary attachments or convert XISF tables into FITS tables; original XML block locations still refer to the source XISF. Keep the source for full-fidelity recovery. Complex images cannot be converted to standard FITS image HDUs. See [conversion limits](io-and-limitations.md#conversion-and-numeric-precision).

The metadata archive is a batch-application feature. The core `FitsConverter` APIs perform image/metadata conversion without automatically adding this archive.

## Benchmarks and example files

Use Release builds. The default benchmark accepts an examples-folder argument and measures `slow.xisf` and `slow.fits`; the specialized `--performance`, `--fits-performance` and `--wpf-load` modes cover synthetic processing, the FITS examples and viewer presentation respectively. The [performance guide](performance.md) contains complete commands and explains allocation and timing limits.

The repository's `Examples` folder includes real XISF/FITS inputs. `Examples/README.md` documents provenance and fixture caveats; `sources.json`, `validation.json`, and `fits-validation.json` record downloaded XISF sources and validation results. Example files are test/benchmark inputs and are not required by library consumers.
