
import sys

def fix_file(path):
    with open(path, 'rb') as f:
        content = f.read()
    
    if b'\r\n' in content:
        print(f"Fixing CRLF in {path}")
        content = content.replace(b'\r\n', b'\n')
        with open(path, 'wb') as f:
            f.write(content)
    else:
        print(f"No CRLF found in {path}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        fix_file(sys.argv[1])
    else:
        print("Usage: python fix_crlf.py <file>")
