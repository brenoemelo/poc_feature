
import boto3
import json
import uuid
import sys
import os
import time

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../utils')))
import aws_helpers

def test_ingestion():
    queue_name = "material-ingestion-queue"
    aws_helpers.write_log(f"Testing ingestion on queue: {queue_name}", "INFO")
    
    sqs = aws_helpers.get_boto3_client("sqs")
    
    try:
        response = sqs.get_queue_url(QueueName=queue_name)
        queue_url = response['QueueUrl']
    except Exception as e:
        aws_helpers.write_log(f"Error getting queue URL: {e}", "ERROR")
        return

    material_id = f"test-mat-{uuid.uuid4()}"
    material_event = {
        "material": {
            "material_id": material_id,
            "name": f"Test Material {material_id}",
            "density": {
                "value": 1.5,
                "unit": "g/cm3"
            },
            "formulation": [
                {
                    "component": "Water",
                    "percentage": 100,
                    "type": "Liquid"
                }
            ],
            "properties": {
                "test_prop": "test_value"
            },
            "version": None
        },
        "EventId": str(uuid.uuid4()),
        "Timestamp": "2024-01-01T00:00:00Z"
    }
    
    # Wrap in SNS envelope structure as expected by the Lambda
    message_body = {
        "Message": json.dumps(material_event)
    }
    
    aws_helpers.write_log(f"Sending message for material: {material_id}", "INFO")
    
    sqs.send_message(
        QueueUrl=queue_url,
        MessageBody=json.dumps(message_body)
    )
    
    aws_helpers.write_log("Message sent. Waiting for processing...", "INFO")
    time.sleep(5) # Wait for Lambda to process
    
    # Verify in DynamoDB
    dynamodb = aws_helpers.get_boto3_client("dynamodb")
    table_name = "materials-table"
    
    try:
        response = dynamodb.get_item(
            TableName=table_name,
            Key={
                'material_id': {'S': material_id}
            }
        )
        
        if 'Item' in response:
            aws_helpers.write_log(f"SUCCESS: Material {material_id} found in DynamoDB!", "SUCCESS")
        else:
            aws_helpers.write_log(f"FAILURE: Material {material_id} NOT found in DynamoDB.", "ERROR")
            
    except Exception as e:
        aws_helpers.write_log(f"Error checking DynamoDB: {e}", "ERROR")

if __name__ == "__main__":
    test_ingestion()
