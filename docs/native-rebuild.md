# Corresponding native sources and rebuilding

This source bundle accompanies binaries using Magick.NET **15.0.0** and
Magick.Native **2026.1010.1943**. `manifest.json` pins every repository revision,
archive URL and SHA-256 hash. It includes Magick.NET, Magick.Native, ImageMagick,
Configure, the dependency build scripts, and every dependency source selected by
that pinned build collection. Some optional upstream GPL components are included
as sources for completeness; this does not mean those optional components are
linked into the published non-`all` native binary.

Archives contain upstream source, license files, modifications and build scripts,
not Git history. None has an unresolved Git submodule. Preserve the original
license/copyright notices. Do not replace these pinned archives with links to
moving branches. Redistribution of this bundle retains each source's own terms.

## Native build

Extract the archives into separate directories. Start with `Building.md` and
`.github/workflows/main.yml` inside the Magick.Native archive. Windows uses Visual
Studio 2026 C++/MSBuild, Windows SDK, Git/Bash and Docker as documented there.
The source lock records the exact ImageMagick and Configure commits, and
Dependencies release `2026.10.09.2225` with its original build scripts.

The native checkout scripts normally download release-built static dependency
libraries. To change a delegate, use the archived Dependencies build workflows and
scripts to rebuild its static library from the matching source archive, retaining
the same architecture/runtime options. Recreate the dependency artifact using
that repository's workflow, or put the rebuilt artifacts in the native checkout's
`src/ImageMagick/Artifacts` layout. The archived Configure project supplies its
source and project-file templates. Keep the dependency source tree names used by
`clone-dependencies.sh`; the `jpeg-turbo-12` and `jpeg-turbo-16` repositories contain
configuration wrappers using the included `jpeg-turbo` sources.

For SkyBrowser the native variant is **Q16, x64, noHdri, noOpenMP** with the linked
runtime configuration. The corresponding native build command, from
`Magick.Native/build/windows`, is:

```text
build.cmd Release Q16 x64 noHdri noOpenMP
```

Follow the archived checkout/configure steps first. Viewer variants on other
platforms use their matching quantum/architecture settings from the archived
workflow. Sources for all selected platform delegates are supplied, including
non-Windows fontconfig. The resulting DLL can replace the original native DLL.

## Recombine with the .NET application

The application source is freely available under Apache-2.0 and is supplied with
the release. It permits modification and rebuilding. No license term here prohibits
reverse engineering needed to debug modifications to LGPL components.

Use a separate local NuGet package cache while experimenting. Restore the same
package versions, replace the native DLL in that cache's
`magick.net-q16-anycpu/15.0.0/runtimes/win-x64/native` directory with your rebuilt
`Magick.Native-Q16-x64.dll`, then republish from the supplied application sources.
For a replaceable, unpacked layout, override `PublishSingleFile=false` and
`IncludeNativeLibrariesForSelfExtract=false` when publishing. The managed wrapper
and P/Invoke sources are also included for changes requiring interface adjustments.

Keep the resulting native library's licenses and source changes when sharing it.
These instructions accompany complete source/build materials; they are not an
offer to supply source later. The C++ native rebuild itself has not been certified
on the SkyBrowser maintainer's machine; consult upstream build prerequisites and
the exact archived workflows for platform details.
