# Performance

The library uses bulk copies for matching numeric representations, vectorized byte-order reversal, direct decompression into the final subblock buffer, and cached RGB working-space matrices. Adaptive display functions use exact median selection instead of sorting every sample twice. These paths retain checked numeric conversions, floating-point bit patterns, complex component order, checksum verification before decompression, and declared decompression limits.

Matrix caches use weak references to working spaces and detect changes to both primary-coordinate lists. Inverse matrices are computed only when needed. Rendering computes orientation once per image and allocates its adaptive sample buffers once per channel.

## Measurements

The following measurements compare the implementation before and after the October 2026 performance changes, on Windows with .NET 10.0.12 and an AMD Ryzen 9 9950X (32 logical processors). Each operation was warmed once; the table shows medians of five baseline and seven optimized iterations. Full garbage collections occurred outside the timed operations. File caches were warm. Allocation counts include managed allocations across all threads, rather than peak working set or native codec memory. These measurements describe this machine and workload; they are not performance guarantees.

| Operation | Before (ms) | After (ms) | Before allocation (MiB) | After allocation (MiB) |
| --- | ---: | ---: | ---: | ---: |
| Create owned UInt16 samples, 2048 × 2048 | 37.62 | 0.38 | 104.00 | 8.00 |
| Create owned Float32 samples, 2048 × 2048 | 28.73 | 0.78 | 63.42 | 16.00 |
| Read matching Float32 samples, 2048 × 2048 | 44.98 | 7.92 | 32.00 | 32.00 |
| Decode 16 MiB of shuffled Zstandard subblocks | 23.95 | 23.20 | 64.71 | 32.38 |
| Write a 1024 × 1024 Float64 property matrix | 11.38 | 4.07 | 40.06 | 16.06 |
| Render a 256 × 256 Lab image | 69.03 | 9.97 | 35.25 | 0.25 |
| Render a rotated 1024 × 1024 RGB image | 34.91 | 24.98 | 112.00 | 8.00 |
| Adaptive display parameters, 262144 samples | 35.64 | 6.32 | 10.00 | 2.00 |
| Decode `Examples/slow.xisf`, RGB zlib | 427.71 | 427.80 | 220.98 | 220.98 |
| Render its owned image with adaptive stretch | 417.85 | 348.70 | 120.99 | 84.99 |

The real-image rendering measurement excludes file loading and decompression. The separate decode measurement includes reading its attached compressed block. Matching typed reads still allocate the decoded byte buffer and the returned typed array; public APIs return independently owned buffers.

## Run the benchmarks

From the repository root, on Windows:

```powershell
New-Item -ItemType Directory -Force artifacts
dotnet run --project XISFLib.Benchmarks -c Release -- --performance 7 artifacts/performance.json
```

The synthetic cases run without example files. Real-image cases run when `Examples/slow.xisf` is present. Run comparisons on the same hardware, runtime, build configuration, and input files, with other heavy workloads stopped. The existing whole-file XISF/FITS benchmark remains available by running the project without `--performance`. Timing thresholds are excluded from correctness tests.

## Choosing efficient API paths

- Request the stored numeric type and channel layout when possible. Converting types uses checked conversions; changing layout needs an additional array.
- Reuse an owned `XisfImageData` when repeatedly rendering an unchanged loaded image. `ReadRawData` and typed reads decode afresh rather than retaining a hidden image-sized cache.
- Use `ReadRawData` when serialized sample representations are needed directly. `GetNormalizedSamples` produces a double array and applies the declared bounds.
- Choose compression using representative images. Zlib decoding of the large example remains the main cost; changing codecs trades compatibility, compression ratio, and encoding time against decoding speed.

These optimizations use exact arithmetic and order statistics. They do not approximate astronomical transforms, color conversions, or display statistics, and do not disable validation or checksum checks. File-backed zlib attachments with checksums retain a verification pass before decompression, as required by the specification.

Correctness checks include bit-level floating-point and endian cases, exact UInt64 values, complex and binary128 storage, mutable color primaries, sorted-reference adaptive parameters, and all supported subblock codecs with shuffle tails. All 63 collected example files (66 images) decoded to the same SHA-256 hashes and produced the same validation diagnostics before and after these changes. Some example files have pre-existing specification diagnostics; see [specification coverage](specification-compliance.md) and the example manifest.
