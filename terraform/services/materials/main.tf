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
  zip_path = "${path.module}/../../../dist/PoC-Materials/PoC-Materials.zip"
}

# DynamoDB
resource "aws_dynamodb_table" "materials" {
  name           = "materials-table"
  billing_mode   = "PROVISIONED"
  read_capacity  = 5
  write_capacity = 5
  hash_key       = "material_id"

  attribute {
    name = "material_id"
    type = "S"
  }

  attribute {
    name = "record_type"
    type = "S"
  }

  global_secondary_index {
    name            = "IX_Materials_By_Type"
    hash_key        = "record_type"
    range_key       = "material_id"
    read_capacity   = 5
    write_capacity  = 5
    projection_type = "ALL"
  }
}

# API Lambda
module "materials_api" {
  source = "../../modules/lambda-api"

  service_name    = "PoC-Materials"
  runtime         = "dotnet8"
  handler         = "PoC.Materials"
  timeout         = 30
  memory_size     = 1024
  zip_path        = local.zip_path
  lambda_role_arn = data.aws_iam_role.lambda_exec.arn

  environment_variables = merge(local.common_env_vars, {
    "Materials__TableName"   = aws_dynamodb_table.materials.name
    "OTEL_SERVICE_NAME"      = "PoC-Materials"
    "Observability__Enabled" = "true"
    "ASPNETCORE_ENVIRONMENT" = "Development"
  })
}

# API Gateway Permission
resource "aws_lambda_permission" "apigw" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = module.materials_api.lambda_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${data.aws_api_gateway_rest_api.shared.execution_arn}/*/*/*"
}

# Worker Lambda
module "materials_worker" {
  source = "../../modules/lambda-worker"

  service_name    = "PoC-Materials-Ingestion"
  topic_name      = "material-events"
  queue_name      = "material-ingestion-queue"
  runtime         = "dotnet8"
  handler         = "PoC.Materials::PoC.Materials.Functions.MaterialIngestionFunction::FunctionHandler"
  timeout         = 30
  memory_size     = 1024
  zip_path        = local.zip_path
  lambda_role_arn = data.aws_iam_role.lambda_exec.arn

  environment_variables = merge(local.common_env_vars, {
    "Materials__TableName"   = aws_dynamodb_table.materials.name
    "OTEL_SERVICE_NAME"      = "PoC-Materials-Ingestion"
    "Observability__Enabled" = "true"
    "ASPNETCORE_ENVIRONMENT" = "Development"
  })

  filter_policy = jsonencode({
    EventType = ["MaterialCreated"]
  })
}
