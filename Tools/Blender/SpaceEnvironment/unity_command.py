"""Invoke installed Unity CLI with exact argv (Windows PowerShell 5 quoting is lossy)."""
import os
import subprocess
import sys
from pathlib import Path

cli = Path(os.environ['LOCALAPPDATA']) / 'Unity/bin/unity.exe'
args = sys.argv[1:]
if args and args[0] == 'eval-file':
    args = ['command', 'eval', '--code', Path(args[1]).read_text(encoding='utf-8-sig'), '--json']
result = subprocess.run([str(cli), *args], encoding='utf-8', errors='replace', capture_output=True)
sys.stdout.buffer.write(result.stdout.encode('utf-8'))
sys.stderr.buffer.write(result.stderr.encode('utf-8'))
sys.exit(result.returncode)
