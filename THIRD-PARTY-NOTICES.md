# Licensing and third-party notices

XISFLib.Net-authored code is Apache-2.0; retain [LICENSE](LICENSE) and the relevant
attribution in [NOTICE](NOTICE) when redistributing. Changes and commercial use are
permitted under that license. Third-party libraries and sample photographs keep
their original terms; the project license does not relicense them.

## Core library

| Component | Version | License / source |
| --- | --- | --- |
| K4os.Compression.LZ4 | 1.3.8 | MIT, https://github.com/MiloszKrajewski/K4os.Compression.LZ4 |
| ZstdSharp.Port | 0.8.8 | MIT, https://github.com/oleg-st/ZstdSharp; underlying Zstandard uses BSD-3-Clause |
| System.Security.Cryptography.Xml and Pkcs | 10.0.13 | MIT, https://github.com/dotnet/runtime |
| .NET runtime and Windows Desktop runtime, when redistributed | Release-resolved version | MIT and bundled third-party notices, https://github.com/dotnet/runtime and https://github.com/dotnet/windowsdesktop |

Core NuGet packages include this file and the project license/NOTICE. Their NuGet
dependencies remain separately versioned packages and retain their own notices.
An application that bundles the dependency binaries must also distribute their
license/copyright texts; package metadata alone is not a binary license bundle.

## Viewer and native components

The Avalonia viewer additionally uses Avalonia 12.1.4 (DataGrid 12.1.2, MIT),
CommunityToolkit.Mvvm 8.4.2 (MIT), Magick.NET 15.0.0 (Apache-2.0), ImageMagick and its
native delegate libraries, and the SkiaSharp/HarfBuzzSharp/native graphics stack.
Full upstream Avalonia, Skia/SkiaSharp, toolkit and Magick notices are retained in
the viewer's embedded resources. The Magick.NET `Notice.txt` is a multi-license
notice, including LGPL/CDDL/source-availability terms for some native delegates.
It is not correct to describe the entire native bundle as Apache-2.0.

Distributing a native viewer also requires the corresponding native sources and
build instructions where those licenses require them. An embedded notice alone
does not meet source/relinking requirements. The linked SkyBrowser project provides
a pinned Magick.NET 15.0.0 Windows source-compliance bundle and rebuild guide.
Do not publish new viewer binaries until the complete platform-specific native
source/relinking materials have been prepared and reviewed. Existing library
packages do not contain these viewer dependencies.

## Examples and documentation

Large `Examples/` photographs are external test fixtures, not Apache-licensed
project code. Their presence in Git LFS does not establish permission to republish
them. Preserve provenance and use generated fixtures for redistributable tests.
See `Examples/README.md` for existing corpus provenance and limitations.

Build and test tools are not included in the core runtime package. Keep their
own licenses if redistributing those tools. New dependency versions require a
fresh notice and native-source review.
