# Contributing to Promptino

Thanks for your interest in improving Promptino.

## Before you start

- Search existing issues before opening a new one.
- Use the bug report or feature request template when possible.
- Keep changes focused and avoid unrelated formatting or refactoring.
- For substantial changes, open an issue first to discuss the approach.

## Development setup

Promptino requires Windows and the .NET 10 SDK.

```powershell
dotnet restore Promptino.slnx -r win-x64
dotnet build Promptino.slnx -c Release
dotnet test Promptino.App.Tests/Promptino.App.Tests.csproj -c Release
```

## Pull requests

- Describe the problem and the chosen solution.
- Include tests for behavioral changes.
- Confirm that the existing test suite passes.
- Update documentation when user-facing behavior changes.

By contributing, you agree that your contribution will be licensed under the repository's MIT License.
