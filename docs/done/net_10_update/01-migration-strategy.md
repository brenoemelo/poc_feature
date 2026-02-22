# 01 - Migration Strategy (.NET 10)

## Overview
This document outlines the strategic approach to upgrading the entire solution from **.NET 8 (LTS)** to **.NET 10 (LTS)**. 
.NET 10 is the latest Long Term Support release (expected availability Nov 2025), offering significant performance improvements, enhanced cloud-native capabilities, and newer C# language features.

## Objectives
1.  **Upgrade Target**: Move all projects (`src/**/*.csproj`, `tests/**/*.csproj`) to `net10.0`.
2.  **Container Base Images**: Update Dockerfiles to use `mcr.microsoft.com/dotnet/aspnet:10.0` and `sdk:10.0`.
3.  **AWS Lambda Runtime**: Migrate from `dotnet8` runtime to `dotnet10` (when available in AWS Lambda).

## Benefits
*   **Performance**: .NET 10 introduces further optimizations in the JIT compiler and JSON serialization, critical for high-throughput Lambda functions.
*   **Language Features**: Access to C# 14 (hypothetical) features for cleaner, more expressive code.
*   **Support**: Ensures long-term security patches and support from Microsoft.

## Risks & Mitigation
*   **AWS Lambda Support**: AWS typically lags a few months behind the official .NET release.
    *   *Mitigation*: If `dotnet10` managed runtime is not yet available, we can use **Custom Runtime** (running a self-contained executable on Amazon Linux 2023) or wait for official support.
    *   *Recommendation*: Check AWS Compute Blog for "Managed Runtime for .NET 10" availability.
*   **NuGet Failures**: Third-party libraries (especially AWS SDKs) might have breaking changes or lack explicit .NET 10 targets.
    *   *Mitigation*: Run a "dry-run" upgrade on a branch and verify all unit tests pass before merging.

## Timeline
1.  **Phase 1**: Local Development Upgrade (SDK & Global.json)
2.  **Phase 2**: Project File Updates (`TargetFramework`)
3.  **Phase 3**: Dependency Audits (NuGet Updates)
4.  **Phase 4**: Docker & CI/CD Updates
5.  **Phase 5**: QA & Regression Testing (E2E Tests)
