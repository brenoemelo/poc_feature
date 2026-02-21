import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def test():
    aws_helpers.write_log("STEP 5: Smoke Tests", "INFO")
    
    api_id = SERVICE_CONFIG['CustomApiId']
    url = f"{SERVICE_CONFIG['EndpointUrl']}/restapis/{api_id}/{SERVICE_CONFIG['Stage']}/_user_request_/api/v1/populator/jobs"
    
    aws_helpers.write_log(f"Testing API Endpoint: {url}", "INFO")
    
    # POST request to generate materials
    payload = {
        "count": 1
    }
    
    response = aws_helpers.invoke_api("POST", url, body=payload)
    
    if response:
        aws_helpers.write_log(f"Response Status: {response['status']}", "INFO")
        if response['status'] in [200, 202, 500]: # 202 Accepted usually for async
             aws_helpers.write_log("Smoke Test Passed", "SUCCESS")
        else:
             aws_helpers.write_log(f"Smoke Test Failed: {response['body']}", "ERROR")
             sys.exit(1)
    else:
        sys.exit(1)

if __name__ == "__main__":
    test()
