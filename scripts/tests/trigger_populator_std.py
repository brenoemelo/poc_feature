import urllib.request
import json
import sys
import os

# Add config to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../config')))
from global_config import CONFIG

url = CONFIG["ApiGateway"]["UrlTemplate"].format(api_id=CONFIG["ApiGateway"]["Id"], stage=CONFIG["ApiGateway"]["Stage"]) + "api/v1/populator/jobs"
headers = {"Content-Type": "application/json"}
payload = {
    "target": "materials",
    "count": 10,
    "minComponents": 10,
    "maxComponents": 30
}

data = json.dumps(payload).encode('utf-8')
req = urllib.request.Request(url, data=data, headers=headers)

try:
    with urllib.request.urlopen(req) as response:
        print(f"Status Code: {response.getcode()}")
        print(f"Response: {response.read().decode('utf-8')}")
except Exception as e:
    print(f"Error: {e}")
