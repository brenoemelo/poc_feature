import boto3
import logging
import os
import sys
import time
import json
import subprocess
from botocore.exceptions import ClientError

# Add config to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../config')))
from global_config import CONFIG

# Add utils to path to import logger
sys.path.append(os.path.dirname(__file__))
from logger import write_log

# Global Configuration
AWS_ENDPOINT_URL = os.getenv("AWS_ENDPOINT_URL", CONFIG["Aws"].get("LocalStackUrl", "http://localhost:4566"))
AWS_REGION = os.getenv("AWS_REGION", CONFIG["Aws"].get("Region", "us-east-1"))
AWS_ACCESS_KEY_ID = os.getenv("AWS_ACCESS_KEY_ID", CONFIG["Aws"].get("AccessKeyId", "test"))
AWS_SECRET_ACCESS_KEY = os.getenv("AWS_SECRET_ACCESS_KEY", CONFIG["Aws"].get("SecretAccessKey", "test"))

def get_boto3_client(service_name):
    return boto3.client(
        service_name,
        endpoint_url=AWS_ENDPOINT_URL,
        region_name=AWS_REGION,
        aws_access_key_id=AWS_ACCESS_KEY_ID,
        aws_secret_access_key=AWS_SECRET_ACCESS_KEY
    )

def validate_aws_connection():
    try:
        sts = get_boto3_client("sts")
        identity = sts.get_caller_identity()
        write_log(f"AWS Connection Verified. Account: {identity['Account']}", "SUCCESS")
        return True
    except Exception as e:
        write_log(f"AWS Connection Failed: {e}", "ERROR")
        return False

def get_common_env_vars():
    env_vars = {
        "Observability__Enabled": "true",
        "OTEL_EXPORTER_OTLP_ENDPOINT": "http://otel-collector:4318",
        "OTEL_EXPORTER_OTLP_PROTOCOL": "http/protobuf",
        "FeatureFlags__UnleashApiUrl": "http://unleash:4242/api/",
        "FeatureFlags__FetchTogglesIntervalSeconds": "1",
        "AWS_REGION": AWS_REGION,
        "AWS_ACCESS_KEY_ID": AWS_ACCESS_KEY_ID,
        "AWS_SECRET_ACCESS_KEY": AWS_SECRET_ACCESS_KEY,
        "AWS_ENDPOINT_URL": "http://localstack:4566" # Explicitly point to LocalStack internal URL
    }
    write_log(f"DEBUG: Common Env Vars: {env_vars}", "INFO")
    return env_vars

def _retry_with_backoff(func, max_attempts=5, base_delay_seconds=0.5, operation_name="operation"):
    for attempt in range(1, max_attempts + 1):
        try:
            return func()
        except ClientError as e:
            code = e.response.get("Error", {}).get("Code")
            if code == "ResourceConflictException" and attempt < max_attempts:
                delay = base_delay_seconds * attempt
                write_log(
                    f"{operation_name} hit ResourceConflictException. Retrying in {delay:.1f}s (attempt {attempt}/{max_attempts})",
                    "WARN",
                )
                time.sleep(delay)
                continue
            raise

def _wait_for_lambda_ready(lambda_client, name, max_wait_seconds=30):
    deadline = time.time() + max_wait_seconds
    while time.time() < deadline:
        try:
            config = lambda_client.get_function_configuration(FunctionName=name)
            status = config.get("LastUpdateStatus")
            if not status or status != "InProgress":
                return
        except ClientError as e:
            code = e.response.get("Error", {}).get("Code")
            if code not in ("ResourceNotFoundException", "ResourceConflictException"):
                raise
        time.sleep(1)
    write_log(f"Timeout waiting for Lambda {name} to be ready", "WARN")

# -----------------------------------------------------------------------------
# High-Level Resource Management Functions (Idempotent)
# -----------------------------------------------------------------------------

def ensure_sns_topic(name):
    sns = get_boto3_client("sns")
    write_log(f"Ensuring SNS Topic exists: {name}", "INFO")
    response = sns.create_topic(Name=name)
    return response['TopicArn']

def ensure_sqs_queue(name):
    sqs = get_boto3_client("sqs")
    write_log(f"Ensuring SQS Queue exists: {name}", "INFO")
    response = sqs.create_queue(QueueName=name)
    return response['QueueUrl']

def ensure_sns_subscription(topic_arn, protocol, endpoint, attributes=None):
    sns = get_boto3_client("sns")
    write_log(f"Ensuring SNS Subscription: {protocol} -> {endpoint}", "INFO")
    
    params = {
        'TopicArn': topic_arn,
        'Protocol': protocol,
        'Endpoint': endpoint,
        'ReturnSubscriptionArn': True
    }
    
    if attributes:
        params['Attributes'] = attributes
        
    sns.subscribe(**params)

def ensure_dynamodb_table(table_def):
    dynamodb = get_boto3_client("dynamodb")
    table_name = table_def['TableName']
    write_log(f"Ensuring DynamoDB Table exists: {table_name}", "INFO")
    
    try:
        dynamodb.describe_table(TableName=table_name)
        write_log(f"DynamoDB Table '{table_name}' already exists.", "INFO")
        return f"arn:aws:dynamodb:{AWS_REGION}:000000000000:table/{table_name}"
    except ClientError as e:
        if e.response['Error']['Code'] == 'ResourceNotFoundException':
            write_log(f"Creating DynamoDB Table: {table_name}", "INFO")
            dynamodb.create_table(**table_def)
            return f"arn:aws:dynamodb:{AWS_REGION}:000000000000:table/{table_name}"
        else:
            raise e

def ensure_lambda_function(name, handler, role_arn, zip_path, runtime="dotnet8", timeout=30, memory_size=1024, env_vars=None):
    lambda_client = get_boto3_client("lambda")
    write_log(f"Ensuring Lambda Function: {name}", "INFO")
    write_log(f"DEBUG: Lambda Env Vars: {env_vars}", "INFO")
    
    with open(zip_path, 'rb') as f:
        zip_content = f.read()
    
    try:
        lambda_client.get_function(FunctionName=name)
        write_log(f"Updating existing Lambda function: {name}", "INFO")
        lambda_client.update_function_code(FunctionName=name, ZipFile=zip_content)
        _wait_for_lambda_ready(lambda_client, name)
        def _update_config():
            lambda_client.update_function_configuration(
                FunctionName=name,
                Handler=handler,
                Timeout=int(timeout),
                MemorySize=int(memory_size),
                Environment={'Variables': env_vars} if env_vars else {}
            )
        _retry_with_backoff(_update_config, operation_name=f"Update Lambda {name}")
    except ClientError as e:
        if e.response['Error']['Code'] == 'ResourceNotFoundException':
            write_log(f"Creating new Lambda function: {name}", "INFO")
            def _create():
                lambda_client.create_function(
                    FunctionName=name,
                    Runtime=runtime,
                    Role=role_arn,
                    Handler=handler,
                    Code={'ZipFile': zip_content},
                    Timeout=int(timeout),
                    MemorySize=int(memory_size),
                    Environment={'Variables': env_vars} if env_vars else {}
                )
            _retry_with_backoff(_create, operation_name=f"Create Lambda {name}")
        else:
            raise e

def grant_lambda_permission(function_name, statement_id, principal, source_arn):
    lambda_client = get_boto3_client("lambda")
    write_log(f"Ensuring Lambda Permission: {statement_id}", "INFO")
    
    try:
        lambda_client.add_permission(
            FunctionName=function_name,
            StatementId=statement_id,
            Action="lambda:InvokeFunction",
            Principal=principal,
            SourceArn=source_arn
        )
    except ClientError as e:
        if e.response['Error']['Code'] == 'ResourceConflictException':
            write_log(f"Permission {statement_id} already exists for {function_name}", "INFO")
        else:
            raise e

def ensure_event_source_mapping(function_name, event_source_arn, batch_size=10):
    lambda_client = get_boto3_client("lambda")
    write_log(f"Ensuring Event Source Mapping: {function_name} <- {event_source_arn}", "INFO")
    
    mappings = lambda_client.list_event_source_mappings(
        FunctionName=function_name,
        EventSourceArn=event_source_arn
    )
    
    if mappings['EventSourceMappings']:
        write_log(f"Event source mapping already exists for {function_name}", "INFO")
    else:
        lambda_client.create_event_source_mapping(
            FunctionName=function_name,
            EventSourceArn=event_source_arn,
            BatchSize=batch_size
        )

def ensure_api_gateway(name, custom_id=None):
    apigateway = get_boto3_client("apigateway")
    write_log(f"Ensuring API Gateway exists: {name}", "INFO")
    
    if custom_id:
        try:
            api = apigateway.get_rest_api(restApiId=custom_id)
            write_log(f"Found existing API Gateway by ID: {custom_id}", "INFO")
            return custom_id
        except ClientError as e:
            if e.response['Error']['Code'] != 'NotFoundException':
                raise e
    
    # Check by name if not found by ID or ID not provided
    apis = apigateway.get_rest_apis()
    for item in apis.get('items', []):
        if item['name'] == name:
            write_log(f"API Gateway '{name}' already exists (ID: {item['id']}).", "INFO")
            return item['id']
            
    write_log(f"Creating API Gateway: {name}", "INFO")
    tags_arg = {}
    if custom_id:
        tags_arg['_custom_id_'] = custom_id
        
    api = apigateway.create_rest_api(name=name, tags=tags_arg)
    return api['id']

def remove_lambda_function(function_name):
    lambda_client = get_boto3_client("lambda")
    write_log(f"Removing Lambda Function: {function_name}", "INFO")
    
    # Delete ESMs
    try:
        mappings = lambda_client.list_event_source_mappings(FunctionName=function_name)
        for mapping in mappings.get('EventSourceMappings', []):
            try:
                lambda_client.delete_event_source_mapping(UUID=mapping['UUID'])
            except ClientError:
                pass
    except ClientError:
        pass
        
    # Delete Function
    try:
        lambda_client.delete_function(FunctionName=function_name)
    except ClientError as e:
        if e.response['Error']['Code'] != 'ResourceNotFoundException':
            write_log(f"Error deleting function: {e}", "WARN")

def remove_dynamodb_table(table_name):
    dynamodb = get_boto3_client("dynamodb")
    write_log(f"Removing DynamoDB Table: {table_name}", "INFO")
    try:
        dynamodb.delete_table(TableName=table_name)
    except ClientError as e:
        if e.response['Error']['Code'] != 'ResourceNotFoundException':
            write_log(f"Error deleting table: {e}", "WARN")

def remove_sqs_queue(queue_url):
    sqs = get_boto3_client("sqs")
    write_log(f"Removing SQS Queue: {queue_url}", "INFO")
    try:
        sqs.delete_queue(QueueUrl=queue_url)
    except ClientError as e:
        if e.response['Error']['Code'] != 'AWS.SimpleQueueService.NonExistentQueue':
            write_log(f"Error deleting queue: {e}", "WARN")

def remove_sns_topic(topic_arn):
    sns = get_boto3_client("sns")
    write_log(f"Removing SNS Topic: {topic_arn}", "INFO")
    try:
        sns.delete_topic(TopicArn=topic_arn)
    except ClientError as e:
        if e.response['Error']['Code'] != 'NotFound':
            write_log(f"Error deleting topic: {e}", "WARN")

def cleanup_all_resources():
    """
    Deletes all resources in LocalStack to ensure a clean slate for Terraform.
    EXCEPT DynamoDB tables (as per user request).
    """
    write_log(">>> STARTING LOCALSTACK CLEANUP <<<", "INFO")
    
    lambda_client = get_boto3_client("lambda")

    # 0. Global Event Source Mappings (Clean these first to avoid conflicts)
    try:
        write_log("Cleaning all Event Source Mappings...", "INFO")
        # List all mappings (no FunctionName filter)
        paginator = lambda_client.get_paginator('list_event_source_mappings')
        for page in paginator.paginate():
            for mapping in page.get('EventSourceMappings', []):
                uuid = mapping['UUID']
                write_log(f"Removing ESM: {uuid}", "INFO")
                try:
                    lambda_client.delete_event_source_mapping(UUID=uuid)
                except Exception as e:
                    write_log(f"Error deleting ESM {uuid}: {e}", "WARN")
    except Exception as e:
        write_log(f"Error cleaning ESMs: {e}", "WARN")

    # 1. Lambdas
    try:
        paginator = lambda_client.get_paginator('list_functions')
        for page in paginator.paginate():
            for func in page.get('Functions', []):
                remove_lambda_function(func['FunctionName'])
    except Exception as e:
        write_log(f"Error cleaning Lambdas: {e}", "WARN")

    # 2. DynamoDB Tables - SKIPPED (User request: keep databases)
    write_log("Skipping DynamoDB cleanup (keeping databases).", "INFO")
    # try:
    #     dynamodb = get_boto3_client("dynamodb")
    #     tables = dynamodb.list_tables()
    #     for table in tables.get('TableNames', []):
    #         remove_dynamodb_table(table)
    # except Exception as e:
    #     write_log(f"Error cleaning DynamoDB: {e}", "WARN")

    # 3. SQS Queues
    try:
        sqs = get_boto3_client("sqs")
        queues = sqs.list_queues()
        for queue_url in queues.get('QueueUrls', []):
            remove_sqs_queue(queue_url)
    except Exception as e:
        write_log(f"Error cleaning SQS: {e}", "WARN")

    # 4. SNS Topics
    try:
        sns = get_boto3_client("sns")
        paginator = sns.get_paginator('list_topics')
        for page in paginator.paginate():
            for topic in page.get('Topics', []):
                remove_sns_topic(topic['TopicArn'])
    except Exception as e:
        write_log(f"Error cleaning SNS: {e}", "WARN")

    # 5. API Gateways
    try:
        apigw = get_boto3_client("apigateway")
        apis = apigw.get_rest_apis()
        for api in apis.get('items', []):
            write_log(f"Removing API Gateway: {api['name']} ({api['id']})", "INFO")
            try:
                apigw.delete_rest_api(restApiId=api['id'])
            except Exception as e:
                write_log(f"Error deleting API Gateway {api['id']}: {e}", "WARN")
    except Exception as e:
        write_log(f"Error cleaning API Gateways: {e}", "WARN")

    # 6. S3 Buckets
    try:
        s3 = get_boto3_client("s3")
        buckets = s3.list_buckets()
        for bucket in buckets.get('Buckets', []):
            name = bucket['Name']
            write_log(f"Removing S3 Bucket: {name}", "INFO")
            try:
                # Delete objects first
                objects = s3.list_objects_v2(Bucket=name)
                if 'Contents' in objects:
                    for obj in objects['Contents']:
                        s3.delete_object(Bucket=name, Key=obj['Key'])
                s3.delete_bucket(Bucket=name)
            except Exception as e:
                write_log(f"Error deleting S3 Bucket {name}: {e}", "WARN")
    except Exception as e:
        write_log(f"Error cleaning S3: {e}", "WARN")
        
    # 7. CloudWatch Log Groups
    try:
        logs = get_boto3_client("logs")
        paginator = logs.get_paginator('describe_log_groups')
        for page in paginator.paginate():
            for group in page.get('logGroups', []):
                 write_log(f"Removing Log Group: {group['logGroupName']}", "INFO")
                 try:
                     logs.delete_log_group(logGroupName=group['logGroupName'])
                 except Exception as e:
                     write_log(f"Error deleting log group {group['logGroupName']}: {e}", "WARN")
    except Exception as e:
        write_log(f"Error cleaning Log Groups: {e}", "WARN")
        
    write_log(">>> LOCALSTACK CLEANUP COMPLETED <<<", "SUCCESS")

def invoke_lambda(function_name, payload={}):
    lambda_client = get_boto3_client("lambda")
    write_log(f"Invoking Lambda: {function_name}", "INFO")
    
    try:
        response = lambda_client.invoke(
            FunctionName=function_name,
            InvocationType='RequestResponse',
            Payload=json.dumps(payload)
        )
        
        payload_stream = response['Payload']
        response_payload = payload_stream.read().decode('utf-8')
        
        if response.get('FunctionError'):
             write_log(f"Lambda Invocation Error: {response_payload}", "ERROR")
             return None
             
        return json.loads(response_payload)
    except Exception as e:
        write_log(f"Error invoking Lambda: {e}", "ERROR")
        return None

import urllib.request
import urllib.error

def invoke_api(method, url, body=None, headers={}):
    write_log(f"Invoking API: {method} {url}", "INFO")
    
    req = urllib.request.Request(url, method=method)
    
    for k, v in headers.items():
        req.add_header(k, v)
        
    if body:
        req.add_header('Content-Type', 'application/json')
        data = json.dumps(body).encode('utf-8')
    else:
        data = None
        
    try:
        with urllib.request.urlopen(req, data=data) as response:
            try:
                response_body = json.loads(response.read().decode('utf-8'))
            except:
                response_body = response.read().decode('utf-8')
                
            return {
                'status': response.status,
                'body': response_body
            }
    except urllib.error.HTTPError as e:
        try:
            error_body = json.loads(e.read().decode('utf-8'))
        except:
            error_body = str(e)
            
        return {
            'status': e.code,
            'body': error_body
        }
    except Exception as e:
        write_log(f"API Invocation Error: {e}", "ERROR")
        return None
