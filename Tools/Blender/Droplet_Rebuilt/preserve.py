from pathlib import Path
import hashlib, json, shutil, subprocess
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'docs/verification/Droplet_Rebuilt'
OUT.mkdir(parents=True,exist_ok=True)
index=OUT/'baseline.json'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
if not index.exists():
    files={}
    for folder in ['Assets','Packages','ProjectSettings','ArtSource','Tools']:
        for p in (ROOT/folder).rglob('*'):
            if p.is_file() and '__pycache__' not in str(p):files[p.relative_to(ROOT).as_posix()]=sha(p)
    for name in ['docs/STATUS.md','docs/ENVIRONMENT.md','AGENTS.md']:files[name]=sha(ROOT/name)
    index.write_text(json.dumps(files,indent=2),encoding='utf8')
    for folder in ['Assets/_Project/Scenes','Assets/_Project/Prefabs','Assets/Settings','ProjectSettings','ArtSource/Blender/Droplet/PerfectDroplet','ArtSource/Exports/Droplet/PerfectDroplet']:
        shutil.copytree(ROOT/folder,OUT/'BaselineBackup'/folder)
    for name in ['docs/STATUS.md','docs/ENVIRONMENT.md']:
        dest=OUT/'BaselineBackup'/name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/name,dest)
    p=subprocess.run(['git','status','--short'],cwd=ROOT,capture_output=True)
    (OUT/'git-status-before.txt').write_bytes(p.stdout)
    print('Protected',len(files),'files')
else:
    files=json.loads(index.read_text(encoding='utf8'))
    changed=[p for p,h in files.items() if not (ROOT/p).exists() or sha(ROOT/p)!=h]
    report={'count':len(files),'unchanged':len(files)-len(changed),'changed':changed}
    (OUT/'preservation-final.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(report)
