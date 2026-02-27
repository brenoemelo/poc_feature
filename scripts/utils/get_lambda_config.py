
import boto3
import json
import sys

def main():
    function_name = "PoC-Populator"
    if len(sys.argv) > 1:
        function_name = sys.argv[1]

    endpoint_url = "http://localhost:4566"
    region = "us-east-1"
    
    print(f"Fetching configuration for {function_name}...")
    
    client = boto3.client(
        'lambda',
        endpoint_url=endpoint_url,
        region_name=region,
        aws_access_key_id="test",
        aws_secret_access_key="test"
    )
    
    try:
        response = client.get_function_configuration(FunctionName=function_name)
        
        print("\n--- Environment Variables ---")
        if 'Environment' in response and 'Variables' in response['Environment']:
            env_vars = response['Environment']['Variables']
            for key, value in env_vars.items():
                print(f"{key} = {value}")
        else:
            print("No environment variables found.")
            
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    main()
