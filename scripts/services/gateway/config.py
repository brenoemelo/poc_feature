import os
import sys

# Add utils and config to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../config')))

from aws_helpers import AWS_REGION, AWS_ENDPOINT_URL
from global_config import RESOURCES

SERVICE_CONFIG = {
    "Name": "PoC-Gateway",
    "ApiName": "Material-Formulation-API",
    "CustomApiId": "material-api",
    "Stage": "prod",
    "Region": AWS_REGION,
    "EndpointUrl": AWS_ENDPOINT_URL,
    "OpenApiPath": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../docs/openapi.yaml")),
    "LambdaFunctions": {
        "Materials": RESOURCES["Lambda"]["Materials"]["ApiFunction"],
        "Costing": RESOURCES["Lambda"]["Costing"]["ApiFunction"],
        "Populator": RESOURCES["Lambda"]["Populator"]["ApiFunction"],
        "DataHelper": RESOURCES["Lambda"]["DataHelper"]["ApiFunction"]
    }
}
