# Setup Local Environment

This guide explains how to run the Material Formulation System locally using LocalStack and Lambda Function URLs.

## Prerequisites

- Docker Desktop with Docker Compose
- .NET SDK 8.0
- Git

## 1) Clone the Repository

```bash
git clone <repository-url>
cd poc_feature
```

## 2) Start LocalStack

```bash
docker-compose up -d
```

Verify LocalStack:

```bash
docker ps
```

You should see the container `poc_feature-localstack` running on port `4566`.

## 3) Build the Solution

```bash
dotnet restore PoC.sln
dotnet build PoC.sln
```

Expected output: `Build succeeded`.

## 4) Run Architecture Tests

```bash
dotnet test src/PoC.ArchitectureTests
```

All tests should pass before proceeding.

## 5) Deploy Services to LocalStack (Windows/PowerShell)

The project uses **API Gateway** as the single entry point. You can deploy everything at once using the master script.

### Option A: Deploy All Services (Recommended)

Run the master deployment script to deploy API Gateway and all microservices in the correct order:

```powershell
./deployment/localstack/deploy-all.ps1
```

This script will:
1. Deploy **PoC.Materials**
2. Deploy **PoC.Costing**
3. Deploy **PoC.Populator**
4. Deploy **API Gateway**

### Option B: Deploy Individually

If you need to deploy specific services, use the individual scripts in `deployment/localstack/`:

1. **Deploy API Gateway** (Must be deployed to route requests):
   ```powershell
   ./deployment/localstack/gateway.ps1
   ```
   This sets up the unified API at `http://localhost:4566/_aws/execute-api/material-api/prod/`.

2. **Deploy Microservices**:
   ```powershell
   ./deployment/localstack/materials.ps1   # Deploys PoC.Materials
   ./deployment/localstack/costing.ps1     # Deploys PoC.Costing
   ./deployment/localstack/populator.ps1   # Deploys PoC.Populator
   ```

Each script publishes the .NET application, creates the Lambda function (removing any existing one to ensure a clean state), and updates the API Gateway integration.

## 6) Test the API

Use the provided PowerShell script to verify all endpoints automatically:

```powershell
./scripts/tests/test_all_apis.ps1
```
 
 This script:
 - Detects the API Gateway ID
 - Runs functional tests for Materials (CRUD), Costing (Price/Calc), and Population
 - Validates the responses against expected results
 
 ## 7) Run E2E Tests
 
 The E2E tests are configured to use the API Gateway.
 
 ```bash
 dotnet test tests/PoC.E2E/PoC.E2E.csproj
 ```
 
 ## 8) Insomnia Collection

- Import `docs/api/insomnia_antigravity_v1.json` into Insomnia
- Switch to the "LocalStack Environment"
- The `base_url` is pre-configured to `http://localhost:4566/_aws/execute-api/material-api/prod/`.
- If you changed the ID manually, update the variable accordingly.
 
 ## Troubleshooting

- **500 Internal Server Error**:
  - Check Lambda logs using the commands in [Deployment Health Check](deployment-health.md).
  - Ensure the API Gateway script (`deployment/localstack/gateway.ps1`) was run successfully.
- **403 Forbidden**:
  - Ensure you are using the **API Gateway URL** and NOT the Lambda Function URL.
  - Run `./deployment/localstack/gateway.ps1` again to ensure permissions are set correctly.
- **DynamoDB concurrency errors**:
  - The system uses Optimistic Locking. If you see concurrency errors, retry the operation with the latest version of the entity.
 
 ## Clean Up
 
 ```bash
 docker-compose down -v
 ```
 
 This stops containers and removes volumes.
