namespace XISFLib;

public static partial class XisfWriter
{
    /// <summary>Writes editable content on a worker thread, checking cancellation before atomic publication.</summary>
    /// <param name="path">The destination path.</param>
    /// <param name="document">The editable document. Do not mutate it until completion.</param>
    /// <param name="options">Settings, snapshotted at invocation.</param>
    /// <param name="cancellationToken">Cancellation observed before work and before publication.</param>
    /// <returns>The write operation.</returns>
    public static Task WriteAsync(string path, XisfDocumentData document, XisfWriterOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var settings = options?.Snapshot();
        return WriteFileAsync(path, stream => Write(stream, document, settings), cancellationToken);
    }

    /// <summary>Writes parsed content on a worker thread, preserving its XML and opaque blocks.</summary>
    /// <param name="path">The destination path.</param>
    /// <param name="document">The parsed document.</param>
    /// <param name="cancellationToken">Cancellation observed before work and publication.</param>
    /// <returns>The write operation.</returns>
    public static Task WriteAsync(string path, XisfDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return WriteFileAsync(path, stream => Write(stream, document), cancellationToken);
    }

    /// <summary>Writes an image sequence on a worker thread, with cancellation before publication.</summary>
    /// <param name="path">The destination path.</param>
    /// <param name="images">Images, enumerated at invocation; do not mutate them until completion.</param>
    /// <param name="options">Settings, snapshotted at invocation.</param>
    /// <param name="cancellationToken">Cancellation observed before work and publication.</param>
    /// <returns>The write operation.</returns>
    public static Task WriteAsync(string path, IEnumerable<XisfImageData> images, XisfWriterOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(images);
        var list = images.ToArray();
        var settings = options?.Snapshot();
        return WriteFileAsync(path, stream => Write(stream, list, settings), cancellationToken);
    }

    /// <summary>Writes editable content on a worker thread to a caller-owned stream.</summary>
    /// <param name="stream">A writable stream; cancellation or errors can leave partial output.</param>
    /// <param name="document">Editable content, unchanged until completion.</param>
    /// <param name="options">Settings, snapshotted at invocation.</param>
    /// <param name="cancellationToken">Cancellation checked before and after serialization.</param>
    /// <returns>The write operation.</returns>
    public static Task WriteAsync(Stream stream, XisfDocumentData document, XisfWriterOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        var settings = options?.Snapshot();
        return Task.Run(() => { cancellationToken.ThrowIfCancellationRequested(); Write(stream, document, settings); cancellationToken.ThrowIfCancellationRequested(); }, cancellationToken);
    }

    /// <summary>Writes parsed content on a worker thread to a caller-owned stream.</summary>
    /// <param name="stream">A writable stream; errors may leave partial output.</param>
    /// <param name="document">The parsed document.</param>
    /// <param name="cancellationToken">Cancellation checked before and after serialization.</param>
    /// <returns>The write operation.</returns>
    public static Task WriteAsync(Stream stream, XisfDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        return Task.Run(() => { cancellationToken.ThrowIfCancellationRequested(); Write(stream, document); cancellationToken.ThrowIfCancellationRequested(); }, cancellationToken);
    }

    /// <summary>Writes an image sequence to a caller-owned stream on a worker thread.</summary>
    /// <param name="stream">A writable stream; errors may leave partial output.</param>
    /// <param name="images">Images, enumerated at invocation.</param>
    /// <param name="options">Settings, snapshotted at invocation.</param>
    /// <param name="cancellationToken">Cancellation checked before and after serialization.</param>
    /// <returns>The write operation.</returns>
    public static Task WriteAsync(Stream stream, IEnumerable<XisfImageData> images, XisfWriterOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(images);
        var list = images.ToArray();
        var settings = options?.Snapshot();
        return Task.Run(() => { cancellationToken.ThrowIfCancellationRequested(); Write(stream, list, settings); cancellationToken.ThrowIfCancellationRequested(); }, cancellationToken);
    }

    private static Task WriteFileAsync(string path, Action<Stream> write, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            AtomicFile.Write(path, stream => { write(stream); token.ThrowIfCancellationRequested(); });
        }, token);
    }
}
