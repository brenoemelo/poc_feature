data "aws_iam_role" "lambda_exec" {
  name = "lambda-role"
}

data "aws_api_gateway_rest_api" "shared" {
  name = "Material-Formulation-API"
}

locals {
  common_env_vars = {
    OTEL_EXPORTER_OTLP_ENDPOINT = "http://otel-collector:4317"
    OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
    AWS__Region                 = "us-east-1"
    AWS__ServiceUrl             = "http://localstack:4566"
    FeatureFlags__UnleashApiUrl = "http://host.docker.internal:4242/api/" # Use host.docker.internal for stable access
    FeatureFlags__FetchTogglesIntervalSeconds = "1"
  }
  zip_path = "${path.module}/../../../dist/PoC-DataHelper/PoC-DataHelper.zip"
}

# API Lambda
module "datahelper_api" {
  source = "../../modules/lambda-api"

  service_name    = "PoC-DataHelper"
  runtime         = "dotnet8"
  handler         = "PoC.DataHelper"
  timeout         = 30
  memory_size     = 512
  zip_path        = local.zip_path
  lambda_role_arn = data.aws_iam_role.lambda_exec.arn

  environment_variables = merge(local.common_env_vars, {
    "OTEL_SERVICE_NAME"      = "PoC-DataHelper"
    "Observability__Enabled" = "true"
    "ASPNETCORE_ENVIRONMENT" = "Development"
  })
}

# API Gateway Permission
resource "aws_lambda_permission" "apigw" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = module.datahelper_api.lambda_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${data.aws_api_gateway_rest_api.shared.execution_arn}/*/*/*"
}
