# 03 - Upgrade Steps

This guide details the exact steps to migrate the solution.

## Step 1: Update Global SDK
Modify `global.json` at the root of the repository.

```json
{
  "sdk": {
    "version": "10.0.100", // Check latest available version
    "rollForward": "latestFeature"
  }
}
```

## Step 2: Update Project Files
For each `.csproj` file in `src/` and `tests/`:

1.  **Change Target Framework**:
    ```xml
    <TargetFramework>net10.0</TargetFramework>
    ```

2.  **Update Microsoft Packages**:
    *   `Microsoft.Extensions.*` -> `10.0.0`
    *   `Microsoft.NET.Test.Sdk` -> `18.0.0` (or compatible)

3.  **Update AWS Packages**:
    *   Ensure `Amazon.Lambda.Core` and others are compatible with .NET 10 runtime.

## Step 3: Update Dockerfiles

**File**: `docker/app.Dockerfile` (and others)

```dockerfile
# Build Image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# ...

# Runtime Image (if not using Lambda base)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
```

**Lambda Images**:
If deploying as container image to Lambda:
```dockerfile
FROM public.ecr.aws/lambda/dotnet:10
```

## Step 4: Verification

1.  **Clean & Restore**:
    ```powershell
    dotnet clean
    dotnet restore
    ```
2.  **Build**:
    ```powershell
    dotnet build --no-restore
    ```
3.  **Test**:
    ```powershell
    dotnet test
    ```
    *   *Critical*: Start the LocalStack environment first (`docker-compose up -d`) to ensure integration tests pass.

## CI/CD Pipeline
Update `.github/workflows/*.yml` (or Azure DevOps pipelines):
*   `actions/setup-dotnet@v4` with `dotnet-version: 10.0.x`.
