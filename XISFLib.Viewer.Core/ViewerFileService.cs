namespace XISFLib.Viewer;

/// <summary>Shared selected-image save modes and whole-file copy behavior.</summary>
public static class ViewerFileService
{
    public static bool IsMonolithicXisf(string path)
    {
        if (!Path.GetExtension(path).Equals(".xisf", StringComparison.OrdinalIgnoreCase)) return false;
        using var stream = File.OpenRead(path); Span<byte> signature = stackalloc byte[8];
        return stream.Read(signature) == 8 && signature.SequenceEqual("XISF0100"u8);
    }
    public static IReadOnlyList<ConversionFinding> Save(string path, XisfImageData image, DocumentEditorSnapshot document,
        XisfWriterOptions options, bool distributed, string? copySource)
    {
        if (ViewerSession.IsFitsPath(path))
        {
            var result = FitsConverter.ConvertToFits([image], ConversionLossPolicy.Allow);
            FitsWriter.Write(path, result.Value, new FitsWriterOptions { WriteChecksums = true });
            return result.Findings;
        }
        if (!distributed && copySource is not null) { CopyOriginal(copySource, path); return []; }
        var content = document.CreateContent(image);
        if (distributed) XisfWriter.WriteDistributed(path, Path.ChangeExtension(path, ".xisb"), content, options);
        else XisfWriter.Write(path, content, options);
        return [];
    }

    public static void CopyOriginal(string source, string destination)
    {
        source = Path.GetFullPath(source); destination = Path.GetFullPath(destination);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(source, destination, comparison)) return;
        // Stage copies too: a truncated copy must never replace an existing output.
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(source, temporary, false);
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
