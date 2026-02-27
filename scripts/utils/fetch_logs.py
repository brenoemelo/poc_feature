
import boto3
import sys
import time

def main():
    log_group_name = "/aws/lambda/PoC-Populator"
    if len(sys.argv) > 1:
        log_group_name = sys.argv[1]

    endpoint_url = "http://localhost:4566"
    region = "us-east-1"
    
    print(f"Fetching logs for {log_group_name} from {endpoint_url}...")
    
    client = boto3.client(
        'logs',
        endpoint_url=endpoint_url,
        region_name=region,
        aws_access_key_id="test",
        aws_secret_access_key="test"
    )
    
    try:
        response = client.filter_log_events(
            logGroupName=log_group_name,
            startTime=int((time.time() - 3600) * 1000), # Last hour
            limit=50
        )
        
        events = response.get('events', [])
        if not events:
            print("No log events found.")
        else:
            for event in events:
                print(f"[{event['timestamp']}] {event['message'].strip()}")
                
    except client.exceptions.ResourceNotFoundException:
        print(f"Log group {log_group_name} not found.")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    main()
