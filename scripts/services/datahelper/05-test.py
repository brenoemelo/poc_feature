import sys
import os
import time
import json
import urllib.request
import urllib.error

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "../../utils")))
from logger import write_log
from config import SERVICE_CONFIG


def smoke_test():
    write_log("STEP 5: Smoke Tests", "INFO")

    api_gateway_id = SERVICE_CONFIG["CustomApiId"]
    endpoint_url = (
        f"{SERVICE_CONFIG['EndpointUrl']}/restapis/"
        f"{api_gateway_id}/{SERVICE_CONFIG['Stage']}/_user_request_/api/v1/datahelper/version"
    )

    write_log(f"Testing API Endpoint: {endpoint_url}", "INFO")

    time.sleep(2)

    try:
        request = urllib.request.Request(endpoint_url, method="GET")
        with urllib.request.urlopen(request) as response:
            status_code = response.getcode()
            body = response.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        status_code = e.code
        body = e.read().decode("utf-8")
    except Exception as ex:
        write_log(f"Smoke Test Failed: Unexpected error {ex}", "ERROR")
        sys.exit(1)

    write_log(f"Status Code: {status_code}", "INFO")

    if status_code != 200:
        write_log("Smoke Test Failed: Non-200 response", "ERROR")
        write_log(f"Body: {body}", "ERROR")
        sys.exit(1)

    try:
        data = json.loads(body)
    except json.JSONDecodeError as ex:
        write_log(f"Smoke Test Failed: Invalid JSON body ({ex})", "ERROR")
        sys.exit(1)

    if "version" not in data:
        write_log("Smoke Test Failed: 'version' field missing", "ERROR")
        sys.exit(1)

    write_log("Smoke Test Passed.", "SUCCESS")


if __name__ == "__main__":
    try:
        smoke_test()
    except Exception as e:
        write_log(f"Smoke Test Failed: {e}", "ERROR")
        sys.exit(1)
