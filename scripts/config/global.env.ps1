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
    }
    ApiGateway = @{
        Id = $ApiId
        Stage = "prod"
    }
    Project = @{
        Name = "PoC-Feature"
        Environment = "Local"
    }
    Paths = @{
        Logs = "$PSScriptRoot/../../logs"
    }
}
