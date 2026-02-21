import os
import sys

# Add config folder to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../config')))
from global_config import RESOURCES, CONFIG

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
from aws_helpers import AWS_REGION, AWS_ENDPOINT_URL

SERVICE_CONFIG = {
    "Name": RESOURCES["Lambda"]["Populator"]["ApiFunction"],
    "WorkerFunctionName": RESOURCES["Lambda"]["Populator"]["WorkerFunction"],
    "QueueName": RESOURCES["Sqs"]["PopulatorQueue"],
    "SnsTopic": RESOURCES["Sns"]["PopulationRequestsTopic"],
    "OutputSnsTopic": RESOURCES["Sns"]["MaterialEventsTopic"],
    "MaterialsTableName": RESOURCES["DynamoDb"]["MaterialsTable"],
    "PricesTableName": RESOURCES["DynamoDb"]["CostingTable"],
    "ZipPath": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../PoC-Populator.zip")),
    "ProjectFile": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../src/PoC.Populator/PoC.Populator.csproj")),
    "CustomApiId": CONFIG["ApiGateway"]["Id"],
    "MaterialsApiUrl": f"{AWS_ENDPOINT_URL}/restapis/{CONFIG['ApiGateway']['Id']}/{CONFIG['ApiGateway']['Stage']}/_user_request_/api/v1/materials",
    "Stage": CONFIG["ApiGateway"]["Stage"],
    "Region": AWS_REGION,
    "EndpointUrl": AWS_ENDPOINT_URL
}
