import hashlib, json, pathlib, shutil
root = pathlib.Path(__file__).resolve().parents[2]
out = root / 'docs/verification/LightingUpgrade'
out.mkdir(parents=True, exist_ok=True)
index = out / 'baseline-files.json'
if not index.exists():
    files = {}
    for folder in ['Assets', 'Packages', 'ProjectSettings', 'Tools', 'ArtSource']:
        for p in (root/folder).rglob('*'):
            if p.is_file() and '__pycache__' not in str(p) and 'LightingUpgrade' not in str(p) and 'FusionDrive' not in p.name and 'LightingValidation' not in p.name:
                files[p.relative_to(root).as_posix()] = hashlib.sha256(p.read_bytes()).hexdigest()
    for path in ['docs/STATUS.md','docs/ENVIRONMENT.md']:
        files[path] = hashlib.sha256((root/path).read_bytes()).hexdigest()
    index.write_text(json.dumps(files, indent=2), encoding='utf8')
    for folder in ['Assets/_Project/Scenes','Assets/_Project/Prefabs','Assets/Settings','ProjectSettings']:
        shutil.copytree(root/folder, out/'BaselineBackup'/folder, dirs_exist_ok=True)
    print('Baseline protected:',len(files))
else:
    files=json.loads(index.read_text(encoding='utf8'))
    changed=[p for p,h in files.items() if not (root/p).exists() or hashlib.sha256((root/p).read_bytes()).hexdigest()!=h]
    (out/'preservation-final.json').write_text(json.dumps({'baselineCount':len(files),'changed':changed},indent=2),encoding='utf8')
    print(json.dumps(changed,ensure_ascii=False))
