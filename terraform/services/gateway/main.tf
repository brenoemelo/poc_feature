data "aws_api_gateway_rest_api" "shared" {
  name = "Material-Formulation-API"
}

# Use a null_resource to deploy the OpenAPI spec using awslocal.
# We skip the python replacement step as the lambda names match the spec.
resource "null_resource" "deploy_openapi" {
  triggers = {
    openapi_hash   = filemd5("${path.module}/../../../docs/openapi.yaml")
    always_run     = timestamp()
  }

  provisioner "local-exec" {
    # We use PowerShell since the host OS is Windows (as per user_information), or we can just use command prompt.
    command = "aws apigateway put-rest-api --rest-api-id ${data.aws_api_gateway_rest_api.shared.id} --mode overwrite --body fileb://${path.module}/../../../docs/openapi.yaml --endpoint-url http://localhost:4566 && aws apigateway create-deployment --rest-api-id ${data.aws_api_gateway_rest_api.shared.id} --stage-name prod --endpoint-url http://localhost:4566"
  }
}
