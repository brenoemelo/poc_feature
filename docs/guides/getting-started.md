# Setup Local Environment

This guide explains how to run the Material Formulation System locally using LocalStack and Terraform.

## Prerequisites

- **Docker Desktop** (with Docker Compose)
- **.NET SDK 10** (or latest .NET 8/9 compatible)
- **Python 3.x** (for deployment scripts)
- **Terraform** (installed and in PATH)
- **Git**

## 1) Clone the Repository

```bash
git clone <repository-url>
cd poc_feature
```

## 2) Start Infrastructure (LocalStack + Observability)

We use a single Docker Compose file for the entire stack.

```bash
docker-compose up -d
```

**Verify:**
```bash
docker ps
```
Ensure `localstack`, `otel-collector`, `prometheus`, `tempo`, `loki`, `grafana`, and `unleash` are running.

## 3) Build & Deploy (The "One-Click" Way)

We use a Python script to orchestrate the build and deployment via Terraform.

```bash
# Deploys API Gateway, Shared Infra, and All Microservices
python deployment/localstack/deploy_all_terraform.py
```

**What this does:**
1.  **Builds** .NET projects (`dotnet publish`).
2.  **Zips** artifacts for Lambda.
3.  **Initializes** Terraform backends.
4.  **Applies** Terraform configurations to LocalStack.
5.  **Verifies** resources using `boto3`.

## 4) Verify the Deployment

### Run E2E Tests
Use the PowerShell test script to verify all endpoints are reachable and functioning.

```powershell
./scripts/tests/test_all_apis.ps1
```

### Check Observability
*   **Grafana:** [http://localhost:3000](http://localhost:3000)
*   **Unleash:** [http://localhost:4242](http://localhost:4242) (admin/password)

## 5) Development Workflow

### Making Code Changes
1.  Modify C# code.
2.  Redeploy the specific service:
    ```bash
    python deployment/localstack/deploy_all_terraform.py --service materials
    ```

### Making Infra Changes (Terraform)
1.  Modify `.tf` files.
2.  Redeploy skipping build (faster):
    ```bash
    python deployment/localstack/deploy_all_terraform.py --skip-build
    ```

## 6) Troubleshooting

*   **"Resource Conflict"**: If you get errors about existing resources, try a clean deploy:
    ```bash
    python deployment/localstack/deploy_all_terraform.py --clean
    ```
*   **Terraform Locks**: If Terraform gets stuck, delete the `.terraform` folder and `.tfstate` files in `terraform/services/<service>/`.
