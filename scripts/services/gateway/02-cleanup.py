import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def cleanup():
    aws_helpers.write_log("STEP 2: Cleanup (Gateway)", "INFO")
    # Gateway cleanup is usually handled by overwriting or explicit teardown.
    # We don't delete the API in the pipeline usually to preserve ID, but if we wanted to:
    # apigateway = aws_helpers.get_boto3_client("apigateway")
    # apigateway.delete_rest_api(restApiId=SERVICE_CONFIG["ApiId"])
    pass

if __name__ == "__main__":
    cleanup()
