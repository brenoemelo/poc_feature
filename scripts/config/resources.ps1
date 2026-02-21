# Centralized Resource Names
$Global:Resources = @{
    DynamoDb = @{
        MaterialsTable = "materials-table"
        CostingTable = "costing-prices-table"
    }
    Sns = @{
        MaterialEventsTopic = "material-events"
        PopulationRequestsTopic = "population-requests"
    }
    Sqs = @{
        MaterialIngestionQueue = "material-ingestion-queue"
        CostingIngestionQueue = "costing-ingestion-queue"
        PopulatorQueue = "populator-queue"
    }
    Lambda = @{
        Materials = @{
            ApiFunction = "PoC-Materials"
            IngestionFunction = "PoC-Materials-Ingestion"
        }
        Costing = @{
            ApiFunction = "PoC-Costing"
            IngestionFunction = "PoC-Costing-PriceIngestion"
        }
        Populator = @{
            ApiFunction = "PoC-Populator"
            WorkerFunction = "PoC-Populator-Worker"
        }
    }
    S3 = @{
        MaterialsDataBucket = "poc-materials-data"
        FeatureFlagsBucket = "flags"
    }
}
