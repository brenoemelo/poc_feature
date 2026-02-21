import boto3
import sys

def list_tables():
    try:
        dynamodb = boto3.client(
            'dynamodb',
            endpoint_url='http://localhost:4566',
            region_name='us-east-1',
            aws_access_key_id='test',
            aws_secret_access_key='test'
        )
        response = dynamodb.list_tables()
        print("Tables:", response.get('TableNames'))
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    list_tables()
