# Astronomy example collection

This folder contains the user-supplied `slow.xisf` and **62 downloaded XISF files (1,292,323,155 bytes, approximately 1.29 GB)**. The downloaded originals have not been re-encoded or edited.

| Folder | Files | Useful features | Source |
| --- | ---: | --- | --- |
| `xisfits/` | 9 | 256 × 256 Gray/RGB; UInt8/16/32 and Float32/64; zlib and shuffled zlib | [Víctor R. Ruiz's XISFITS test images](https://github.com/vrruiz/xisfits/tree/f38642761d10895a3dd97e877d135f8c9ab5d1da/tests/images) |
| `xisf-rust/generated/` | 40 | Five sample formats; raw, zlib, LZ4, LZ4HC, Zstandard and shuffled blocks; RGB cases | [Independent libXISF-generated corpus](https://github.com/cruzzil/xisf/tree/7043131801abc829ed05c002c7c2a85f818ef4b9/corpus/generated) |
| `xisf-rust/pixinsight/` | 3 | PixInsight output; embedded base64; Float32 attachments; shuffled zlib and SHA-256 | [PixInsight fixtures in the Rust XISF repository](https://github.com/cruzzil/xisf/tree/7043131801abc829ed05c002c7c2a85f818ef4b9/corpus/pixinsight) |
| `meridian/NGC3718/` | 5 | Full-resolution 6248 × 4176 UInt16 telescope frames; red/luminance acquisition metadata | [Mac Observatory's Meridian sample archive](https://macobservatory.com/meridian-review-sample-data/) |
| `nina/` | 1 | N.I.N.A. autofocus test image | [N.I.N.A. TestImage_Jelly](https://github.com/isbeorn/nina/blob/5f137612aba9a4360f365232763525bfd8d4a866/NINA.Test/Autofocus/TestImage_Jelly.xisf) |
| `community/` | 1 | Large Float32 master shared for plate-solving troubleshooting | [AmyWarble's public AstroBin thread](https://app.astrobin.com/forum/topic/168667/pleiades-astrophoto-pixinsight/why-cant-i-plate-solve-this-image-in-pixinsight) |
| `tim-ray/` | 3 | Sh2-188 Ha/OIII/SII masters; each contains a 6016 × 6016 Float32 integration and weight map; legacy spline astrometry | [Tim Ray / NRIO's public practice datasets](https://app.astrobin.com/forum/topic/198004/data-for-practice) |

`slow.xisf` provides an additional large, big-endian, zlib-compressed RGB Float32 example. Its source was not provided.

## FITS examples

The user supplied four FITS files. They are uncompressed Float32 RGB images in planar, big-endian storage, with identity physical scaling and no declared DATASUM/CHECKSUM cards.

| File | Image dimensions | File bytes |
| --- | --- | ---: |
| `slow.fits` | 4092 × 4676 × 3 | 229613760 |
| `372x3m_m81_m82_ff80_0.76x_2600air_uvircut.fits` | 6405 × 4424 × 3 | 340038720 |
| `horsehead_164x2m_hyperstar_unfiltered_poseidon-cpro.fits` | 6365 × 4266 × 3 | 325846080 |
| `m31_190x3m_2600_ff80.fits` | 6324 × 4288 × 3 | 325416960 |

All four loaded and converted successfully with zero FITS conformance diagnostics. [fits-validation.json](fits-validation.json) records their headers, raw/converted SHA-256 hashes, linear/adaptive rendering hashes, and hashes of checksummed FITS serialization. These outputs match the implementation before the FITS performance changes. The original example files have not been modified.

## Metadata inventory

[metadata-inventory.json](metadata-inventory.json) records the 119 distinct XISF property identifiers and 181 FITS keyword names found across all 63 XISF and four FITS files, including FITS cards embedded in XISF. It lists observed storage types without assigning inferred units or meanings. The typed catalogs cover all these names; see [typed metadata](../docs/metadata-properties.md) for standard definitions, application conventions, and ambiguous representations.

## Astrometry

The Sh2-188 masters contain `PCL:AstrometricSolution:*` properties, including legacy spline data. They are useful for metadata decoding, preservation and multi-image handling. They do **not** use the September 2026 standard `AstrometricSolution:*` namespace, and the native Revision 1 evaluator does not interpret them. No downloadable file with that new namespace was found during this search.

## Verification

All **63 XISF files loaded and their 66 image pixel blocks decoded successfully** with XISFLib. Any declared block checksums were checked during decoding. No parsed property was reported unavailable.

[validation.json](validation.json) records per-file results, decoded-byte counts, decoded-pixel SHA-256 hashes and semantic diagnostics. Decoding success does not mean strict conformance: the original files include findings for missing explicit UTF-8 declarations, creation timestamps stored as String instead of TimePoint, ICC embedded-profile flags and absent FITS keyword comments. Those findings remain visible; the source files have not been repaired.

[sources.json](sources.json) records retrieval time, immutable repository revisions where applicable, download URLs, archive member names, original filenames, file sizes, SHA-256 hashes and image header features. It includes the user XISF file so the complete XISF collection can be identified.

## Attribution and use

Repository licenses are retained beside the corresponding examples: `xisfits/LICENSE`, `xisf-rust/LICENSE` and `nina/LICENSE.txt`. `xisf-rust/CORPUS-README.md` preserves the upstream fixture provenance.

The Mac Observatory and community files were publicly shared as sample/practice data; no separate general redistribution license was identified. Their original source pages and stated usage conditions are recorded in the manifest. Tim Ray asks that processed practice results be posted only on AstroBin. Public download access does not establish blanket redistribution rights for these files.

The repository already configures `.xisf` files for Git LFS. No files were committed or uploaded as part of collecting these examples.

To browse them in the example application:

```powershell
dotnet run --project XISFLib.WpfViewer
```
