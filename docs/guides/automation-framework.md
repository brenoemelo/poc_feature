# Automation Framework Guide

**Role:** DevOps & SRE Documentation
**Status:** Active
**Scope:** LocalStack & AWS Deployment (Terraform)

## 1. Overview

The project uses a **Python + Terraform** based automation framework to manage the lifecycle of microservices. This framework ensures robust, idempotent, and observable deployments to both LocalStack (Dev) and AWS (Prod - future).

> **Note:** The legacy PowerShell scripts (`deploy-all.ps1`) are **DEPRECATED** and should not be used.

### Key Features
*   **Terraform-First:** All infrastructure (Lambda, DynamoDB, API Gateway, IAM) is defined as code (`.tf`).
*   **Python Orchestration:** A single master script (`deploy_all_terraform.py`) handles the build -> plan -> apply workflow.
*   **LocalStack Native:** Optimized for local development with "Delete-then-Create" strategies for clean state.
*   **Service Isolation:** Each service has its own Terraform state and module, preventing "blast radius" issues.

## 2. Folder Structure

```text
deployment/
└── localstack/
    ├── deploy_all_terraform.py   # MASTER ORCHESTRATOR
    └── ...
terraform/
├── modules/                      # Reusable Terraform Modules
│   ├── lambda-function/
│   ├── dynamodb-table/
│   └── ...
├── services/                     # Service-Specific Configurations
│   ├── materials/
│   │   ├── main.tf
│   │   └── variables.tf
│   ├── costing/
│   └── populator/
└── shared/                       # Shared Infra (Networking, SSM)
    ├── main.tf
    └── variables.tf
```

## 3. The Master Orchestrator (`deploy_all_terraform.py`)

Located in `deployment/localstack/deploy_all_terraform.py`.

### 3.1 Capabilities
*   **Builds .NET Projects:** Runs `dotnet publish` with optimization flags.
*   **Manages LocalStack:** Checks connectivity and cleans up resources if requested.
*   **Runs Terraform:** Executes `terraform init` and `terraform apply` for each service.
*   **Verifies Deployment:** Uses `boto3` to confirm resources (Lambdas, Tables) are actually active.

### 3.2 Usage

**Deploy Everything (Standard Dev Loop):**
```bash
python deployment/localstack/deploy_all_terraform.py
```

**Deploy Specific Service:**
```bash
python deployment/localstack/deploy_all_terraform.py --service materials
```

**Clean Slate (Nuke & Deploy):**
```bash
python deployment/localstack/deploy_all_terraform.py --clean
```

**Skip Build (Infra Changes Only):**
```bash
python deployment/localstack/deploy_all_terraform.py --skip-build
```

## 4. Terraform Strategy

### 4.1 Modules
We use local modules in `terraform/modules/` to standardize resource creation.
*   **`lambda-function`**: Configures runtime, memory, timeout, environment variables, and IAM roles.
*   **`dynamodb-table`**: Configures Single Table Design, GSIs, and Billing Mode.

### 4.2 State Management
*   **Local:** State is stored in local `.tfstate` files within each service folder in `terraform/services/`.
*   **Isolation:** `materials` state is separate from `costing` state.

## 5. CI/CD Integration (Future)

This framework is designed to be ported to GitHub Actions/GitLab CI.
1.  **Build:** `dotnet publish` (Artifact generation).
2.  **Infrastructure:** `terraform plan` -> `terraform apply`.
3.  **Test:** `dotnet test` (Unit) -> `scripts/tests/test_all_apis.ps1` (E2E).
