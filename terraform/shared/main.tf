# Global API Gateway
resource "aws_api_gateway_rest_api" "main" {
  name        = "Material-Formulation-API"
  description = "Shared API Gateway for all PoC services"
  
  tags = {
    _custom_id_ = "material-api"
  }
}

resource "aws_api_gateway_resource" "api" {
  rest_api_id = aws_api_gateway_rest_api.main.id
  parent_id   = aws_api_gateway_rest_api.main.root_resource_id
  path_part   = "api"
}

resource "aws_api_gateway_resource" "v1" {
  rest_api_id = aws_api_gateway_rest_api.main.id
  parent_id   = aws_api_gateway_resource.api.id
  path_part   = "v1"
}



# Global IAM Role for Lambdas
resource "aws_iam_role" "lambda_exec" {
  name = "lambda-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })
}

# Attach basic execution role policy
resource "aws_iam_role_policy_attachment" "lambda_basic" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

# In LocalStack, we can be more permissive for ease of development.
# Adding permissions for SNS, SQS, DynamoDB.
resource "aws_iam_role_policy" "lambda_extra" {
  name = "lambda-extra-permissions"
  role = aws_iam_role.lambda_exec.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = [
          "sns:Publish",
          "sqs:SendMessage",
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:GetQueueAttributes",
          "dynamodb:*",
          "s3:*"
        ]
        Effect   = "Allow"
        Resource = "*"
      }
    ]
  })
}

# S3 Buckets
resource "aws_s3_bucket" "materials_data" {
  bucket = "poc-materials-data"
}

resource "aws_s3_bucket" "flags" {
  bucket = "flags"
}

# Initial Feature Flags file
resource "aws_s3_object" "flags_json" {
  bucket  = aws_s3_bucket.flags.id
  key     = "flags.json"
  content = jsonencode({ "population-jobs" : { "default" : true } })
  content_type = "application/json"
}
