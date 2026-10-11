# Contributing

## Development setup

Use the .NET 10 SDK from `global.json`. Build and test the complete solution on Windows; the core project is platform independent. Rider and Visual Studio can open `XISFLib.Net.slnx`.

```powershell
dotnet restore XISFLib.Net.slnx
dotnet build XISFLib.Net.slnx -c Release --no-restore
dotnet test XISFLib.Tests/XISFLib.Tests.csproj -c Release --no-build
dotnet test XISFLib.Windows.Tests/XISFLib.Windows.Tests.csproj -c Release --no-build
dotnet test XISFLib.Viewer.Core.Tests/XISFLib.Viewer.Core.Tests.csproj -c Release --no-build
dotnet test XISFLib.AvaloniaViewer.Tests/XISFLib.AvaloniaViewer.Tests.csproj -c Release --no-build
dotnet pack XISFLib/XISFLib.csproj -c Release --no-build --output artifacts
dotnet pack XISFLib.Windows/XISFLib.Windows.csproj -c Release --no-build --output artifacts
```

Generated XML documentation is included beside the package assemblies. Build outputs, generated API metadata, and generated documentation websites should not be committed.

## Code and tests

Follow existing C# conventions: nullable annotations, file-scoped namespaces, explicit ownership of buffers and streams, invariant format parsing, checked size arithmetic, and narrow format-specific exceptions. Keep UI dependencies in the Windows project or viewer. Preserve caller-owned stream lifetime.

When changing parsing, validate bounds before allocation and exercise truncated, overflowing, and malformed input. For conversions, test numeric boundary values, channel layout, byte order, scaling, and relevant metadata. Add focused regressions for observable defects. Use independently constructed fixtures as well as reader/writer round trips, since matching implementations can share a mistake.

The core and Windows test projects use xUnit. Tests create temporary outputs and clean them up. Avoid requiring external network access for normal tests. Use the benchmark harness with representative data for allocation-sensitive changes; do not make machine-dependent timings into correctness assertions.

## Documentation

Shared viewer tests use xUnit v2. Avalonia 12 headless tests use xUnit v3 3.2.2 with an executable test project. Their test application uses Skia/HarfBuzz with headless drawing disabled to exercise the real bitmap backend. CI runs all four test suites. See the repository migration plan for pending parity work.

Document public APIs with C# XML comments. Add a useful `<summary>`, then `<param>`, `<returns>`, `<remarks>`, and `<exception>` where callers need contracts beyond the name. For positional records, document primary-constructor parameters so generated properties receive documentation. Include units, index origins, buffer ownership, stream lifetime, and precision limits when relevant.

Both library projects generate XML documentation and treat missing public documentation (`CS1591`) as an error. Update the [documentation guides](docs/index.md) and README when behavior changes. Keep implementation architecture in the architecture guide and usage contracts in the API/I/O guides.

Optional website generation uses `docfx docfx.json`. Normal builds and package generation do not require DocFX. Do not edit generated API files; change C# comments or conceptual Markdown instead.

## Review and CI

Describe the concrete behavior changed, its compatibility implications, and tests performed. Single-file path output must preserve existing files when serialization fails. Distributed output and stream output have different failure contracts; keep those distinctions documented.

The Windows CI job covers the whole solution, all four test suites, core NuGet packaging and dependency auditing. A portable matrix also runs the core/shared/Avalonia suites on Windows x64, Linux x64 and macOS arm64, publishes one self-contained executable per RID and runs its native `--smoke-test` probe before uploading ZIP artifacts. Linux uses Xvfb for the real X11 backend. Vulnerability warnings are errors under `Directory.Build.props`; do not suppress them.

CI also installs DocFX 2.81.0, builds the documentation with warnings treated as errors, and uploads the generated site as the `documentation` artifact. Pull requests validate documentation without deploying it. Successful pushes to the repository's default branch, or manual CI runs on that branch, deploy the artifact to GitHub Pages through the `github-pages` environment. Pages deployment concurrency is limited to one active job.

Avalonia ZIP artifacts are named `avalonia-viewer-{rid}`. Version tags beginning with `v` publish all viewer assets only after both job groups succeed. Hyphenated tags produce prereleases. The release job alone has `contents: write`; build/PR jobs remain read-only. Releases verify tags and generate notes; reruns replace assets without replacing notes. See [getting started](docs/getting-started.md#run-the-applications) for local publishing. Neither workflow nor artifacts imply completed interactive OS certification; keep the migration audit's evidence current.

For Linux/macOS development, build/test the three portable projects individually instead of the Windows-containing solution. Run `dotnet run --project XISFLib.AvaloniaViewer -c Release -- --smoke-test artifacts/avalonia-smoke/report.json` for a real desktop-backend probe. It exits with a failure code on errors and produces six tab renders. Normal startup never runs probes. The standalone Windows Avalonia profile is `Properties/PublishProfiles/WinX64.pubxml`. The Avalonia viewer disables trimming and embeds dependency notices; update `XISFLib.AvaloniaViewer/Notices` when framework/backend licenses change. Magick.NET and toolkit notices are embedded directly from their pinned NuGet packages.

For initial hosting setup, select **GitHub Actions** under repository **Settings > Pages > Build and deployment > Source**. The workflow uses the repository's `GITHUB_TOKEN`; no personal access token or `gh-pages` branch is needed. Environment protection rules can be configured on `github-pages`.
