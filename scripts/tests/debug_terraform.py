import subprocess
import os
import sys

# Add config to path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../config')))
from global_config import CONFIG

# Setup environment
PROJECT_ROOT = os.path.abspath(os.path.join(os.getcwd()))
BIN_DIR = os.path.join(PROJECT_ROOT, '.bin')
TERRAFORM_EXE = os.path.join(BIN_DIR, 'terraform.exe')
SHARED_DIR = os.path.join(PROJECT_ROOT, 'terraform', 'shared')

print(f"Project Root: {PROJECT_ROOT}")
print(f"Terraform Exe: {TERRAFORM_EXE}")
print(f"Shared Dir: {SHARED_DIR}")

if not os.path.exists(TERRAFORM_EXE):
    print("Terraform executable not found!")
    sys.exit(1)

os.environ["PATH"] = BIN_DIR + os.pathsep + os.environ["PATH"]

env = os.environ.copy()
env["AWS_ACCESS_KEY_ID"] = CONFIG["Aws"].get("AccessKeyId", "test")
env["AWS_SECRET_ACCESS_KEY"] = CONFIG["Aws"].get("SecretAccessKey", "test")
env["AWS_DEFAULT_REGION"] = CONFIG["Aws"].get("Region", "us-east-1")
env["AWS_ENDPOINT_URL"] = CONFIG["Aws"].get("LocalStackUrl", "http://localhost:4566")

print("Running terraform init...")
try:
    subprocess.run([TERRAFORM_EXE, "init"], cwd=SHARED_DIR, env=env, check=True)
    print("Init successful.")
except Exception as e:
    print(f"Init failed: {e}")
    sys.exit(1)

print("Running terraform apply...")
try:
    subprocess.run([TERRAFORM_EXE, "apply", "-auto-approve"], cwd=SHARED_DIR, env=env, check=True)
    print("Apply successful.")
except Exception as e:
    print(f"Apply failed: {e}")
    sys.exit(1)
