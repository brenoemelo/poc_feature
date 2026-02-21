import requests
import json

url = "http://localhost:4566/restapis/material-api/prod/_user_request_/api/v1/populator/jobs"
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
