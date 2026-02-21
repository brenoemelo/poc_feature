import sys
import os
import shutil
import subprocess
import argparse

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def build():
    parser = argparse.ArgumentParser(description="Build Service")
    parser.add_argument("--artifact-path", help="Path to the artifact zip file")
    args = parser.parse_args()

    aws_helpers.write_log("STEP 3: Build", "INFO")
    
    project_path = SERVICE_CONFIG['ProjectFile']
    publish_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../publish/PoC-Populator"))
    
    if args.artifact_path:
        zip_path = args.artifact_path
    else:
        zip_path = SERVICE_CONFIG['ZipPath']
    
    # 1. Clean
    aws_helpers.write_log("Cleaning .NET Project...", "INFO")
    subprocess.check_call(["dotnet", "clean", project_path, "-c", "Release"])

    if os.path.exists(publish_dir):
        shutil.rmtree(publish_dir)
    
    # 2. Publish
    aws_helpers.write_log("Publishing .NET Project...", "INFO")
    subprocess.check_call([
        "dotnet", "publish", project_path,
        "-c", "Release",
        "-o", publish_dir,
        "-r", "linux-x64",
        "--no-self-contained"
    ])
    
    # 3. Zip
    aws_helpers.write_log(f"Zipping Artifact to {zip_path}...", "INFO")
    if os.path.exists(zip_path):
        os.remove(zip_path)
        
    shutil.make_archive(zip_path.replace('.zip', ''), 'zip', publish_dir)
    
    if os.path.exists(zip_path):
        aws_helpers.write_log(f"Build Artifact Created: {zip_path}", "SUCCESS")
    else:
        aws_helpers.write_log("Zip Creation Failed", "ERROR")
        sys.exit(1)

if __name__ == "__main__":
    try:
        build()
    except Exception as e:
        aws_helpers.write_log(f"Build Failed: {e}", "ERROR")
        sys.exit(1)
