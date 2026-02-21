import os
import sys

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
from aws_helpers import AWS_REGION, AWS_ENDPOINT_URL

SERVICE_CONFIG = {
    "Name": "PoC-DataHelper",
    "ZipPath": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../PoC-DataHelper.zip")),
    "ProjectFile": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../src/PoC.DataHelper/PoC.DataHelper.csproj")),
    "CustomApiId": "material-api",
    "Stage": "prod",
    "Region": AWS_REGION,
    "EndpointUrl": AWS_ENDPOINT_URL
}

