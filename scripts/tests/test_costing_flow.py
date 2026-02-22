
import boto3
import json
import uuid
import sys
import os
import time
import requests

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../utils')))
import aws_helpers

def test_costing_flow():
    # 1. Ingest a Price
    queue_name = "costing-ingestion-queue"
    aws_helpers.write_log(f"Testing price ingestion on queue: {queue_name}", "INFO")
    
    sqs = aws_helpers.get_boto3_client("sqs")
    
    try:
        response = sqs.get_queue_url(QueueName=queue_name)
        queue_url = response['QueueUrl']
    except Exception as e:
        aws_helpers.write_log(f"Error getting queue URL: {e}", "ERROR")
        return

    component_name = f"TestComponent-{uuid.uuid4()}"
    unit_price = 10.0
    currency = "USD"
    
    # Create price update event (matches PoC.Shared.Events.PriceUpdatedEvent)
    # NOTE: The C# record uses [JsonPropertyName("component_name")] so we must use snake_case
    price_event = {
        "component_name": component_name,
        "unit_price": unit_price,
        "currency": currency,
        "updated_at": "2024-01-01T00:00:00Z"
    }
    
    # Wrap in SNS envelope structure
    message_body = {
        "Message": json.dumps(price_event)
    }
    
    aws_helpers.write_log(f"Sending price update for: {component_name} ($10.0)", "INFO")
    
    sqs.send_message(
        QueueUrl=queue_url,
        MessageBody=json.dumps(message_body)
    )
    
    aws_helpers.write_log("Price message sent. Waiting for processing...", "INFO")
    sys.stdout.flush()
    
    # 2. Verify Price in DynamoDB (with retry)
    print("DEBUG: Checking DynamoDB...")
    sys.stdout.flush()
    dynamodb = aws_helpers.get_boto3_client("dynamodb")
    table_name = "costing-prices-table"
    
    found = False
    for i in range(10): # Retry for 10 seconds
        time.sleep(1)
        try:
            response = dynamodb.get_item(
                TableName=table_name,
                Key={'ComponentName': {'S': component_name}}
            )
            if 'Item' in response:
                aws_helpers.write_log(f"SUCCESS: Price for {component_name} found in DynamoDB!", "SUCCESS")
                found = True
                break
        except Exception as e:
            print(f"Error checking DynamoDB (attempt {i+1}): {e}")
            
    if not found:
         aws_helpers.write_log(f"FAILURE: Price for {component_name} NOT found in DynamoDB after retries.", "ERROR")
         # Try scanning to see what is there
         try:
            scan = dynamodb.scan(TableName=table_name)
            print(f"DEBUG: Scan result: {scan.get('Items')}")
         except:
            pass
         return

    # 3. Calculate Cost via API
    print("DEBUG: Calculating Cost via API...")
    # We need the API Gateway URL for Costing Service
    # Assuming standard localstack port and structure or using config
    # The config.py uses: f"{AWS_ENDPOINT_URL}/restapis/{CONFIG['ApiGateway']['Id']}/{CONFIG['ApiGateway']['Stage']}/_user_request_/api/v1/costing"
    
    # Get API ID
    apigateway = aws_helpers.get_boto3_client("apigateway")
    apis = apigateway.get_rest_apis()
    # Look for 'Material-Formulation-API' (from global_config.py)
    api_id = next((item['id'] for item in apis.get('items', []) if item['name'] == 'Material-Formulation-API'), None)
    
    if not api_id:
        aws_helpers.write_log("Could not find API Gateway 'Material-Formulation-API'. Attempting to create it...", "WARN")
        try:
            response = apigateway.create_rest_api(
                name='Material-Formulation-API',
                description='Created by test script'
            )
            api_id = response['id']
            aws_helpers.write_log(f"Created API Gateway with ID: {api_id}", "SUCCESS")
        except Exception as e:
            aws_helpers.write_log(f"Error creating API Gateway: {e}", "ERROR")
            return

    base_url = f"http://localhost:4566/restapis/{api_id}/prod/_user_request_/api/v1/costing"
    aws_helpers.write_log(f"Using Costing API URL: {base_url}", "INFO")
    
    # Calculate Cost Request
    # Formulation: 50% of the component we just added
    # Material: 100g total? No, formulation uses percentages.
    # The logic usually is: Cost = Sum(ComponentPrice * Percentage/100) per unit of material?
    # Or ComponentPrice is per kg, and density is involved?
    # Let's assume simple calculation first.
    
    payload = {
        "material_id": "temp-calc-1",
        "formulation": [
            {
                "component": component_name,
                "percentage": 100,
                "type": "Base"
            }
        ],
        "desired_margin_percent": 20
    }
    
    try:
        resp = requests.post(f"{base_url}/estimations", json=payload)
        if resp.status_code == 200:
            result = resp.json()
            # If price is 10.0/kg and we use 100%, cost is 10.0/kg.
            # Total cost should be around 10.0
            total_cost = result['data']['total_cost']
            aws_helpers.write_log(f"SUCCESS: Calculated Cost: {total_cost}", "SUCCESS")
            
            if abs(total_cost - unit_price) < 0.1:
                 aws_helpers.write_log("Verification Passed: Cost matches expected unit price.", "SUCCESS")
            else:
                 aws_helpers.write_log(f"Verification Warning: Cost {total_cost} differs from unit price {unit_price}. Check calculation logic.", "WARNING")
        else:
            aws_helpers.write_log(f"FAILURE: API call failed: {resp.status_code} {resp.text}", "ERROR")
            
    except Exception as e:
        aws_helpers.write_log(f"Error calling API: {e}", "ERROR")

    # 4. Verify Batch Calculation (Inter-service communication)
    print("DEBUG: Verifying Batch Calculation (Costing -> Materials)...")
    try:
        resp = requests.get(f"{base_url}/estimations/batch")
        if resp.status_code == 200:
            aws_helpers.write_log("SUCCESS: Batch calculation API called successfully.", "SUCCESS")
            # We don't strictly need to validate the content, just that it didn't crash with 500/502
            # due to connection refused.
            data = resp.json().get('data', [])
            aws_helpers.write_log(f"Batch response contains {len(data)} items.", "INFO")
        else:
            aws_helpers.write_log(f"FAILURE: Batch calculation failed: {resp.status_code} {resp.text}", "ERROR")
    except Exception as e:
        aws_helpers.write_log(f"Error calling Batch API: {e}", "ERROR")

if __name__ == "__main__":
    test_costing_flow()
