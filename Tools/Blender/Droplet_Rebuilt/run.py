from pathlib import Path
import subprocess,sys
root=Path(__file__).resolve().parents[3]
phase=sys.argv[1]
out=root/'docs/verification/Droplet_Rebuilt';out.mkdir(parents=True,exist_ok=True)
with (out/(phase+'.log')).open('wb') as log:
    p=subprocess.run([r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe','--background','--factory-startup','--python-exit-code','1','--python',str(Path(__file__).with_name('pipeline.py')),'--',phase],cwd=root,stdout=log,stderr=subprocess.STDOUT)
print(phase,'exit',p.returncode)
sys.exit(p.returncode)
