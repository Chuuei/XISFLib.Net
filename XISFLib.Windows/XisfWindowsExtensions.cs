using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XISFLib;

/// <summary>Creates Windows Forms bitmaps, frozen WPF sources, and BGRA display buffers.</summary>
public static class XisfWindowsExtensions
{
    /// <summary>Creates a Windows Forms bitmap; the caller must dispose the returned bitmap.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A new bitmap that the caller must dispose.</returns>
    public static Bitmap ToBitmap(this XisfImage image, XisfStretchOptions? stretch = null)
        => CreateBitmap(XisfDisplayRenderer.Render(image, stretch));

    /// <summary>Creates a Windows Forms bitmap; the caller must dispose the returned bitmap.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A new bitmap that the caller must dispose.</returns>
    public static Bitmap ToBitmap(this XisfImageData image, XisfStretchOptions? stretch = null)
        => CreateBitmap(XisfDisplayRenderer.Render(image, stretch));

    /// <summary>Creates a frozen WPF bitmap source suitable for use across threads.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A frozen WPF bitmap source.</returns>
    public static BitmapSource ToBitmapSource(this XisfImage image, XisfStretchOptions? stretch = null)
        => CreateBitmapSource(XisfDisplayRenderer.Render(image, stretch));

    /// <summary>Creates a frozen WPF bitmap source suitable for use across threads.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A frozen WPF bitmap source.</returns>
    public static BitmapSource ToBitmapSource(this XisfImageData image, XisfStretchOptions? stretch = null)
        => CreateBitmapSource(XisfDisplayRenderer.Render(image, stretch));

    /// <summary>Returns a newly allocated BGRA display buffer with optional stretching.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A newly allocated BGRA display buffer.</returns>
    public static byte[] ToBgra32Pixels(this XisfImage image, XisfStretchOptions? stretch = null)
        => XisfDisplayRenderer.Render(image, stretch).Pixels;

    /// <summary>Returns a newly allocated BGRA display buffer with optional stretching.</summary>
    /// <param name="image">The source image.</param>
    /// <param name="stretch">The display stretch, or null for normalized linear rendering.</param>
    /// <returns>A newly allocated BGRA display buffer.</returns>
    public static byte[] ToBgra32Pixels(this XisfImageData image, XisfStretchOptions? stretch = null)
        => XisfDisplayRenderer.Render(image, stretch).Pixels;

    private static BitmapSource CreateBitmapSource(XisfBgra32Image image)
    {
        BitmapSource bitmap = BitmapSource.Create(image.Width, image.Height, image.DpiX, image.DpiY,
            PixelFormats.Bgra32, null, image.Pixels, image.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static Bitmap CreateBitmap(XisfBgra32Image image)
    {
        Bitmap bitmap = new(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                int sourceStride = image.Stride;
                for (int row = 0; row < image.Height; row++)
                    Marshal.Copy(image.Pixels, row * sourceStride, bitmapData.Scan0 + row * bitmapData.Stride, sourceStride);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
            bitmap.SetResolution((float)image.DpiX, (float)image.DpiY);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

}
