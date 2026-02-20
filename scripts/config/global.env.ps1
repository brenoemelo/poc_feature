# Global Environment Variables
$Global:Config = @{
    Aws = @{
        Region = "us-east-1"
        AccountId = "000000000000"
        LocalStackUrl = "http://localhost:4566"
    }
    ApiGateway = @{
        Id = "material-api"
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
