import boto3
import sys
import os
import json

# Add parent directory to path to import config
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../services/costing')))
from config import RESOURCES, CONFIG

AWS_ENDPOINT = "http://localhost:4566"
REGION = "us-east-1"

dynamodb = boto3.resource('dynamodb', endpoint_url=AWS_ENDPOINT, region_name=REGION)
materials_table = dynamodb.Table(RESOURCES['DynamoDb']['MaterialsTable'])
prices_table = dynamodb.Table(RESOURCES['DynamoDb']['CostingTable'])

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
    scan_kwargs = {}
    
    done = False
    start_key = None
    
    while not done:
        if start_key:
            scan_kwargs['ExclusiveStartKey'] = start_key
        response = prices_table.scan(**scan_kwargs)
        
        # Debug: print first item to see structure
        if response.get('Items') and not prices:
             print(f"Sample price item: {response.get('Items')[0]}")

        for item in response.get('Items', []):
            # The PricePopulationStrategy generates ComponentPriceRequest
            # The Costing service likely saves it with a PK related to component name.
            
            if 'ComponentName' in item:
                prices.add(item['ComponentName'])
            elif 'component_name' in item:
                prices.add(item['component_name'])
            elif 'pk' in item:
                 # If PK is "PRICE#{ComponentName}"
                 pk = item['pk']
                 if pk.startswith("PRICE#"):
                     prices.add(pk.replace("PRICE#", ""))
                 else:
                     prices.add(pk)
        
        start_key = response.get('LastEvaluatedKey', None)
        done = start_key is None
        
    return prices

def main():
    try:
        unique_components = get_unique_components_from_db()
        print(f"Found {len(unique_components)} unique components in Materials Table.")
        
        prices_found = get_prices_from_db()
        print(f"Found {len(prices_found)} prices in Costing Table.")
        
        missing = unique_components - prices_found
        
        if missing:
            print(f"ERROR: Missing prices for {len(missing)} components.")
            print(f"Sample missing: {list(missing)[:5]}")
            sys.exit(1)
        
        print("SUCCESS: All components have prices.")
        
    except Exception as e:
        print(f"An error occurred: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
