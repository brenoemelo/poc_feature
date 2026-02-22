resource "aws_api_gateway_resource" "this" {
  rest_api_id = var.api_id
  parent_id   = var.parent_id
  path_part   = var.path_part
}

resource "aws_api_gateway_method" "proxy" {
  count         = length(var.http_methods)
  rest_api_id   = var.api_id
  resource_id   = aws_api_gateway_resource.this.id
  http_method   = var.http_methods[count.index]
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "lambda" {
  count                   = length(var.http_methods)
  rest_api_id             = var.api_id
  resource_id             = aws_api_gateway_method.proxy[count.index].resource_id
  http_method             = aws_api_gateway_method.proxy[count.index].http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = var.lambda_invoke_arn
}

resource "aws_lambda_permission" "apigw" {
  statement_id  = "AllowAPIGatewayInvoke-${var.path_part}"
  action        = "lambda:InvokeFunction"
  function_name = var.lambda_function_name
  principal     = "apigateway.amazonaws.com"

  # The /*/* portion grants access from any method on any resource
  # within the API Gateway REST API.
  source_arn = "arn:aws:execute-api:${data.aws_region.current.name}:${data.aws_caller_identity.current.account_id}:${var.api_id}/*/*/*"
}

data "aws_region" "current" {}
data "aws_caller_identity" "current" {}
