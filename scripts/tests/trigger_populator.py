import requests
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

try:
    response = requests.post(url, headers=headers, json=payload)
    print(f"Status Code: {response.status_code}")
    print(f"Response: {response.text}")
except Exception as e:
    print(f"Error: {e}")
