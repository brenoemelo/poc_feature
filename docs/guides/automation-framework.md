# Automation Framework Guide

**Role:** DevOps & SRE Documentation
**Status:** Active
**Scope:** LocalStack & AWS Deployment

## 1. Overview

The project uses a **Service-Isolated Automation Framework** to manage the lifecycle of microservices. This framework treats infrastructure scripts as production software, enforcing DRY principles, robust error handling, and strict isolation.

### Key Features
*   **PowerShell Core (`pwsh`):** Cross-platform compatibility (Windows/Linux/Mac).
*   **Service Isolation:** Each service has its own `pipeline.ps1` and lifecycle scripts.
*   **Master Orchestration:** `deployment/localstack/deploy-all.ps1` orchestrates services in isolated processes.
*   **Fail-Fast Logging:** Centralized logging with `Start-Transcript` and 7-day auto-rotation.
*   **Idempotency:** `Remove-AwsResource` helper ensures clean deployments by handling "Resource Not Found" errors gracefully.

## 2. Folder Structure

The structure enforces separation of concerns.

```text
scripts/
├── config/
│   └── global.env.ps1          # Global constants (AWS Account ID, Region, LocalStack URL)
├── utils/
│   ├── logger.ps1              # Centralized logging (Transcript based) & Rotation
│   ├── common.ps1              # Shared helper functions (Fail-fast)
│   ├── aws_helpers.ps1         # AWS CLI wrappers (Auth, Idempotent Cleanup)
│   └── docker_helpers.ps1      # Docker wrappers
├── logs/                       # Auto-generated logs (GitIgnored)
│   └── .gitkeep
└── services/
    ├── materials/              # Service: PoC.Materials
    │   ├── config.local.ps1    # Service-specific variables
    │   ├── 01-validate.ps1     # Pre-flight checks
    │   ├── 02-cleanup.ps1      # Idempotency (Teardown)
    │   ├── 03-build.ps1        # Compilation & Artifacts (Optional)
    │   ├── 04-deploy.ps1       # Infrastructure & Code Deployment
    │   ├── 05-test.ps1         # Smoke/E2E Tests
    │   └── pipeline.ps1        # Service Orchestrator
    ├── costing/                # Service: PoC.Costing
    │   └── ...
    ├── populator/              # Service: PoC.Populator
    │   └── ...
    └── gateway/                # Service: PoC-Gateway
        └── ...
```

## 3. Core Components

### 3.1 Master Orchestrator (`deploy-all.ps1`)
Located in `deployment/localstack/deploy-all.ps1`.
*   **Function:** Iterates through defined services and executes their `pipeline.ps1`.
*   **Isolation:** Runs each pipeline in a separate PowerShell process (`Start-Process` / `powershell -File`) to prevent variable pollution.
*   **Parallelism:** Currently sequential to ensure dependency order (Materials -> Costing -> Gateway).

### 3.2 Service Pipeline (`pipeline.ps1`)
Each service has a `pipeline.ps1` that acts as the entry point.
*   **Phases:** Validate -> Cleanup -> Build (Optional) -> Deploy -> Test.
*   **Logging:** Initializes a unique log file for the run.
*   **Error Handling:** Traps errors and logs them before exiting.

### 3.3 Idempotency (`Remove-AwsResource`)
Located in `scripts/utils/aws_helpers.ps1`.
Prevents "ResourceNotFound" errors from breaking the teardown phase.

```powershell
function Remove-AwsResource {
    param([string]$Description, [scriptblock]$Action)
    try {
        & $Action 2>&1 | Out-Null
        $LASTEXITCODE = 0
    } catch {
        Write-Log "Cleanup Warning for ${Description}: $_" -Level WARN
    }
}
```

### 3.4 Logging Strategy
Located in `scripts/utils/logger.ps1`.
*   **Transcripts:** Uses `Start-Transcript` to capture ALL console output (stdout/stderr).
*   **Rotation:** Automatically deletes logs older than 7 days.
*   **Location:** `scripts/logs/`.

## 4. Usage Instructions

### Deploy All Services (LocalStack)
This is the standard command to deploy the entire environment.

```powershell
cd deployment/localstack
.\deploy-all.ps1
```

**Options:**
- `-SkipBuild`: Skips the `dotnet publish` step. Use this if you have already built the artifacts and just want to redeploy infrastructure/code.
  ```powershell
  .\deploy-all.ps1 -SkipBuild
  ```

### Deploy a Single Service
Useful for iterating on a specific service without redeploying everything.

```powershell
cd scripts/services/materials
.\pipeline.ps1
```

**Options:**
- `-SkipBuild`: Same as above.

### Run E2E Tests
Runs the comprehensive API test suite against the deployed environment.

```powershell
.\scripts\tests\test_all_apis.ps1
```

## 5. Adding a New Service

To add a new service (e.g., `PoC.NewService`):

1.  Create `scripts/services/newservice/`.
2.  Copy the structure from `materials/` or another existing service.
3.  Update `config.local.ps1` with the new service name, ports, and resource names.
4.  Customize `04-deploy.ps1` for specific AWS resources (DynamoDB tables, SNS topics, etc.).
5.  Add the new service to the `$Services` list in `deployment/localstack/deploy-all.ps1`.
