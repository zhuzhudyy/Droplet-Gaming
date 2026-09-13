"""Run the installed Blender in a separate background process, preserving UI sessions."""
import argparse
from pathlib import Path
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--stage', choices=('calibration', 'source', 'full', 'layout'), required=True)
parser.add_argument('--blender', default=r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe')
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
log = root/'ArtSource'/'Blender'/(args.stage+'_generation.log')
with log.open('wb') as output:
    result = subprocess.run([args.blender, '--background', '--factory-startup', '--python',
                             str(Path(__file__).with_name('build_fleet_assets.py')),
                             '--', '--stage', args.stage], cwd=root, stdout=output,
                            stderr=subprocess.STDOUT, check=False)
print(f'Blender process exit code: {result.returncode}. Full log: {log}')
sys.exit(result.returncode)
