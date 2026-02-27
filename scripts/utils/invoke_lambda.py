
import boto3
import json
import base64

def main():
    function_name = "PoC-Populator"
    endpoint_url = "http://localhost:4566"
    region = "us-east-1"
    
    client = boto3.client(
        'lambda',
        endpoint_url=endpoint_url,
        region_name=region,
        aws_access_key_id="test",
        aws_secret_access_key="test"
    )
    
    # Construct a valid API Gateway Proxy Request payload
    payload = {
        "resource": "/api/v1/populator/jobs",
        "path": "/api/v1/populator/jobs",
        "httpMethod": "POST",
        "headers": {
            "Content-Type": "application/json"
        },
        "body": json.dumps({
            "target": "materials",
            "count": 5,
            "min_components": 10,
            "max_components": 5 # Invalid request to trigger validation
        }),
        "isBase64Encoded": False
    }
    
    print(f"Invoking {function_name}...")
    try:
        response = client.invoke(
            FunctionName=function_name,
            InvocationType='RequestResponse',
            Payload=json.dumps(payload)
        )
        
        payload_stream = response['Payload']
        response_payload = payload_stream.read().decode('utf-8')
        
        print("\n--- Response Payload ---")
        print(response_payload)
        
        if 'FunctionError' in response:
            print(f"\nFunction Error: {response['FunctionError']}")
            
        # Try to parse the response payload as JSON if possible
        try:
            json_resp = json.loads(response_payload)
            print("\n--- Parsed Body ---")
            if 'body' in json_resp:
                print(json_resp['body'])
            else:
                print(json.dumps(json_resp, indent=2))
        except:
            pass
            
    except Exception as e:
        print(f"Error invoking lambda: {e}")

if __name__ == "__main__":
    main()
