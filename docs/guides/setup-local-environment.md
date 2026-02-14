# Setup Local Environment

This guide will help you set up the Material Formulation System on your local machine using LocalStack.

## Prerequisites

- **Docker** (version 20.10+)
- **Docker Compose** (version 2.0+)
- **.NET SDK 8.0**
- **Git**
- **sg** command (for Docker group access on Linux)

## Step 1: Clone the Repository

```bash
git clone <repository-url>
cd poc_feature
```

## Step 2: Start LocalStack

```bash
docker-compose up -d
```

This will start LocalStack with the following services:

- Lambda
- DynamoDB
- SNS
- SQS
- S3

**Verify LocalStack is running:**

```bash
docker ps
```

You should see `poc_feature-localstack-1` running on port `4566`.

## Step 3: Build the Solution

```bash
dotnet restore PoC.sln
dotnet build PoC.sln
```

**Expected output:** `Build succeeded` with 0 errors.

## Step 4: Run Architecture Tests

```bash
dotnet test src/PoC.ArchitectureTests
```

**Expected output:** All tests should pass.

## Step 5: Build Docker Images

### PoC.Lambda

```bash
sg docker -c "docker build --network=host -t poc-lambda:latest -f src/PoC.Lambda/Dockerfile ."
```

### PoC.Populator

```bash
sg docker -c "docker build --network=host -t poc-populator:latest -f src/PoC.Populator/Dockerfile ."
```

### PoC.Costing

```bash
sg docker -c "docker build --network=host -t poc-costing:latest -f src/PoC.Costing/Dockerfile ."
```

## Step 6: Deploy Lambda Functions

### Extract and Package Lambda Code

**For PoC.Lambda:**

```bash
rm -rf lambda-publish function.zip
sg docker -c "docker create --name extract-lambda poc-lambda:latest"
sg docker -c "docker cp extract-lambda:/var/task ./lambda-publish"
sg docker -c "docker rm extract-lambda"
cd lambda-publish && zip -r ../function.zip . && cd ..
sg docker -c "docker cp function.zip poc_feature-localstack-1:/tmp/function.zip"
```

**For PoC.Populator:**

```bash
rm -rf populator-publish populator.zip
sg docker -c "docker create --name extract-populator poc-populator:latest"
sg docker -c "docker cp extract-populator:/var/task ./populator-publish"
sg docker -c "docker rm extract-populator"
cd populator-publish && zip -r ../populator.zip . && cd ..
sg docker -c "docker cp populator.zip poc_feature-localstack-1:/tmp/populator.zip"
```

**For PoC.Costing:**

```bash
rm -rf costing-publish costing.zip
sg docker -c "docker create --name extract-costing poc-costing:latest"
sg docker -c "docker cp extract-costing:/var/task ./costing-publish"
sg docker -c "docker rm extract-costing"
cd costing-publish && zip -r ../costing.zip . && cd ..
sg docker -c "docker cp costing.zip poc_feature-localstack-1:/tmp/costing.zip"
```

### Create Lambda Functions

```bash
# PoC.Lambda functions
sg docker -c "docker exec poc_feature-localstack-1 awslocal lambda create-function \
    --function-name poc-lambda \
    --runtime dotnet8 \
    --role arn:aws:iam::000000000000:role/lambda-role \
    --handler PoC.Lambda::PoC.Lambda.Function::FunctionHandler \
    --zip-file fileb:///tmp/function.zip"

sg docker -c "docker exec poc_feature-localstack-1 awslocal lambda create-function \
    --function-name poc-query \
    --runtime dotnet8 \
    --role arn:aws:iam::000000000000:role/lambda-role \
    --handler PoC.Lambda::PoC.Lambda.QueryFunction::FunctionHandler \
    --zip-file fileb:///tmp/function.zip"

sg docker -c "docker exec poc_feature-localstack-1 awslocal lambda create-function \
    --function-name poc-ingestion-worker \
    --runtime dotnet8 \
    --role arn:aws:iam::000000000000:role/lambda-role \
    --handler PoC.Lambda::PoC.Lambda.MaterialIngestionFunction::FunctionHandler \
    --zip-file fileb:///tmp/function.zip"

# PoC.Populator functions
sg docker -c "docker exec poc_feature-localstack-1 awslocal lambda create-function \
    --function-name poc-populator-api \
    --runtime dotnet8 \
    --role arn:aws:iam::000000000000:role/lambda-role \
    --handler PoC.Populator::PoC.Populator.PopulatorApiFunction::FunctionHandler \
    --zip-file fileb:///tmp/populator.zip"

sg docker -c "docker exec poc_feature-localstack-1 awslocal lambda create-function \
    --function-name poc-populator-worker \
    --runtime dotnet8 \
    --role arn:aws:iam::000000000000:role/lambda-role \
    --handler PoC.Populator::PoC.Populator.PopulatorWorkerFunction::FunctionHandler \
    --zip-file fileb:///tmp/populator.zip"

# PoC.Costing functions
sg docker -c "docker exec poc_feature-localstack-1 awslocal lambda create-function \
    --function-name poc-costing-price-mgmt \
    --runtime dotnet8 \
    --role arn:aws:iam::000000000000:role/lambda-role \
    --handler PoC.Costing::PoC.Costing.PriceManagementFunction::FunctionHandler \
    --zip-file fileb:///tmp/costing.zip"

sg docker -c "docker exec poc_feature-localstack-1 awslocal lambda create-function \
    --function-name poc-costing-engine \
    --runtime dotnet8 \
    --role arn:aws:iam::000000000000:role/lambda-role \
    --handler PoC.Costing::PoC.Costing.CostCalculationFunction::FunctionHandler \
    --zip-file fileb:///tmp/costing.zip"
```

### Configure Event Source Mappings

```bash
# Populator worker consumes from populator-queue
sg docker -c "docker exec poc_feature-localstack-1 awslocal lambda create-event-source-mapping \
    --function-name poc-populator-worker \
    --event-source-arn arn:aws:sqs:us-east-1:000000000000:populator-queue"

# Ingestion worker consumes from material-ingestion-queue
sg docker -c "docker exec poc_feature-localstack-1 awslocal lambda create-event-source-mapping \
    --function-name poc-ingestion-worker \
    --event-source-arn arn:aws:sqs:us-east-1:000000000000:material-ingestion-queue"
```

## Step 7: Test the APIs

### Using Insomnia

1. Import `docs/insomnia_collection.json` into Insomnia
2. Update the `base_url` environment variable if needed
3. Test the endpoints:
   - **List Materials** (GET)
   - **Create Material** (POST)
   - **Populate Data** (POST)
   - **Upsert Component Price** (PUT)
   - **Calculate Material Cost** (POST)

### Using cURL

**Create a material:**

```bash
curl -X POST http://localhost:4566/restapis/lambda/local/outputs/v1/materials \
  -H "Content-Type: application/json" \
  -d '{
    "material_id": "test-001",
    "name": "Test Material",
    "formulation": [
      {"component": "Polycarbonate", "percentage": 100.0, "type": "Base"}
    ]
  }'
```

**Populate 1000 materials:**

```bash
curl -X POST http://localhost:4566/restapis/lambda/local/outputs/v1/populate \
  -H "Content-Type: application/json" \
  -d '{"target": "materials", "count": 1000}'
```

## Troubleshooting

### LocalStack not starting

```bash
docker-compose logs localstack
```

### Lambda function errors

```bash
sg docker -c "docker exec poc_feature-localstack-1 awslocal logs tail /aws/lambda/poc-lambda"
```

### DynamoDB table not found

Verify tables exist:

```bash
sg docker -c "docker exec poc_feature-localstack-1 awslocal dynamodb list-tables"
```

## Clean Up

```bash
docker-compose down -v
```

This removes all containers and volumes.
