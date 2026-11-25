# Repository guidance

## Scope

This repository contains the Caerllion.Light .NET library and its TUnit tests.
Keep changes small and preserve the in-process, dependency-light architecture.

## Build

- Target framework: `net10.0`
- SDK convention: use the repository `global.json`
- Build: `dotnet build .\Caerllion.Light.slnx --nologo`
- Test: `dotnet test .\tests\Caerllion.Light.Tests\Caerllion.Light.Tests.csproj --nologo`
- Package: `dotnet pack .\src\Caerllion.Light\Caerllion.Light.csproj --configuration Release --nologo`

## Code conventions

- Enable nullable reference types and add XML documentation for public members.
- Use file-scoped namespaces, UTF-8 text files without a byte-order mark, and a final newline.
- Prefer `ConfigureAwait(false)` in library async code.
- Keep channel dispatch and state transitions deterministic and thread-safe.
- Do not add runtime dependencies unless a concrete compatibility or feature requirement needs one.
- Keep NuGet package versions in the root `Directory.Packages.props`.
- Keep shared target-framework and compiler settings in the root `Directory.Build.props`; keep project-specific settings local.
- Keep AOT compatibility and NuGet package metadata declared in the library project file.
- Keep AOT compatibility enabled for the library and resolve trimming/AOT analyzer warnings.
- Preserve existing public overloads unless a breaking change is explicitly approved.
- Use `IAsyncDisposable` for components that own a channel lifecycle.
- Treat event subscriber exceptions and handler exceptions according to the existing hub semantics:
  they must not escape the dispatch loop.

## Tests

- Use TUnit and the Microsoft Testing Platform configuration.
- Add tests for public behavior, lifecycle boundaries, cancellation, and edge-state transitions.
- Tests should assert observable outcomes, not private implementation details.
- Keep test projects targeting `net10.0`.

## Validation

For C# changes, run the focused test project and a release package build.
Do not finish with compiler or test warnings.
