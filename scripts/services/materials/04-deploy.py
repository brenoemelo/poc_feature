import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def deploy():
    aws_helpers.write_log("STEP 4: Deploy Materials Service", "INFO")
    
    if not os.path.exists(SERVICE_CONFIG["ZipPath"]):
        raise Exception(f"Build artifact not found: {SERVICE_CONFIG['ZipPath']}")
        
    # 1. DynamoDB Table
    table_def = {
        'TableName': SERVICE_CONFIG['DynamoTable'],
        'AttributeDefinitions': [
            {'AttributeName': 'material_id', 'AttributeType': 'S'},
            {'AttributeName': 'record_type', 'AttributeType': 'S'}
        ],
        'KeySchema': [
            {'AttributeName': 'material_id', 'KeyType': 'HASH'}
        ],
        'ProvisionedThroughput': {
            'ReadCapacityUnits': 5,
            'WriteCapacityUnits': 5
        },
        'GlobalSecondaryIndexes': [
            {
                'IndexName': 'IX_Materials_By_Type',
                'KeySchema': [
                    {'AttributeName': 'record_type', 'KeyType': 'HASH'},
                    {'AttributeName': 'material_id', 'KeyType': 'RANGE'}
                ],
                'Projection': {'ProjectionType': 'ALL'},
                'ProvisionedThroughput': {
                    'ReadCapacityUnits': 5,
                    'WriteCapacityUnits': 5
                }
            }
        ]
    }
    aws_helpers.ensure_dynamodb_table(table_def)
    
    # 2. SNS Topic
    topic_arn = aws_helpers.ensure_sns_topic(SERVICE_CONFIG['SnsTopic'])
    
    # 3. Lambda Function
    common_env = aws_helpers.get_common_env_vars()
    env_vars = {
        "Materials__TableName": SERVICE_CONFIG['DynamoTable'],
        **common_env
    }
    
    aws_helpers.ensure_lambda_function(
        name=SERVICE_CONFIG['Name'],
        handler="PoC.Materials",
        role_arn="arn:aws:iam::000000000000:role/lambda-role",
        zip_path=SERVICE_CONFIG['ZipPath'],
        timeout=30,
        memory_size=1024,
        env_vars=env_vars
    )
    
    # 4. API Gateway Permission
    aws_helpers.grant_lambda_permission(
        function_name=SERVICE_CONFIG['Name'],
        statement_id="apigateway-invoke",
        principal="apigateway.amazonaws.com",
        source_arn=f"arn:aws:execute-api:us-east-1:000000000000:{SERVICE_CONFIG['CustomApiId']}/*/*/*"
    )

    # 5. Ingestion Queue
    queue_url = aws_helpers.ensure_sqs_queue(SERVICE_CONFIG['IngestionQueueName'])
    queue_arn = f"arn:aws:sqs:{SERVICE_CONFIG['Region']}:000000000000:{SERVICE_CONFIG['IngestionQueueName']}"
    
    # 6. Subscribe Queue to Topic
    aws_helpers.ensure_sns_subscription(
        topic_arn=topic_arn,
        protocol="sqs",
        endpoint=queue_arn
    )

    # 7. Ingestion Lambda
    ingestion_env_vars = {
        "Materials__TableName": SERVICE_CONFIG['DynamoTable'],
        "OTEL_SERVICE_NAME": SERVICE_CONFIG['IngestionFunctionName'],
        **common_env
    }
    aws_helpers.write_log(f"Ingestion Env Vars: {ingestion_env_vars}", "INFO")

    aws_helpers.ensure_lambda_function(
        name=SERVICE_CONFIG['IngestionFunctionName'],
        handler="PoC.Materials::PoC.Materials.Functions.MaterialIngestionFunction::FunctionHandler",
        role_arn="arn:aws:iam::000000000000:role/lambda-role",
        zip_path=SERVICE_CONFIG['ZipPath'],
        timeout=30,
        memory_size=1024,
        env_vars=ingestion_env_vars
    )

    # 8. Event Source Mapping
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
