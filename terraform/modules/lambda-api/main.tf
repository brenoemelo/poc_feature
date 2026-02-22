# Compute (Lambda)
resource "aws_lambda_function" "api" {
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

# CloudWatch Log Group for Lambda
resource "aws_cloudwatch_log_group" "api_logs" {
  name              = "/aws/lambda/${aws_lambda_function.api.function_name}"
  retention_in_days = 7
}
