import urllib.request
import json
import os
import sys

# Add config folder to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../config')))
from global_config import CONFIG, RESOURCES

def get_materials():
    url = f"{CONFIG['Aws']['LocalStackUrl']}/restapis/{CONFIG['ApiGateway']['Id']}/{CONFIG['ApiGateway']['Stage']}/_user_request_/api/v1/materials"
    print(f"Requesting: {url}")
    
    try:
        req = urllib.request.Request(url)
        with urllib.request.urlopen(req) as response:
            status = response.getcode()
            print(f"Status Code: {status}")
            
            content = response.read().decode('utf-8')
            try:
                data = json.loads(content)
                print(f"Response Data Count: {len(data.get('data', []))}")
                print("First item sample:", json.dumps(data.get('data', [])[0], indent=2) if data.get('data') else "No data")
            except json.JSONDecodeError:
                print("Response content (not JSON):", content)
                
    except urllib.error.HTTPError as e:
        print(f"HTTP Error: {e.code} - {e.reason}")
        print(e.read().decode('utf-8'))
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    get_materials()
