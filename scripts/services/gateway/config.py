import os
import sys

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
from aws_helpers import AWS_REGION, AWS_ENDPOINT_URL

SERVICE_CONFIG = {
    "Name": "PoC-Gateway",
    "ApiName": "Material-Formulation-API",
    "CustomApiId": "material-api",
    "Stage": "prod",
    "Region": AWS_REGION,
    "EndpointUrl": AWS_ENDPOINT_URL,
    "OpenApiPath": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../docs/openapi.yaml"))
}
