import os

CONFIG = {
    "Aws": {
        "Region": "us-east-1",
        "AccountId": "000000000000",
        "AccessKeyId": "test",
        "SecretAccessKey": "test",
        "LocalStackUrl": "http://localhost:4566",
        "LocalStackInternalUrl": "http://localstack:4566"
    },
    "ApiGateway": {
        "Id": "material-api",
        "Name": "Material-Formulation-API",
        "Stage": "prod",
        "UrlTemplate": "http://localhost:4566/_aws/execute-api/{api_id}/{stage}/"
    },
    "Project": {
        "Name": "PoC-Feature",
        "Environment": "Local"
    },
    "Observability": {
        "TempoUrl": "http://localhost:3200",
        "PrometheusUrl": "http://localhost:9090",
        "LokiUrl": "http://localhost:3100",
        "OtelCollectorUrl": "http://localhost:4317"
    },
    "Paths": {
        "Logs": os.path.abspath(os.path.join(os.path.dirname(__file__), "../../logs"))
    }
}

RESOURCES = {
    "DynamoDb": {
        "MaterialsTable": "materials-table",
        "CostingTable": "costing-prices-table"
    },
    "Sns": {
        "MaterialEventsTopic": "material-events",
        "PopulationRequestsTopic": "population-requests"
    },
    "Sqs": {
        "MaterialIngestionQueue": "material-ingestion-queue",
        "CostingIngestionQueue": "costing-ingestion-queue",
        "PopulatorQueue": "populator-queue"
    },
    "Lambda": {
        "Materials": {
            "ApiFunction": "PoC-Materials",
            "IngestionFunction": "PoC-Materials-Ingestion"
        },
        "Costing": {
            "ApiFunction": "PoC-Costing",
            "IngestionFunction": "PoC-Costing-PriceIngestion"
        },
        "Populator": {
            "ApiFunction": "PoC-Populator",
            "WorkerFunction": "PoC-Populator-Worker"
        },
        "DataHelper": {
            "ApiFunction": "PoC-DataHelper"
        }
    },
    "S3": {
        "MaterialsDataBucket": "poc-materials-data",
        "FeatureFlagsBucket": "flags"
    }
}
