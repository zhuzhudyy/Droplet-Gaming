"""Publish a verified player ZIP using existing Git Credential Manager credentials."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import urllib.error
import urllib.parse
import urllib.request


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('check', 'publish'))
    parser.add_argument('--repository', default='zhuzhudyy/Droplet-Gaming')
    parser.add_argument('--proxy', required=True)
    parser.add_argument('--commit')
    parser.add_argument('--tag', default='seed-audio-20260929')
    parser.add_argument('--archive', type=Path)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    if args.action == 'publish' and not all((args.commit, args.archive, args.report)):
        parser.error('Publishing requires --commit, --archive and --report')
    credentials = subprocess.run(
        ['git', 'credential', 'fill'],
        input='protocol=https\nhost=github.com\n\n',
        capture_output=True, text=True, check=True,
    )
    fields = dict(line.split('=', 1) for line in credentials.stdout.splitlines() if '=' in line)
    token = fields.get('password')
    if not token:
        raise SystemExit('No existing GitHub credential available')
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({'https': args.proxy}))
    api = 'https://api.github.com/repos/' + args.repository

    def request(url, method='GET', payload=None, binary=None):
        if urllib.parse.urlparse(url).hostname not in ('api.github.com', 'uploads.github.com'):
            raise SystemExit('Refusing to send credentials outside GitHub API hosts')
        headers = {
            'Authorization': 'Bearer ' + token,
            'Accept': 'application/vnd.github+json',
            'X-GitHub-Api-Version': '2022-11-28',
            'User-Agent': 'DropletGaming-release-authoring',
        }
        if binary is not None:
            data = binary
            headers['Content-Type'] = 'application/zip'
        elif payload is not None:
            data = json.dumps(payload, ensure_ascii=False).encode('utf-8')
            headers['Content-Type'] = 'application/json'
        else:
            data = None
        with opener.open(urllib.request.Request(url, data=data, headers=headers, method=method), timeout=180) as response:
            return json.load(response)

    repository = request(api)
    if args.action == 'check':
        print(json.dumps({'repository': repository['full_name'], 'pushPermission': repository.get('permissions', {}).get('push')}))
        return
    remote = subprocess.run(
        ['git', '-c', 'http.proxy=' + args.proxy, '-c', 'https.proxy=' + args.proxy,
         'ls-remote', 'origin', 'refs/heads/main'],
        capture_output=True, text=True, check=True,
    ).stdout.split()[0]
    if remote != args.commit:
        raise SystemExit('Publish only after the intended commit is present on remote main')
    with args.archive.open('rb') as stream:
        sha256 = hashlib.file_digest(stream, 'sha256').hexdigest()
    size = args.archive.stat().st_size
    notes = Path('Releases/README.md').read_text(encoding='utf-8')
    try:
        release = request(api + '/releases/tags/' + urllib.parse.quote(args.tag, safe=''))
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise
        release = request(api + '/releases', 'POST', {
            'tag_name': args.tag,
            'target_commitish': args.commit,
            'name': 'Windows Seed 音频版 · 2000 舰 · 2026-09-29',
            'body': notes,
            'draft': True,
            'prerelease': False,
        })
    matches = [asset for asset in release['assets'] if asset['name'] == args.archive.name]
    if matches:
        asset = matches[0]
        if asset.get('digest') != 'sha256:' + sha256 or asset['size'] != size:
            raise SystemExit('Existing release asset differs; refusing to replace it')
    else:
        upload = release['upload_url'].split('{', 1)[0]
        upload += '?' + urllib.parse.urlencode({'name': args.archive.name})
        print('Uploading verified player archive...', flush=True)
        asset = request(upload, 'POST', binary=args.archive.read_bytes())
    if asset['state'] != 'uploaded' or asset['size'] != size or asset.get('digest') != 'sha256:' + sha256:
        raise SystemExit('Remote asset size/state/SHA-256 verification failed; release stays draft')
    if release['draft']:
        release = request(release['url'], 'PATCH', {'draft': False, 'make_latest': 'true'})
    verified = request(api + '/releases/tags/' + urllib.parse.quote(args.tag, safe=''))
    assets = [item for item in verified['assets'] if item['name'] == args.archive.name]
    if verified['draft'] or len(assets) != 1 or assets[0].get('digest') != 'sha256:' + sha256:
        raise SystemExit('Published release verification failed')
    report = {
        'repository': args.repository,
        'commit': args.commit,
        'tag': args.tag,
        'releaseUrl': verified['html_url'],
        'downloadUrl': assets[0]['browser_download_url'],
        'assetBytes': assets[0]['size'],
        'sha256': sha256,
        'remoteDigestMatches': True,
        'published': True,
    }
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=True))


if __name__ == '__main__':
    try:
        main()
    except urllib.error.HTTPError as error:
        raise SystemExit(f'GitHub request failed: HTTP {error.code}')
