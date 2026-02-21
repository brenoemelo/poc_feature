import os
import sys

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
from aws_helpers import AWS_REGION, AWS_ENDPOINT_URL

SERVICE_CONFIG = {
    "Name": "PoC-Materials",
    "DynamoTable": "Materials",
    "SnsTopic": "material-events",
    "ZipPath": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../PoC-Materials.zip")),
    "ProjectFile": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../src/PoC.Materials/PoC.Materials.csproj")),
    "CustomApiId": "material-api",
    "Stage": "prod",
    "Region": AWS_REGION,
    "EndpointUrl": AWS_ENDPOINT_URL
}
