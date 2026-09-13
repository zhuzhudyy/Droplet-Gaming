"""Protect hand edits when regenerating this task's complete source artifact."""
import hashlib
import json
from pathlib import Path


def require_unmodified_source(destination: Path, receipts):
    if not destination.exists():
        return
    actual = hashlib.sha256(destination.read_bytes()).hexdigest()
    for receipt in receipts:
        if receipt.exists():
            record = json.loads(receipt.read_text(encoding='utf-8-sig'))
            if record.get('source_sha256', '').lower() == actual:
                return
    raise RuntimeError('Refusing to replace an unrecognized or hand-edited .blend. '
                       'Keep the authored source and generate in a separate project copy: ' + str(destination))


def check_project_source(root: Path):
    art = root / 'ArtSource/Blender/SpaceEnvironment'
    require_unmodified_source(art / 'SpaceEnvironment.blend', [
        art / 'generated-source-receipt.json'])
