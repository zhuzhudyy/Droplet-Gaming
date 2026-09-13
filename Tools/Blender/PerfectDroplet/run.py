"""Run the geometry-only pipeline in isolated Blender processes; never open Unity."""
from pathlib import Path
import argparse
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
parser = argparse.ArgumentParser()
parser.add_argument('phase', choices=['build', 'inspect', 'render', 'export', 'roundtrip'])
args = parser.parse_args()
evidence = ROOT / 'docs/verification/PerfectDroplet'
evidence.mkdir(parents=True, exist_ok=True)
command = [r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe',
           '--background', '--factory-startup', '--python-exit-code', '1',
           '--python', str(Path(__file__).with_name('pipeline.py')), '--', args.phase]
with (evidence / f'{args.phase}.log').open('wb') as log:
    result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
print(f'{args.phase}: exit {result.returncode}; {evidence / (args.phase + ".log")}')
sys.exit(result.returncode)
