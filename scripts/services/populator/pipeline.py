import sys
import os
import argparse
import subprocess
import datetime
import glob

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
from logger import init_log, write_log

def run_pipeline():
    # Parse arguments
    parser = argparse.ArgumentParser(description="Service Pipeline")
    parser.add_argument("--skip-build", action="store_true", help="Skip build stage")
    parser.add_argument("--skip-test", action="store_true", help="Skip test stage")
    args = parser.parse_args()

    # Setup
    script_dir = os.path.dirname(os.path.abspath(__file__))
    service_name = os.path.basename(script_dir)
    
    # Artifacts Setup
    artifacts_dir = os.path.abspath(os.path.join(script_dir, "../../../deployment/artifacts"))
    if not os.path.exists(artifacts_dir):
        os.makedirs(artifacts_dir)

    artifact_path = ""
    if not args.skip_build:
        timestamp = datetime.datetime.now().strftime("%Y%m%d%H%M%S")
        # Service name is 'populator' (directory name), but artifact usually matches service config name.
        # Let's use 'PoC-Populator' to match ps1 script, or derive it.
        # ps1 used: $ServiceName = $ServiceConfig.Name which is "PoC-Populator"
        # In python config.py (not shown but inferred), it likely has 'PoC-Populator' or similar.
        # Let's assume 'PoC-Populator' based on ps1 behavior.
        artifact_name = f"PoC-Populator_{timestamp}.zip"
        artifact_path = os.path.join(artifacts_dir, artifact_name)
    else:
        # Find latest artifact
        search_pattern = os.path.join(artifacts_dir, "PoC-Populator_*.zip")
        files = glob.glob(search_pattern)
        if not files:
            print(f"No artifact found in {artifacts_dir}")
            sys.exit(1)
        # Sort by modification time
        artifact_path = max(files, key=os.path.getmtime)
        print(f"Skipping Build. Using latest artifact: {artifact_path}")

    # Initialize Logger
    init_log(service_name)
    write_log(f">>> STARTING PIPELINE: {service_name} <<<", "INFO")
    
    if artifact_path:
        write_log(f"Target Artifact: {artifact_path}", "INFO")
    
    python_exe = sys.executable

    try:
        # 1. Validate
        validate_script = os.path.join(script_dir, "01-validate.py")
        if os.path.exists(validate_script):
            write_log("Running Validation...", "INFO")
            subprocess.check_call([python_exe, validate_script])
        
        # 2. Cleanup
        cleanup_script = os.path.join(script_dir, "02-cleanup.py")
        if os.path.exists(cleanup_script):
            write_log("Running Cleanup...", "INFO")
            subprocess.check_call([python_exe, cleanup_script])
        
        # 3. Build
        build_script = os.path.join(script_dir, "03-build.py")
        if os.path.exists(build_script):
            if not args.skip_build:
                write_log("Running Build...", "INFO")
                subprocess.check_call([python_exe, build_script, "--artifact-path", artifact_path])
            else:
                write_log("Build Stage Skipped.", "WARN")
        else:
             write_log("No Build Script Found. Skipping.", "INFO")
            
        # 4. Deploy
        deploy_script = os.path.join(script_dir, "04-deploy.py")
        if os.path.exists(deploy_script):
            write_log("Running Deploy...", "INFO")
            subprocess.check_call([python_exe, deploy_script, "--artifact-path", artifact_path])
        
        # 5. Test
        test_script = os.path.join(script_dir, "05-test.py")
        if os.path.exists(test_script):
            if not args.skip_test:
                write_log("Running Tests...", "INFO")
                subprocess.check_call([python_exe, test_script])
            else:
                write_log("Test Stage Skipped.", "WARN")
        
        write_log(">>> PIPELINE COMPLETED SUCCESSFULLY <<<", "SUCCESS")
        sys.exit(0)
        
    except subprocess.CalledProcessError as e:
        write_log(">>> PIPELINE FAILED <<<", "ERROR")
        write_log(f"Command failed with exit code {e.returncode}", "ERROR")
        sys.exit(1)
    except Exception as e:
        write_log(">>> PIPELINE FAILED <<<", "ERROR")
        write_log(str(e), "ERROR")
        sys.exit(1)

if __name__ == "__main__":
    run_pipeline()
