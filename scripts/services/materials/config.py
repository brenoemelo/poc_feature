import os
import sys

# Add config folder to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../config')))
from global_config import RESOURCES, CONFIG

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
from aws_helpers import AWS_REGION, AWS_ENDPOINT_URL

SERVICE_CONFIG = {
    "Name": RESOURCES["Lambda"]["Materials"]["ApiFunction"],
    "IngestionFunctionName": RESOURCES["Lambda"]["Materials"]["IngestionFunction"],
    "IngestionQueueName": RESOURCES["Sqs"]["MaterialIngestionQueue"],
    "DynamoTable": RESOURCES["DynamoDb"]["MaterialsTable"],
    "SnsTopic": RESOURCES["Sns"]["MaterialEventsTopic"],
    "ZipPath": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../PoC-Materials.zip")),
    "ProjectFile": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../src/PoC.Materials/PoC.Materials.csproj")),
    "CustomApiId": CONFIG["ApiGateway"]["Id"],
    "Stage": CONFIG["ApiGateway"]["Stage"],
    "Region": AWS_REGION,
    "EndpointUrl": AWS_ENDPOINT_URL
}
