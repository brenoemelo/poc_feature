import boto3
import sys

AWS_ENDPOINT = "http://localhost:4566"
REGION = "us-east-1"

def check_status():
    print("Checking AWS Resources...")
    
    # Lambda
    lambda_client = boto3.client('lambda', endpoint_url=AWS_ENDPOINT, region_name=REGION)
    functions = lambda_client.list_functions()
    print("Lambda Functions:")
    for f in functions.get('Functions', []):
        print(f" - {f['FunctionName']}")
        
    # SQS
    sqs = boto3.client('sqs', endpoint_url=AWS_ENDPOINT, region_name=REGION)
    queues = sqs.list_queues()
    print("\nSQS Queues:")
    for q_url in queues.get('QueueUrls', []):
        attrs = sqs.get_queue_attributes(QueueUrl=q_url, AttributeNames=['ApproximateNumberOfMessages', 'ApproximateNumberOfMessagesNotVisible'])
        print(f" - {q_url}: {attrs.get('Attributes')}")
        
    # DynamoDB
    dynamo = boto3.resource('dynamodb', endpoint_url=AWS_ENDPOINT, region_name=REGION)
    for table_name in ['materials-table', 'costing-prices-table']:
        table = dynamo.Table(table_name)
        try:
            count = table.item_count
            # item_count is updated every 6 hours, so scan is better for small tables
            scan = table.scan(Select='COUNT')
            print(f"\nDynamoDB Table: {table_name}")
            print(f" - ItemCount (metadata): {count}")
            print(f" - Scanned Count: {scan['Count']}")
        except Exception as e:
            print(f"\nDynamoDB Table: {table_name} - Error: {e}")

    # Logs
    logs = boto3.client('logs', endpoint_url=AWS_ENDPOINT, region_name=REGION)
    print("\nLog Groups:")
    groups = logs.describe_log_groups()
    for g in groups.get('logGroups', []):
        print(f" - {g['logGroupName']}")
        streams = logs.describe_log_streams(logGroupName=g['logGroupName'], orderBy='LastEventTime', descending=True, limit=1)
        if streams.get('logStreams'):
            stream_name = streams['logStreams'][0]['logStreamName']
            print(f"   Last Stream: {stream_name}")
            events = logs.get_log_events(logGroupName=g['logGroupName'], logStreamName=stream_name, limit=5)
            for e in events.get('events', []):
                print(f"     [{e['timestamp']}] {e['message'].strip()}")

if __name__ == "__main__":
    check_status()
