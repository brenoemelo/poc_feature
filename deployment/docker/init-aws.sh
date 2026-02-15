#!/bin/bash
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_DEFAULT_REGION=us-east-1

echo "Initializing LocalStack resources..."

# Configurar AWS CLI alias
awslocal() {
    aws --endpoint-url=http://localhost:4566 "$@"
}

# Criar Tópico SNS
awslocal sns create-topic --name poc-topic

# Criar Filas SQS
awslocal sqs create-queue --queue-name poc-queue
awslocal sqs create-queue --queue-name populator-queue
awslocal sqs create-queue --queue-name material-ingestion-queue

# Inscrever Fila SQS no Tópico SNS
TOPIC_ARN=$(awslocal sns create-topic --name poc-topic --query "TopicArn" --output text)
QUEUE_URL=$(awslocal sqs get-queue-url --queue-name poc-queue --query "QueueUrl" --output text)
QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url $QUEUE_URL --attribute-names QueueArn --query "Attributes.QueueArn" --output text)

awslocal sns subscribe --topic-arn $TOPIC_ARN --protocol sqs --notification-endpoint $QUEUE_ARN

# Criar Tabela DynamoDB para Materiais
awslocal dynamodb create-table \
    --table-name materials-table \
    --attribute-definitions AttributeName=material_id,AttributeType=S \
    --key-schema AttributeName=material_id,KeyType=HASH \
    --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5

# Criar Tabela DynamoDB para Preços de Componentes (Costing Engine)
awslocal dynamodb create-table \
    --table-name costing-prices-table \
    --attribute-definitions AttributeName=ComponentName,AttributeType=S \
    --key-schema AttributeName=ComponentName,KeyType=HASH \
    --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5

# Criar Bucket S3 para Materiais (evita erro NoSuchBucket)
awslocal s3 mb s3://poc-materials-data

echo "Resources initialized!"

# Deploy Lambda functions automatically if artifacts exist
echo "Checking for deployment artifacts in /opt/deploy ..."

create_lambda() {
  local name=$1
  local handler=$2
  local zip=$3

  if [ -f "$zip" ]; then
    echo "Deploying Lambda $name from $zip ..."
    awslocal lambda delete-function --function-name "$name" >/dev/null 2>&1 || true
    awslocal lambda create-function \
      --function-name "$name" \
      --runtime dotnet8 \
      --handler "$handler" \
      --role arn:aws:iam::000000000000:role/lambda-role \
      --zip-file "fileb://$zip" \
      --timeout 30 \
      --memory-size 512 \
      --environment "Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}"

    # Create Function URL
    awslocal lambda create-function-url-config \
      --function-name "$name" \
      --auth-type NONE >/dev/null 2>&1 || true
  else
    echo "Artifact not found: $zip (skip)"
  fi
}

# Materials
create_lambda "PoC-Materials" "PoC.Materials" "/opt/deploy/PoC.Materials.zip"
# Populator (API)
create_lambda "PoC-Populator" "PoC.Populator" "/opt/deploy/PoC.Populator.zip"
# Costing (API)
create_lambda "PoC-Costing" "PoC.Costing" "/opt/deploy/PoC.Costing.zip"

# Populator Worker (SQS)
if [ -f "/opt/deploy/PoC.Populator.zip" ]; then
  echo "Deploying Populator Worker..."
  awslocal lambda delete-function --function-name "PoC-Populator-Worker" >/dev/null 2>&1 || true
  awslocal lambda create-function \
    --function-name "PoC-Populator-Worker" \
    --runtime dotnet8 \
    --handler "PoC.Populator::PoC.Populator.PopulatorWorkerFunction::FunctionHandler" \
    --role arn:aws:iam::000000000000:role/lambda-role \
    --zip-file fileb:///opt/deploy/PoC.Populator.zip \
    --timeout 30 \
    --memory-size 512 \
    --environment "Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test,SNS_TOPIC_ARN=arn:aws:sns:us-east-1:000000000000:material-events}"

  # Event source mapping to SQS
  awslocal lambda create-event-source-mapping \
    --function-name "PoC-Populator-Worker" \
    --event-source-arn arn:aws:sqs:us-east-1:000000000000:populator-queue >/dev/null 2>&1 || true
fi

# Materials Ingestion Worker (SQS)
if [ -f "/opt/deploy/PoC.Materials.zip" ]; then
  echo "Deploying Materials Ingestion Worker..."
  awslocal lambda delete-function --function-name "PoC-Materials-Ingestion" >/dev/null 2>&1 || true
  awslocal lambda create-function \
    --function-name "PoC-Materials-Ingestion" \
    --runtime dotnet8 \
    --handler "PoC.Materials::PoC.Materials.Functions.MaterialIngestionFunction::FunctionHandler" \
    --role arn:aws:iam::000000000000:role/lambda-role \
    --zip-file fileb:///opt/deploy/PoC.Materials.zip \
    --timeout 30 \
    --memory-size 512 \
    --environment "Variables={AWS_ENDPOINT_URL=http://localstack:4566,AWS_REGION=us-east-1,AWS_ACCESS_KEY_ID=test,AWS_SECRET_ACCESS_KEY=test}"

  # Event source mapping to SQS
  awslocal lambda create-event-source-mapping \
    --function-name "PoC-Materials-Ingestion" \
    --event-source-arn arn:aws:sqs:us-east-1:000000000000:material-ingestion-queue >/dev/null 2>&1 || true
fi

# SNS material-events -> SQS material-ingestion-queue
MATERIAL_TOPIC_ARN=$(awslocal sns create-topic --name material-events --query "TopicArn" --output text)
INGESTION_QUEUE_URL=$(awslocal sqs get-queue-url --queue-name material-ingestion-queue --query "QueueUrl" --output text)
INGESTION_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url $INGESTION_QUEUE_URL --attribute-names QueueArn --query "Attributes.QueueArn" --output text)
awslocal sqs set-queue-attributes --queue-url $INGESTION_QUEUE_URL --attributes "{\"Policy\":\"{\\\"Version\\\":\\\"2012-10-17\\\",\\\"Statement\\\":[{\\\"Effect\\\":\\\"Allow\\\",\\\"Principal\\\":\\\"*\\\",\\\"Action\\\":\\\"sqs:SendMessage\\\",\\\"Resource\\\":\\\"$INGESTION_QUEUE_ARN\\\",\\\"Condition\\\":{\\\"ArnEquals\\\":{\\\"aws:SourceArn\\\":\\\"$MATERIAL_TOPIC_ARN\\\"}}}]}\"}"
awslocal sns subscribe --topic-arn $MATERIAL_TOPIC_ARN --protocol sqs --notification-endpoint $INGESTION_QUEUE_ARN

echo "Deployment hooks finished."
