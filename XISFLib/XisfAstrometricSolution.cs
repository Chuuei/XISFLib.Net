using System.Globalization;

namespace XISFLib;

/// <summary>Evaluates Revision 1 astrometric solutions without consulting FITS keywords or solver settings.</summary>
/// <remarks>Image pixel centers are (column + 0.5, row + 0.5). All celestial angles are degrees.
/// Both directions use their independently stored transformations. Unavailable layers fall back as specified.</remarks>
public sealed class XisfAstrometricSolution
{
    private const string Prefix = "AstrometricSolution:";
    private const double R = 180 / Math.PI;
    private readonly IReadOnlyDictionary<string, XisfProperty> _properties;
    private readonly List<string> _diagnostics = [];
    private double[] _reference = [], _imageReference = [];
    private double[,] _linear = new double[2, 2];
    private double[,]? _forward, _reverse;
    private Distortion? _forwardDistortion, _reverseDistortion;
    private double _alphaPole, _deltaPole, _phiPole;

    private XisfAstrometricSolution(IReadOnlyDictionary<string, XisfProperty> properties)
    {
        _properties = properties;
        try
        {
            string version = Get<string>("Version");
            string[] components = version.Split('.');
            if (components.Length != 2 || !uint.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out uint major) ||
                !uint.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out _) || major != 1)
                throw new XisfFormatException($"Unsupported astrometric solution revision '{version}'.");
            ProjectionSystem = Get<string>("ProjectionSystem");
            bool zenithal = ProjectionSystem is "Gnomonic" or "Stereographic" or "ZenithalEqualArea" or "Orthographic";
            if (!zenithal && ProjectionSystem is not ("PlateCarree" or "Mercator" or "HammerAitoff"))
                throw new XisfFormatException($"Unknown projection '{ProjectionSystem}'.");
            _reference = Vector("ReferenceCelestialCoordinates", 2);
            _imageReference = Vector("ReferenceImageCoordinates", 2);
            _linear = Matrix("LinearTransformationMatrix", 2, 2);
            if (Determinant(_linear) == 0) throw new XisfFormatException("Singular linear transformation.");
            double[] native = OptionalVector("ReferenceNativeCoordinates", 2) ?? [0, zenithal ? 90 : 0];
            double[] pole = OptionalVector("CelestialPoleNativeCoordinates", 2) ?? [native[0] + (_reference[1] >= native[1] ? 0 : 180), 90];
            InitializeRotation(native, pole);
            CelestialReferenceSystem = Optional<string>("CelestialReferenceSystem") ?? "ICRS";
            AvailableLayer = 1;
        }
        catch (XisfFormatException exception) { _diagnostics.Add("Layer 1: " + exception.Message); return; }
        if (!Present("ProjectiveTransformation:ImageToProjection") && !Present("ProjectiveTransformation:ProjectionToImage")) return;
        try
        {
            _forward = Matrix("ProjectiveTransformation:ImageToProjection", 3, 3);
            _reverse = Matrix("ProjectiveTransformation:ProjectionToImage", 3, 3);
            AvailableLayer = 2;
        }
        catch (XisfFormatException exception) { _diagnostics.Add("Layer 2: " + exception.Message); return; }
        if (!properties.Keys.Any(id => id.StartsWith(Prefix + "DistortionModel:", StringComparison.Ordinal))) return;
        try
        {
            _forwardDistortion = new Distortion(this, "DistortionModel:ImageToProjection:");
            _reverseDistortion = new Distortion(this, "DistortionModel:ProjectionToImage:");
            AvailableLayer = 3;
        }
        catch (XisfFormatException exception) { _diagnostics.Add("Layer 3: " + exception.Message); }
    }

    /// <summary>The highest evaluable mathematical layer (0 when the solution is unavailable).</summary>
    public int AvailableLayer { get; }
    /// <summary>The projection identifier, or null when the required properties are unavailable.</summary>
    public string? ProjectionSystem { get; }
    /// <summary>The reference system of the output coordinates, including unrecognized systems.</summary>
    public string? CelestialReferenceSystem { get; }
    /// <summary>Reasons that a present layer could not be evaluated.</summary>
    public IReadOnlyList<string> Diagnostics => _diagnostics.AsReadOnly();
    /// <summary>All original properties, including provenance and unrecognized future properties.</summary>
    public IReadOnlyDictionary<string, XisfProperty> Properties => _properties;

    /// <summary>Reads an image's astrometric properties; returns null when no solution properties exist.</summary>
    /// <param name="properties">The image properties.</param>
    /// <returns>The solution and its availability diagnostics, or null.</returns>
    public static XisfAstrometricSolution? FromProperties(IReadOnlyDictionary<string, XisfProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        Dictionary<string, XisfProperty> solution = properties.Where(p => p.Key.StartsWith(Prefix, StringComparison.Ordinal))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return solution.Count == 0 ? null : new(solution);
    }

    /// <summary>Creates and validates a solution from explicitly typed astrometric properties.</summary>
    /// <param name="properties">Properties with full AstrometricSolution identifiers, including both directions of each optional layer.</param>
    /// <returns>The validated mathematical model.</returns>
    public static XisfAstrometricSolution Create(IReadOnlyDictionary<string, XisfPropertyData> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        Dictionary<string, XisfProperty> parsed = new(StringComparer.Ordinal);
        foreach (var pair in properties)
        {
            if (!pair.Key.StartsWith(Prefix, StringComparison.Ordinal)) throw new ArgumentException("Only astrometric solution properties are accepted.", nameof(properties));
            parsed.Add(pair.Key, new(pair.Key, pair.Value.Type, pair.Value.Value, pair.Value.Format,
                new System.Xml.Linq.XElement("Property", new System.Xml.Linq.XAttribute("id", pair.Key), new System.Xml.Linq.XAttribute("type", pair.Value.Type)), pair.Value.Comment));
        }
        XisfAstrometricSolution solution = new(parsed);
        if (solution.AvailableLayer == 0 || solution.Diagnostics.Count != 0) throw new XisfFormatException(string.Join(" ", solution.Diagnostics));
        if (properties.Keys.Any(k => k.StartsWith(Prefix + "DistortionModel:", StringComparison.Ordinal)) && solution.AvailableLayer != 3)
            throw new XisfFormatException("A distortion model requires both projective transformations.");
        return solution;
    }

    /// <summary>Creates a first-layer solution with the default WCS native reference and pole parameters.</summary>
    /// <param name="projectionSystem">One of the seven standard projection identifiers.</param>
    /// <param name="referenceCelestialCoordinates">Reference right ascension and declination in degrees.</param>
    /// <param name="referenceImageCoordinates">Reference image coordinates.</param>
    /// <param name="linearTransformationMatrix">The 2 by 2 transformation in degrees per pixel.</param>
    /// <param name="celestialReferenceSystem">The celestial reference system, normally ICRS or GCRS.</param>
    /// <returns>The validated linear solution.</returns>
    public static XisfAstrometricSolution CreateLinear(string projectionSystem, double[] referenceCelestialCoordinates,
        double[] referenceImageCoordinates, double[,] linearTransformationMatrix, string celestialReferenceSystem = "ICRS") => Create(
        new Dictionary<string, XisfPropertyData>(StringComparer.Ordinal)
        {
            [Prefix + "Version"] = new("String", "1.0"),
            [Prefix + "ProjectionSystem"] = new("String", projectionSystem),
            [Prefix + "ReferenceCelestialCoordinates"] = new("F64Vector", referenceCelestialCoordinates),
            [Prefix + "ReferenceImageCoordinates"] = new("F64Vector", referenceImageCoordinates),
            [Prefix + "LinearTransformationMatrix"] = new("F64Matrix", linearTransformationMatrix),
            [Prefix + "CelestialReferenceSystem"] = new("String", celestialReferenceSystem)
        });

    /// <summary>Replaces an image's solution properties, preserving unrecognized properties of a loaded solution.</summary>
    /// <param name="image">The editable image with unchanged coordinate geometry.</param>
    public void ApplyTo(XisfImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        foreach (string id in image.Properties.Keys.Where(k => k.StartsWith(Prefix, StringComparison.Ordinal)).ToArray()) image.Properties.Remove(id);
        foreach (var pair in _properties)
            image.Properties[pair.Key] = pair.Value.Source is null ? new XisfPropertyData(pair.Value.Type, pair.Value.Value, pair.Value.Format, pair.Value.Comment) : pair.Value;
    }

    /// <summary>Removes the entire solution namespace after a geometric transformation that cannot preserve it exactly.</summary>
    /// <param name="image">The transformed editable image.</param>
    public static void RemoveFrom(XisfImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        foreach (string id in image.Properties.Keys.Where(k => k.StartsWith(Prefix, StringComparison.Ordinal)).ToArray()) image.Properties.Remove(id);
    }

    /// <summary>Transforms image coordinates to right ascension and declination in degrees.</summary>
    /// <param name="x">The image coordinate along columns.</param>
    /// <param name="y">The image coordinate along rows.</param>
    /// <returns>The right ascension in [0,360) and declination.</returns>
    public (double RightAscension, double Declination) ImageToCelestial(double x, double y)
    {
        EnsureAvailable(x, y);
        var plane = ImageToProjection(x, y);
        var native = Deproject(plane.X, plane.Y);
        return NativeToCelestial(native.X, native.Y);
    }

    /// <summary>Transforms right ascension and declination to image coordinates.</summary>
    /// <param name="rightAscension">Right ascension in degrees.</param>
    /// <param name="declination">Declination in degrees.</param>
    /// <returns>Image coordinates, with pixel centers at half integers.</returns>
    public (double X, double Y) CelestialToImage(double rightAscension, double declination)
    {
        EnsureAvailable(rightAscension, declination);
        if (Math.Abs(declination) > 90) throw new ArgumentOutOfRangeException(nameof(declination));
        var native = CelestialToNative(rightAscension, declination);
        var plane = Project(native.X, native.Y);
        return ProjectionToImage(plane.X, plane.Y);
    }

    /// <summary>Evaluates the highest available image-to-projection transformation.</summary>
    /// <param name="x">The image column coordinate.</param>
    /// <param name="y">The image row coordinate.</param>
    /// <returns>Projection plane coordinates in degrees.</returns>
    public (double X, double Y) ImageToProjection(double x, double y)
    {
        EnsureAvailable(x, y);
        if (AvailableLayer == 1)
            return (_linear[0, 0] * (x - _imageReference[0]) + _linear[0, 1] * (y - _imageReference[1]),
                _linear[1, 0] * (x - _imageReference[0]) + _linear[1, 1] * (y - _imageReference[1]));
        var p = Transform(_forward!, x, y);
        var residual = AvailableLayer == 3 ? _forwardDistortion!.Evaluate(x, y) : (0.0, 0.0);
        return (p.X + residual.Item1, p.Y + residual.Item2);
    }

    /// <summary>Evaluates the separately stored projection-to-image transformation.</summary>
    /// <param name="u">The projection coordinate along the first axis in degrees.</param>
    /// <param name="v">The projection coordinate along the second axis in degrees.</param>
    /// <returns>Image coordinates.</returns>
    public (double X, double Y) ProjectionToImage(double u, double v)
    {
        EnsureAvailable(u, v);
        if (AvailableLayer == 1)
        {
            double determinant = Determinant(_linear);
            return (_imageReference[0] + (_linear[1, 1] * u - _linear[0, 1] * v) / determinant,
                _imageReference[1] + (-_linear[1, 0] * u + _linear[0, 0] * v) / determinant);
        }
        var p = Transform(_reverse!, u, v);
        var residual = AvailableLayer == 3 ? _reverseDistortion!.Evaluate(u, v) : (0.0, 0.0);
        return (p.X + residual.Item1, p.Y + residual.Item2);
    }

    private void EnsureAvailable(double a, double b)
    {
        if (AvailableLayer == 0) throw new InvalidOperationException(string.Join(" ", Diagnostics));
        if (!double.IsFinite(a) || !double.IsFinite(b)) throw new ArgumentException("Coordinates must be finite.");
    }
    private bool Present(string name) => _properties.ContainsKey(Prefix + name);
    private T Get<T>(string name) => _properties.TryGetValue(Prefix + name, out var p) && p.IsAvailable && HasType<T>(p.Type) && p.Value is T value ? value :
        throw new XisfFormatException($"Missing or invalid {Prefix + name}.");
    private static bool HasType<T>(string type) => typeof(T) switch
    {
        var t when t == typeof(string) => type == "String",
        var t when t == typeof(int) => type is "Int32" or "Int",
        var t when t == typeof(bool) => type == "Boolean",
        var t when t == typeof(double) => type is "Float64" or "Double",
        var t when t == typeof(double[]) => type is "F64Vector" or "DVector" or "Vector",
        var t when t == typeof(double[,]) => type is "F64Matrix" or "DMatrix" or "Matrix",
        var t when t == typeof(int[]) => type is "I32Vector" or "IVector",
        _ => false
    };
    private T? Optional<T>(string name) where T : class => Present(name) ? Get<T>(name) : null;
    private double[] Vector(string name, int length)
    {
        double[] v = Get<double[]>(name);
        if (v.Length != length || v.Any(x => !double.IsFinite(x))) throw new XisfFormatException($"Invalid vector {name}.");
        return v.ToArray();
    }
    private double[]? OptionalVector(string name, int length) => Present(name) ? Vector(name, length) : null;
    private double[,] Matrix(string name, int rows, int columns)
    {
        double[,] m = Get<double[,]>(name);
        if (m.GetLength(0) != rows || m.GetLength(1) != columns || m.Cast<double>().Any(x => !double.IsFinite(x)))
            throw new XisfFormatException($"Invalid matrix {name}.");
        return (double[,])m.Clone();
    }
    private static double Determinant(double[,] m) => m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0];
    private static (double X, double Y) Transform(double[,] m, double x, double y)
    {
        double w = m[2, 0] * x + m[2, 1] * y + m[2, 2];
        if (w == 0) throw new ArgumentOutOfRangeException(nameof(x), "Point lies on the projective horizon.");
        return ((m[0, 0] * x + m[0, 1] * y + m[0, 2]) / w, (m[1, 0] * x + m[1, 1] * y + m[1, 2]) / w);
    }
    private static double Sin(double a) => Math.Sin(a / R);
    private static double Cos(double a) => Math.Cos(a / R);
    private static double Arg(double x, double y) => Math.Atan2(y, x) * R;
    private static double Longitude(double a) => ((a + 180) % 360 + 360) % 360 - 180;
    private static double Ra(double a) => (a % 360 + 360) % 360;
    private static double Latitude(double z, double x, double y) => Arg(Math.Sqrt(x * x + y * y), z);
    private static double Asin(double x)
    {
        if (Math.Abs(x) > 1 + 1e-13) throw new ArgumentOutOfRangeException(nameof(x), "Point lies outside the projection domain.");
        return Math.Asin(Math.Clamp(x, -1, 1)) * R;
    }
    private void InitializeRotation(double[] native, double[] pole)
    {
        if (Math.Abs(_reference[1]) > 90 || Math.Abs(native[1]) > 90 || Math.Abs(pole[1]) > 90)
            throw new XisfFormatException("Invalid spherical latitude.");
        _phiPole = Longitude(pole[0]);
        double phi0 = native[0], theta0 = native[1], alpha0 = _reference[0], delta0 = _reference[1];
        if (theta0 == 90) { _alphaPole = alpha0; _deltaPole = delta0; return; }
        double c = Cos(theta0) * Cos(_phiPole - phi0), s = Sin(theta0), z = Math.Sqrt(c * c + s * s);
        if (z < 1e-14)
        {
            if (Math.Abs(delta0) > 1e-12) throw new XisfFormatException("Inconsistent celestial pole parameters.");
            _deltaPole = pole[1];
        }
        else
        {
            if (Math.Abs(Sin(delta0) / z) > 1 + 1e-13) throw new XisfFormatException("Inconsistent celestial pole parameters.");
            double d = Math.Acos(Math.Clamp(Sin(delta0) / z, -1, 1)) * R;
            double a = Longitude(Arg(c, s) + d), b = Longitude(Arg(c, s) - d);
            bool validA = Math.Abs(a) <= 90 + 1e-12, validB = Math.Abs(b) <= 90 + 1e-12;
            if (!validA && !validB) throw new XisfFormatException("No valid native pole latitude.");
            _deltaPole = validA && (!validB || Math.Abs(a - pole[1]) <= Math.Abs(b - pole[1])) ? a : b;
            _deltaPole = Math.Clamp(_deltaPole, -90, 90);
        }
        if (Math.Abs(Cos(_deltaPole)) < 1e-14)
            _alphaPole = _deltaPole > 0 ? alpha0 + _phiPole - phi0 - 180 : alpha0 - _phiPole + phi0;
        else
            _alphaPole = alpha0 - Arg((Sin(theta0) - Sin(_deltaPole) * Sin(delta0)) / (Cos(_deltaPole) * Cos(delta0)),
                Sin(_phiPole - phi0) * Cos(theta0) / Cos(delta0));
    }
    private (double X, double Y) NativeToCelestial(double phi, double theta)
    {
        if (Math.Abs(Cos(_deltaPole)) < 1e-14)
            return (Ra(_deltaPole > 0 ? phi + _alphaPole - _phiPole + 180 : _alphaPole + _phiPole - phi), _deltaPole > 0 ? theta : -theta);
        double x = Sin(theta) * Cos(_deltaPole) - Cos(theta) * Sin(_deltaPole) * Cos(phi - _phiPole);
        double y = -Cos(theta) * Sin(phi - _phiPole);
        double z = Sin(theta) * Sin(_deltaPole) + Cos(theta) * Cos(_deltaPole) * Cos(phi - _phiPole);
        return (Ra(_alphaPole + Arg(x, y)), Latitude(z, x, y));
    }
    private (double X, double Y) CelestialToNative(double alpha, double delta)
    {
        if (Math.Abs(Cos(_deltaPole)) < 1e-14)
            return (Longitude(_deltaPole > 0 ? alpha - _alphaPole + _phiPole - 180 : _alphaPole + _phiPole - alpha), _deltaPole > 0 ? delta : -delta);
        double x = Sin(delta) * Cos(_deltaPole) - Cos(delta) * Sin(_deltaPole) * Cos(alpha - _alphaPole);
        double y = -Cos(delta) * Sin(alpha - _alphaPole);
        double z = Sin(delta) * Sin(_deltaPole) + Cos(delta) * Cos(_deltaPole) * Cos(alpha - _alphaPole);
        return (Longitude(_phiPole + Arg(x, y)), Latitude(z, x, y));
    }
    private (double X, double Y) Project(double phi, double theta)
    {
        double radius;
        switch (ProjectionSystem)
        {
            case "PlateCarree": return (phi, theta);
            case "Mercator":
                if (Math.Abs(theta) >= 90) throw new ArgumentOutOfRangeException(nameof(theta));
                return (phi, R * Math.Log(Math.Tan((90 + theta) / (2 * R))));
            case "HammerAitoff":
                double gamma = R * Math.Sqrt(2 / (1 + Cos(theta) * Cos(phi / 2)));
                return (2 * gamma * Cos(theta) * Sin(phi / 2), gamma * Sin(theta));
            case "Gnomonic":
                if (theta < 0.001) throw new ArgumentOutOfRangeException(nameof(theta));
                radius = R / Math.Tan(theta / R); break;
            case "Stereographic":
                if (theta <= -90) throw new ArgumentOutOfRangeException(nameof(theta));
                radius = 2 * R * Math.Tan((90 - theta) / (2 * R)); break;
            case "ZenithalEqualArea": radius = 2 * R * Sin((90 - theta) / 2); break;
            default:
                if (theta < 0) throw new ArgumentOutOfRangeException(nameof(theta));
                radius = R * Cos(theta); break;
        }
        return (radius * Sin(phi), -radius * Cos(phi));
    }
    private (double X, double Y) Deproject(double u, double v)
    {
        double radius = Math.Sqrt(u * u + v * v), theta;
        switch (ProjectionSystem)
        {
            case "PlateCarree":
                if (Math.Abs(v) > 90) throw new ArgumentOutOfRangeException(nameof(v));
                return (Longitude(u), v);
            case "Mercator": return (Longitude(u), 2 * Math.Atan(Math.Exp(v / R)) * R - 90);
            case "HammerAitoff":
                double z2 = 1 - Math.Pow(u / (4 * R), 2) - Math.Pow(v / (2 * R), 2);
                if (z2 < 0.5 - 1e-13) throw new ArgumentOutOfRangeException(nameof(u));
                double z = Math.Sqrt(Math.Max(0, z2));
                return (Longitude(2 * Arg(2 * z2 - 1, z * u / (2 * R))), Asin(v * z / R));
            case "Gnomonic":
                theta = Arg(radius, R);
                if (theta < 0.001) throw new ArgumentOutOfRangeException(nameof(u), "The gnomonic projection is too close to its horizon.");
                break;
            case "Stereographic": theta = 90 - 2 * Math.Atan(radius / (2 * R)) * R; break;
            case "ZenithalEqualArea": theta = 90 - 2 * Asin(radius / (2 * R)); break;
            default: theta = 90 - Asin(radius / R); break;
        }
        return (radius == 0 ? 0 : Arg(-v, u), theta);
    }

    private sealed class Distortion
    {
        private readonly string _basis;
        private readonly int _order, _polynomials;
        private readonly Term? _global, _fallback;
        private readonly Term[] _local = [];
        private readonly double _threshold;
        public Distortion(XisfAstrometricSolution owner, string prefix)
        {
            _basis = owner.Get<string>(prefix + "BasisFunction");
            _order = owner.Get<int>(prefix + "Order");
            bool polynomial = !owner.Present(prefix + "Polynomial") || owner.Get<bool>(prefix + "Polynomial");
            bool shape = _basis is "Gaussian" or "Multiquadric" or "InverseMultiquadric" or "InverseQuadratic";
            if (!shape && _basis is not ("ThinPlateSpline" or "VariableOrder")) throw new XisfFormatException("Unknown spline basis function.");
            if (_order < 1 || (_basis == "VariableOrder" && _order < 3) || (_basis == "ThinPlateSpline" && _order < 2) || (!shape && !polynomial))
                throw new XisfFormatException("Invalid spline order or polynomial setting.");
            if (_order > 1024) throw new XisfFormatException("Spline order exceeds the evaluation limit.");
            _polynomials = polynomial ? checked(_order * (_order + 1) / 2) : 0;
            string[] kinds = owner.Get<string>(prefix + "Terms").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (kinds.Length == 0 || kinds.Any(k => k is not ("Global" or "Local" or "Fallback")) || kinds.Distinct().Count() != kinds.Length)
                throw new XisfFormatException("Unknown or repeated distortion term kind.");
            if (kinds.Contains("Global")) _global = ReadSingle(owner, prefix + "Global:", shape);
            if (kinds.Contains("Local"))
            {
                double[,] centers = owner.Get<double[,]>(prefix + "Local:Center");
                int count = centers.GetLength(0);
                if (count == 0 || centers.GetLength(1) != 2 || centers.Cast<double>().Any(v => !double.IsFinite(v)))
                    throw new XisfFormatException("Invalid local support centers.");
                double[] radii = owner.Vector(prefix + "Local:Radius", count);
                if (radii.Any(v => v <= 0)) throw new XisfFormatException("Invalid local support radii.");
                Spline[] x = ReadPacked(owner, prefix + "Local:X:", count, shape, null);
                Spline[] y = ReadPacked(owner, prefix + "Local:Y:", count, shape, x);
                _local = Enumerable.Range(0, count).Select(i => new Term(x[i], y[i], centers[i, 0], centers[i, 1], radii[i])).ToArray();
            }
            if (kinds.Contains("Fallback"))
            {
                if (_local.Length == 0) throw new XisfFormatException("Fallback requires Local terms.");
                _threshold = owner.Get<double>(prefix + "Fallback:Threshold");
                if (!double.IsFinite(_threshold) || _threshold <= 0) throw new XisfFormatException("Invalid fallback threshold.");
                _fallback = ReadSingle(owner, prefix + "Fallback:", shape);
            }
        }
        private Term ReadSingle(XisfAstrometricSolution owner, string prefix, bool shape)
        {
            Spline x = ReadSpline(owner, prefix + "X:", shape, null);
            Spline y = ReadSpline(owner, prefix + "Y:", shape, x);
            return new(x, y);
        }
        private Spline ReadSpline(XisfAstrometricSolution owner, string prefix, bool shape, Spline? shared)
        {
            bool ownNodes = owner.Present(prefix + "Nodes");
            if (shared is null || ownNodes)
            {
                var norm = owner.Vector(prefix + "Normalization", 3);
                var nodes = owner.Get<double[,]>(prefix + "Nodes");
                double epsilon = shape ? owner.Get<double>(prefix + "ShapeParameter") : 0;
                if (!shape && owner.Present(prefix + "ShapeParameter")) throw new XisfFormatException("Unexpected shape parameter.");
                return new(norm, nodes, owner.Get<double[]>(prefix + "Coefficients"), epsilon, shape, _polynomials);
            }
            if (!shape && owner.Present(prefix + "ShapeParameter")) throw new XisfFormatException("Unexpected shape parameter.");
            return new(owner.OptionalVector(prefix + "Normalization", 3) ?? shared.Normalization, shared.Nodes,
                owner.Get<double[]>(prefix + "Coefficients"), owner.Present(prefix + "ShapeParameter") ? owner.Get<double>(prefix + "ShapeParameter") : shared.Epsilon, shape, _polynomials);
        }
        private Spline[] ReadPacked(XisfAstrometricSolution owner, string prefix, int count, bool shape, Spline[]? shared)
        {
            bool ownNodes = owner.Present(prefix + "Nodes");
            double[,]? nodes = shared is null || ownNodes ? owner.Get<double[,]>(prefix + "Nodes") : null;
            int[]? offsets = shared is null || ownNodes || owner.Present(prefix + "NodeOffsets") ? owner.Get<int[]>(prefix + "NodeOffsets") : null;
            double[,]? norms = shared is null || ownNodes || owner.Present(prefix + "Normalization") ? owner.Matrix(prefix + "Normalization", count, 3) : null;
            double[]? epsilons = shape && (shared is null || ownNodes || owner.Present(prefix + "ShapeParameter")) ? owner.Vector(prefix + "ShapeParameter", count) : null;
            if (!shape && owner.Present(prefix + "ShapeParameter")) throw new XisfFormatException("Unexpected shape parameter.");
            if (nodes is not null && (nodes.GetLength(1) != 2 || offsets!.Length != count + 1 || offsets[0] != 0 || offsets[^1] != nodes.GetLength(0) ||
                offsets.Zip(offsets.Skip(1)).Any(p => p.First > p.Second))) throw new XisfFormatException("Invalid packed nodes or offsets.");
            if (nodes is null && offsets is not null) throw new XisfFormatException("Node offsets require nodes.");
            double[] coefficients = owner.Get<double[]>(prefix + "Coefficients");
            int totalNodes = nodes?.GetLength(0) ?? shared!.Sum(s => s.Nodes.GetLength(0));
            if (coefficients.Length != totalNodes + count * _polynomials) throw new XisfFormatException("Invalid packed coefficients.");
            Spline[] result = new Spline[count];
            int offset = 0;
            for (int i = 0; i < count; i++)
            {
                double[,] n;
                if (nodes is null) n = shared![i].Nodes;
                else
                {
                    n = new double[offsets![i + 1] - offsets[i], 2];
                    for (int j = 0; j < n.GetLength(0); j++) { n[j, 0] = nodes[offsets[i] + j, 0]; n[j, 1] = nodes[offsets[i] + j, 1]; }
                }
                double[] normalization = norms is null ? shared![i].Normalization : [norms[i, 0], norms[i, 1], norms[i, 2]];
                int length = n.GetLength(0) + _polynomials;
                result[i] = new(normalization, n, coefficients.AsSpan(offset, length).ToArray(), epsilons?[i] ?? shared?[i].Epsilon ?? 0, shape, _polynomials);
                offset += length;
            }
            return result;
        }
        public (double X, double Y) Evaluate(double x, double y)
        {
            double sx = 0, sy = 0, weight = 0, localWeight = 0, closest = double.PositiveInfinity;
            Term? nearest = null;
            void Add(Term t, double w)
            {
                if (w == 0) return;
                sx += w * t.X.Evaluate(x, y, _basis, _order, _polynomials);
                sy += w * t.Y.Evaluate(x, y, _basis, _order, _polynomials);
                weight += w;
            }
            if (_global is not null) Add(_global, 1);
            foreach (Term t in _local)
            {
                double distance = Math.Sqrt((x - t.Cx) * (x - t.Cx) + (y - t.Cy) * (y - t.Cy)) / t.Radius;
                if (distance < closest) { closest = distance; nearest = t; }
                double w = Wendland(distance);
                localWeight += w; Add(t, w);
            }
            if (_fallback is not null) Add(_fallback, Wendland(localWeight / _threshold));
            if (weight > 0) return (sx / weight, sy / weight);
            return nearest is null ? (0, 0) : (nearest.X.Evaluate(x, y, _basis, _order, _polynomials), nearest.Y.Evaluate(x, y, _basis, _order, _polynomials));
        }
        private static double Wendland(double t) => t < 1 ? Math.Pow(1 - t, 4) * (4 * t + 1) : 0;
        private sealed record Term(Spline X, Spline Y, double Cx = 0, double Cy = 0, double Radius = 1);
        private sealed class Spline
        {
            public readonly double[] Normalization, Coefficients;
            public readonly double[,] Nodes;
            public readonly double Epsilon;
            public Spline(double[] normalization, double[,] nodes, double[] coefficients, double epsilon, bool shape, int polynomials)
            {
                if (normalization.Length != 3 || normalization.Any(v => !double.IsFinite(v)) || normalization[2] <= 0 || nodes.GetLength(1) != 2 ||
                    nodes.Cast<double>().Any(v => !double.IsFinite(v)) || coefficients.Any(v => !double.IsFinite(v)) || coefficients.Length != nodes.GetLength(0) + polynomials ||
                    (shape && (!double.IsFinite(epsilon) || epsilon <= 0))) throw new XisfFormatException("Invalid spline record.");
                Normalization = normalization.ToArray(); Nodes = (double[,])nodes.Clone(); Coefficients = coefficients.ToArray(); Epsilon = epsilon;
            }
            public double Evaluate(double x, double y, string basis, int order, int polynomials)
            {
                double xi = Normalization[2] * (x - Normalization[0]), eta = Normalization[2] * (y - Normalization[1]), sum = 0;
                int n = Nodes.GetLength(0);
                for (int i = 0; i < n; i++)
                {
                    double dx = xi - Nodes[i, 0], dy = eta - Nodes[i, 1], radius = Math.Sqrt(dx * dx + dy * dy), er2 = Epsilon * Epsilon * radius * radius;
                    double kernel = basis switch
                    {
                        "ThinPlateSpline" => radius == 0 ? 0 : radius * radius * Math.Log(radius),
                        "VariableOrder" => radius == 0 ? 0 : Math.Pow(radius * radius, order - 1) * Math.Log(radius * radius),
                        "Gaussian" => Math.Exp(-er2), "Multiquadric" => Math.Sqrt(1 + er2),
                        "InverseMultiquadric" => 1 / Math.Sqrt(1 + er2), _ => 1 / (1 + er2)
                    };
                    sum += Coefficients[i] * kernel;
                }
                if (polynomials > 0)
                    for (int degree = 0; degree < order; degree++)
                        for (int powerX = degree; powerX >= 0; powerX--)
                            sum += Coefficients[n++] * Math.Pow(xi, powerX) * Math.Pow(eta, degree - powerX);
                return sum;
            }
        }
    }
}
