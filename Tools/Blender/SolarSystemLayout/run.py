import subprocess,sys
from pathlib import Path
here=Path(__file__).resolve().parent;root=here.parents[2]
stage=sys.argv[1];assert stage in ['build','inspect','export','adopt-local']
e=root/'docs/verification/SolarSystemLayout';e.mkdir(parents=True,exist_ok=True)
with (e/('blender-'+stage+'.log')).open('wb') as log:
 r=subprocess.run([r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe','--background','--factory-startup','--python-exit-code','1','--python',str(here/'pipeline.py'),'--',stage],cwd=root,stdout=log,stderr=subprocess.STDOUT)
print(stage,'actual Blender exit:',r.returncode);sys.exit(r.returncode)
