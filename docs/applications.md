# Applications

The solution includes a Windows WPF workbench, a batch converter, and a benchmark harness. They demonstrate the public library APIs; reference `XISFLib` or `XISFLib.Windows` directly when integrating the library into another application.

## WPF workbench

Run on Windows with the .NET 10 SDK:

```powershell
dotnet run --project XISFLib.WpfViewer -c Release
```

Open `.xisf`, `.xish`, `.fit`, `.fits`, or `.fts` files and select an image in multi-image documents. Raster import accepts PNG, JPEG, TIFF, BMP, GIF, and Windows-supported WDP/JXR images. Linear and automatic stretched displays affect the preview only. Use the mouse wheel to zoom and drag with the left button to pan.

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

The workbench's text editors cover fewer types than the library. Scalar editing supports strings, Boolean, integers through 64 bits, Float32/64 and timestamps. Numeric vector/matrix editing supports integers through 128 bits and Float32/64, with comma-separated values and explicit matrix dimensions. Complex and binary128 values and unavailable properties do not have full text-editing support. Use the typed library APIs to edit them, or byte-identical copy to preserve an unchanged monolithic file.

Table fields use one `id|type|label|format` definition per line; label and format are optional. Rows use one line per row with pipe-separated cells matching the fields. This text interface cannot escape a literal pipe in a cell and edits only the first image table. Array-valued table cells require the typed library APIs.

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
