import boto3
import requests
import time
import json
import sys
import os

# Add parent directory to path to import config
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../services/costing')))
from config import RESOURCES, CONFIG

AWS_ENDPOINT = CONFIG['Aws'].get('LocalStackUrl', "http://localhost:4566")
REGION = CONFIG['Aws'].get('Region', "us-east-1")
API_GATEWAY_ID = CONFIG['ApiGateway']['Id']
STAGE = CONFIG['ApiGateway']['Stage']
if "UrlTemplate" in CONFIG["ApiGateway"]:
    BASE_URL = CONFIG["ApiGateway"]["UrlTemplate"].format(api_id=API_GATEWAY_ID, stage=STAGE)
else:
    BASE_URL = f"{AWS_ENDPOINT}/_aws/execute-api/{API_GATEWAY_ID}/{STAGE}"

dynamodb = boto3.resource('dynamodb', endpoint_url=AWS_ENDPOINT, region_name=REGION)
materials_table = dynamodb.Table(RESOURCES['DynamoDb']['MaterialsTable'])
prices_table = dynamodb.Table(RESOURCES['DynamoDb']['CostingTable'])

def trigger_job(job_type, count=None):
    url = f"{BASE_URL}/api/v1/populator/jobs"
    payload = {"target": job_type}
    if count:
        payload["count"] = count
    
    print(f"Triggering {job_type} job...", flush=True)
    resp = requests.post(url, json=payload)
    if resp.status_code not in [200, 202]:
        print(f"Failed to trigger job: {resp.text}")
        sys.exit(1)
    print(f"Job triggered: {resp.status_code}")

def get_unique_components_from_db():
    print("Scanning materials table for unique components...")
    components = set()
    scan_kwargs = {
        'FilterExpression': boto3.dynamodb.conditions.Attr('record_type').eq('MATERIAL')
    }
    done = False
    start_key = None
    
    while not done:
        if start_key:
            scan_kwargs['ExclusiveStartKey'] = start_key
        response = materials_table.scan(**scan_kwargs)
        
        for item in response.get('Items', []):
            formulation = item.get('formulation', [])
            for comp in formulation:
                c_name = comp.get('component')
                if c_name:
                    components.add(c_name)
        
        start_key = response.get('LastEvaluatedKey', None)
        done = start_key is None
        
    return components

def get_prices_from_db():
    print("Scanning prices table...")
    prices = set()
    scan_kwargs = {} # Prices table likely has PK as ComponentName or similar
    # Check schema? Assuming PK is 'component_name' or similar.
    # Actually, let's just scan and inspect.
    
    done = False
    start_key = None
    
    while not done:
        if start_key:
            scan_kwargs['ExclusiveStartKey'] = start_key
        response = prices_table.scan(**scan_kwargs)
        
        for item in response.get('Items', []):
            # Debug first item keys
            if not prices and len(response.get('Items', [])) > 0:
                 print(f"DEBUG: First item keys: {list(item.keys())}", flush=True)

            if 'component_name' in item:
                prices.add(item['component_name'])
            elif 'ComponentName' in item:
                prices.add(item['ComponentName'])
            elif 'pk' in item: 
                 prices.add(item['pk'])
        
        start_key = response.get('LastEvaluatedKey', None)
        done = start_key is None
        
    return prices

def main():
    try:
        # 1. Generate Materials
        print("--- Step 1: Generate Materials ---", flush=True)
        # trigger_job("materials", 50) # Skip if already populated to save time
        
        # Check if materials exist first
        existing_components = get_unique_components_from_db()
        if len(existing_components) < 10:
             print(f"Only {len(existing_components)} components found. Triggering materials job...", flush=True)
             trigger_job("materials", 50)
             print("Waiting for materials ingestion...", flush=True)
             # Poll until count increases
             for _ in range(10):
                 time.sleep(2)
                 if len(get_unique_components_from_db()) >= 50:
                     break
        else:
             print(f"Found {len(existing_components)} components. Skipping generation.", flush=True)

        unique_components = get_unique_components_from_db()
        print(f"Total unique components: {len(unique_components)}", flush=True)
        
        if not unique_components:
            print("ERROR: No components found. Materials ingestion failed?", flush=True)
            sys.exit(1)
            
        # 3. Ensure Prices
        print("\n--- Step 2: Ensure Prices ---", flush=True)
        trigger_job("ensure-prices", 1) # Count is ignored by strategy but required by API
        
        print("Waiting for price population...", flush=True)
        # Poll for prices
        prices_found = set()
        for i in range(20):
            time.sleep(2)
            prices_found = get_prices_from_db()
            print(f"Iteration {i+1}: Found {len(prices_found)} prices.", flush=True)
            if len(prices_found) >= len(unique_components):
                break
        
        # 4. Verify Prices
        print("Verifying prices...", flush=True)
        prices_found = get_prices_from_db()
        print(f"Final count: Found {len(prices_found)} prices in Costing Table.", flush=True)
        
        missing = unique_components - prices_found
        if missing:
            print(f"ERROR: Missing prices for {len(missing)} components.", flush=True)
            print(f"Sample missing: {list(missing)[:5]}", flush=True)
            sys.exit(1)
        
        print("SUCCESS: All components have prices.", flush=True)
        
    except Exception as e:
        print(f"An unexpected error occurred: {e}", flush=True)
        sys.exit(1)

if __name__ == "__main__":
    main()
