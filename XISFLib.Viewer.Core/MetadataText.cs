using System.Text;

namespace XISFLib.Viewer;

/// <summary>Bounds text layout work independently of metadata payload size.</summary>
public static class MetadataText
{
    public const int InlineLimit = 256;
    public const int PageSize = 4096;

    public static string Preview(string text)
    {
        string prefix = text[..Math.Min(text.Length, InlineLimit)].Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        return text.Length > InlineLimit ? $"{text.Length:N0} characters · {prefix}…" : prefix;
    }
}

/// <summary>A bounded, fixed-offset text editor. Edits are staged until explicitly accepted.</summary>
public sealed class PagedMetadataText
{
    private readonly string _original;
    private readonly Dictionary<int, string> _edits = [];
    public PagedMetadataText(string text) => _original = text;
    public int PageCount => Math.Max(1, (int)(((long)_original.Length + MetadataText.PageSize - 1) / MetadataText.PageSize));
    public int CharacterCount => _original.Length;
    public bool HasChanges => _edits.Count != 0;
    public string GetPage(int page)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(page, PageCount);
        if (_edits.TryGetValue(page, out var text)) return text;
        int start = page * MetadataText.PageSize;
        int length = Math.Min(MetadataText.PageSize, _original.Length - start);
        // Keep a UTF-16 surrogate pair together without changing the fixed page offsets.
        if (start > 0 && char.IsLowSurrogate(_original[start]) && char.IsHighSurrogate(_original[start - 1])) { start++; length--; }
        if (start + length < _original.Length && length > 0 && char.IsHighSurrogate(_original[start + length - 1]) && char.IsLowSurrogate(_original[start + length])) length++;
        return _original.Substring(start, length);
    }
    public void SetPage(int page, string text)
    {
        string previous = GetPage(page);
        if (previous == text) return;
        _edits[page] = text;
    }
    public string BuildText()
    {
        if (!HasChanges) return _original;
        var result = new StringBuilder();
        for (int page = 0; page < PageCount; page++) result.Append(GetPage(page));
        return result.ToString();
    }
}
