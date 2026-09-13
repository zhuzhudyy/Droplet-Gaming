"""Launch a separate installed Blender process, preserving all live UI sessions."""
import argparse,subprocess,sys
from pathlib import Path
from source_guard import check_project_source
p=argparse.ArgumentParser();p.add_argument('stage',choices=['build','inspect','export']);args=p.parse_args()
root=Path(__file__).resolve().parents[3];art=root/'ArtSource/Blender/SpaceEnvironment';art.mkdir(parents=True,exist_ok=True)
if args.stage == 'build':
    check_project_source(root)
script={'build':'build_environment.py','inspect':'inspect_environment.py','export':'export_environment.py'}[args.stage]
log=art/(args.stage+'-process.log')
with log.open('wb') as out:
    result=subprocess.run([r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe','--background','--factory-startup','--python-exit-code','1','--python',str(Path(__file__).with_name(script))],cwd=root,stdout=out,stderr=subprocess.STDOUT)
print('Blender '+args.stage+' actual process exit code: '+str(result.returncode)+'. Log: '+str(log))
sys.exit(result.returncode)
