import sys
import os
import argparse

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG


def deploy():
    parser = argparse.ArgumentParser(description="Deploy DataHelper Service")
    parser.add_argument("--artifact-path", help="Path to the artifact zip file")
    args = parser.parse_args()

    aws_helpers.write_log("STEP 4: Deploy DataHelper Service", "INFO")

    if args.artifact_path:
        zip_path = args.artifact_path
    else:
        zip_path = SERVICE_CONFIG["ZipPath"]

    if not os.path.exists(zip_path):
        raise Exception(f"Build artifact not found: {zip_path}")

    aws_helpers.write_log(f"Using Artifact: {zip_path}", "INFO")

    common_env = aws_helpers.get_common_env_vars()
    env_vars = {
        "OTEL_SERVICE_NAME": SERVICE_CONFIG["Name"],
        **common_env,
    }

    aws_helpers.ensure_lambda_function(
        name=SERVICE_CONFIG["Name"],
        handler="PoC.DataHelper",
        role_arn="arn:aws:iam::000000000000:role/lambda-role",
        zip_path=zip_path,
        timeout=30,
        memory_size=512,
        env_vars=env_vars,
    )

    aws_helpers.grant_lambda_permission(
        function_name=SERVICE_CONFIG["Name"],
        statement_id="apigateway-invoke",
        principal="apigateway.amazonaws.com",
        source_arn=f"arn:aws:execute-api:us-east-1:000000000000:{SERVICE_CONFIG['CustomApiId']}/*/*/*",
    )

    aws_helpers.write_log("Deployment Successful.", "SUCCESS")


if __name__ == "__main__":
    try:
        deploy()
    except Exception as e:
        aws_helpers.write_log(f"Deployment Failed: {e}", "ERROR")
        sys.exit(1)

