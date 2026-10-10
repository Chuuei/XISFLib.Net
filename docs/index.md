# XISFLib.Net documentation

XISFLib.Net reads, writes, validates, and converts XISF and FITS astronomy images. The core package targets .NET 10. Windows Forms and WPF adapters are in a separate Windows package.

The public API uses C# XML documentation comments. Building the library projects produces XML documentation beside each assembly; packing includes these files for IntelliSense. Missing public API documentation is a build error (`CS1591`).

| Guide | Contents |
| --- | --- |
| [Getting started](getting-started.md) | Build, reference, read, edit, write, and convert |
| [API guide](api-guide.md) | Types, layouts, metadata, tables, validation, utilities, and Windows rendering |
| [Applications](applications.md) | WPF workbench, save modes and editor limits, batch converter, and example files |
| [I/O and limitations](io-and-limitations.md) | Ownership, safe saving, resource policies, cancellation, precision, and memory |
| [Astrometric solutions](astrometry.md) | Native projections, projective transforms, spline distortion and preservation |
| [Specification coverage](specification-compliance.md) | Revision 1 audit, feature coverage, verification and bounds |
| [Architecture](architecture.md) | Responsibilities of every project and the main processing paths |
| [Performance](performance.md) | Measured optimizations, benchmark commands, and efficient API usage |
| [Contributing](../CONTRIBUTING.md) | Development, testing, packaging, and documentation conventions |

The [repository README](../README.md) includes further executable usage examples and the supported format features. The XML comments on each overload are the API reference. A DocFX configuration is supplied to render these guides and the API reference as a local website; DocFX is optional for normal builds.

With DocFX installed, run from the repository root:

```powershell
docfx docfx.json
docfx serve docs/_site
```

Generated API metadata and website output are excluded from source control. The checked-in guides and C# comments are the documentation sources.
