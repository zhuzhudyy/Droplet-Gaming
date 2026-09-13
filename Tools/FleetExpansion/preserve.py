import hashlib, json, shutil
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'docs/verification/FleetExpansion'
out.mkdir(parents=True, exist_ok=True)
manifest = out / 'baseline-files.json'
if not manifest.exists():
    records = {}
    for folder in ['Assets', 'ProjectSettings', 'Packages', 'Tools', 'ArtSource', 'docs']:
        for p in (root / folder).rglob('*'):
            if not p.is_file() or out in p.parents or '__pycache__' in p.parts:
                continue
            rel = p.relative_to(root).as_posix()
            records[rel] = hashlib.sha256(p.read_bytes()).hexdigest()
            if folder in ['ProjectSettings','Packages'] or (folder == 'Assets' and p.suffix not in ['.fbx','.png','.jpg','.wav']):
                dest=out/'BaselineBackup'/rel
                dest.parent.mkdir(parents=True,exist_ok=True)
                shutil.copy2(p,dest)
    manifest.write_text(json.dumps(records,indent=2,ensure_ascii=False),encoding='utf-8')
    print(f'Preserved hashes of {len(records)} pre-existing files and Unity authored text backups.')
else:
    print('Baseline exists; retained.')
