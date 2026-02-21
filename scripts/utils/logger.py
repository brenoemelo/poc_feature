import logging
import os
import sys
import datetime
import glob

# Global logger setup
_logger = logging.getLogger("PoC_Logger")
_logger.setLevel(logging.INFO)

# Avoid adding multiple handlers if already configured
if not _logger.handlers:
    ch = logging.StreamHandler(sys.stdout)
    ch.setLevel(logging.INFO)
    formatter = logging.Formatter('[%(asctime)s] [%(levelname)s] %(message)s', datefmt='%Y-%m-%d %H:%M:%S')
    ch.setFormatter(formatter)
    _logger.addHandler(ch)

def init_log(service_name, clean_all=False):
    global _logger
    
    # Setup log directory
    # Assuming this script is in scripts/utils/logger.py
    log_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../logs"))
    if not os.path.exists(log_dir):
        os.makedirs(log_dir)

    # Clean logs
    if clean_all:
        print(f"Cleaning all logs in {log_dir}...")
        for f in glob.glob(os.path.join(log_dir, "*.log")):
            try:
                os.remove(f)
            except:
                print(f"Could not delete {f}")
    else:
        # Auto-clean older than 7 days
        cutoff = datetime.datetime.now() - datetime.timedelta(days=7)
        for f in glob.glob(os.path.join(log_dir, "*.log")):
            if os.path.getmtime(f) < cutoff.timestamp():
                try:
                    os.remove(f)
                except:
                    pass

    # Create log file
    timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    log_file = os.path.join(log_dir, f"{service_name}_pipeline_{timestamp}.log")
    
    print(f"Logging to: {log_file}")
    
    # File Handler
    fh = logging.FileHandler(log_file)
    fh.setLevel(logging.INFO)
    formatter = logging.Formatter('[%(asctime)s] [%(levelname)s] %(message)s', datefmt='%Y-%m-%d %H:%M:%S')
    fh.setFormatter(formatter)
    _logger.addHandler(fh)
    
    return log_file

def write_log(message, level="INFO"):
    if level == "INFO":
        _logger.info(message)
    elif level == "WARN":
        _logger.warning(message)
    elif level == "ERROR":
        _logger.error(message)
    elif level == "SUCCESS":
        _logger.info(f"SUCCESS: {message}")
