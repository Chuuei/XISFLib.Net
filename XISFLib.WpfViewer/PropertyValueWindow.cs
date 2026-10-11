using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using XISFLib.Viewer;

namespace XISFLib.WpfViewer;

/// <summary>Never lays out more than one bounded page of a metadata value.</summary>
public sealed class PropertyValueWindow : Window
{
    private PropertyValueWindow(PropertyEditorRow row, string value)
    {
        Title = "Property value"; Width = 850; Height = 600;
        var state = new PropertyValueEditorState(row, value);
        DataContext = state;
        var root = new DockPanel { Margin = new Thickness(12) };
        var heading = new TextBlock { Text = state.Identifier, TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var text = new TextBox { AcceptsReturn = true, AcceptsTab = true, IsReadOnly = row.IsReadOnly,
            MaxLength = state.MaximumPageLength, TextWrapping = TextWrapping.NoWrap,
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
        text.SetBinding(TextBox.TextProperty, new Binding(nameof(state.PageText)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        position.SetBinding(TextBlock.TextProperty, new Binding(nameof(state.Position)));
        previous.SetBinding(IsEnabledProperty, new Binding(nameof(state.HasPrevious)));
        next.SetBinding(IsEnabledProperty, new Binding(nameof(state.HasNext)));
        previous.Click += (_, _) => state.Previous();
        next.Click += (_, _) => state.Next();
        apply.Click += async (_, _) =>
        {
            IsEnabled = false;
            await state.AcceptAsync();
            DialogResult = true;
        };
        export.Click += async (_, _) =>
        {
            var picker = new SaveFileDialog { Title = "Export property text", Filter = "Text files|*.txt|All files|*.*", DefaultExt = ".txt" };
            if (picker.ShowDialog(this) != true) return;
            IsEnabled = false;
            try { string full = await state.GetFullTextAsync(); await Task.Run(() => File.WriteAllText(picker.FileName, full, new System.Text.UTF8Encoding(false))); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Export failed"); }
            finally { IsEnabled = true; }
        };
    }

    public static async Task OpenAsync(Window owner, PropertyEditorRow row)
    {
        string value = await Task.Run(() => row.Value);
        new PropertyValueWindow(row, value) { Owner = owner }.ShowDialog();
    }
}
