#!/bin/bash

# Execute inside LocalStack container
docker exec poc_feature-localstack-1 awslocal sns create-topic --name material-events &
docker exec poc_feature-localstack-1 awslocal sqs create-queue --queue-name material-ingestion-queue &

wait

# Get ARNs
TOPIC_ARN=$(docker exec poc_feature-localstack-1 awslocal sns list-topics --query "Topics[?contains(TopicArn, 'material-events')].TopicArn" --output text)
QUEUE_URL=$(docker exec poc_feature-localstack-1 awslocal sqs get-queue-url --queue-name material-ingestion-queue --query "QueueUrl" --output text)
QUEUE_ARN=$(docker exec poc_feature-localstack-1 awslocal sqs get-queue-attributes --queue-url $QUEUE_URL --attribute-names QueueArn --query "Attributes.QueueArn" --output text)

# Subscribe
docker exec poc_feature-localstack-1 awslocal sns subscribe \
    --topic-arn "$TOPIC_ARN" \
    --protocol sqs \
    --notification-endpoint "$QUEUE_ARN"

echo "Infrastructure setup complete via Docker."
