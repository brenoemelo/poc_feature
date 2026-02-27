# Global Environment Variables
# Use static ID for LocalStack stability (mimics prod environment with known IDs or custom domains)
$ApiId = "material-api"

# Load Resources Config
. "$PSScriptRoot/resources.ps1"

$Global:Config = @{
    Aws = @{
        Region = "us-east-1"
        AccountId = "000000000000"
        LocalStackUrl = "http://localhost:4566"
        LocalStackInternalUrl = "http://localstack:4566"
    }
    ApiGateway = @{
        Id = $ApiId
        Stage = "prod"
        UrlTemplate = "http://localhost:4566/_aws/execute-api/{api_id}/{stage}/"
    }
    Project = @{
        Name = "PoC-Feature"
        Environment = "Local"
    }
    Observability = @{
        TempoUrl = "http://localhost:3200"
        PrometheusUrl = "http://localhost:9090"
        LokiUrl = "http://localhost:3100"
        OtelCollectorUrl = "http://localhost:4317"
    }
    Paths = @{
        Logs = "$PSScriptRoot/../../logs"
    }
}
