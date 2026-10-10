# I/O contracts and limitations

## Ownership and lifetime

File-path XISF loads retain the path and reopen it when an attached block is requested. They do not retain an open file handle. Keep the file available and unchanged while using the parsed document. `XisfImageData.FromImage` materializes an owned image for subsequent editing or processing without the original pixel source.

Monolithic XISF stream loads retain a copy of the input because later attachment reads require absolute file offsets. This applies even to seekable streams and is bounded by `MaximumBufferedInputBytes` (default 1 GiB). Distributed XML input is bounded by the header-size limit; pass a base directory when resolving header-relative blocks from a stream.

FITS loads materialize raw HDU data and supported decoded content. Non-seekable input is buffered first and has its own input limit. Raw payload limits do not account for every managed object allocated for decoded table cells.

Caller-supplied streams remain open. Stream writes begin at the current position and are not transactional. Use a fresh or appropriately positioned stream; attached XISF locations describe offsets from the file origin.

## Safe file output

Single-file XISF and FITS path writers serialize into a uniquely named temporary file in the destination directory, flush and close it, then replace the destination. Serialization errors leave an existing destination unchanged. A loaded XISF document can be saved to its own source path because all attachments are read before replacement.

This requires permission to create files and replace the destination in its directory. Replacement changes the file identity and does not promise preservation of custom permissions, links, or filesystem metadata. It does not guarantee recovery after a filesystem or power failure. Concurrent writers require coordination by the caller.

Distributed XISF output consists of two files and is not a transactional pair. Choose distinct header and block paths in the same directory and handle partial-output failures in the calling application. Stream overloads likewise may leave partial output after an error.

## Resource policy

XISF defaults disable network resources and general external filesystem paths, enable header-relative blocks, and reject lexical traversal outside the header directory. For fully isolated input, disable header-relative access as well. Path normalization is not a filesystem sandbox: callers handling hostile input must also control links, mount points, and the underlying directory.

Set limits for the expected workload before loading:

```csharp
var options = new XisfReaderOptions
{
    AllowHeaderRelativeDataBlocks = false,
    MaximumXmlHeaderSize = 8 * 1024 * 1024,
    MaximumBufferedInputBytes = 256L * 1024 * 1024,
    MaximumDataBlockSize = 128 * 1024 * 1024,
    MaximumDecompressionRatio = 100
};
XisfDocument document = XisfReader.Load("input.xisf", options);
```

DTD processing and XML resolution are disabled. XML character/byte limits bound input parsing; element and depth limits are enforced after XML materialization. Block limits cover serialized and declared decompressed sizes. Compression subblocks are checked against the total declared output before allocating each block. Reader options are mutable; do not change them while their documents are being used.

FITS options bound HDU counts, headers, axes, individual/cumulative payload bytes, table cells, and buffered non-seekable input. Limits are workload guards rather than a total managed-memory budget.

## Cancellation and asynchronous work

File and stream `LoadAsync` methods run synchronous parsing on a worker thread. Tokens are checked before scheduling/opening, before each source input read, during XISF XML parsing/limit scanning and between FITS table rows, and before returning the parsed result. They support cancelling a load already underway at these boundaries. They do not interrupt an individual synchronous operating-system read, an unchecked CPU-only section or decompression operation, or later lazy image reads. Caller-owned streams remain open after cancellation; their position may have advanced.

FITS `WriteAsync` also runs on a worker thread. It checks cancellation before work and after serialization, before destination replacement. Cancellation leaves the original destination intact but may wait for serialization to finish. These APIs avoid blocking the caller; they are not fully asynchronous streaming implementations.

## Conversion and numeric precision

Unsigned FITS images using standard `BZERO` offsets round-trip through XISF using typed integer buffers, including 64-bit values above `2^53`. This exact route requires unchanged matching storage metadata, unit scale, and no blank sentinel.

Other scaled, signed, or blank-containing FITS images use a physical-value double representation. Signed 64-bit stored values that cannot be represented exactly as doubles are rejected with `NotSupportedException` rather than silently rounded. Scaling arithmetic can still introduce floating-point rounding; conversion does not guarantee exact storage recovery for arbitrary scaling. `FitsImage.GetPhysicalSamples` itself returns doubles and consequently has the usual integer precision limits.

Complex XISF images cannot be converted into standard FITS image HDUs. FITS table conversion into XISF flattens array-valued cells into text; converting XISF images back to FITS does not recreate those tables. Unknown HDUs and arbitrary extensions are not guaranteed to survive format conversion. Disable `TreatThirdAxisAsColorChannels` to preserve a three- or four-plane scientific cube as dimensions rather than color channels.

`Float128` property text is read as a double, and decimal serialization does not create an IEEE binary128 value. Do not rely on this property path for quadruple-precision arithmetic. Typed image reads preserve integer values when the requested type can represent them; normalization, display, statistics, and physical double conversions may lose large-integer precision.

## Memory and performance

Image decoding materializes the complete raw block. Typed reads allocate a converted array and may allocate another array when reordering channels. Region/channel extraction limits converted output size but still decodes the entire raw input for parsed images. Repeated reads repeat decoding and checksum verification; materialize an editable image for repeated utility/rendering operations when memory permits.

Writers retain attachments during serialization. Compression and format conversion can create additional full-sized buffers. FITS output may allocate padded copies. Benchmark representative files before choosing concurrency or raising resource limits.
