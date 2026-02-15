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

You should see the container `poc_feature-localstack-1` running on port `4566`.

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
 
 The project uses **API Gateway** as the single entry point. You must deploy the Gateway first, then the services.
 
 1. **Deploy API Gateway**:
    ```powershell
    ./deploy-gateway.ps1
    ```
    This sets up the unified API at `http://localhost:4566/restapis/<api-id>/prod/_user_request_/`.
 
 2. **Deploy Microservices**:
    ```powershell
    ./deploy-localstack.ps1           # Deploys PoC.Materials
    ./deploy-localstack-costing.ps1   # Deploys PoC.Costing
    ./deploy-localstack-populator.ps1 # Deploys PoC.Populator
    ```
 
 Each script publishes the .NET application, creates the Lambda function, and updates the API Gateway integration.
 
 ## 6) Test the API
 
 Use the provided PowerShell script to verify all endpoints automatically:
 
 ```powershell
 ./test_all_apis.ps1
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
- Update the `base_url` variable by replacing `API_ID_HERE` with your actual API Gateway ID.
  - Example: `http://localhost:4566/restapis/bgl2ladyeo/prod/_user_request_`
- You can find the correct URL in the output of `./test_all_apis.ps1` or `./deploy-gateway.ps1`.
 
 ## Troubleshooting
 
 - **500 Internal Server Error**:
   - Check Lambda logs using the commands in [Deployment Health Check](deployment-health.md).
   - Ensure `deploy-gateway.ps1` was run *before* the service deployment scripts if you are setting up for the first time.
 - **403 Forbidden**:
   - Ensure you are using the **API Gateway URL** and NOT the Lambda Function URL.
   - Run `./deploy-gateway.ps1` again to ensure permissions are set correctly.
 - **DynamoDB concurrency errors**:
   - The system uses Optimistic Locking. If you see concurrency errors, retry the operation with the latest version of the entity.
 
 ## Clean Up
 
 ```bash
 docker-compose down -v
 ```
 
 This stops containers and removes volumes.
