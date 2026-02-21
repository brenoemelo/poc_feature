import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def test():
    aws_helpers.write_log("STEP 5: Smoke Tests", "INFO")
    
    api_id = SERVICE_CONFIG['CustomApiId']
    # Use LocalStack URL format for custom IDs if possible, or just the standard localhost:4566
    # LocalStack maps http://localhost:4566/restapis/{id}/{stage}/_user_request_/{path}
    
    url = f"{SERVICE_CONFIG['EndpointUrl']}/restapis/{api_id}/{SERVICE_CONFIG['Stage']}/_user_request_/api/v1/materials"
    
    aws_helpers.write_log(f"Testing API Endpoint: {url}", "INFO")
    
    response = aws_helpers.invoke_api("GET", url)
    
    if response:
        aws_helpers.write_log(f"Response Status: {response['status']}", "INFO")
        if response['status'] in [200, 404]:
             aws_helpers.write_log("Smoke Test Passed", "SUCCESS")
        else:
             aws_helpers.write_log(f"Smoke Test Failed: {response['body']}", "ERROR")
             sys.exit(1)
    else:
        sys.exit(1)

if __name__ == "__main__":
    test()
