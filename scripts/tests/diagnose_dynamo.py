import boto3
import json
import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../utils')))
import aws_helpers

def diagnose():
    try:
        dynamodb = aws_helpers.get_boto3_client('dynamodb')
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
