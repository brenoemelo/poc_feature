# Terraform Deployment Pipeline

**Status:** Active (Primary Deployment Method)
**Script:** `deployment/localstack/deploy_all_terraform.py`

## 1. Overview

The `deploy_all_terraform.py` script is the master orchestrator for deploying the PoC Microservices to LocalStack using Terraform. It replaces the legacy PowerShell scripts and provides a robust, idempotent, and observable deployment process.

## 2. Prerequisites

*   **Python 3.x**
*   **Terraform** (The script will attempt to download it if missing, but having it in PATH is recommended)
*   **Docker** (Must be running for LocalStack)
*   **LocalStack** (`docker-compose up -d`)

## 3. Usage

Run the script from the project root or the `deployment/localstack` directory.

```bash
# Basic deployment (Builds & Deploys all services)
python deployment/localstack/deploy_all_terraform.py
```

### 3.1 Arguments

| Argument | Description |
| :--- | :--- |
| `--clean` | **[NEW]** Aggressively cleans up ALL LocalStack resources (Lambdas, API Gateway, DynamoDB) before starting the deployment. Use this if you want a completely fresh state. |
| `--skip-build` | Skips the `dotnet clean` and `dotnet publish` steps. Useful for rapid infrastructure iteration when code hasn't changed. |
| `--service <name>` | Deploys a specific service only. Options: `materials`, `costing`, `populator`, `datahelper`, `gateway`. |

### 3.2 Examples

**Full Clean Deployment (Fresh Start):**
```bash
python deployment/localstack/deploy_all_terraform.py --clean
```

**Deploy Only Infrastructure Changes (Fast):**
```bash
python deployment/localstack/deploy_all_terraform.py --skip-build
```

**Deploy Specific Service (e.g., Materials):**
```bash
python deployment/localstack/deploy_all_terraform.py --service materials
```

## 4. Pipeline Stages

The pipeline executes in the following order:

1.  **Validation**: Checks if Docker is running, Terraform is installed, and LocalStack is reachable.
2.  **Cleanup (Conditional)**: If `--clean` is passed, it removes all existing resources in LocalStack to ensure no conflicts.
3.  **Build**:
    *   Cleans previous artifacts.
    *   Runs `dotnet publish -c Release -r linux-x64` for each service.
    *   Zips the artifacts for Lambda deployment.
4.  **Shared Infrastructure**: Deploys `terraform/shared` (Networking, Common Resources).
5.  **Service Deployment**: Runs `terraform apply` for each selected service in `terraform/services/`.
    *   *Note:* It automatically handles DynamoDB table imports if they exist in LocalStack but not in Terraform state.
6.  **Verification**: Uses `boto3` to query LocalStack and verify that API Gateways, Lambdas, and DynamoDB tables were actually created.

## 5. Troubleshooting

*   **Terraform Lock**: If the script fails, Terraform lock files (`.terraform.lock.hcl`) or state locks might remain. The `--clean` flag does NOT remove local Terraform state files (`.tfstate`), it only cleans the *remote* (LocalStack) resources. To reset Terraform state locally, delete the `.terraform` folders and `.tfstate` files.
*   **Docker Connection**: Ensure `docker ps` shows the LocalStack container running.
