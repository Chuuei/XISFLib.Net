# XISF specification coverage

This audit targets [XISF 1.0 Revision 1, document version 1.01](https://pixinsight.com/doc/docs/XISF-1.0-spec/XISF-1.0-spec.html). Astrometric solutions have their own revision, currently `1.0`.

The table describes implemented format support. It is not a certification of interoperability with every producer. Automated tests exercise the implementation and representative generated headers have also been checked against the published XSD. Independent PixInsight files containing real fitted distortion models would provide additional interoperability evidence.

## Findings addressed by this audit

The previous library had useful container, image, compression and metadata foundations, but it did not implement the complete revised specification. The audit identified these gaps and added or corrected their implementations:

- Native astrometric solutions were missing; the existing FITS WCS helper covered only linear and TAN transformations. Native solution evaluation, version handling, layer fallback, distortion models and preservation are now separate APIs.
- Zstandard encoding and decoding were missing. Writer control over inline/embedded blocks, compression subblocks and alignment was also incomplete.
- Float128 properties passed through double precision. Dedicated binary128 and complex binary128 storage now preserves their precision, including vectors and matrices.
- Array-valued table cells and empty collections lacked conforming serialization. String blocks now handle characters that cannot appear literally in XML.
- RGB working spaces lacked primary y coordinates and used supplied luminance coefficients without deriving them from the D50 white point. Lab rendering and display-function order differed from the revised definitions.
- Signatures used an enveloped layout. New signatures are detached, whole-root signatures; attachment layout and integrity checks now account for that layout.
- Unsupported optional representations could prevent otherwise useful content from being read. Availability diagnostics, opaque block preservation and astrometric fallback now expose usable content.
- Creation metadata, UUID rules, thumbnail constraints, XISB index lengths, table comments, FITS commentary keywords and case-sensitive property identifiers needed additional conformance handling.

## Implemented format features

| Specification area | Implementation |
| --- | --- |
| XML header and references | UTF-8 XML 1.0; namespace-aware core elements; `uid` references with duplicate, chain and cycle checks; original XML and extension preservation |
| Containers | Monolithic `.xisf`, distributed `.xish`, indexed `.xisb`; linked index nodes, free entries, unique identifiers and reserved fields |
| Data blocks | Attachments, embedded `Data`, inline base64/hex, direct and indexed external resources; both byte orders; configurable alignment |
| Compression | zlib, LZ4, LZ4HC and Zstandard; byte shuffling; independent compression subblocks; bounded decoding |
| Checksums | SHA-1, SHA-256, SHA-512, SHA3-256 and SHA3-512 over serialized bytes, before decompression; portable SHA-3 fallback |
| Scalar properties | Boolean, signed/unsigned integers through 128 bits, Float32/64/128, Complex32/64/128, UTF-8 String and UTC TimePoint; standard aliases and numeric spellings |
| Collections | All specified integer, real and complex vectors/matrices, including binary128; dimensions, byte order, empty inline collections and array-valued table cells |
| Property presentation | Width, alignment, fill, sign, precision, numeric base, Boolean and floating-point formatting through `XisfPropertyFormatter`; original format/comment retained |
| Images | All specified unsigned, real and complex image sample types; arbitrary axes; planar/interleaved channels; Gray, RGB and CIELab; alpha channels, bounds, orientation, UUID, offset and image classifications |
| Ancillary image elements | FITS keywords, ICC profiles, thumbnails, CFA, RGB working spaces, display functions and resolution |
| Color definitions | D50 RGB/XYZ/normalized CIELab conversion; luminance derived from primary chromaticities; numeric gamma and sRGB transfer function |
| Display functions | Clipping, midtones and range expansion in the specified order; recommended adaptive algorithm, including inverted images and linked RGB channels |
| Metadata | Global, image and creation properties; reserved metadata identifiers remain accessible through typed property dictionaries, including identifiers without a convenience accessor |
| Astrometry | All seven projections; spherical rotation and native pole parameters; both projective directions; all six spline kernels; Global, Local and Fallback terms; provenance preservation; revision negotiation and layer fallback |
| Digital signatures | Detached XML signature following the root; whole-root references; RSA creation, embedded-key verification and external-block integrity checks; canonical XML 1.1 for complete XISF root/SignedInfo trees |
| Validation | Semantic diagnostics and optional caller-supplied XSD validation through `XisfDocument.Validate(XmlSchemaSet)` |

## Compatibility and preservation

Unknown property types and optional objects expose availability diagnostics and retain their original XML. An unsupported image does not prevent access to other images. Astrometric layers fall back independently as required; unknown major revisions preserve the entire solution without interpreting it. No FITS keyword is used to evaluate a native XISF astrometric solution.

Saving a loaded `XisfDocument` preserves opaque serialized blocks without requiring their compression codec. `XisfImageData.FromImage` materializes pixels and known image metadata for editing; use the document-preserving writer when arbitrary extension or unsupported ancillary objects must survive. Unknown astrometric properties and unavailable properties/table cells retain their original representations in editable copies.

For unchanged signed documents, the writer retains the signed XML and attachment positions. If an edited layout requires a new signature, use `WriteSigned` with the private key. Signature verification establishes integrity against the embedded key; application code must establish signer trust. Certificate-chain policy and arbitrary XML signature subset transforms are outside these XISF signing helpers.

## Operational bounds and external services

Reader limits are configurable application policies, not omissions from the file format. They bound XML, buffers, blocks, expansion ratios and collection sizes. Spline evaluation currently limits polynomial order to 1024; formatting limits precision to 10,000 digits and width to 1,000,000 characters. Image and collection dimensions use .NET arrays and therefore have CLR allocation limits.

HTTP(S) and file URLs have built-in readers. Other URI schemes can be supplied through `ExternalResourceResolver`; network and filesystem permissions still apply. Resource retrieval protocols are not defined by XISF. See [I/O contracts](io-and-limitations.md).

The astrometry API evaluates stored solutions. Catalog queries, fitting/re-solving, conversion between celestial reference systems, and proper-motion propagation require application-level astronomy services. These are not part of the specified stored transformation. Floating-point evaluation cannot guarantee the specification's positional tolerance for arbitrarily ill-conditioned user-supplied matrices or coefficients.

## Regression evidence

`SpecificationRevisionTests` covers projection reference points and round trips, poles, independently stored inverse transforms, layer fallback, spline bases and weighting, Zstandard, storage modes, subblocks, alignment, binary128 bit-pattern round trips, nonfinite values, integer spellings, D50 color conversion, adaptive display, external resource policy, detached signatures and opaque block preservation. Existing reader/writer, FITS, validation, Windows and regression suites remain part of verification.

Schema validation checks structural rules; semantic validation and decoding test numerical and binary rules separately. A clean schema result alone does not prove a fitted astrometric solution is accurate.
