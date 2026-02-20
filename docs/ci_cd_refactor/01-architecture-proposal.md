# CI/CD Refactoring: Modular Automation Framework

**Role:** Principal DevOps Engineer & SRE
**Date:** 2026-02-20
**Status:** Implemented
**Goal:** A highly modular, reusable, and service-isolated automation framework for LocalStack and AWS.

## 1. Executive Summary

We have transitioned from monolithic scripts to a **Service-Isolated Automation Framework**.
This framework treats infrastructure scripts as production software, enforcing DRY principles, robust error handling, and strict isolation.

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

## 4. Usage

### Deploy All (LocalStack)
```powershell
cd deployment/localstack
.\deploy-all.ps1
# OR with SkipBuild
.\deploy-all.ps1 -SkipBuild
```

### Deploy Single Service
```powershell
cd scripts/services/materials
.\pipeline.ps1
# OR
.\pipeline.ps1 -SkipBuild
```

## 5. Next Steps
*   **Parallel Execution:** Enable parallel execution for independent services (Costing/Populator) in `deploy-all.ps1`.
*   **Container Support:** Extend `docker_helpers.ps1` for container-based services (Observability).
