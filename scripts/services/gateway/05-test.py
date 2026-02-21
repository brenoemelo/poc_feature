import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def test():
    aws_helpers.write_log("STEP 5: Smoke Tests", "INFO")
    # Gateway doesn't have specific smoke tests other than checking if it responds.
    # We can check the health endpoint if defined, or just list resources.
    
    api_id = aws_helpers.ensure_api_gateway(SERVICE_CONFIG["ApiName"], SERVICE_CONFIG["CustomApiId"])
    if api_id:
         aws_helpers.write_log(f"API Gateway ID verified: {api_id}", "SUCCESS")
    else:
         aws_helpers.write_log("API Gateway verification failed.", "ERROR")
         sys.exit(1)

if __name__ == "__main__":
    test()
