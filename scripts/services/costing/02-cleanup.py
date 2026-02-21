import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def cleanup():
    aws_helpers.write_log("STEP 2: Cleanup (Costing)", "INFO")
    
    aws_helpers.remove_lambda_function(SERVICE_CONFIG['Name'])
    aws_helpers.remove_lambda_function(SERVICE_CONFIG['IngestionFunctionName'])
    
    queue_url = f"{SERVICE_CONFIG['EndpointUrl']}/000000000000/{SERVICE_CONFIG['IngestionQueueName']}"
    aws_helpers.remove_sqs_queue(queue_url)
    
    aws_helpers.remove_dynamodb_table(SERVICE_CONFIG['DynamoTable'])

if __name__ == "__main__":
    cleanup()
