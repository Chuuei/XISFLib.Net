using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace XISFLib.AvaloniaViewer;

public static class AvaloniaBitmapAdapter
{
    // Creation/upload occurs on the UI thread; pixel calculation is performed separately.
    public static WriteableBitmap Create(XisfBgra32Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(image.DpiX, image.DpiY),
            PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        try
        {
            using var framebuffer = bitmap.Lock();
            if (framebuffer.RowBytes < image.Stride) throw new InvalidOperationException("The framebuffer stride is too small.");
            for (int row = 0; row < image.Height; row++)
                Marshal.Copy(image.Pixels, row * image.Stride, framebuffer.Address + row * framebuffer.RowBytes, image.Stride);
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }
}
