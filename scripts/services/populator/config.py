import os
import sys

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
from aws_helpers import AWS_REGION, AWS_ENDPOINT_URL

SERVICE_CONFIG = {
    "Name": "PoC-Populator",
    "WorkerFunctionName": "PoC-Populator-Worker",
    "QueueName": "populator-queue",
    "SnsTopic": "population-requests",
    "ZipPath": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../PoC-Populator.zip")),
    "ProjectFile": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../src/PoC.Populator/PoC.Populator.csproj")),
    "CustomApiId": "material-api",
    "MaterialsApiUrl": f"{AWS_ENDPOINT_URL}/restapis/material-api/prod/", 
    "Stage": "prod",
    "Region": AWS_REGION,
    "EndpointUrl": AWS_ENDPOINT_URL
}
