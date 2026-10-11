namespace XISFLib.Viewer;

/// <summary>Framework-independent pointer-anchored zoom and captured-pan geometry.</summary>
public sealed class ViewerViewportState
{
    private double _startX, _startY, _originX, _originY;
    public double Zoom { get; private set; } = 1;
    public double TranslationX { get; private set; }
    public double TranslationY { get; private set; }
    public bool IsPanning { get; private set; }
    public void Reset() { Zoom = 1; TranslationX = TranslationY = 0; EndPan(); }
    public void Wheel(double delta, double x, double y)
    {
        double zoom = Math.Clamp(Zoom * (delta > 0 ? 1.2 : 1 / 1.2), 1, 64);
        double ratio = zoom / Zoom;
        TranslationX = x - (x - TranslationX) * ratio;
        TranslationY = y - (y - TranslationY) * ratio;
        Zoom = zoom;
    }
    public void BeginPan(double x, double y)
    {
        IsPanning = true; _startX = x; _startY = y; _originX = TranslationX; _originY = TranslationY;
    }
    public void Move(double x, double y)
    {
        if (!IsPanning) return;
        TranslationX = _originX + x - _startX; TranslationY = _originY + y - _startY;
    }
    public void EndPan() => IsPanning = false;
}
