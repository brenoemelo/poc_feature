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

# Criar Fila SQS
awslocal sqs create-queue --queue-name poc-queue

# Inscrever Fila SQS no Tópico SNS
TOPIC_ARN=$(awslocal sns create-topic --name poc-topic --query "TopicArn" --output text)
QUEUE_URL=$(awslocal sqs get-queue-url --queue-name poc-queue --query "QueueUrl" --output text)
QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url $QUEUE_URL --attribute-names QueueArn --query "Attributes.QueueArn" --output text)

awslocal sns subscribe --topic-arn $TOPIC_ARN --protocol sqs --notification-endpoint $QUEUE_ARN

# Criar Tabela DynamoDB
awslocal dynamodb create-table \
    --table-name poc-table \
    --attribute-definitions AttributeName=Id,AttributeType=S \
    --key-schema AttributeName=Id,KeyType=HASH \
    --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5

# Criar Tabela DynamoDB para Preços de Componentes (Costing Engine)
awslocal dynamodb create-table \
    --table-name costing-prices-table \
    --attribute-definitions AttributeName=ComponentName,AttributeType=S \
    --key-schema AttributeName=ComponentName,KeyType=HASH \
    --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5

# Criar Bucket S3 para Materiais (evita erro NoSuchBucket)
awslocal s3 mb s3://materials

echo "Resources initialized!"
