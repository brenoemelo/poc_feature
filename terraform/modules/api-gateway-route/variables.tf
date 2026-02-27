variable "api_id" {
  description = "The ID of the REST API"
  type        = string
}

variable "parent_id" {
  description = "The ID of the parent resource to attach the new route to"
  type        = string
}

variable "path_part" {
  description = "The new path segment (e.g., 'products')"
  type        = string
}

variable "lambda_invoke_arn" {
  description = "The ARN used to invoke the lambda function"
  type        = string
}

variable "lambda_function_name" {
  description = "The name of the lambda function (for permissions)"
  type        = string
}

variable "http_methods" {
  description = "List of HTTP methods to enable (e.g. ['GET', 'POST', 'ANY'])"
  type        = list(string)
  default     = ["ANY"]
}
