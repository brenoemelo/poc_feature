import sys
import os
import argparse
import subprocess
import time

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../scripts/utils')))
try:
    import aws_helpers
    from logger import init_log, write_log
except ImportError:
    # Fallback if running from a different directory
    print("Error importing utils. Ensure you are running from the correct directory or PYTHONPATH is set.")
    sys.exit(1)

def main():
    parser = argparse.ArgumentParser(description="Master Deployment Script (Python)")
    parser.add_argument("--skip-build", action="store_true", help="Skip build stage")
    parser.add_argument("--skip-test", action="store_true", help="Skip test stage")
    args = parser.parse_args()

    # Initialize Logging
    init_log("Master-Deploy", clean_all=True)
    write_log(">>> STARTING MASTER DEPLOYMENT <<<", "INFO")
    write_log(f"Parameters: SkipBuild={args.skip_build} SkipTest={args.skip_test}", "INFO")

    # 1. Validation
    write_log("STEP 1: Global Validation", "INFO")
    if not aws_helpers.validate_aws_connection():
         write_log("AWS Connection Failed. Aborting.", "ERROR")
         sys.exit(1)
    
    # 2. Ensure API Gateway Exists (Shared Resource)
    write_log("STEP 1.1: Ensure API Gateway Exists", "INFO")
    # Using static ID as per project convention
    api_name = "Material-Formulation-API"
    api_id_static = "material-api"
    
    api_id = aws_helpers.ensure_api_gateway(api_name, api_id_static)
    write_log(f"API Gateway ID: {api_id}", "INFO")

    # 3. Define Services
    # Order: Materials -> Costing -> Populator -> Gateway
    # Gateway is last to update the OpenAPI definition with all routes
    services = [
        "materials",
        "costing",
        "populator",
        "gateway",
        "datahelper"
    ]
    
    script_root = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../scripts/services'))

    for service in services:
        write_log("-" * 50, "INFO")
        write_log(f"Deploying Service: {service}", "INFO")
        write_log("-" * 50, "INFO")
        
        pipeline_script = os.path.join(script_root, service, "pipeline.py")
        
        if not os.path.exists(pipeline_script):
            write_log(f"Pipeline script not found for {service}: {pipeline_script}", "ERROR")
            sys.exit(1)
            
        cmd = [sys.executable, pipeline_script]
        if args.skip_build:
            cmd.append("--skip-build")
        
        if args.skip_test:
            cmd.append("--skip-test")
            
        write_log(f"Executing: {' '.join(cmd)}", "INFO")
        
        try:
            # Run in subprocess
            # We don't need shell=True for list args, which is safer
            subprocess.check_call(cmd)
        except subprocess.CalledProcessError as e:
            write_log(f"Pipeline for {service} failed with exit code {e.returncode}", "ERROR")
            sys.exit(1)
            
    if not args.skip_test:
        write_log("-" * 50, "INFO")
        write_log("Running Smoke Tests for All Services", "INFO")
        write_log("-" * 50, "INFO")
        
        for service in services:
            write_log(f"Testing Service: {service}", "INFO")
            test_script = os.path.join(script_root, service, "05-test.py")
            
            if os.path.exists(test_script):
                try:
                    subprocess.check_call([sys.executable, test_script])
                except subprocess.CalledProcessError as e:
                    write_log(f"Smoke Test for {service} failed with exit code {e.returncode}", "ERROR")
                    sys.exit(1)
            else:
                 write_log(f"No test script for {service}", "WARN")

    write_log(">>> MASTER DEPLOYMENT COMPLETED SUCCESSFULLY <<<", "SUCCESS")

if __name__ == "__main__":
    main()
