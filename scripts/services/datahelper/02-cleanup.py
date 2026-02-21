import sys
import os

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
from logger import write_log
from config import SERVICE_CONFIG


def cleanup():
    write_log("STEP 2: Cleanup (Idempotency)", "INFO")

    zip_path = SERVICE_CONFIG["ZipPath"]
    if os.path.exists(zip_path):
        write_log(f"Removing existing artifact: {zip_path}", "INFO")
        os.remove(zip_path)

    write_log("Cleanup completed.", "SUCCESS")


if __name__ == "__main__":
    cleanup()

