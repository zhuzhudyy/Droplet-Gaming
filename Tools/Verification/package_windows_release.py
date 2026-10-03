"""Archive an existing Windows player and verify every entry against its source."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('player', type=Path)
    parser.add_argument('archive', type=Path)
    parser.add_argument('report', type=Path)
    args = parser.parse_args()
    source = args.player.resolve(strict=True)
    if not (source / 'DropletGaming.exe').is_file():
        raise SystemExit('Player executable is missing')
    if args.archive.exists():
        raise SystemExit('Refusing to overwrite an existing release archive')
    files = sorted(path for path in source.rglob('*') if path.is_file())
    args.archive.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in files:
            archive.write(path, str(Path(source.name) / path.relative_to(source)))
    entries = []
    with zipfile.ZipFile(args.archive) as archive:
        if archive.testzip() is not None or len(archive.infolist()) != len(files):
            raise SystemExit('Archive integrity or entry count mismatch')
        for path in files:
            name = (Path(source.name) / path.relative_to(source)).as_posix()
            expected = digest(path)
            with archive.open(name) as stream:
                actual = hashlib.file_digest(stream, 'sha256').hexdigest()
            if actual != expected:
                raise SystemExit(f'Archive content mismatch: {name}')
            entries.append({'path': name, 'bytes': path.stat().st_size, 'sha256': actual})
    report = {
        'source': args.player.as_posix(),
        'archive': args.archive.as_posix(),
        'method': 'Archive existing verified player; no recompilation or gameplay rerun',
        'files': len(files),
        'uncompressedBytes': sum(entry['bytes'] for entry in entries),
        'archiveBytes': args.archive.stat().st_size,
        'archiveSha256': digest(args.archive),
        'allEntriesMatchSource': True,
        'entries': entries,
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: value for key, value in report.items() if key != 'entries'}, ensure_ascii=True))


if __name__ == '__main__':
    main()
