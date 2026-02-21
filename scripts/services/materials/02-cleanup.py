import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def cleanup():
    aws_helpers.write_log("STEP 2: Cleanup (Materials)", "INFO")
    
    aws_helpers.remove_lambda_function(SERVICE_CONFIG['Name'])
    
    account_id = "000000000000" # LocalStack default
    region = SERVICE_CONFIG['Region']
    topic_arn = f"arn:aws:sns:{region}:{account_id}:{SERVICE_CONFIG['SnsTopic']}"
    
    aws_helpers.remove_sns_topic(topic_arn)
    aws_helpers.remove_dynamodb_table(SERVICE_CONFIG['DynamoTable'])

if __name__ == "__main__":
    cleanup()
