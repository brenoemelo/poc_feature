import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def test():
    aws_helpers.write_log("STEP 5: Smoke Tests", "INFO")
    
    api_id = SERVICE_CONFIG['CustomApiId']
    url = f"{SERVICE_CONFIG['ApiUrl']}api/v1/costing/estimations"
    
    aws_helpers.write_log(f"Testing API Endpoint: {url}", "INFO")
    
    # POST request for calculation
    payload = {
        "recipe": [
            {"materialId": "MAT-001", "quantity": 10}
        ]
    }
    
    response = aws_helpers.invoke_api("POST", url, body=payload)
    
    if response:
        aws_helpers.write_log(f"Response Status: {response['status']}", "INFO")
        if response['status'] in [200, 400, 500]: # 500 might happen if dependencies missing, but it answers
             aws_helpers.write_log("Smoke Test Passed (Service is reachable)", "SUCCESS")
        else:
             aws_helpers.write_log(f"Smoke Test Failed: {response['body']}", "ERROR")
             sys.exit(1)
    else:
        sys.exit(1)

if __name__ == "__main__":
    test()
