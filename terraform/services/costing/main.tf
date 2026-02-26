data "aws_iam_role" "lambda_exec" {
  name = "lambda-role"
}

data "aws_api_gateway_rest_api" "shared" {
  name = "Material-Formulation-API"
}

locals {
  # Updated to use the new LocalStack format to avoid deprecation warnings
  materials_api_url = "http://localstack:4566/_aws/execute-api/${data.aws_api_gateway_rest_api.shared.id}/prod/"
  
  common_env_vars = {
    OTEL_EXPORTER_OTLP_ENDPOINT = "http://otel-collector:4317"
    OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
    AWS__Region                 = "us-east-1"
    AWS__ServiceUrl             = "http://localstack:4566"
    FeatureFlags__UnleashApiUrl = "http://host.docker.internal:4242/api/" # Use host.docker.internal for stable access
    FeatureFlags__FetchTogglesIntervalSeconds = "1"
  }
  zip_path = "${path.module}/../../../dist/PoC-Costing/PoC-Costing.zip"
}

# DynamoDB
resource "aws_dynamodb_table" "costing" {
  name           = "costing-prices-table"
  billing_mode   = "PROVISIONED"
  read_capacity  = 5
  write_capacity = 5
  hash_key       = "ComponentName"

  attribute {
    name = "ComponentName"
    type = "S"
  }
}

# API Lambda
module "costing_api" {
  source = "../../modules/lambda-api"

  service_name    = "PoC-Costing"
  runtime         = "dotnet8"
  handler         = "PoC.Costing"
  timeout         = 30
  memory_size     = 1024
  zip_path        = local.zip_path
  lambda_role_arn = data.aws_iam_role.lambda_exec.arn

  environment_variables = merge(local.common_env_vars, {
    "Costing__TableName"        = aws_dynamodb_table.costing.name
    "OTEL_SERVICE_NAME"         = "PoC-Costing"
    "Observability__Enabled"    = "true"
    "Services__MaterialsApiUrl" = local.materials_api_url
    "ASPNETCORE_ENVIRONMENT"    = "Development"
  })
}

# API Gateway Permission
resource "aws_lambda_permission" "apigw" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = module.costing_api.lambda_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${data.aws_api_gateway_rest_api.shared.execution_arn}/*/*/*"
}

# Worker Lambda
module "costing_worker" {
  source = "../../modules/lambda-worker"

  service_name    = "PoC-Costing-PriceIngestion"
  topic_name      = "material-events"
  queue_name      = "costing-ingestion-queue"
  runtime         = "dotnet8"
  handler         = "PoC.Costing::PoC.Costing.Functions.PriceIngestionFunction::FunctionHandler"
  timeout         = 30
  memory_size     = 1024
  zip_path        = local.zip_path
  lambda_role_arn = data.aws_iam_role.lambda_exec.arn

  environment_variables = merge(local.common_env_vars, {
    "Costing__TableName"     = aws_dynamodb_table.costing.name
    "OTEL_SERVICE_NAME"      = "PoC-Costing-PriceIngestion"
    "Observability__Enabled" = "true"
    "ASPNETCORE_ENVIRONMENT" = "Development"
  })

  filter_policy = jsonencode({
    EventType = ["PriceUpdated"]
  })
}
