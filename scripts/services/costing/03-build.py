import sys
import os
import shutil
import subprocess

# Add utils to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../utils')))
import aws_helpers
from config import SERVICE_CONFIG

def build():
    aws_helpers.write_log("STEP 3: Build", "INFO")
    
    project_path = SERVICE_CONFIG['ProjectFile']
    publish_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../publish/PoC-Costing"))
    zip_path = SERVICE_CONFIG['ZipPath']
    
    # 1. Clean
    aws_helpers.write_log("Cleaning .NET Project...", "INFO")
    
    # Force delete bin/obj folders to ensure clean build
    bin_dir = os.path.join(os.path.dirname(project_path), "bin")
    obj_dir = os.path.join(os.path.dirname(project_path), "obj")
    if os.path.exists(bin_dir):
        shutil.rmtree(bin_dir)
    if os.path.exists(obj_dir):
        shutil.rmtree(obj_dir)

    # Clean dependent projects to avoid DLL conflicts (AWS SDK V3 vs V4)
    shared_path = os.path.abspath(os.path.join(os.path.dirname(project_path), "../PoC.Shared/PoC.Shared.csproj"))
    shared_bin = os.path.join(os.path.dirname(shared_path), "bin")
    shared_obj = os.path.join(os.path.dirname(shared_path), "obj")
    if os.path.exists(shared_bin): shutil.rmtree(shared_bin)
    if os.path.exists(shared_obj): shutil.rmtree(shared_obj)

    ff_path = os.path.abspath(os.path.join(os.path.dirname(project_path), "../PoC.FeatureFlags/PoC.FeatureFlags.csproj"))
    ff_bin = os.path.join(os.path.dirname(ff_path), "bin")
    ff_obj = os.path.join(os.path.dirname(ff_path), "obj")
    if os.path.exists(ff_bin): shutil.rmtree(ff_bin)
    if os.path.exists(ff_obj): shutil.rmtree(ff_obj)

    shared_infra_path = os.path.abspath(os.path.join(os.path.dirname(project_path), "../PoC.Shared.Infrastructure/PoC.Shared.Infrastructure.csproj"))
    shared_infra_bin = os.path.join(os.path.dirname(shared_infra_path), "bin")
    shared_infra_obj = os.path.join(os.path.dirname(shared_infra_path), "obj")
    if os.path.exists(shared_infra_bin): shutil.rmtree(shared_infra_bin)
    if os.path.exists(shared_infra_obj): shutil.rmtree(shared_infra_obj)

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
