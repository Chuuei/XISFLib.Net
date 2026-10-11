namespace XISFLib.Viewer;

/// <summary>Redistribution notices embedded in both standalone viewers.</summary>
public static class ViewerNotices
{
    public static string Read()
    {
        return string.Join(Environment.NewLine + Environment.NewLine, new[] { "Viewer.Magick.Notice.txt", "Viewer.Toolkit.License.txt", "Viewer.Toolkit.Notice.txt" }.Select(name =>
        {
            using var stream = typeof(ViewerNotices).Assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream); return reader.ReadToEnd();
        }));
    }
}
