"""Read-only baseline comparison; write this batch's delivery evidence only."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
evidence = root / 'docs/verification/FleetExpansion'
baseline = json.loads((evidence / 'baseline-files.json').read_text(encoding='utf-8'))
expected = {'docs/STATUS.md', 'docs/ENVIRONMENT.md'}

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

changed = []
missing = []
for relative, before in baseline.items():
    path = root / relative
    if not path.is_file():
        missing.append(relative)
    elif digest(path) != before:
        changed.append({'path': relative, 'before': before, 'after': digest(path)})

unexpected = [entry['path'] for entry in changed if entry['path'] not in expected]
preservation = {
    'baselineFiles': len(baseline),
    'unchangedFiles': len(baseline) - len(changed) - len(missing),
    'expectedChanged': changed,
    'unexpectedChanged': unexpected,
    'missingFiles': missing,
    'passed': not unexpected and not missing,
    'scope': 'Baseline 809 files; STATUS and ENVIRONMENT gain this batch entry. Historical TestRange issue remains as documented.',
}
(evidence / 'preservation-final.json').write_text(json.dumps(preservation, indent=2, ensure_ascii=False), encoding='utf-8')

new_files = []
for folder in ['Assets', 'ProjectSettings', 'Packages', 'Tools', 'ArtSource', 'docs']:
    for path in (root / folder).rglob('*'):
        if not path.is_file() or evidence in path.parents or '__pycache__' in path.parts:
            continue
        relative = path.relative_to(root).as_posix()
        if relative not in baseline:
            new_files.append(relative)
inventory = {'modifiedBaseline': [entry['path'] for entry in changed], 'newFiles': sorted(new_files),
             'evidenceDirectory': 'docs/verification/FleetExpansion',
             'note': 'Evidence PNG/JSON/logs and baseline backups are grouped separately; existing user modifications were not reverted.'}
(evidence / 'changed-files.json').write_text(json.dumps(inventory, indent=2, ensure_ascii=False), encoding='utf-8')

artifact_paths = sorted(set(new_files + [entry['path'] for entry in changed]))
hashes = {path: digest(root / path) for path in artifact_paths}
(evidence / 'delivery-hashes.json').write_text(json.dumps(hashes, indent=2, ensure_ascii=False), encoding='utf-8')
print(json.dumps({'preservation': preservation, 'newFileCount': len(new_files)}, ensure_ascii=False, indent=2))
if not preservation['passed']:
    raise SystemExit(1)
