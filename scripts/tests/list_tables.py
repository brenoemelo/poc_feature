import boto3
import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../utils')))
import aws_helpers

def list_tables():
    try:
        dynamodb = aws_helpers.get_boto3_client('dynamodb')
        response = dynamodb.list_tables()
        print("Tables:", response.get('TableNames'))
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    list_tables()
