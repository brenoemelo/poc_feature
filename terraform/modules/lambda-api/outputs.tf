output "lambda_arn" {
  value = aws_lambda_function.api.arn
}

output "lambda_name" {
  value = aws_lambda_function.api.function_name
}

output "invoke_arn" {
  value = aws_lambda_function.api.invoke_arn
}
