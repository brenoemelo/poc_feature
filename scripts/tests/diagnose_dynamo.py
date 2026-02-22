import boto3
import json
import sys

AWS_ENDPOINT = "http://localhost:4566"
REGION = "us-east-1"

def diagnose():
    try:
        dynamodb = boto3.client('dynamodb', endpoint_url=AWS_ENDPOINT, region_name=REGION)
        print("Listing tables...")
        tables = dynamodb.list_tables()
        print(json.dumps(tables, indent=2, default=str))
        
        if 'materials-table' in tables.get('TableNames', []):
            print("\nScanning materials-table (Limit 5)...")
            scan = dynamodb.scan(TableName='materials-table', Limit=5)
            print(f"Items count: {scan['Count']}")
            print(json.dumps(scan.get('Items', []), indent=2, default=str))
        else:
            print("\nmaterials-table not found!")

    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    diagnose()
