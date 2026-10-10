# Native XISF astrometric solutions

`XisfImage.AstrometricSolution` evaluates the Revision 1 `AstrometricSolution` properties. All celestial angles are degrees. Pixel centers use `(column + 0.5, row + 0.5)`, which differs from the FITS WCS convenience API.

```csharp
XisfImage image = XisfReader.Load("solved.xisf").Images[0];
XisfAstrometricSolution? solution = image.AstrometricSolution;
if (solution is { AvailableLayer: > 0 })
{
    var sky = solution.ImageToCelestial(100.5, 200.5);
    var pixel = solution.CelestialToImage(sky.RightAscension, sky.Declination);
    Console.WriteLine($"{sky.RightAscension}, {sky.Declination}: {pixel.X}, {pixel.Y}");
}
```

`AvailableLayer` is 0 when the solution cannot be evaluated, 1 for the linear projection model, 2 for projective transformations, or 3 when both distortion models are available. `Diagnostics` explains why a present layer was unavailable. Layer 4 contains optional provenance, available through `Properties`; it does not change the transformation.

The supported projection identifiers are `Gnomonic`, `Stereographic`, `ZenithalEqualArea`, `Orthographic`, `PlateCarree`, `Mercator` and `HammerAitoff`. Nondefault reference-native and celestial-pole coordinates are honored. `CelestialReferenceSystem` reports the stored system, defaulting to ICRS, without converting coordinates to another system.

## Creating a solution

```csharp
var image = XisfImageData.FromSamples(new ushort[1024 * 1024], [1024, 1024], 1);
var solution = XisfAstrometricSolution.CreateLinear(
    "Gnomonic",
    [83.822, -5.391],
    [512.0, 512.0],
    new double[,] { { -0.0003, 0 }, { 0, 0.0003 } });
solution.ApplyTo(image);
XisfWriter.Write("solved.xisf", [image], new XisfWriterOptions
{
    Compression = XisfCompression.Zstd,
    Shuffle = true
});
```

For fitted models, pass explicitly typed properties to `XisfAstrometricSolution.Create`. Supply both projective directions and, when present, both distortion directions. Their matrices and spline coefficients are independent fitted models: the evaluator uses each stored direction directly.

Distortion models support ThinPlateSpline, VariableOrder, Gaussian, Multiquadric, InverseMultiquadric and InverseQuadratic kernels. Polynomial terms, normalization, shared or independent X/Y nodes, concatenated Local arrays and offsets, compact local-disc weights, Fallback thresholds and nearest-local behavior follow the specification. Creating a model validates required properties and dimensions; the library does not fit coefficients from control points.

## Editing and preservation

`XisfImageData.FromImage` and `solution.ApplyTo` retain unknown solution properties. Unknown minor revisions are interpreted through known properties; unknown major revisions remain preserved with layer 0. Reading never invokes a solver or consults FITS WCS cards.

When pixels keep their coordinate geometry, preserve the existing solution. For geometric operations, ensure the stored model is transformed consistently. If that cannot be done exactly, call `XisfAstrometricSolution.RemoveFrom(image)` before writing and generate a new solution explicitly. Changing dimensions or resampling pixels does not automatically update coefficients.
