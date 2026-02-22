data "aws_iam_role" "lambda_exec" {
  name = "lambda-role"
}

data "aws_api_gateway_rest_api" "shared" {
  name = "Material-Formulation-API"
}

data "aws_caller_identity" "current" {}

locals {
  materials_api_url = "http://localstack:4566/restapis/${data.aws_api_gateway_rest_api.shared.id}/prod/_user_request_/"
  output_topic_arn  = "arn:aws:sns:us-east-1:${data.aws_caller_identity.current.account_id}:material-events"
  
  common_env_vars = {
    OTEL_EXPORTER_OTLP_ENDPOINT = "http://otel-collector:4318"
    OTEL_EXPORTER_OTLP_PROTOCOL = "http/protobuf"
    AWS__Region                 = "us-east-1"
    AWS__LocalStackUrl          = "http://localstack:4566"
    FeatureFlags__UnleashApiUrl = "http://unleash:4242/api/"
  }
  zip_path = "${path.module}/../../../dist/PoC-Populator/PoC-Populator.zip"
}

# API Lambda
module "populator_api" {
  source = "../../modules/lambda-api"

  service_name    = "PoC-Populator"
  runtime         = "dotnet8"
  handler         = "PoC.Populator"
  timeout         = 30
  memory_size     = 1024
  zip_path        = local.zip_path
  lambda_role_arn = data.aws_iam_role.lambda_exec.arn

  environment_variables = merge(local.common_env_vars, {
    "OTEL_SERVICE_NAME" = "PoC-Populator"
  })
}

# API Gateway Permission
resource "aws_lambda_permission" "apigw" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = module.populator_api.lambda_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${data.aws_api_gateway_rest_api.shared.execution_arn}/*/*/*"
}

# Worker Lambda
module "populator_worker" {
  source = "../../modules/lambda-worker"

  service_name    = "PoC-Populator-Worker"
  topic_name      = "population-requests"
  queue_name      = "populator-queue"
  runtime         = "dotnet8"
  handler         = "PoC.Populator::PoC.Populator.Functions.PopulatorWorkerFunction::FunctionHandler"
  timeout         = 60
  memory_size     = 1024
  zip_path        = local.zip_path
  lambda_role_arn = data.aws_iam_role.lambda_exec.arn

  environment_variables = merge(local.common_env_vars, {
    "MATERIALS_API_URL"             = local.materials_api_url
    "Populator__MaterialsTableName" = "materials-table"
    "Populator__PricesTableName"    = "costing-prices-table"
    "Populator__OutputTopicArn"     = local.output_topic_arn
    "OTEL_SERVICE_NAME"             = "PoC-Populator-Worker"
  })
}
