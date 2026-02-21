import sys
import os

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
from logger import write_log
from config import SERVICE_CONFIG


def validate():
    write_log("STEP 1: Validation", "INFO")

    project_file = SERVICE_CONFIG["ProjectFile"]
    if not os.path.exists(project_file):
        write_log(f"Project file not found: {project_file}", "ERROR")
        sys.exit(1)

    write_log("Validation successful.", "SUCCESS")


if __name__ == "__main__":
    validate()

