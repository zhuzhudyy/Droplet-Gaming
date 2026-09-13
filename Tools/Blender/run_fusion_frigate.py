"""Argument-safe runner. Never invokes Unity or replaces an existing UI session."""
import argparse
from pathlib import Path
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument('phase', choices=['build', 'render', 'inspect', 'export', 'roundtrip', 'verify-export', 'details'])
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
folder = root / 'docs/verification/FusionFrigate'
folder.mkdir(parents=True, exist_ok=True)
script, extra = {
    'build': ('build_fusion_frigate.py', ['--phase', 'build']),
    'render': ('build_fusion_frigate.py', ['--phase', 'render']),
    'inspect': ('inspect_fusion_frigate.py', ['--source']),
    'export': ('export_fusion_frigate.py', []),
    'roundtrip': ('inspect_fusion_frigate.py', ['--fbx']),
    'verify-export': ('export_fusion_frigate.py', ['--verify']),
    'details': ('verify_fusion_details.py', []),
}[args.phase]
command = [r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe', '--background',
           '--factory-startup', '--python-exit-code', '1', '--python', str(Path(__file__).with_name(script)), '--', *extra]
log = folder / (args.phase + '.log')
with log.open('wb') as stream:
    result = subprocess.run(command, cwd=root, stdout=stream, stderr=subprocess.STDOUT, check=False)
print(f'{args.phase}: exit={result.returncode}; log={log}')
sys.exit(result.returncode)
