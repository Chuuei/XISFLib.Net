using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using XISFLib.Viewer;

namespace XISFLib.WpfViewer;

/// <summary>Never lays out more than one bounded page of a metadata value.</summary>
public sealed class PropertyValueWindow : Window
{
    private PropertyValueWindow(PropertyEditorRow row, string value)
    {
        Title = "Property value"; Width = 850; Height = 600;
        var pages = new PagedMetadataText(value);
        int page = 0;
        string displayedText = "";
        var root = new DockPanel { Margin = new Thickness(12) };
        var heading = new TextBlock { Text = row.Id, TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var text = new TextBox { AcceptsReturn = true, AcceptsTab = true, IsReadOnly = row.IsReadOnly,
            MaxLength = MetadataText.PageSize * 2, TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var previous = new Button { Content = "Previous", Padding = new Thickness(8) };
        var next = new Button { Content = "Next", Padding = new Thickness(8) };
        var position = new TextBlock { Margin = new Thickness(12), VerticalAlignment = VerticalAlignment.Center };
        var export = new Button { Content = "Export full text…", Padding = new Thickness(8) };
        var apply = new Button { Content = "Accept edits", Padding = new Thickness(8), IsEnabled = !row.IsReadOnly };
        var cancel = new Button { Content = "Close / cancel", Padding = new Thickness(8), IsCancel = true };
        foreach (var control in new Control[] { previous, next, export, apply, cancel }) buttons.Children.Add(control);
        buttons.Children.Insert(2, position);
        root.Children.Add(text); Content = root;
        void Store() { if (!row.IsReadOnly && text.Text != displayedText) pages.SetPage(page, text.Text); }
        void Refresh()
        {
            text.Text = pages.GetPage(page); text.CaretIndex = 0;
            displayedText = text.Text;
            position.Text = $"Page {page + 1:N0}/{pages.PageCount:N0} · {pages.CharacterCount:N0} characters";
            previous.IsEnabled = page > 0; next.IsEnabled = page + 1 < pages.PageCount;
        }
        previous.Click += (_, _) => { Store(); page--; Refresh(); };
        next.Click += (_, _) => { Store(); page++; Refresh(); };
        apply.Click += async (_, _) =>
        {
            Store(); IsEnabled = false;
            if (pages.HasChanges) row.Value = await Task.Run(pages.BuildText);
            DialogResult = true;
        };
        export.Click += async (_, _) =>
        {
            var picker = new SaveFileDialog { Title = "Export property text", Filter = "Text files|*.txt|All files|*.*", DefaultExt = ".txt" };
            if (picker.ShowDialog(this) != true) return;
            Store(); IsEnabled = false;
            try { await Task.Run(() => File.WriteAllText(picker.FileName, pages.BuildText(), new System.Text.UTF8Encoding(false))); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Export failed"); }
            finally { IsEnabled = true; }
        };
        Refresh();
    }

    public static async Task OpenAsync(Window owner, PropertyEditorRow row)
    {
        string value = await Task.Run(() => row.Value);
        new PropertyValueWindow(row, value) { Owner = owner }.ShowDialog();
    }
}
