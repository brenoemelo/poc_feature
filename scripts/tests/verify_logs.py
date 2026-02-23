import subprocess
import time
import sys

def check_logs():
    print("Checking LocalStack logs for MaterialIngestion errors...")
    cmd = ["docker", "logs", "localstack"]
    try:
        # Get only the last 2000 lines to avoid massive output
        result = subprocess.run(cmd, capture_output=True, text=True, errors='replace')
        logs = result.stdout
        
        # Look for the specific error message
        error_msg = "has null material"
        context_msg = "[MaterialIngestion]"
        
        found_errors = []
        for line in logs.splitlines():
            if context_msg in line and error_msg in line:
                found_errors.append(line)
        
        if found_errors:
            print(f"FAILURE: Found {len(found_errors)} null material errors:")
            for err in found_errors[-5:]: # Show last 5
                print(f"  {err}")
            return False
        else:
            print("SUCCESS: No null material errors found in logs.")
            return True
            
    except Exception as e:
        print(f"Error checking logs: {e}")
        return False

if __name__ == "__main__":
    success = check_logs()
    sys.exit(0 if success else 1)
