using CommunityToolkit.Mvvm.ComponentModel;

namespace XISFLib.Viewer;

/// <summary>Shared page navigation and staged value edits; closing the view does not accept edits.</summary>
public sealed class PropertyValueEditorState : ObservableObject
{
    private readonly PropertyEditorRow _row;
    private readonly PagedMetadataText _pages;
    private int _page;
    private string _pageText;
    private string _displayed;
    public PropertyValueEditorState(PropertyEditorRow row, string fullText)
    {
        _row = row; _pages = new(fullText); _pageText = _displayed = _pages.GetPage(0);
    }
    public string Identifier => MetadataText.Preview(_row.Id);
    public bool IsReadOnly => _row.IsReadOnly;
    public int MaximumPageLength => MetadataText.PageSize * 2;
    public string PageText { get => _pageText; set => SetProperty(ref _pageText, value); }
    public bool HasPrevious => _page > 0;
    public bool HasNext => _page + 1 < _pages.PageCount;
    public string Position => $"Page {_page + 1:N0}/{_pages.PageCount:N0} · {_pages.CharacterCount:N0} characters";
    public void Previous() { if (HasPrevious) Navigate(-1); }
    public void Next() { if (HasNext) Navigate(1); }
    private void Navigate(int direction)
    {
        Store(); _page += direction; PageText = _displayed = _pages.GetPage(_page);
        OnPropertyChanged(nameof(Position)); OnPropertyChanged(nameof(HasPrevious)); OnPropertyChanged(nameof(HasNext));
    }
    private void Store() { if (!IsReadOnly && PageText != _displayed) _pages.SetPage(_page, PageText); }
    public Task<string> GetFullTextAsync() { Store(); return Task.Run(_pages.BuildText); }
    public async Task AcceptAsync()
    {
        Store();
        if (!IsReadOnly && _pages.HasChanges) _row.Value = await Task.Run(_pages.BuildText);
    }
}
