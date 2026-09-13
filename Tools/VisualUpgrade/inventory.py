"""Record delivery footprint and new-source hashes without touching old assets."""
from pathlib import Path
import hashlib, json

root = Path(__file__).resolve().parents[2]
out = root / 'docs/verification/VisualUpgrade'
protected = json.loads((out / 'preservation-before.json').read_text(encoding='utf-8'))
folders = ['Assets', 'Packages', 'ProjectSettings', 'ArtSource', 'Tools', 'Builds', 'Library', 'docs']
footprint = {}
added = []
for name in folders:
    paths = [p for p in (root / name).rglob('*') if p.is_file()]
    footprint[name] = {'files': len(paths), 'bytes': sum(p.stat().st_size for p in paths)}
    if name in ['Assets', 'Tools']:
        for p in paths:
            rel = p.relative_to(root).as_posix()
            if rel not in protected:
                added.append({'path': rel, 'bytes': p.stat().st_size,
                              'sha256': hashlib.sha256(p.read_bytes()).hexdigest()})
build = root / 'Builds/Windows-VisualUpgrade-20260908'
runtime = [p for p in build.rglob('*') if p.is_file()
           and not any('DoNotShip' in part for part in p.parts)]
result = {'scope': 'Directory sizes are disk files, not runtime memory or VRAM. '
                   'Old builds/source/Library/docs are kept and are not game distribution content.',
          'folders': footprint, 'newSourceFiles': added,
          'newSourceBytes': sum(p['bytes'] for p in added),
          'distributionWithoutDebugBytes': sum(p.stat().st_size for p in runtime),
          'distributionFiles': len(runtime)}
(out / 'delivery-inventory.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'newSourceFiles'}, ensure_ascii=False))
