using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public class ViewerInteractionStateTests
{
    [Fact]
    public void ZoomKeepsPointerAnchorAndPanStopsAtReleaseAndReset()
    {
        var state = new ViewerViewportState();
        state.Wheel(1, 100, 50);
        Assert.Equal(100, 100 * state.Zoom + state.TranslationX, 10);
        Assert.Equal(50, 50 * state.Zoom + state.TranslationY, 10);
        double x = state.TranslationX, y = state.TranslationY;
        state.BeginPan(10, 20); state.Move(40, 60);
        Assert.Equal(x + 30, state.TranslationX, 10);
        Assert.Equal(y + 40, state.TranslationY, 10);
        state.EndPan(); state.Move(500, 500);
        Assert.Equal(x + 30, state.TranslationX, 10);
        for (int i = 0; i < 100; i++) state.Wheel(1, 100, 50);
        Assert.Equal(64, state.Zoom);
        for (int i = 0; i < 100; i++) state.Wheel(-1, 100, 50);
        Assert.Equal(1, state.Zoom);
        state.BeginPan(1, 1); state.Reset(); state.Move(20, 20);
        Assert.False(state.IsPanning);
        Assert.Equal(1, state.Zoom);
        Assert.Equal(0, state.TranslationX);
        Assert.Equal(0, state.TranslationY);
    }

    [Fact]
    public async Task PagedEditsAreStagedAcrossNavigationAndExportUntilAccepted()
    {
        string original = new('x', MetadataText.PageSize * 2 + 10);
        var row = ViewerEditorCodec.ToEditorRow("Test:Text", new XisfPropertyData("String", original));
        var state = new PropertyValueEditorState(row, original);
        Assert.False(state.HasPrevious); Assert.True(state.HasNext);
        state.PageText = "first"; state.Next(); state.PageText = "second";
        state.Next(); Assert.False(state.HasNext); state.Previous();
        Assert.Equal("second", state.PageText);
        Assert.Equal("firstsecond" + new string('x', 10), await state.GetFullTextAsync());
        Assert.Equal(original, row.Value);
        await state.AcceptAsync();
        Assert.Equal("firstsecond" + new string('x', 10), row.Value);
    }

    [Fact]
    public async Task InspectingDeferredArrayDoesNotReplaceOriginalProperty()
    {
        var original = new XisfPropertyData("UI64Vector", Enumerable.Repeat(ulong.MaxValue, 1000).ToArray());
        var row = ViewerEditorCodec.ToEditorRow("Test:Array", original);
        var state = new PropertyValueEditorState(row, row.Value);
        state.Next(); state.Previous(); await state.GetFullTextAsync(); await state.AcceptAsync();
        Assert.Same(original, ViewerEditorCodec.ParseProperties([row])[row.Id]);
    }
}
