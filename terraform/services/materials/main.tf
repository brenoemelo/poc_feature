data "aws_iam_role" "lambda_exec" {
  name = "lambda-role"
}

data "aws_api_gateway_rest_api" "shared" {
  name = "Material-Formulation-API"
}

data "terraform_remote_state" "shared" {
  backend = "local"
  config = {
    path = "../../shared/terraform.tfstate"
  }
}

locals {
  common_env_vars = {
    OTEL_EXPORTER_OTLP_ENDPOINT = "http://otel-collector:4317"
    OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
    OTEL_PROPAGATORS            = "tracecontext,xray"
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
  billing_mode   = "PAY_PER_REQUEST"
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

# API Gateway Integration
resource "aws_api_gateway_resource" "materials" {
  rest_api_id = data.aws_api_gateway_rest_api.shared.id
  parent_id   = data.terraform_remote_state.shared.outputs.v1_resource_id
  path_part   = "materials"
}

resource "aws_api_gateway_method" "materials_any" {
  rest_api_id   = data.aws_api_gateway_rest_api.shared.id
  resource_id   = aws_api_gateway_resource.materials.id
  http_method   = "ANY"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "materials_integration" {
  rest_api_id             = data.aws_api_gateway_rest_api.shared.id
  resource_id             = aws_api_gateway_method.materials_any.resource_id
  http_method             = aws_api_gateway_method.materials_any.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = module.materials_api.invoke_arn
}

resource "aws_api_gateway_resource" "proxy" {
  rest_api_id = data.aws_api_gateway_rest_api.shared.id
  parent_id   = aws_api_gateway_resource.materials.id
  path_part   = "{proxy+}"
}

resource "aws_api_gateway_method" "proxy_any" {
  rest_api_id   = data.aws_api_gateway_rest_api.shared.id
  resource_id   = aws_api_gateway_resource.proxy.id
  http_method   = "ANY"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "proxy_integration" {
  rest_api_id             = data.aws_api_gateway_rest_api.shared.id
  resource_id             = aws_api_gateway_method.proxy_any.resource_id
  http_method             = aws_api_gateway_method.proxy_any.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = module.materials_api.invoke_arn
}

resource "aws_api_gateway_deployment" "this" {
  rest_api_id = data.aws_api_gateway_rest_api.shared.id

  triggers = {
    redeployment = sha1(jsonencode([
      aws_api_gateway_resource.materials,
      aws_api_gateway_method.materials_any,
      aws_api_gateway_integration.materials_integration,
      aws_api_gateway_resource.proxy,
      aws_api_gateway_method.proxy_any,
      aws_api_gateway_integration.proxy_integration,
    ]))
  }

  lifecycle {
    create_before_destroy = true
  }

  depends_on = [
    aws_api_gateway_integration.materials_integration,
    aws_api_gateway_integration.proxy_integration
  ]
}

resource "aws_api_gateway_stage" "prod" {
  deployment_id = aws_api_gateway_deployment.this.id
  rest_api_id   = data.aws_api_gateway_rest_api.shared.id
  stage_name    = "prod"
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
