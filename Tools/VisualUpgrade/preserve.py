"""Task-local, read-only source hash inventory and selected baseline copies."""
from pathlib import Path
import hashlib, json, shutil, sys
root=Path(__file__).resolve().parents[2]
out=root/'docs/verification/VisualUpgrade'
out.mkdir(parents=True,exist_ok=True)
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
if '--check' in sys.argv:
    before=json.loads((out/'preservation-before.json').read_text(encoding='utf-8'))
    result={'checked':len(before),'changed':[], 'missing':[]}
    for name,h in before.items():
        p=root/name
        if not p.exists(): result['missing'].append(name)
        elif digest(p)!=h: result['changed'].append(name)
    (out/'preservation-after.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))
else:
    before={}
    for folder in ['Assets','Packages','ProjectSettings','ArtSource','Tools']:
        for p in (root/folder).rglob('*'):
            if not p.is_file() or 'VisualUpgrade' in p.parts or 'VisualUpgrade' in p.name: continue
            if folder=='ArtSource' and any(x in p.parts for x in ['Deliveries','Previews']): continue
            before[p.relative_to(root).as_posix()]=digest(p)
    for name in ['docs/STATUS.md','docs/ENVIRONMENT.md']:
        before[name]=digest(root/name)
    (out/'preservation-before.json').write_text(json.dumps(before,ensure_ascii=False,indent=2),encoding='utf-8')
    for folder in ['Assets/_Project/Scenes','Assets/_Project/Prefabs','Assets/_Project/Data','ProjectSettings','Packages']:
        for p in (root/folder).rglob('*'):
            if p.is_file():
                dest=out/'BaselineBackup'/p.relative_to(root)
                dest.parent.mkdir(parents=True,exist_ok=True)
                shutil.copy2(p,dest)
    print(f'Protected {len(before)} existing source files; copied scenes, prefabs, configs and settings.')
