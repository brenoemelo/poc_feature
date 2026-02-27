
import boto3
import os
import sys

# Add utils to path
sys.path.append(os.path.dirname(__file__))
import aws_helpers

try:
    apigateway = aws_helpers.get_boto3_client('apigateway')
    apis = apigateway.get_rest_apis()
    print("REST APIs:")
    for item in apis.get('items', []):
        print(f"Name: {item['name']}, ID: {item['id']}")
        
    lambda_client = aws_helpers.get_boto3_client('lambda')
    functions = lambda_client.list_functions()
    print("\nLambda Functions:")
    for func in functions.get('Functions', []):
        print(f"Name: {func['FunctionName']}")

except Exception as e:
    print(f"Error: {e}")
