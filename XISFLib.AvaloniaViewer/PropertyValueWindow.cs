using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using XISFLib.Viewer;

namespace XISFLib.AvaloniaViewer;

/// <summary>Never lays out more than one bounded page of a metadata value.</summary>
public sealed class PropertyValueWindow : Window
{
    public PropertyValueWindow(PropertyEditorRow row, string value)
    {
        Title = "Property value"; Width = 850; Height = 600;
        var pages = new PagedMetadataText(value);
        int page = 0;
        string displayedText = "";
        var root = new Grid { Margin = new Thickness(12), RowDefinitions = new("Auto,*,Auto") };
        root.Children.Add(new TextBlock { Text = row.Id, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var text = new TextBox { Name = "ValuePage", AcceptsReturn = true, AcceptsTab = true, IsReadOnly = row.IsReadOnly,
            MaxLength = MetadataText.PageSize * 2, TextWrapping = TextWrapping.NoWrap };
        ScrollViewer.SetVerticalScrollBarVisibility(text, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(text, ScrollBarVisibility.Auto);
        Grid.SetRow(text, 1); root.Children.Add(text);
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetRow(buttons, 2); root.Children.Add(buttons);
        var previous = new Button { Name = "PreviousPage", Content = "Previous" };
        var next = new Button { Name = "NextPage", Content = "Next" };
        var position = new TextBlock { Margin = new Thickness(12), VerticalAlignment = VerticalAlignment.Center };
        var export = new Button { Content = "Export full text…" };
        var apply = new Button { Name = "AcceptValue", Content = "Accept edits", IsEnabled = !row.IsReadOnly };
        var cancel = new Button { Content = "Close / cancel" };
        foreach (var control in new Control[] { previous, next, position, export, apply, cancel }) buttons.Children.Add(control);
        Content = root;
        void Store() { if (!row.IsReadOnly && text.Text != displayedText) pages.SetPage(page, text.Text ?? ""); }
        void Refresh()
        {
            text.Text = pages.GetPage(page); text.CaretIndex = 0;
            displayedText = text.Text ?? "";
            position.Text = $"Page {page + 1:N0}/{pages.PageCount:N0} · {pages.CharacterCount:N0} characters";
            previous.IsEnabled = page > 0; next.IsEnabled = page + 1 < pages.PageCount;
        }
        previous.Click += (_, _) => { Store(); page--; Refresh(); };
        next.Click += (_, _) => { Store(); page++; Refresh(); };
        cancel.Click += (_, _) => Close();
        apply.Click += async (_, _) =>
        {
            Store(); IsEnabled = false;
            if (pages.HasChanges) row.Value = await Task.Run(pages.BuildText);
            Close();
        };
        export.Click += async (_, _) =>
        {
            var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Export property text", DefaultExtension = "txt", ShowOverwritePrompt = true });
            if (file is null) return;
            Store(); IsEnabled = false;
            try
            {
                string full = await Task.Run(pages.BuildText);
                await using var stream = await file.OpenWriteAsync();
                stream.SetLength(0);
                await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
                await writer.WriteAsync(full);
            }
            catch (Exception error)
            {
                await new Window { Title = "Export failed", Width = 500, Height = 180,
                    Content = new TextBlock { Text = MetadataText.Preview(error.Message), TextWrapping = TextWrapping.Wrap } }.ShowDialog(this);
            }
            finally { IsEnabled = true; }
        };
        Refresh();
    }

    public static async Task OpenAsync(Window owner, PropertyEditorRow row)
    {
        string value = await Task.Run(() => row.Value);
        await new PropertyValueWindow(row, value).ShowDialog(owner);
    }
}
