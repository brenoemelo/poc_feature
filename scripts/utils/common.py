import shutil
import os
import sys

# Add utils to path
sys.path.append(os.path.dirname(__file__))
from logger import write_log

def assert_command(command):
    if not shutil.which(command):
        write_log(f"CRITICAL: Required command '{command}' not found.", "ERROR")
        sys.exit(1)

def assert_env_var(name):
    if not os.environ.get(name):
        write_log(f"CRITICAL: Environment variable '{name}' is missing.", "ERROR")
        sys.exit(1)
