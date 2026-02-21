import sys
import os

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers

def validate():
    aws_helpers.write_log("STEP 1: Validation", "INFO")
    if aws_helpers.validate_aws_connection():
        sys.exit(0)
    else:
        sys.exit(1)

if __name__ == "__main__":
    validate()
