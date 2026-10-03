"""Generate authored Seed Audio cues after the six representative sounds are accepted.

Each raw result is immutable. A failed/unknown request stops the run and its
original ID is never submitted again. The player never calls this script.
"""
import argparse
import base64
from decimal import Decimal, ROUND_CEILING
import hashlib
import json
from pathlib import Path
import sys
import uuid

import seed_audio as audio
import volc_voice as voice


WORK = audio.WORK
CATALOG = WORK / 'catalog.json'
REVIEW = WORK / 'capability-review.json'
OUTPUT = WORK / 'raw-catalog'


def committed_maximum_cost():
    """Count actual model seconds; reserve unknown network outcomes from raw jobs."""
    total = Decimal(0)
    for parent in (WORK / 'raw', OUTPUT):
        if not parent.exists():
            continue
        for path in parent.glob('*/job.json'):
            job = json.loads(path.read_text(encoding='utf-8'))
            billed = job.get('originalDurationSeconds')
            if billed is not None:
                seconds = Decimal(str(billed))
                if not seconds.is_finite() or not 0 < seconds <= 120:
                    raise voice.VoiceError('Invalid billed duration in ' + str(path))
                total += seconds.to_integral_value(rounding=ROUND_CEILING) / 60
            elif job.get('diagnostic', {}).get('httpStatus') in (400, 401, 402, 403, 404, 409, 429):
                # The provider explicitly rejected these requests before output.
                continue
            else:
                total += Decimal(2)
    return total


def authored():
    value = json.loads(CATALOG.read_text(encoding='utf-8'))
    cues = value['cues']
    if len(cues) != 131 or len({x['id'] for x in cues}) != 131:
        raise voice.VoiceError('Expected 131 stable, unique catalogue IDs.')
    if any(x['model'] != audio.MODEL or x['provider'] != 'seed-audio' or
           not x['text_prompt'].strip() or len(x['text_prompt']) > 3000 for x in cues):
        raise voice.VoiceError('Catalogue model or prompt is invalid.')
    source_ids = [x.get('sourceId', x['id']) for x in cues]
    if len(set(source_ids)) != len(source_ids) or any(
            not value or any(not (letter.isalnum() or letter == '_') for letter in value)
            for value in source_ids):
        raise voice.VoiceError('Catalogue source IDs must be safe and unique.')
    return cues


def request_for(cue):
    return dict(model=audio.MODEL, text_prompt=cue['text_prompt'],
                audio_config=dict(format='wav', sample_rate=48000))


def accepted_gate():
    review = json.loads(REVIEW.read_text(encoding='utf-8'))
    if review.get('generated') != 6 or review.get('subjectiveListening') != 'accepted' or \
            review.get('acceptedForUnity') != 6:
        raise voice.VoiceError('Six representative sounds require actual listening acceptance before batch generation.')
    if len(review.get('representatives', [])) != 6 or any(
            x.get('status') != 'accepted_after_listening' for x in review['representatives']):
        raise voice.VoiceError('Each representative needs explicit listening acceptance.')


def cached(cue, request):
    directory = OUTPUT / cue.get('sourceId', cue['id'])
    job_path = directory / 'job.json'
    if not directory.exists():
        return False
    if not job_path.exists():
        raise voice.VoiceError('Existing output has no job; investigate: ' + cue['id'])
    job = json.loads(job_path.read_text(encoding='utf-8'))
    path = directory / 'audio.wav'
    if job.get('status') != 'complete':
        raise voice.VoiceError('Prior submission unresolved; no replay: ' + cue['id'])
    if job.get('request') != request or not path.exists() or \
            hashlib.sha256(path.read_bytes()).hexdigest() != job.get('sha256'):
        raise voice.VoiceError('Completed cache is missing or changed: ' + cue['id'])
    return True


def execute(cues, allowed_replacements=frozenset()):
    accepted_gate()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    lock_path = WORK / 'catalog-generation.lock'
    try:
        with lock_path.open('x', encoding='utf-8') as lock:
            lock.write(audio.now())
    except FileExistsError:
        raise voice.VoiceError('Catalogue generation is already active or needs recovery.') from None
    try:
        key = voice.credential('VOLC_AUDIO_API_KEY')
        for cue in cues:
            request = request_for(cue)
            if cached(cue, request):
                print(json.dumps(dict(id=cue['id'], cached=True)), flush=True)
                continue
            source_id = cue.get('sourceId', cue['id'])
            if source_id != cue['id']:
                original = OUTPUT / cue['id'] / 'job.json'
                if (cue['id'] not in allowed_replacements or not original.is_file() or
                        json.loads(original.read_text(encoding='utf-8')).get('status') != 'submission_unknown'):
                    raise voice.VoiceError('Replacement requires explicit --replace-unknown and an unresolved original: ' + cue['id'])
            if committed_maximum_cost() + Decimal(2) > Decimal(60):
                raise voice.VoiceError('Next request could exceed the user-authorized 60 CNY ceiling.')
            directory = OUTPUT / source_id
            directory.mkdir()
            record = dict(id=source_id, cueId=cue['id'], model=audio.MODEL, endpoint=audio.ENDPOINT,
                          requestId=str(uuid.uuid4()), request=request, status='submission_unknown',
                          submittedAt=audio.now(), credentialVariable='VOLC_AUDIO_API_KEY',
                          contentAccepted=False, subjectiveListening='not_performed')
            voice.write_json(directory / 'job.json', record)
            print(json.dumps(dict(generating=source_id, cueId=cue['id'], targetSeconds=cue['targetSeconds'])), flush=True)
            try:
                response = audio.submit(request, key, record['requestId'])
                wav = base64.b64decode(response['audio'], validate=True)
                (directory / 'audio.wav').write_bytes(wav)
                qa = audio.inspect_wav(wav)
                voice.write_json(directory / 'qa.json', qa)
                record.update(status='complete', sha256=qa['sha256'],
                              originalDurationSeconds=response.get('original_duration'),
                              actualSeconds=qa['durationSeconds'], channels=qa['channels'],
                              clippedSamples=qa['clippedSamples'])
            except Exception as error:
                record['error'] = str(error) if isinstance(error, voice.VoiceError) else type(error).__name__
                record['diagnostic'] = getattr(error, 'diagnostic', {})
                record['completedAt'] = audio.now()
                voice.write_json(directory / 'job.json', record)
                raise voice.VoiceError('Catalogue generation stopped at ' + source_id + ': ' + record['error']) from None
            record['completedAt'] = audio.now()
            voice.write_json(directory / 'job.json', record)
            print(json.dumps(dict(complete=source_id, cueId=cue['id'], seconds=record['actualSeconds'],
                                  channels=record['channels'], clippedSamples=record['clippedSamples'])), flush=True)
    finally:
        lock_path.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ids', nargs='+')
    parser.add_argument('--all', action='store_true')
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--replace-unknown', help='Explicit canonical cue ID whose unresolved request is replaced under a distinct sourceId')
    args = parser.parse_args()
    if bool(args.ids) == bool(args.all):
        raise voice.VoiceError('Choose --ids or --all.')
    cues = authored()
    if args.ids:
        chosen = set(args.ids)
        if len(chosen) != len(args.ids) or chosen - {x['id'] for x in cues}:
            raise voice.VoiceError('Unknown or duplicate catalogue ID.')
        cues = [x for x in cues if x['id'] in chosen]
    if args.replace_unknown and (not args.execute or not args.ids or
            args.replace_unknown not in args.ids or len(args.ids) != 1 or
            cues[0].get('sourceId', cues[0]['id']) == cues[0]['id']):
        raise voice.VoiceError('--replace-unknown requires --execute --ids <same cue> with a distinct authored sourceId.')
    print(json.dumps(dict(execute=args.execute, count=len(cues),
                          targetSeconds=sum(x['targetSeconds'] for x in cues),
                          maximumCNYPerRequest='2',
                          ids=[x['id'] for x in cues]), ensure_ascii=False), flush=True)
    if args.execute:
        execute(cues, {args.replace_unknown} if args.replace_unknown else frozenset())


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
    try:
        main()
    except (voice.VoiceError, ValueError, KeyError, OSError) as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(1)
