import urllib.request
import json

url = "http://localhost:4566/restapis/material-api/prod/_user_request_/api/v1/populator/jobs"
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
