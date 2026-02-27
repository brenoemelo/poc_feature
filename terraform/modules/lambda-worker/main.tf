# 1. Messaging (SNS and SQS)
resource "aws_sns_topic" "this" {
  name = var.topic_name
}

resource "aws_sqs_queue" "this" {
  name = var.queue_name
}

resource "aws_sns_topic_subscription" "this" {
  topic_arn            = aws_sns_topic.this.arn
  protocol             = "sqs"
  endpoint             = aws_sqs_queue.this.arn
  raw_message_delivery = false
  filter_policy        = var.filter_policy
}

# 2. Compute (Lambda)
resource "aws_lambda_function" "worker" {
  function_name    = var.service_name
  role             = var.lambda_role_arn
  runtime          = var.runtime
  handler          = var.handler
  timeout          = var.timeout
  memory_size      = var.memory_size
  
  filename         = var.zip_path
  source_code_hash = filebase64sha256(var.zip_path)

  environment {
    variables = var.environment_variables
  }
}

# 3. Integration (Event Source Mapping)
resource "aws_lambda_event_source_mapping" "sqs_trigger" {
  event_source_arn = aws_sqs_queue.this.arn
  function_name    = aws_lambda_function.worker.arn
  batch_size       = var.batch_size
}
