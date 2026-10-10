# Contributing

## Development setup

Use the .NET 10 SDK from `global.json`. Build and test the complete solution on Windows; the core project is platform independent. Rider and Visual Studio can open `XISFLib.Net.slnx`.

```powershell
dotnet restore XISFLib.Net.slnx
dotnet build XISFLib.Net.slnx -c Release --no-restore
dotnet test XISFLib.Tests/XISFLib.Tests.csproj -c Release --no-build
dotnet test XISFLib.Windows.Tests/XISFLib.Windows.Tests.csproj -c Release --no-build
dotnet pack XISFLib/XISFLib.csproj -c Release --no-build --output artifacts
dotnet pack XISFLib.Windows/XISFLib.Windows.csproj -c Release --no-build --output artifacts
```

Generated XML documentation is included beside the package assemblies. Build outputs, generated API metadata, and generated documentation websites should not be committed.

## Code and tests

Follow existing C# conventions: nullable annotations, file-scoped namespaces, explicit ownership of buffers and streams, invariant format parsing, checked size arithmetic, and narrow format-specific exceptions. Keep UI dependencies in the Windows project or viewer. Preserve caller-owned stream lifetime.

When changing parsing, validate bounds before allocation and exercise truncated, overflowing, and malformed input. For conversions, test numeric boundary values, channel layout, byte order, scaling, and relevant metadata. Add focused regressions for observable defects. Use independently constructed fixtures as well as reader/writer round trips, since matching implementations can share a mistake.

The core and Windows test projects use xUnit. Tests create temporary outputs and clean them up. Avoid requiring external network access for normal tests. Use the benchmark harness with representative data for allocation-sensitive changes; do not make machine-dependent timings into correctness assertions.

## Documentation

Document public APIs with C# XML comments. Add a useful `<summary>`, then `<param>`, `<returns>`, `<remarks>`, and `<exception>` where callers need contracts beyond the name. For positional records, document primary-constructor parameters so generated properties receive documentation. Include units, index origins, buffer ownership, stream lifetime, and precision limits when relevant.

Both library projects generate XML documentation and treat missing public documentation (`CS1591`) as an error. Update the [documentation guides](docs/index.md) and README when behavior changes. Keep implementation architecture in the architecture guide and usage contracts in the API/I/O guides.

Optional website generation uses `docfx docfx.json`. Normal builds and package generation do not require DocFX. Do not edit generated API files; change C# comments or conceptual Markdown instead.

## Review and CI

Describe the concrete behavior changed, its compatibility implications, and tests performed. Single-file path output must preserve existing files when serialization fails. Distributed output and stream output have different failure contracts; keep those distinctions documented.

CI runs on Windows and covers Release builds, both test suites, core NuGet packaging, and dependency auditing. Vulnerability warnings are errors under `Directory.Build.props`. Do not suppress these warnings to make a build pass.

CI also installs DocFX 2.81.0, builds the documentation with warnings treated as errors, and uploads the generated site as the `documentation` artifact. Pull requests validate documentation without deploying it. Successful pushes to the repository's default branch, or manual CI runs on that branch, deploy the artifact to GitHub Pages through the `github-pages` environment. Pages deployment concurrency is limited to one active job.

CI publishes the WPF viewer with the `WinX64` profile, verifies that the output contains exactly one executable, and uploads it as `wpf-viewer-win-x64`. Push a version tag beginning with `v` to publish a GitHub release containing this self-contained Windows x64 executable. Hyphenated tags produce prereleases. The release job has `contents: write` permission; build and pull-request jobs retain read-only repository permissions. Release creation verifies the existing tag and generates release notes; reruns update the executable asset without replacing the notes. See [getting started](docs/getting-started.md#run-the-applications) for local publishing.

For initial hosting setup, select **GitHub Actions** under repository **Settings > Pages > Build and deployment > Source**. The workflow uses the repository's `GITHUB_TOKEN`; no personal access token or `gh-pages` branch is needed. Environment protection rules can be configured on `github-pages`.
