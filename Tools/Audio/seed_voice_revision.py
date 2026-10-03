"""Budgeted, resumable Seed-TTS 2.0 narrative revision authoring.

generate --ids E001 previews without writes, credentials or network access.
Add --execute only for an authorized paid request. Unknown submissions stop the
batch and are never retried. AFP accounting is a conservative local estimate,
not a provider invoice; the historical ledger always remains part of the cap.
"""
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
import hashlib
import json
import os
from pathlib import Path
import sys

import volc_voice as voice

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'ArtSource/Audio/SeedAudio20260929'
MANIFEST = WORK / 'narrative-revision.json'
PRIOR_LEDGER = ROOT / 'ArtSource/Audio/SeedTTS20260929/ledger.json'
BUDGET_AFP = Decimal('20000')
AFP_PER_UNIT = Decimal('0.135')
SCHEMA = 'droplet.seed-voice-ledger.v1'


def now():
    return datetime.now(timezone.utc).isoformat()


def amount(value, label):
    if isinstance(value, bool):
        raise voice.VoiceError('Invalid ' + label + '.')
    try:
        result = Decimal(str(value))
    except (InvalidOperation, ValueError):
        raise voice.VoiceError('Invalid ' + label + '.') from None
    if not result.is_finite() or result < 0:
        raise voice.VoiceError('Invalid ' + label + '.')
    return result


def read_json(path):
    raw = path.read_bytes()
    return json.loads(raw.decode('utf-8-sig')), hashlib.sha256(raw).hexdigest()


def durable_write(path, value):
    """Flush the journal before replacement, and before any paid submission."""
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.tmp')
    with temporary.open('w', encoding='utf-8', newline='\n') as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2, allow_nan=False)
        handle.write('\n')
        handle.flush()
        os.fsync(handle.fileno())
    os.replace(temporary, path)


def usage_units(usage):
    value = usage.get('text_words') if isinstance(usage, dict) else None
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        raise voice.VoiceError('Missing/invalid service usage.text_words; reservation retained.')
    return amount(value, 'service usage.text_words')


def entry_cost(entry):
    if entry.get('serviceTextUnits') is not None:
        return amount(entry['serviceTextUnits'], 'service text units') * AFP_PER_UNIT
    return amount(entry['reservedAFP'], 'reserved AFP')


def inherited_ledger():
    """Read the live historical account, including every unresolved reservation."""
    if (PRIOR_LEDGER.parent / 'generation.lock').exists():
        raise voice.VoiceError('Historical generation lock exists; do not run both batches concurrently.')
    if not PRIOR_LEDGER.is_file():
        raise voice.VoiceError('Historical ledger is required; spending cannot start from zero.')
    source, sha = read_json(PRIOR_LEDGER)
    if amount(source.get('budgetAFP'), 'historical budget') != BUDGET_AFP or \
            amount(source.get('afpPerCharacter'), 'historical rate') != AFP_PER_UNIT:
        raise voice.VoiceError('Historical budget/rate changed; reconcile before continuing.')
    total = sum((entry_cost(item) for item in source['entries']), Decimal(0))
    if total != amount(source.get('accountedAFP'), 'historical accounted AFP'):
        raise voice.VoiceError('Historical ledger totals disagree; reconcile before continuing.')
    return dict(path=str(PRIOR_LEDGER), sha256=sha, accountedAFP=str(total))


def load_manifest():
    manifest, sha = read_json(MANIFEST)
    if manifest.get('schema') != 'droplet.seed-narrative-revision.v1' or \
            manifest.get('voiceProvider') != 'Seed-TTS 2.0':
        raise voice.VoiceError('Expected the authored Seed-TTS 2.0 narrative revision manifest.')
    voice.identifier(manifest['take'])
    items = manifest['lines']
    if not items or len({item['id'] for item in items}) != len(items):
        raise voice.VoiceError('Manifest line IDs must be nonempty and unique.')
    for item in items:
        voice.identifier(item['id'])
        if not isinstance(item.get('english'), str) or not item['english'].strip():
            raise voice.VoiceError('Each line needs English synthesis text: ' + item['id'])
        directions = item.get('context_texts')
        # volc_voice represents exactly one context string. Never silently drop
        # or reinterpret additional instructions from a later manifest revision.
        if not isinstance(directions, list) or len(directions) != 1 or \
                not isinstance(directions[0], str) or not directions[0].strip():
            raise voice.VoiceError('Each line needs exactly one context_texts direction: ' + item['id'])
        rate = item.get('speech_rate', 0)
        if not isinstance(rate, int) or isinstance(rate, bool) or not -50 <= rate <= 100:
            raise voice.VoiceError('Invalid speech_rate: ' + item['id'])
        if not isinstance(item.get('speaker'), str) or not item['speaker'].strip():
            raise voice.VoiceError('Each line needs its authored speaker: ' + item['id'])
    return manifest, sha


def request_plan(item, take):
    args = argparse.Namespace(provider='seed-tts', id=[item['id']], all=False,
        text=item['english'], speaker=item['speaker'], direction=item['context_texts'][0],
        speech_rate=item.get('speech_rate', 0), take=take, execute=False)
    payload = voice.payload_for(args, voice.lines_for(args)[0])
    raw = json.dumps(payload, sort_keys=True, ensure_ascii=False).encode('utf-8')
    sha = hashlib.sha256(raw).hexdigest()
    directory = voice.OUTPUT / 'seed-tts' / take / (item['id'] + '-' + sha[:16])
    units = 4 * len((args.text + args.direction).encode('utf-8'))
    identity = dict(provider='seed-tts', endpoint=voice.TTS, resource_id='seed-tts-2.0',
                    payload=payload, usageTokensReturn='*')
    return dict(id=item['id'], args=args, request=identity, requestSha256=sha,
                directory=directory, reservedAFP=Decimal(units) * AFP_PER_UNIT)


def load_ledger(take, inherited):
    path = WORK / 'voice-ledger.json'
    if not path.exists():
        if (voice.OUTPUT / 'seed-tts' / take).exists():
            raise voice.VoiceError('Missing voice ledger for an existing take; reconcile, never regenerate.')
        return dict(schema=SCHEMA, take=take, budgetAFP=str(BUDGET_AFP),
                    afpPerTextUnit=str(AFP_PER_UNIT), inheritedLedger=inherited,
                    billingNote='Service text_words at 0.135 AFP; otherwise full reservation. Not an invoice.',
                    entries=[])
    ledger, _ = read_json(path)
    if ledger.get('schema') != SCHEMA or ledger.get('take') != take or \
            amount(ledger.get('budgetAFP'), 'voice budget') != BUDGET_AFP or \
            amount(ledger.get('afpPerTextUnit'), 'voice rate') != AFP_PER_UNIT:
        raise voice.VoiceError('Voice ledger identity/budget/rate mismatch; refusing to reset it.')
    previous = ledger['inheritedLedger']
    if previous.get('path') != inherited['path'] or \
            amount(inherited['accountedAFP'], 'inherited AFP') < amount(previous['accountedAFP'], 'prior AFP'):
        raise voice.VoiceError('Historical accounted cost decreased or source changed; reconcile first.')
    ids = [entry['id'] for entry in ledger['entries']]
    if len(ids) != len(set(ids)):
        raise voice.VoiceError('Duplicate voice ledger IDs; reconcile first.')
    subtotal = sum((entry_cost(entry) for entry in ledger['entries']), Decimal(0))
    if amount(ledger.get('revisionAccountedAFP'), 'revision accounted AFP') != subtotal or \
            amount(ledger.get('accountedAFP'), 'combined accounted AFP') != \
            subtotal + amount(previous['accountedAFP'], 'prior AFP'):
        raise voice.VoiceError('Voice ledger totals disagree; refusing to reset accounting.')
    ledger['inheritedLedger'] = inherited
    return ledger


def write_ledger(ledger):
    known = sum((entry_cost(entry) for entry in ledger['entries']
                 if entry.get('serviceTextUnits') is not None), Decimal(0))
    unknown = sum((entry_cost(entry) for entry in ledger['entries']
                   if entry.get('serviceTextUnits') is None), Decimal(0))
    ledger.update(serviceUsageConvertedAFP=str(known), unconfirmedReservedAFP=str(unknown),
                  revisionAccountedAFP=str(known + unknown),
                  accountedAFP=str(amount(ledger['inheritedLedger']['accountedAFP'], 'inherited AFP') + known + unknown),
                  completedRequests=sum(entry['status'] == 'complete' for entry in ledger['entries']),
                  updatedAt=now())
    durable_write(WORK / 'voice-ledger.json', ledger)


def validate_cache(plan, entry):
    directory = plan['directory']
    job_path, audio_path = directory / 'job.json', directory / 'voice.wav'
    if entry['request'] != plan['request'] or entry['requestSha256'] != plan['requestSha256'] or \
            entry['job'] != str(job_path):
        raise voice.VoiceError('An immutable voice ID has changed; do not regenerate it: ' + plan['id'])
    if entry['status'] != 'complete':
        raise voice.VoiceError('Unknown voice submission/usage; reservation retained, no automatic retry.')
    if not job_path.is_file() or not audio_path.is_file():
        raise voice.VoiceError('Ledgered voice cache is missing; no paid regeneration.')
    cached, _ = read_json(job_path)
    if cached.get('status') != 'complete' or cached.get('request') != plan['request']['payload'] or \
            cached.get('provider') != 'seed-tts' or cached.get('resource_id') != 'seed-tts-2.0' or \
            cached.get('endpoint') != voice.TTS:
        raise voice.VoiceError('Voice cache request/provider does not match its ledger.')
    sha = hashlib.sha256(audio_path.read_bytes()).hexdigest()
    if sha != cached.get('sha256') or sha != entry.get('sha256') or \
            usage_units(cached.get('usage')) != amount(entry.get('serviceTextUnits'), 'ledger service units'):
        raise voice.VoiceError('Voice audio SHA/usage does not match its ledger.')
    return cached


def state_for(plan, ledger):
    existing = next((entry for entry in ledger['entries'] if entry['id'] == plan['id']), None)
    if existing is not None:
        validate_cache(plan, existing)
        return 'cached'
    if plan['directory'].exists():
        raise voice.VoiceError('Unledgered voice output exists; reconcile before submitting.')
    return 'new'


def select_plans(manifest, ids=None, all_lines=False):
    if bool(ids) == bool(all_lines):
        raise voice.VoiceError('Select --ids or explicitly use --all.')
    if ids and (len(ids) != len(set(ids)) or set(ids) - {item['id'] for item in manifest['lines']}):
        raise voice.VoiceError('Unknown or repeated voice IDs.')
    return [request_plan(item, manifest['take']) for item in manifest['lines']
            if all_lines or item['id'] in ids]


def preview(plans, manifest, manifest_sha):
    inherited = inherited_ledger()
    ledger = load_ledger(manifest['take'], inherited)
    states = [(plan, state_for(plan, ledger)) for plan in plans]
    subtotal = sum((entry_cost(entry) for entry in ledger['entries']), Decimal(0))
    # An interrupted request remains reserved and cannot be retried. It does
    # not prevent authoring unrelated IDs under the same cumulative cap.
    selected_ids = {plan['id'] for plan in plans}
    blocked = any(entry['status'] != 'complete' and entry['id'] in selected_ids
                  for entry in ledger['entries'])
    report = dict(execute=False, take=manifest['take'], manifestSha256=manifest_sha,
        budgetAFP=str(BUDGET_AFP), inheritedLedger=inherited,
        revisionAccountedAFP=str(subtotal),
        remainingAFP=str(BUDGET_AFP - amount(inherited['accountedAFP'], 'inherited AFP') - subtotal),
        blockedByUnknownSubmission=blocked,
        reservationRule='4 * UTF-8 bytes of English text plus context direction * 0.135 AFP, per request; settle only service usage.text_words.',
        lines=[dict(id=plan['id'], state=state, request=plan['request'],
                    requestSha256=plan['requestSha256'], output=str(plan['directory']),
                    reservedAFP=str(plan['reservedAFP'])) for plan, state in states])
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return report


@contextmanager
def generation_lock():
    WORK.mkdir(parents=True, exist_ok=True)
    path = WORK / 'voice-generation.lock'
    try:
        handle = path.open('x', encoding='utf-8')
    except FileExistsError:
        raise voice.VoiceError('Voice generation lock exists; investigate its owner, never remove blindly.') from None
    try:
        with handle:
            json.dump(dict(pid=os.getpid(), createdAt=now()), handle)
            handle.flush()
            os.fsync(handle.fileno())
        yield
    finally:
        path.unlink(missing_ok=True)


def execute_plans(plans, manifest, manifest_sha):
    results = []
    with generation_lock():
        for plan in plans:
            # Re-read historical accounting before EVERY call, not once per batch.
            inherited = inherited_ledger()
            ledger = load_ledger(manifest['take'], inherited)
            if any(entry['status'] != 'complete' and entry['id'] == plan['id']
                   for entry in ledger['entries']):
                raise voice.VoiceError('Unknown voice submission/usage blocks this ID; no automatic retry.')
            if state_for(plan, ledger) == 'cached':
                results.append(next(entry for entry in ledger['entries'] if entry['id'] == plan['id']))
                continue
            accounted = amount(inherited['accountedAFP'], 'inherited AFP') + \
                sum((entry_cost(entry) for entry in ledger['entries']), Decimal(0))
            if accounted + plan['reservedAFP'] > BUDGET_AFP:
                raise voice.VoiceError('Request would exceed the cumulative 20,000 AFP budget; not submitted.')
            entry = dict(id=plan['id'], status='submission_unknown', submittedAt=now(),
                request=plan['request'], requestSha256=plan['requestSha256'],
                manifestSha256=manifest_sha, inheritedLedger=dict(inherited),
                job=str(plan['directory'] / 'job.json'), reservedAFP=str(plan['reservedAFP']),
                contentAcceptance='not_reviewed')
            ledger['entries'].append(entry)
            write_ledger(ledger)
            try:
                if inherited_ledger() != inherited:
                    raise voice.VoiceError('Historical ledger changed during reservation; stop and reconcile.')
                plan['args'].execute = True
                voice.generate(plan['args'])
                job, _ = read_json(plan['directory'] / 'job.json')
                # Validate the full response and bytes before settling any cost.
                candidate = dict(entry, status='complete', sha256=job.get('sha256'),
                                 serviceTextUnits=str(usage_units(job.get('usage'))))
                validate_cache(plan, candidate)
                units = usage_units(job.get('usage'))
                entry.update(status='complete', serviceTextUnits=str(units),
                    usage=dict(text_words=job['usage']['text_words']), sha256=job['sha256'],
                    durationSeconds=job['duration_seconds'], completedAt=now())
                write_ledger(ledger)
                results.append(entry)
                if units * AFP_PER_UNIT > plan['reservedAFP'] or \
                        amount(ledger['accountedAFP'], 'combined AFP') > BUDGET_AFP:
                    raise voice.VoiceError('Service usage exceeded conservative reservation; halt and reconcile.')
            except Exception:
                # Never persist raw exception strings, HTTP bodies, headers, or keys.
                entry['failureReason'] = 'Generation/validation/accounting did not finish normally; no automatic retry.'
                if entry['status'] == 'complete':
                    entry['status'] = 'accounting_anomaly'
                write_ledger(ledger)
                raise voice.VoiceError('Voice generation stopped; ledger retained. Inspect local job and accounting before proceeding.') from None
    return results


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('generate',), nargs='?', default='generate')
    selection = parser.add_mutually_exclusive_group(required=True)
    selection.add_argument('--ids', nargs='+')
    selection.add_argument('--all', action='store_true')
    selection.add_argument('--replace-unknown', metavar='ID',
        help='Explicitly create a distinct replacement take after inspecting an interrupted ID.')
    parser.add_argument('--execute', action='store_true', help='Submit paid requests; default is read-only preview.')
    args = parser.parse_args(argv)
    manifest, manifest_sha = load_manifest()
    if args.replace_unknown:
        inherited = inherited_ledger()
        ledger = load_ledger(manifest['take'], inherited)
        original = next((entry for entry in ledger['entries']
                         if entry['id'] == args.replace_unknown), None)
        line = next((line for line in manifest['lines']
                     if line['id'] == args.replace_unknown), None)
        replacement_id = args.replace_unknown + '_R2'
        if original is None or original['status'] != 'submission_unknown' or line is None or \
                any(entry['id'] == replacement_id for entry in ledger['entries']):
            raise voice.VoiceError('Replacement requires one inspected unknown ID and no prior replacement.')
        # This is a new, explicitly selected billable request. The original
        # unknown reservation remains in the cumulative cap and is never retried.
        plans = [request_plan(dict(line, id=replacement_id), manifest['take'])]
    else:
        plans = select_plans(manifest, args.ids, args.all)
    preview(plans, manifest, manifest_sha)
    if args.execute:
        results = execute_plans(plans, manifest, manifest_sha)
        print(json.dumps(dict(completed=len(results), ledger=str(WORK / 'voice-ledger.json')), ensure_ascii=False))


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
    try:
        main()
    except (voice.VoiceError, ValueError, KeyError, OSError):
        # Do not echo arbitrary local/remote content from errors to the console.
        print('ERROR: Voice revision stopped; inspect the preview and local ledger. No automatic retry.', file=sys.stderr)
        sys.exit(1)
