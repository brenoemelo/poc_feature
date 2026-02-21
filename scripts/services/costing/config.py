import os
import sys

# Add config folder to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../config')))
from global_config import RESOURCES, CONFIG

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
from aws_helpers import AWS_REGION, AWS_ENDPOINT_URL

SERVICE_CONFIG = {
    "Name": RESOURCES["Lambda"]["Costing"]["ApiFunction"],
    "IngestionFunctionName": RESOURCES["Lambda"]["Costing"]["IngestionFunction"],
    "DynamoTable": RESOURCES["DynamoDb"]["CostingTable"],
    "IngestionQueueName": RESOURCES["Sqs"]["CostingIngestionQueue"],
    "MaterialsTopicArn": f"arn:aws:sns:{AWS_REGION}:{CONFIG['Aws']['AccountId']}:{RESOURCES['Sns']['MaterialEventsTopic']}",
    "ZipPath": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../PoC-Costing.zip")),
    "ProjectFile": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../src/PoC.Costing/PoC.Costing.csproj")),
    "CustomApiId": CONFIG["ApiGateway"]["Id"],
    "MaterialsApiUrl": f"{AWS_ENDPOINT_URL}/restapis/{CONFIG['ApiGateway']['Id']}/{CONFIG['ApiGateway']['Stage']}/_user_request_/api/v1/materials",
    "Stage": CONFIG["ApiGateway"]["Stage"],
    "Region": AWS_REGION,
    "EndpointUrl": AWS_ENDPOINT_URL
}
