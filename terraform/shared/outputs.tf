output "api_gateway_id" {
  value       = aws_api_gateway_rest_api.main.id
  description = "The ID of the Shared API Gateway"
}

output "api_gateway_root_resource_id" {
  value       = aws_api_gateway_rest_api.main.root_resource_id
  description = "The Root Resource ID of the Shared API Gateway"
}

output "lambda_role_arn" {
  value       = aws_iam_role.lambda_exec.arn
  description = "The ARN of the global lambda execution role"
}
