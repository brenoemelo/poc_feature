
import boto3
import os
import json

def main():
    endpoint_url = "http://localhost:4566"
    region = "us-east-1"
    
    print(f"Checking resources at {endpoint_url} in {region}...")
    
    try:
        lambda_client = boto3.client(
            'lambda',
            endpoint_url=endpoint_url,
            region_name=region,
            aws_access_key_id="test",
            aws_secret_access_key="test"
        )
        
        functions = lambda_client.list_functions()
        print("\n--- Lambda Functions ---")
        if 'Functions' in functions:
            for f in functions['Functions']:
                print(f"- {f['FunctionName']} ({f['Runtime']})")
        else:
            print("No functions found.")
            
        apigateway = boto3.client(
            'apigateway',
            endpoint_url=endpoint_url,
            region_name=region,
            aws_access_key_id="test",
            aws_secret_access_key="test"
        )
        
        apis = apigateway.get_rest_apis()
        print("\n--- API Gateways ---")
        if 'items' in apis:
            for api in apis['items']:
                print(f"- {api['name']} (ID: {api['id']})")
        else:
            print("No APIs found.")
            
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    main()
