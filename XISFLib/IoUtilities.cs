namespace XISFLib;

internal static class AtomicFile
{
    internal static void Write(string path, Action<Stream> write)
    {
        string destination = Path.GetFullPath(path);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(true);
            }
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

internal sealed class CancellationReadStream(Stream inner, CancellationToken token) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
    public override int Read(byte[] buffer, int offset, int count)
    {
        token.ThrowIfCancellationRequested();
        return inner.Read(buffer, offset, count);
    }
    public override int Read(Span<byte> buffer)
    {
        token.ThrowIfCancellationRequested();
        return inner.Read(buffer);
    }
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void Flush() => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal sealed class CancellationStringReader(string text, CancellationToken token) : StringReader(text)
{
    public override int Read(char[] buffer, int index, int count)
    {
        token.ThrowIfCancellationRequested();
        return base.Read(buffer, index, count);
    }

    public override int Read(Span<char> buffer)
    {
        token.ThrowIfCancellationRequested();
        return base.Read(buffer);
    }
}
