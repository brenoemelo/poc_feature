# CI/CD Integration for Observability Tests

This guide outlines how to integrate the OpenTelemetry (OTel) Unit and Integration tests into your Continuous Integration pipeline.

## Overview

The observability tests are located in:
*   `tests/PoC.Shared.Tests/PoC.Shared.Tests.csproj`
*   `tests/PoC.Costing.Tests/PoC.Costing.Tests.csproj`

These tests are designed to run with the standard `dotnet test` command and require no external dependencies (like Docker) as they use `InMemoryExporter`.

## Prerequisites

*   .NET 8 SDK installed on the build agent.
*   Enforce .NET 8 SDK usage via `global.json` (already present in repository root).

## Local Execution

To run all tests locally:

```powershell
dotnet test
```

To run only Observability tests:

```powershell
dotnet test --filter "FullyQualifiedName~PoC.Shared.Tests|FullyQualifiedName~PoC.Costing.Tests"
```

## GitHub Actions Example

Add the following step to your `.github/workflows/build.yml`:

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x

    - name: Restore Dependencies
      run: dotnet restore

    - name: Run Observability Tests
      run: |
        dotnet test tests/PoC.Shared.Tests/PoC.Shared.Tests.csproj --no-restore --verbosity normal
        dotnet test tests/PoC.Costing.Tests/PoC.Costing.Tests.csproj --no-restore --verbosity normal
```

## Azure DevOps Example

Add the following task to your `azure-pipelines.yml`:

```yaml
steps:
- task: UseDotNet@2
  displayName: 'Use .NET 8 SDK'
  inputs:
    packageType: 'sdk'
    version: '8.0.x'

- task: DotNetCoreCLI@2
  displayName: 'Run Observability Tests'
  inputs:
    command: 'test'
    projects: |
      tests/PoC.Shared.Tests/PoC.Shared.Tests.csproj
      tests/PoC.Costing.Tests/PoC.Costing.Tests.csproj
    arguments: '--configuration Release'
```

## Troubleshooting

### Dependency Conflicts (System.IO.FileNotFoundException)
If you encounter `FileNotFoundException` related to `Microsoft.Extensions.*` version `9.0.0.0`:
1.  Ensure `global.json` pins the SDK to `8.0.x`.
2.  Check that all project references to `Microsoft.Extensions.*` are strictly version `8.0.0`.
3.  Do not mix `OpenTelemetry` packages with different minor versions if possible (e.g., stick to `1.9.0` for core compatibility).

### Missing Logs in Tests
If `LoggingTelemetryTests` fails:
1.  Ensure `builder.SetMinimumLevel(LogLevel.Trace)` is called in `OtelTestFixture`.
2.  Check `LogRecord.Body` instead of `FormattedMessage` for simple log strings.
