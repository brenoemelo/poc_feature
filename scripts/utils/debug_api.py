
import boto3
import os

# Set environment variables for LocalStack
os.environ['AWS_ACCESS_KEY_ID'] = 'test'
os.environ['AWS_SECRET_ACCESS_KEY'] = 'test'
os.environ['AWS_DEFAULT_REGION'] = 'us-east-1'

endpoint_url = "http://localhost:4566"

try:
    apigateway = boto3.client('apigateway', endpoint_url=endpoint_url)
    apis = apigateway.get_rest_apis()
    print("REST APIs:")
    for item in apis.get('items', []):
        print(f"Name: {item['name']}, ID: {item['id']}")
        
    lambda_client = boto3.client('lambda', endpoint_url=endpoint_url)
    functions = lambda_client.list_functions()
    print("\nLambda Functions:")
    for func in functions.get('Functions', []):
        print(f"Name: {func['FunctionName']}")

except Exception as e:
    print(f"Error: {e}")
