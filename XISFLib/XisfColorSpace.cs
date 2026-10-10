namespace XISFLib;

/// <summary>Implements the normative D50 color transformations in XISF Annex B.</summary>
public static class XisfColorSpace
{
    /// <summary>The D50 reference white X tristimulus value, with Y equal to one.</summary>
    public const double WhiteX = 0.96422;
    /// <summary>The D50 reference white Z tristimulus value, with Y equal to one.</summary>
    public const double WhiteZ = 0.82521;
    private const double Epsilon = 216.0 / 24389, Kappa = 24389.0 / 27;

    /// <summary>Derives luminance coefficients from the primaries and D50 white.</summary>
    /// <param name="workingSpace">The RGB working space.</param>
    /// <returns>The three primary luminance coefficients.</returns>
    public static double[] DeriveLuminanceCoefficients(XisfRgbWorkingSpace workingSpace)
    {
        ArgumentNullException.ThrowIfNull(workingSpace);
        var x = workingSpace.ChromaticityCoordinates;
        var y = workingSpace.YChromaticityCoordinates;
        if (x.Count != 3 || y.Count != 3 || !double.IsFinite(workingSpace.Gamma) || workingSpace.Gamma <= 0 ||
            x.Any(v => !double.IsFinite(v) || v is < 0 or > 1) || y.Any(v => !double.IsFinite(v) || v is <= 0 or > 1))
            throw new ArgumentException("Invalid RGB working-space parameters.", nameof(workingSpace));
        double[,] primaries = new double[3, 3];
        for (int i = 0; i < 3; i++)
        {
            if (x[i] + y[i] > 1) throw new ArgumentException("Invalid primary chromaticity.", nameof(workingSpace));
            primaries[0, i] = x[i] / y[i]; primaries[1, i] = 1; primaries[2, i] = (1 - x[i] - y[i]) / y[i];
        }
        var value = Multiply(Inverse(primaries), WhiteX, 1, WhiteZ);
        double[] coefficients = [value.X, value.Y, value.Z];
        if (coefficients.Any(v => !double.IsFinite(v) || v is < 0 or > 1)) throw new ArgumentException("Primaries do not enclose the D50 white point.", nameof(workingSpace));
        return coefficients;
    }

    /// <summary>Converts normalized RGB samples to CIE XYZ relative to D50.</summary>
    /// <param name="red">Red sample.</param>
    /// <param name="green">Green sample.</param>
    /// <param name="blue">Blue sample.</param>
    /// <param name="workingSpace">The associated RGB working space; defaults to sRGB.</param>
    /// <returns>D50 tristimulus values.</returns>
    public static (double X, double Y, double Z) RgbToXyz(double red, double green, double blue, XisfRgbWorkingSpace? workingSpace = null)
    {
        workingSpace ??= XisfRgbWorkingSpace.Srgb;
        return Multiply(ColorMatrix(workingSpace), Linearize(red, workingSpace), Linearize(green, workingSpace), Linearize(blue, workingSpace));
    }

    /// <summary>Converts D50 CIE XYZ values to normalized RGB without clipping.</summary>
    /// <param name="x">X tristimulus value.</param>
    /// <param name="y">Y tristimulus value.</param>
    /// <param name="z">Z tristimulus value.</param>
    /// <param name="workingSpace">The associated RGB working space; defaults to sRGB.</param>
    /// <returns>The RGB samples.</returns>
    public static (double Red, double Green, double Blue) XyzToRgb(double x, double y, double z, XisfRgbWorkingSpace? workingSpace = null)
    {
        workingSpace ??= XisfRgbWorkingSpace.Srgb;
        var rgb = Multiply(Inverse(ColorMatrix(workingSpace)), x, y, z);
        return (Encode(rgb.X, workingSpace), Encode(rgb.Y, workingSpace), Encode(rgb.Z, workingSpace));
    }

    /// <summary>Converts RGB to the normalized L, a, b sample representation defined by XISF.</summary>
    /// <param name="red">Red sample.</param>
    /// <param name="green">Green sample.</param>
    /// <param name="blue">Blue sample.</param>
    /// <param name="workingSpace">The associated RGB working space; defaults to sRGB.</param>
    /// <returns>The normalized Lab samples, where neutral a and b equal 0.5.</returns>
    public static (double L, double A, double B) RgbToLab(double red, double green, double blue, XisfRgbWorkingSpace? workingSpace = null)
    {
        var xyz = RgbToXyz(red, green, blue, workingSpace);
        double fx = F(xyz.X / WhiteX), fy = F(xyz.Y), fz = F(xyz.Z / WhiteZ);
        return (1.16 * fy - 0.16, 0.5 + 0.58 * (fx - fy), 0.5 + 0.58 * (fy - fz));
    }

    /// <summary>Converts normalized XISF Lab samples to RGB without clipping.</summary>
    /// <param name="l">Normalized lightness.</param>
    /// <param name="a">Normalized a component.</param>
    /// <param name="b">Normalized b component.</param>
    /// <param name="workingSpace">The associated RGB working space; defaults to sRGB.</param>
    /// <returns>The RGB samples.</returns>
    public static (double Red, double Green, double Blue) LabToRgb(double l, double a, double b, XisfRgbWorkingSpace? workingSpace = null)
    {
        double fy = (l + 0.16) / 1.16, fx = fy + (a - 0.5) / 0.58, fz = fy - (b - 0.5) / 0.58;
        return XyzToRgb(WhiteX * G(fx), G(fy), WhiteZ * G(fz), workingSpace);
    }

    /// <summary>Computes colorimetric grayscale lightness from RGB samples.</summary>
    /// <param name="red">Red sample.</param>
    /// <param name="green">Green sample.</param>
    /// <param name="blue">Blue sample.</param>
    /// <param name="workingSpace">The associated RGB working space; defaults to sRGB.</param>
    /// <returns>Normalized CIE lightness.</returns>
    public static double RgbToGray(double red, double green, double blue, XisfRgbWorkingSpace? workingSpace = null) =>
        1.16 * F(RgbToXyz(red, green, blue, workingSpace).Y) - 0.16;

    private static double F(double t) => t > Epsilon ? Math.Cbrt(t) : (Kappa * t + 16) / 116;
    private static double G(double t) => t * t * t > Epsilon ? t * t * t : (116 * t - 16) / Kappa;
    private static double Linearize(double x, XisfRgbWorkingSpace w) => w.IsSrgb ? x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4) : Math.Pow(x, w.Gamma);
    private static double Encode(double x, XisfRgbWorkingSpace w) => w.IsSrgb ? x <= 0.0031308 ? x * 12.92 : 1.055 * Math.Pow(x, 1 / 2.4) - 0.055 : Math.Pow(x, 1 / w.Gamma);
    private static double[,] ColorMatrix(XisfRgbWorkingSpace w)
    {
        double[] luminance = DeriveLuminanceCoefficients(w);
        double[,] m = new double[3, 3];
        for (int i = 0; i < 3; i++)
        {
            m[0, i] = luminance[i] * w.ChromaticityCoordinates[i] / w.YChromaticityCoordinates[i];
            m[1, i] = luminance[i];
            m[2, i] = luminance[i] * (1 - w.ChromaticityCoordinates[i] - w.YChromaticityCoordinates[i]) / w.YChromaticityCoordinates[i];
        }
        return m;
    }
    private static (double X, double Y, double Z) Multiply(double[,] m, double x, double y, double z) =>
        (m[0, 0] * x + m[0, 1] * y + m[0, 2] * z, m[1, 0] * x + m[1, 1] * y + m[1, 2] * z, m[2, 0] * x + m[2, 1] * y + m[2, 2] * z);
    private static double[,] Inverse(double[,] m)
    {
        double[,] adj = new double[3, 3];
        for (int i = 0; i < 3; i++)
        for (int j = 0; j < 3; j++)
            adj[j, i] = m[(i + 1) % 3, (j + 1) % 3] * m[(i + 2) % 3, (j + 2) % 3] - m[(i + 1) % 3, (j + 2) % 3] * m[(i + 2) % 3, (j + 1) % 3];
        double determinant = m[0, 0] * adj[0, 0] + m[0, 1] * adj[1, 0] + m[0, 2] * adj[2, 0];
        if (determinant == 0 || !double.IsFinite(determinant)) throw new ArgumentException("Singular RGB working-space primaries.");
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) adj[i, j] /= determinant;
        return adj;
    }
}
