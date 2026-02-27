import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def test():
    aws_helpers.write_log("STEP 5: Smoke Tests", "INFO")
    
    api_id = SERVICE_CONFIG['CustomApiId']
    # Use configured API URL from global config
    url = f"{SERVICE_CONFIG['ApiUrl']}api/v1/materials"
    
    aws_helpers.write_log(f"Testing API Endpoint: {url}", "INFO")
    
    response = aws_helpers.invoke_api("GET", url)
    
    if response:
        aws_helpers.write_log(f"Response Status: {response['status']}", "INFO")
        if response['status'] in [200, 404]:
             aws_helpers.write_log("Smoke Test Passed", "SUCCESS")
        else:
             error_body = str(response['body'])
             if len(error_body) > 1000:
                 error_body = error_body[:1000] + "... (truncated)"
             aws_helpers.write_log(f"Smoke Test Failed: {error_body}", "ERROR")
             sys.exit(1)
    else:
        sys.exit(1)

if __name__ == "__main__":
    test()
