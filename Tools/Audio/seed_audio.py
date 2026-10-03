"""Domestic Seed Audio authoring; bounded cash ledger, explicit generation only.

Source audio stays outside Assets. Preview never reads credentials or writes.
This client does not query balances or enable/pay for any service.
"""
import argparse
import base64
from datetime import datetime, timezone
from decimal import Decimal, ROUND_CEILING
import hashlib
import io
import json
import math
from pathlib import Path
import re
import sys
import urllib.error
import urllib.request
import uuid
import wave

import volc_voice as voice

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'ArtSource/Audio/SeedAudio20260929'
ENDPOINT = 'https://openspeech.bytedance.com/api/v3/tts/create'
MODEL = 'seed-audio-1.0'
BUDGET = Decimal('60')
RATE_PER_MINUTE = Decimal('1')
MAX_SECONDS = 120
RESERVE = Decimal('2')
API_DOC = 'https://docs.volcengine.com/docs/DoubaoVoice/audio-generation-http?lang=zh'
RATE_DOC = 'https://docs.volcengine.com/docs/DoubaoVoice/Billinginstructions-21?lang=zh'
PROBES = {
    'LASER': ('激光', 4, '一束科幻舰炮激光独立发射音效。0.2秒静音后，锐利的电子放电起音，极短的能量扫频向下收束，细小金属共振尾音在2秒内自然衰减，末尾留干净静音。近距离、干燥、清晰，不含爆炸或其他事件。'),
    'METAL': ('金属贯穿', 6, '一枚坚硬光滑物体高速贯穿厚重舰船钢板。短而有重量的撞击起音，紧接鋼板撕裂、刮擦，低沉船壳共振与几片金属碎片落下，5秒内自然衰减至静音。单次动作，近景音效，紧致而不混浊，不含爆炸。'),
    'EXPLOSION': ('舰船爆炸', 8, '一艘大型科幻舰船反应堆爆炸。单次强劲但不削波的低频冲击起音，随后多层沉重轰鸣扩散，稀疏金属残骸撞击在远处衰减，最后两秒安静。电影听觉表达，瞬态清楚，没有第二次独立爆炸。'),
    'FLIGHT': ('飞行持续层', 16, '水滴飞行器巡航的抽象电子共振持续层。低沉、光滑、冷峻的磁性机械嗡鸣，细微高频泛音缓慢漂移，音量稳定，开头与结尾质地相近以便循环。无明显启动、停止或高潮，不是喷气飞机或风声。'),
    'CABIN': ('平静舰桥环境', 20, '空旷而安静的科幻舰桥内部环境。低电平通风与设备低鸣，远处偶尔轻微继电器咔嗒，近处微弱电子运转。稳定空间感与克制的金属短混响，首尾环境一致可循环。没有人物活动、脚步、警报或戏剧性事件。'),
    'MUSIC': ('无歌词阶段配乐', 65, '原创冷峻电影科幻纯器乐背景音乐。缓慢的低音合成器脉冲，稀疏暗色和弦，细微金属颗粒节奏，克制的紧张感，留出对白中频空间。完整但开放的段落，约60秒持续发展，最后5秒自然回到开头和声质地，适合交叉淡化循环。不要人声或人声采样，不要歌词、吟唱、口哨，不模仿既有曲目。'),
}


def now():
    return datetime.now(timezone.utc).isoformat()


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    voice.write_json(path, value)


def probe(category, revision=1):
    if category not in PROBES or revision not in (1, 2, 3):
        raise voice.VoiceError('Only six representatives, at most three takes each.')
    label, seconds, description = PROBES[category]
    item = dict(id=f'{category}_R{revision}', category=category, label=label,
                targetSeconds=seconds, revision=revision)
    if revision == 1:
        item['prompt'] = f'生成约{seconds}秒音频。{description} 直接输出所描述的声音。不要读出提示词，不要旁白、对白、说话、口技、呼吸或人群。' + ('不要音乐。' if category != 'MUSIC' else '')
    else:
        path = WORK / 'revisions.json'
        revisions = json.loads(path.read_text(encoding='utf-8')) if path.exists() else {}
        revised = revisions.get(item['id'], {})
        if not revised.get('reason') or not revised.get('prompt'):
            raise voice.VoiceError('Revision requires saved prompt and observed reason.')
        item.update(prompt=revised['prompt'], reason=revised['reason'])
    return item


def payload(item):
    if not item['prompt'].strip() or len(item['prompt']) > 3000:
        raise voice.VoiceError('Seed Audio prompt must contain 1-3000 characters.')
    return dict(model=MODEL, text_prompt=item['prompt'],
                audio_config=dict(format='wav', sample_rate=48000))


def request_hash(request):
    return hashlib.sha256(json.dumps(request, sort_keys=True, ensure_ascii=False).encode()).hexdigest()


def load_ledger():
    path = WORK / 'audio-ledger.json'
    if path.exists():
        result = json.loads(path.read_text(encoding='utf-8'))
        if (Decimal(result['budgetCNY']) != BUDGET or result['endpoint'] != ENDPOINT
                or result['model'] != MODEL or Decimal(result['rateCNYPerMinute']) != RATE_PER_MINUTE
                or result['maximumSecondsPerRequest'] != MAX_SECONDS):
            raise voice.VoiceError('Ledger policy changed; refusing submission.')
        return result
    return dict(budgetCNY=str(BUDGET), rateCNYPerMinute=str(RATE_PER_MINUTE),
                maximumSecondsPerRequest=MAX_SECONDS, endpoint=ENDPOINT, model=MODEL,
                rateSource=RATE_DOC, apiSource=API_DOC,
                billingNote='Budget from user; no balance query. Costs computed from returned original_duration, not an invoice. Unknown usage retains full reservation.',
                entries=[])


def cost(entry):
    duration = entry.get('originalDurationSeconds')
    if duration is None:
        return Decimal(entry['reservedCNY'])
    value = Decimal(str(duration))
    if not value.is_finite() or value <= 0 or value > MAX_SECONDS:
        raise voice.VoiceError('Invalid service billing duration; reserve retained.')
    # Whole-second ceiling also covers a provider rounding fractional seconds.
    return (value.to_integral_value(rounding=ROUND_CEILING) * RATE_PER_MINUTE / 60).quantize(Decimal('0.000001'), rounding=ROUND_CEILING)


def save_ledger(ledger):
    known = sum((cost(e) for e in ledger['entries'] if e.get('originalDurationSeconds') is not None), Decimal(0))
    unknown = sum((cost(e) for e in ledger['entries'] if e.get('originalDurationSeconds') is None), Decimal(0))
    ledger.update(serviceDurationCostCNY=str(known), unknownReservedCNY=str(unknown),
                  accountedCNY=str(known + unknown), updatedAt=now())
    write(WORK / 'audio-ledger.json', ledger)


def safe_error(data, key):
    try:
        value = json.loads(data)
        code = value.get('code', value.get('error', {}).get('code') if isinstance(value.get('error'), dict) else None)
        message = str(value.get('message', value.get('msg', '')))
        message = message.replace(key, '[redacted]') if key else message
        message = re.sub(r'https?://\S+', '[url omitted]', message)
        message = re.sub(r'[A-Za-z0-9_-]{32,}', '[opaque value omitted]', message)
        return dict(serviceCode=code if isinstance(code, int) else '[non-numeric code omitted]', serviceMessage=message[:300])
    except (ValueError, AttributeError):
        return dict(serviceMessage='Non-JSON error body omitted.')


def submit(request, key, request_id, app_id=None):
    headers = {'Content-Type': 'application/json', 'X-Api-Request-Id': request_id}
    if app_id:
        headers.update({'X-Api-App-Id': app_id, 'X-Api-Access-Key': key,
                        'X-Api-Resource-Id': 'volc.service_type.10074'})
    else:
        headers['X-Api-Key'] = key
    req = urllib.request.Request(ENDPOINT, json.dumps(request, ensure_ascii=False).encode(), headers)
    try:
        with urllib.request.build_opener(voice.NoRedirect()).open(req, timeout=240) as response:
            raw = response.read(64 * 1024 * 1024 + 1)
        if len(raw) > 64 * 1024 * 1024:
            raise voice.VoiceError('Response exceeded safe audio size; submission remains uncertain.')
        result = json.loads(raw)
        if result.get('code', 0) != 0:
            error = voice.VoiceError('Seed Audio service rejected generation; no retry.')
            error.diagnostic = safe_error(raw, key)
            raise error
        return result
    except urllib.error.HTTPError as cause:
        error = voice.VoiceError(f'HTTP {cause.code}; no automatic retry.')
        error.diagnostic = dict(httpStatus=cause.code, **safe_error(cause.read(16384), key))
        raise error from None
    except (urllib.error.URLError, TimeoutError, OSError, ValueError):
        raise voice.VoiceError('Network/response failure; submission may have reached service. No retry.') from None


def inspect_wav(audio):
    with wave.open(io.BytesIO(audio), 'rb') as wav:
        info = dict(channels=wav.getnchannels(), sampleRate=wav.getframerate(),
                    sampleWidth=wav.getsampwidth(), frames=wav.getnframes())
        pcm = wav.readframes(info['frames'])
    if not info['frames'] or len(pcm) != info['frames'] * info['channels'] * info['sampleWidth']:
        raise voice.VoiceError('Incomplete WAV; not publishable.')
    info['durationSeconds'] = info['frames'] / info['sampleRate']
    if info['durationSeconds'] > 121:
        raise voice.VoiceError('Output exceeds documented maximum; investigate billing.')
    import numpy as np
    if info['sampleWidth'] != 2:
        raise voice.VoiceError('Non-16-bit WAV retained raw; needs separate decoding check.')
    samples = np.frombuffer(pcm, dtype='<i2').astype(np.float64) / 32768
    peak = float(np.max(np.abs(samples)))
    rms = float(np.sqrt(np.mean(samples ** 2)))
    info.update(peakDbFS=20 * math.log10(max(peak, 1e-12)), rmsDbFS=20 * math.log10(max(rms, 1e-12)),
                clippedSamples=int(np.sum(np.abs(samples) >= 32767 / 32768)),
                silenceFraction=float(np.mean(np.abs(samples) < .0001)),
                sha256=hashlib.sha256(audio).hexdigest(), subjectiveListening='not_performed', contentAccepted=False)
    return info


def execute(item):
    request = payload(item)
    WORK.mkdir(parents=True, exist_ok=True)
    lock_path = WORK / 'audio-generation.lock'
    try:
        lock = lock_path.open('x', encoding='utf-8')
    except FileExistsError:
        raise voice.VoiceError('Generation lock exists; investigate owner before continuing.') from None
    try:
        with lock:
            lock.write(now())
        ledger = load_ledger()
        existing = next((e for e in ledger['entries'] if e['id'] == item['id']), None)
        if existing:
            directory = WORK / 'raw' / item['id']
            audio_path = directory / 'audio.wav'
            if existing['request'] != request or existing['status'] != 'complete':
                raise voice.VoiceError('Immutable existing request is not reusable; no resubmission.')
            if not audio_path.exists() or hashlib.sha256(audio_path.read_bytes()).hexdigest() != existing.get('sha256'):
                raise voice.VoiceError('Complete cache hash mismatch.')
            return dict(id=item['id'], cached=True, status='complete')
        if any(e['status'] != 'complete' and not e.get('reviewedAuthRejection') for e in ledger['entries']):
            raise voice.VoiceError('Prior request unresolved; stop new calls and retain reservation.')
        if sum((cost(e) for e in ledger['entries']), Decimal(0)) + RESERVE > BUDGET:
            raise voice.VoiceError('60 CNY budget would be exceeded.')
        credential_variable = 'VOLC_AUDIO_API_KEY'
        try:
            key = voice.credential(credential_variable)
        except voice.VoiceError:
            credential_variable = 'ARK_API_KEY'
            key = voice.credential(credential_variable)
        directory = WORK / 'raw' / item['id']
        if directory.exists():
            raise voice.VoiceError('Unledgered output exists; investigate before submission.')
        directory.mkdir(parents=True)
        entry = dict(**item, request=request, requestSha256=request_hash(request),
                     requestId=str(uuid.uuid4()), submittedAt=now(), reservedCNY=str(RESERVE),
                     status='submission_unknown', originalDurationSeconds=None,
                     credentialVariable=credential_variable, endpoint=ENDPOINT)
        ledger['entries'].append(entry)
        save_ledger(ledger)
        write(directory / 'job.json', entry)
        print(json.dumps(dict(submitting=item['id'], reservedCNY=str(RESERVE))), flush=True)
        try:
            response = submit(request, key, entry['requestId'])
            original = Decimal(str(response.get('original_duration')))
            if not original.is_finite() or not 0 < original <= MAX_SECONDS:
                raise voice.VoiceError('Missing/invalid original_duration; full reservation retained.')
            entry['originalDurationSeconds'] = str(original)
            audio = base64.b64decode(response['audio'], validate=True)
            (directory / 'audio.wav').write_bytes(audio)
            qa = inspect_wav(audio)
            write(directory / 'qa.json', qa)
            entry.update(status='complete', sha256=qa['sha256'], actualSeconds=qa['durationSeconds'],
                         contentAccepted=False, subjectiveListening='not_performed')
        except Exception as error:
            entry['error'] = str(error) if isinstance(error, voice.VoiceError) else type(error).__name__
            if hasattr(error, 'diagnostic'):
                entry['diagnostic'] = error.diagnostic
            entry['completedAt'] = now()
            write(directory / 'job.json', entry)
            save_ledger(ledger)
            raise voice.VoiceError(entry['error']) from None
        entry['completedAt'] = now()
        write(directory / 'job.json', entry)
        save_ledger(ledger)
        return dict(id=item['id'], status=entry['status'], actualSeconds=entry['actualSeconds'],
                    costCNY=str(cost(entry)), contentAccepted=False)
    finally:
        lock_path.unlink()


def review_auth_rejection(identifier, reason):
    """Reviewed credential correction, never a replay or release of reserved cost."""
    if not reason or not reason.strip():
        raise voice.VoiceError('A concrete review reason is required.')
    lock_path = WORK / 'audio-generation.lock'
    try:
        lock = lock_path.open('x', encoding='utf-8')
    except FileExistsError:
        raise voice.VoiceError('Generation is active; cannot review concurrently.') from None
    try:
        lock.close()
        ledger = load_ledger()
        entry = next((e for e in ledger['entries'] if e['id'] == identifier), None)
        if not entry or entry.get('diagnostic', {}).get('httpStatus') != 401 or entry.get('diagnostic', {}).get('serviceCode') != 45000010:
            raise voice.VoiceError('Only the documented invalid-key rejection can be reviewed here.')
        if entry.get('credentialVariable') != 'ARK_API_KEY':
            raise voice.VoiceError('This correction is limited to the rejected Ark credential.')
        corrected = voice.credential('VOLC_AUDIO_API_KEY')
        if corrected == voice.credential('ARK_API_KEY'):
            raise voice.VoiceError('The newly configured credential is unchanged; do not retry.')
        entry['reviewedAuthRejection'] = dict(at=now(), reason=reason,
            nextCredentialVariable='VOLC_AUDIO_API_KEY', policy='Original ID is never resubmitted; full 2 CNY reserve retained.')
        write(WORK / 'raw' / identifier / 'job.json', entry)
        save_ledger(ledger)
        return dict(id=identifier, reviewed=True, reservedCNY=entry['reservedCNY'])
    finally:
        lock_path.unlink()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['preview', 'generate', 'catalog', 'review-auth'])
    parser.add_argument('--ids', nargs='+', choices=list(PROBES), default=list(PROBES))
    parser.add_argument('--round', type=int, choices=[1, 2, 3], default=1)
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--request-id')
    parser.add_argument('--reason')
    args = parser.parse_args()
    if args.command == 'review-auth':
        print(json.dumps(review_auth_rejection(args.request_id, args.reason)))
        return
    if args.command == 'catalog':
        import seed_audio_catalog
        result = seed_audio_catalog.catalog()
        if args.write:
            write(WORK / 'catalog.json', result)
        print(json.dumps(dict(count=len(result['cues']), written=args.write)))
        return
    if args.command == 'preview' and (args.execute or args.write):
        raise voice.VoiceError('Preview must remain read-only.')
    items = [probe(category, args.round) for category in args.ids]
    print(json.dumps(dict(endpoint=ENDPOINT, model=MODEL, execute=args.command == 'generate' and args.execute,
                         perRequestMaximumReservedCNY=str(RESERVE),
                         requests=[dict(id=item['id'], payload=payload(item)) for item in items]), ensure_ascii=False, indent=2), flush=True)
    if args.command == 'generate' and args.execute:
        for item in items:
            print(json.dumps(execute(item), ensure_ascii=False), flush=True)


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
    try:
        main()
    except (voice.VoiceError, ValueError, OSError) as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(1)
