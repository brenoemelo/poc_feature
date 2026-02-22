output "lambda_arn" {
  value = aws_lambda_function.worker.arn
}

output "lambda_name" {
  value = aws_lambda_function.worker.function_name
}

output "sns_topic_arn" {
  value = aws_sns_topic.this.arn
}

output "sqs_queue_arn" {
  value = aws_sqs_queue.this.arn
}

output "sqs_queue_url" {
  value = aws_sqs_queue.this.url
}
