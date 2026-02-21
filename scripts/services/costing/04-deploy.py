import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def deploy():
    aws_helpers.write_log("STEP 4: Deploy Costing Service", "INFO")
    
    if not os.path.exists(SERVICE_CONFIG["ZipPath"]):
        raise Exception(f"Build artifact not found: {SERVICE_CONFIG['ZipPath']}")
        
    # 1. DynamoDB Table
    table_def = {
        'TableName': SERVICE_CONFIG['DynamoTable'],
        'AttributeDefinitions': [
            {'AttributeName': 'ComponentName', 'AttributeType': 'S'}
        ],
        'KeySchema': [
            {'AttributeName': 'ComponentName', 'KeyType': 'HASH'}
        ],
        'ProvisionedThroughput': {
            'ReadCapacityUnits': 5,
            'WriteCapacityUnits': 5
        }
    }
    aws_helpers.ensure_dynamodb_table(table_def)
    
    # 2. SQS Queue
    queue_url = aws_helpers.ensure_sqs_queue(SERVICE_CONFIG['IngestionQueueName'])
    # Need ARN for subscription. LocalStack pattern: arn:aws:sqs:region:account:name
    queue_arn = f"arn:aws:sqs:{SERVICE_CONFIG['Region']}:000000000000:{SERVICE_CONFIG['IngestionQueueName']}"
    
    # 3. SNS Subscription
    aws_helpers.ensure_sns_subscription(
        topic_arn=SERVICE_CONFIG['MaterialsTopicArn'],
        protocol="sqs",
        endpoint=queue_arn
    )
    
    # 4. Main Lambda (API)
    common_env = aws_helpers.get_common_env_vars()
    main_env_vars = {
        "OTEL_SERVICE_NAME": SERVICE_CONFIG['Name'],
        "MATERIALS_API_URL": SERVICE_CONFIG['MaterialsApiUrl'],
        **common_env
    }
    
    aws_helpers.ensure_lambda_function(
        name=SERVICE_CONFIG['Name'],
        handler="PoC.Costing",
        role_arn="arn:aws:iam::000000000000:role/lambda-role",
        zip_path=SERVICE_CONFIG['ZipPath'],
        timeout=30,
        memory_size=1024,
        env_vars=main_env_vars
    )
    
    # 5. API Gateway Permission
    aws_helpers.grant_lambda_permission(
        function_name=SERVICE_CONFIG['Name'],
        statement_id="apigateway-invoke",
        principal="apigateway.amazonaws.com",
        source_arn=f"arn:aws:execute-api:us-east-1:000000000000:{SERVICE_CONFIG['CustomApiId']}/*/*/*"
    )
    
    # 6. Worker Lambda (Ingestion)
    worker_env_vars = {
        "OTEL_SERVICE_NAME": SERVICE_CONFIG['IngestionFunctionName'],
        "COSTING_TABLE_NAME": SERVICE_CONFIG['DynamoTable'],
        **common_env
    }
    
    aws_helpers.ensure_lambda_function(
        name=SERVICE_CONFIG['IngestionFunctionName'],
        handler="PoC.Costing::PoC.Costing.Functions.PriceIngestionFunction::FunctionHandler",
        role_arn="arn:aws:iam::000000000000:role/lambda-role",
        zip_path=SERVICE_CONFIG['ZipPath'],
        timeout=30,
        memory_size=1024,
        env_vars=worker_env_vars
    )
    
    # 7. Event Source Mapping
    aws_helpers.ensure_event_source_mapping(
        function_name=SERVICE_CONFIG['IngestionFunctionName'],
        event_source_arn=queue_arn,
        batch_size=10
    )
    
    aws_helpers.write_log("Deployment Successful.", "SUCCESS")

if __name__ == "__main__":
    try:
        deploy()
    except Exception as e:
        aws_helpers.write_log(f"Deployment Failed: {e}", "ERROR")
        sys.exit(1)
