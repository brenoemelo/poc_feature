import os
import sys

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
from aws_helpers import AWS_REGION, AWS_ENDPOINT_URL

SERVICE_CONFIG = {
    "Name": "PoC-Costing",
    "IngestionFunctionName": "PoC-Costing-Ingestion",
    "DynamoTable": "ComponentPrices",
    "IngestionQueueName": "costing-ingestion-queue",
    "MaterialsTopicArn": f"arn:aws:sns:{AWS_REGION}:000000000000:material-events",
    "ZipPath": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../PoC-Costing.zip")),
    "ProjectFile": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../src/PoC.Costing/PoC.Costing.csproj")),
    "CustomApiId": "material-api",
    "MaterialsApiUrl": f"{AWS_ENDPOINT_URL}/restapis/material-api/prod/_user_request_/api/v1/materials",
    "Stage": "prod",
    "Region": AWS_REGION,
    "EndpointUrl": AWS_ENDPOINT_URL
}
