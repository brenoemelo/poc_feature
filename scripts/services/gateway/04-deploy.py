import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def deploy():
    aws_helpers.write_log("STEP 4: Deploy API Gateway", "INFO")
    
    # 1. Ensure API Gateway
    api_id = aws_helpers.ensure_api_gateway(SERVICE_CONFIG["ApiName"], SERVICE_CONFIG["CustomApiId"])
    
    if not api_id:
        raise Exception("Failed to retrieve or create API Gateway ID.")
        
    # 2. Update API Definition
    aws_helpers.write_log("Updating API Definition from OpenAPI...", "INFO")
    if not os.path.exists(SERVICE_CONFIG["OpenApiPath"]):
        raise Exception(f"OpenAPI definition not found at: {SERVICE_CONFIG['OpenApiPath']}")
        
    apigateway = aws_helpers.get_boto3_client("apigateway")
    
    with open(SERVICE_CONFIG["OpenApiPath"], 'rb') as f:
        openapi_content = f.read()
        
    apigateway.put_rest_api(
        restApiId=api_id,
        mode='overwrite',
        body=openapi_content
    )
    aws_helpers.write_log("API Definition Updated.", "SUCCESS")
    
    # 3. Deploy to Stage
    aws_helpers.write_log(f"Deploying API to Stage: {SERVICE_CONFIG['Stage']}", "INFO")
    apigateway.create_deployment(
        restApiId=api_id,
        stageName=SERVICE_CONFIG['Stage']
    )
    aws_helpers.write_log(f"Deployment to stage '{SERVICE_CONFIG['Stage']}' Successful.", "SUCCESS")

if __name__ == "__main__":
    try:
        deploy()
    except Exception as e:
        aws_helpers.write_log(f"Deployment Failed: {e}", "ERROR")
        sys.exit(1)
