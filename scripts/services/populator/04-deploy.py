import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def deploy():
    aws_helpers.write_log("STEP 4: Deploy Populator Service", "INFO")
    
    if not os.path.exists(SERVICE_CONFIG["ZipPath"]):
        raise Exception(f"Build artifact not found: {SERVICE_CONFIG['ZipPath']}")
        
    # 1. SNS Topic
    topic_arn = aws_helpers.ensure_sns_topic(SERVICE_CONFIG['SnsTopic'])
    
    # 2. SQS Queue
    queue_url = aws_helpers.ensure_sqs_queue(SERVICE_CONFIG['QueueName'])
    queue_arn = f"arn:aws:sqs:{SERVICE_CONFIG['Region']}:000000000000:{SERVICE_CONFIG['QueueName']}"
    
    # 3. SNS Subscription (Topic -> Queue)
    aws_helpers.ensure_sns_subscription(
        topic_arn=topic_arn,
        protocol="sqs",
        endpoint=queue_arn
    )
    
    # 4. Main Lambda (API)
    common_env = aws_helpers.get_common_env_vars()
    main_env_vars = {
        "OTEL_SERVICE_NAME": SERVICE_CONFIG['Name'],
        **common_env
    }
    
    aws_helpers.ensure_lambda_function(
        name=SERVICE_CONFIG['Name'],
        handler="PoC.Populator",
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
    
    # 6. Worker Lambda
    worker_env_vars = {
        "OTEL_SERVICE_NAME": SERVICE_CONFIG['WorkerFunctionName'],
        "MATERIALS_API_URL": SERVICE_CONFIG['MaterialsApiUrl'],
        "SNS_TOPIC_ARN": topic_arn,
        **common_env
    }
    
    aws_helpers.ensure_lambda_function(
        name=SERVICE_CONFIG['WorkerFunctionName'],
        handler="PoC.Populator::PoC.Populator.Functions.PopulatorWorkerFunction::FunctionHandler",
        role_arn="arn:aws:iam::000000000000:role/lambda-role",
        zip_path=SERVICE_CONFIG['ZipPath'],
        timeout=60,
        memory_size=1024,
        env_vars=worker_env_vars
    )
    
    # 7. Event Source Mapping
    aws_helpers.ensure_event_source_mapping(
        function_name=SERVICE_CONFIG['WorkerFunctionName'],
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
