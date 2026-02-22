import sys
import os
import argparse
import subprocess
import shutil

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../scripts/utils')))
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../scripts/config')))

try:
    import aws_helpers
    from logger import init_log, write_log
    from global_config import CONFIG
except ImportError:
    print("Error importing utils/config. Ensure you are running from the correct directory or PYTHONPATH is set.")
    sys.exit(1)

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../'))
TERRAFORM_ROOT = os.path.join(PROJECT_ROOT, 'terraform')
DIST_DIR = os.path.join(PROJECT_ROOT, 'dist')

SERVICES_CONFIG = {
    "materials": {
        "project": "src/PoC.Materials/PoC.Materials.csproj",
        "zip_name": "PoC-Materials"
    },
    "costing": {
        "project": "src/PoC.Costing/PoC.Costing.csproj",
        "zip_name": "PoC-Costing"
    },
    "populator": {
        "project": "src/PoC.Populator/PoC.Populator.csproj",
        "zip_name": "PoC-Populator"
    },
    "datahelper": {
        "project": "src/PoC.DataHelper/PoC.DataHelper.csproj",
        "zip_name": "PoC-DataHelper"
    },
    "gateway": {
        # Gateway has no .NET code to build, just openapi.yaml
        "project": None,
        "zip_name": None
    }
}

def clean_build_and_zip(service_name):
    svc_info = SERVICES_CONFIG.get(service_name)
    if not svc_info or not svc_info["project"]:
        return # Nothing to build
        
    project_path = os.path.join(PROJECT_ROOT, svc_info["project"])
    publish_dir = os.path.join(DIST_DIR, f"{svc_info['zip_name']}_publish")
    zip_dir = os.path.join(DIST_DIR, svc_info['zip_name'])
    zip_path = os.path.join(zip_dir, f"{svc_info['zip_name']}.zip")
    
    write_log(f"[{service_name}] Cleaning...", "INFO")
    subprocess.check_call(["dotnet", "clean", project_path, "-c", "Release"])
    
    if os.path.exists(publish_dir):
        shutil.rmtree(publish_dir)
    if not os.path.exists(zip_dir):
        os.makedirs(zip_dir)
        
    write_log(f"[{service_name}] Publishing...", "INFO")
    subprocess.check_call([
        "dotnet", "publish", project_path,
        "-c", "Release",
        "-o", publish_dir,
        "-r", "linux-x64",
        "--no-self-contained",
        "/p:TreatWarningsAsErrors=false"
    ])
    
    write_log(f"[{service_name}] Zipping to {zip_path}...", "INFO")
    if os.path.exists(zip_path):
        os.remove(zip_path)
        
    shutil.make_archive(zip_path.replace('.zip', ''), 'zip', publish_dir)
    shutil.rmtree(publish_dir) # cleanup temp publish dir
    
    if not os.path.exists(zip_path):
        raise Exception(f"Failed to create zip at {zip_path}")
    
    write_log(f"[{service_name}] Build Artifact Created: {zip_path}", "SUCCESS")

def run_terraform(directory):
    write_log(f"Running Terraform in {directory}...", "INFO")
    
    # Use terraform directly, relying on AWS_ENDPOINT_URL for localstack
    env = os.environ.copy()
    env["AWS_ENDPOINT_URL"] = "http://localhost:4566"
    env["AWS_ACCESS_KEY_ID"] = "test"
    env["AWS_SECRET_ACCESS_KEY"] = "test"
    
    subprocess.check_call(["terraform", "init"], cwd=directory, env=env)
    subprocess.check_call(["terraform", "apply", "-auto-approve"], cwd=directory, env=env)

def main():
    parser = argparse.ArgumentParser(description="Master Deployment Script with Terraform")
    parser.add_argument("--skip-build", action="store_true", help="Skip clean/build stage")
    parser.add_argument("--service", type=str, help="Deploy a single service (e.g. materials, populator)")
    args = parser.parse_args()

    init_log("Terraform-Deploy", clean_all=True)
    write_log(">>> STARTING TERRAFORM DEPLOYMENT <<<", "INFO")
    write_log(f"Parameters: SkipBuild={args.skip_build}, Service={args.service or 'ALL'}", "INFO")

    # 1. Validation
    if not aws_helpers.validate_aws_connection():
         write_log("AWS Connection Failed. Aborting.", "ERROR")
         sys.exit(1)

    services_to_deploy = [args.service] if args.service else ["materials", "costing", "populator", "datahelper", "gateway"]

    # 2. Build Stage (with Code Review/Clean enforcement)
    if not args.skip_build:
        write_log(">>> FULL CLEAN AND BUILD STAGE <<<", "INFO")
        for svc in services_to_deploy:
            try:
                clean_build_and_zip(svc)
            except Exception as e:
                write_log(f"Build failed for {svc}: {e}", "ERROR")
                sys.exit(1)
    
    # 3. Deploy Shared Infrastructure
    shared_dir = os.path.join(TERRAFORM_ROOT, "shared")
    write_log(">>> DEPLOYING SHARED INFRASTRUCTURE <<<", "INFO")
    try:
        run_terraform(shared_dir)
    except Exception as e:
        write_log(f"Failed to deploy shared infrastructure: {e}", "ERROR")
        sys.exit(1)
        
    # 4. Deploy Services
    write_log(">>> DEPLOYING SERVICES <<<", "INFO")
    for svc in services_to_deploy:
        svc_dir = os.path.join(TERRAFORM_ROOT, "services", svc)
        if not os.path.exists(svc_dir):
            write_log(f"Terraform directory not found for {svc}: {svc_dir}", "WARN")
            continue
            
        write_log(f"--- Deploying {svc} ---", "INFO")
        try:
            run_terraform(svc_dir)
        except Exception as e:
            write_log(f"Terraform deploy failed for {svc}: {e}", "ERROR")
            sys.exit(1)
            
    write_log(">>> TERRAFORM DEPLOYMENT COMPLETED SUCCESSFULLY <<<", "SUCCESS")

if __name__ == "__main__":
    main()
