terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region                      = "us-east-1"
  # Authentication is mocked in LocalStack, but variables are required
  access_key                  = "test"
  secret_key                  = "test"

  # We use tflocal, so we don't need to specify endpoints here explicitly.
  # tflocal takes care of automatically mapping requests to localhost:4566.
}
