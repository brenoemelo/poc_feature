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

## 5) Deploy PoC.Materials to LocalStack (Windows/PowerShell)

Use the deployment script to publish and deploy the Lambda with a public Function URL:

- Execute [deploy-localstack.ps1](file:///d:/Projetos/poc_feature/deploy-localstack.ps1)
- The script:
  - Publishes [PoC.Materials.csproj](file:///d:/Projetos/poc_feature/src/PoC.Materials/PoC.Materials.csproj) for Linux (linux-x64)
  - Creates the Lambda function `PoC-Materials`
  - Creates the Function URL with `AuthType=NONE`
- Copy the generated Function URL (e.g., `http://xxxxxxxx.lambda-url.us-east-1.localhost.localstack.cloud:4566/`)

## 6) Test the API

- List materials: `GET {FunctionUrl}/materials`
- Get by ID: `GET {FunctionUrl}/materials/{id}`
- Create: `POST {FunctionUrl}/materials` with JSON body
- Delete: `DELETE {FunctionUrl}/materials/{id}`

## 7) Configure E2E Tests

- Update the BaseUrl in [appsettings.test.json](file:///d:/Projetos/poc_feature/tests/PoC.E2E/appsettings.test.json) with the Function URL
- Run tests:

```bash
dotnet test tests/PoC.E2E/PoC.E2E.csproj
```

## 8) Insomnia Collection

- Import `docs/insomnia_collection.json` into Insomnia
- Set the environment variable `base_url` to the Function URL
- Exercise endpoints for Materials, Population, and Costing

## Troubleshooting

- 403 on Function URL
  - Redeploy with [deploy-localstack.ps1](file:///d:/Projetos/poc_feature/deploy-localstack.ps1) to recreate the Function and its URL
- 500 during startup
  - Ensure `TargetFramework=net8.0` and the publish target is `linux-x64`
- DynamoDB missing tables
  - Check LocalStack init: [init-aws.sh](file:///d:/Projetos/poc_feature/init-aws.sh) should create `materials-table`

## Clean Up

```bash
docker-compose down -v
```

This stops containers and removes volumes.
