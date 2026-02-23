variable "service_name" {
  description = "Name of the lambda service"
  type        = string
}

variable "topic_name" {
  description = "Name of the SNS topic"
  type        = string
}

variable "queue_name" {
  description = "Name of the SQS queue"
  type        = string
}

variable "runtime" {
  description = "Lambda runtime"
  type        = string
  default     = "dotnet8"
}

variable "handler" {
  description = "Lambda handler"
  type        = string
}

variable "timeout" {
  description = "Lambda timeout in seconds"
  type        = number
  default     = 30
}

variable "memory_size" {
  description = "Lambda memory size in MB"
  type        = number
  default     = 256
}

variable "zip_path" {
  description = "Path to the lambda deployment zip file"
  type        = string
}

variable "lambda_role_arn" {
  description = "ARN of the IAM role for the lambda"
  type        = string
}

variable "environment_variables" {
  description = "Map of environment variables for the lambda"
  type        = map(string)
  default     = {}
}

variable "batch_size" {
  description = "Batch size for SQS event source mapping"
  type        = number
  default     = 10
}

variable "filter_policy" {
  description = "JSON filter policy for SNS subscription"
  type        = string
  default     = null
}
