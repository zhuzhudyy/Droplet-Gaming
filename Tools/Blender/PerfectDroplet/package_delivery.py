"""Package only PerfectDroplet-owned deliverables, preserving project paths."""
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[3]
EVIDENCE = ROOT/'docs/verification/PerfectDroplet'
ART = ROOT/'ArtSource/Blender/Droplet/PerfectDroplet'
OUT = ROOT/'ArtSource/Exports/Droplet/PerfectDroplet'
baseline = json.loads((EVIDENCE/'baseline.json').read_text(encoding='utf-8'))
changed = [p for p, h in baseline.items() if not (ROOT/p).exists()
           or hashlib.sha256((ROOT/p).read_bytes()).hexdigest() != h]
preservation = {'checked': len(baseline), 'unchanged': len(baseline)-len(changed),
                'changed_or_missing': changed}
(EVIDENCE/'preservation.json').write_text(json.dumps(preservation, indent=2), encoding='utf-8')
assert not changed, changed
assert len(list((ART/'Previews').glob('*.png'))) == 15
files = [ART/'PerfectDroplet.blend', ART/'generated-source-receipt.json',
         OUT/'PerfectDroplet_Game.fbx', OUT/'export_manifest.json',
         ROOT/'docs/DROPLET_GEOMETRY_REPORT.md', *sorted((ART/'Previews').glob('*.png')),
         *sorted(Path(__file__).parent.glob('*.py')),
         *sorted(p for p in EVIDENCE.iterdir() if p.suffix in {'.json', '.log', '.md'}
                 and p.name not in {'package-verification.json'})]
hashes = {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
destination = ROOT/'ArtSource/Deliveries/PerfectDroplet-20260908.zip'
destination.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(destination, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for path in files:
        archive.write(path, path.relative_to(ROOT).as_posix())
    archive.writestr('SHA256.json', json.dumps(hashes, indent=2))
with zipfile.ZipFile(destination) as archive:
    assert archive.testzip() is None
    for name, expected in hashes.items():
        assert hashlib.sha256(archive.read(name)).hexdigest() == expected, name
report = {'archive': str(destination.relative_to(ROOT)), 'files': len(hashes),
          'bytes': destination.stat().st_size,
          'sha256': hashlib.sha256(destination.read_bytes()).hexdigest(),
          'crc_and_all_content_hashes_passed': True,
          'source_sha256': hashes[(ART/'PerfectDroplet.blend').relative_to(ROOT).as_posix()],
          'fbx_sha256': hashes[(OUT/'PerfectDroplet_Game.fbx').relative_to(ROOT).as_posix()]}
(EVIDENCE/'package-verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
