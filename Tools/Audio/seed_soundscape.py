"""Seed-TTS-only, bounded capability trial. No Unity writes or procedural audio.

Preview is read-only. Explicit execute keeps a durable budget reservation before
delegating to the existing volc_voice client. Content acceptance is a separate
gate; a valid WAV alone never passes it.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
from decimal import Decimal
import hashlib
import html
import json
import os
from pathlib import Path
import re
import sys
import wave

import volc_voice as voice

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'ArtSource/Audio/SeedTTS20260929'
EVIDENCE = ROOT / 'docs/verification/SeedTTS-Soundscape-20260929'
BUDGET_AFP = Decimal('20000')
AFP_PER_CHARACTER = Decimal('0.135')
SPEAKER = 'en_male_tim_uranus_bigtts'
FAMILIES = [
    ('music', 'Ready', 1, 'Mission.Ready', 60, True),
    ('music', 'Narrative', 4, 'Narrative.Stage0..3', 60, True),
    ('music', 'BattleLow', 2, 'Battle.IntensityLow', 60, True),
    ('music', 'BattleMedium', 2, 'Battle.IntensityMedium', 60, True),
    ('music', 'BattleHigh', 2, 'Battle.IntensityHigh', 60, True),
    ('music', 'Aftermath', 1, 'Battle.ActivityDecay', 60, True),
    ('music', 'PauseBed', 1, 'Mission.Paused', 45, True),
    ('music', 'ResultBed', 2, 'Mission.ResultsSuccessOrIncomplete', 60, True),
    ('music', 'Transition', 4, 'NarrativeOrMission.PhaseEdge', 4, False),
    ('ambience', 'SpaceTexture', 3, 'Listener.AbstractCinematicBed', 30, True),
    ('ambience', 'CabinBed', 3, 'Radio.CalmAlertEvacuationScene', 20, True),
    ('flight', 'Cruise', 2, 'Motor.ExecutedSpeed', 12, True),
    ('flight', 'BoostLoop', 2, 'Motor.ExecutedBoost', 12, True),
    ('flight', 'BoostEnter', 3, 'Motor.BoostRisingEdge', 1.5, False),
    ('flight', 'BoostRelease', 3, 'Motor.BoostFallingEdge', 1.5, False),
    ('flight', 'Brake', 3, 'Motor.BrakingRisingEdge', 2, False),
    ('flight', 'Turn', 3, 'Motor.ExecutedTurnThreshold', 1, False),
    ('flight', 'Recover', 2, 'Motor.BrakingFallingEdge', 1.5, False),
    ('combat', 'Laser', 6, 'Combat.WeaponFired', .8, False),
    ('combat', 'Reflect', 6, 'Combat.DropletContact', .8, False),
    ('combat', 'Penetration', 6, 'Combat.HullPenetratedDroplet', 1.8, False),
    ('combat', 'LaserBreach', 3, 'Combat.HullPenetratedLaser', 1.2, False),
    ('combat', 'Explosion', 6, 'Combat.ShipExploded', 5, False),
    ('combat', 'Reactor', 3, 'Combat.ReactorUnstableUntilExploded', 4, True),
    ('combat', 'RetreatEngine', 3, 'Combat.RetreatOrderedUntilEscaped', 8, True),
    ('combat', 'Escape', 2, 'Combat.ShipEscaped', .6, False),
    ('cabin', 'DeckSteps', 3, 'Radio.AuthoredAction', 4, False),
    ('cabin', 'Console', 3, 'Radio.AuthoredAction', 1, False),
    ('cabin', 'Hatch', 3, 'Radio.AuthoredAction', 2, False),
    ('cabin', 'Alarm', 3, 'Radio.AuthoredAlarm', 6, True),
    ('cabin', 'Connect', 3, 'Radio.LinkConnected', .4, False),
    ('cabin', 'Disconnect', 3, 'Radio.LinkInterrupted', .3, False),
    ('cabin', 'Interference', 4, 'Radio.AuthoredSignalDetail', .7, False),
    ('ui', 'Focus', 2, 'UI.FocusChanged', .15, False),
    ('ui', 'Confirm', 2, 'UI.Confirmed', .25, False),
    ('ui', 'Back', 2, 'UI.Returned', .25, False),
    ('ui', 'Toggle', 2, 'UI.ToggleChanged', .2, False),
    ('ui', 'Slider', 2, 'UI.SliderStepChanged', .12, False),
    ('ui', 'Pause', 1, 'Mission.PausedEdge', .35, False),
    ('ui', 'Resume', 1, 'Mission.ResumedEdge', .35, False),
    ('ui', 'Skip', 1, 'Narrative.Skipped', .5, False),
    ('ui', 'Restart', 1, 'Mission.Restarted', .6, False),
    ('ui', 'History', 2, 'Radio.HistoryToggled', .25, False),
    ('mission', 'Boundary', 2, 'Mission.NearBoundaryRisingEdge', 1, False),
    ('mission', 'Return', 2, 'Mission.RecoveryCountChanged', 1, False),
    ('mission', 'TimeWarning', 2, 'Mission.RemainingCrosses60Or30', .8, False),
    ('mission', 'Countdown', 2, 'Mission.Last10Seconds', .2, False),
    ('mission', 'Combo', 3, 'Score.MultiplierIncreased', .5, False),
    ('mission', 'ComboCap', 1, 'Score.MultiplierReachedCap', .8, False),
    ('mission', 'ResultSting', 3, 'Mission.ClearedEscapedOrTimeout', 4, False),
]
PROBES = {
    'LASER': ('激光', '一束短促的电子激光，尖锐起音迅速向下滑落，留下很短的金属尾音。', 1.5),
    'METAL': ('金属贯穿', '厚重钢板被高速尖物贯穿，先是尖锐撕裂，随后是低沉金属共振和碎片落下。', 3),
    'EXPLOSION': ('爆炸', '一次沉重的科幻舰船爆炸，瞬间冲击后低频轰鸣扩散，金属碎片尾音逐渐消失。', 5),
    'FLIGHT': ('飞行持续声', '持续稳定的低频电子共振，柔和高频谐波缓慢变化，冷峻、平滑、适合高速飞行循环。', 8),
    'CABIN': ('舰内环境', '安静舰桥的设备低鸣，远处通风与偶尔细小继电器咔嗒声，稳定且稀疏。', 8),
    'MUSIC': ('无歌词背景', '冷峻科幻纯器乐背景，缓慢低音合成器脉冲与稀疏暗色和弦，克制紧张，没有人声。', 12),
}


def now():
    return datetime.now(timezone.utc).isoformat()


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    voice.write_json(path, data)


def catalog():
    cues = []
    for group, family, count, trigger, duration, loop in FAMILIES:
        for variant in range(1, count + 1):
            cues.append(dict(id=f'{family}_{variant:02}', category=group,
                             trigger=trigger, targetSeconds=duration, loop=loop,
                             provider='seed-tts', resource='seed-tts-2.0',
                             status='planned_not_generated', variant=variant))
    if len(cues) != 131 or len({x['id'] for x in cues}) != 131:
        raise voice.VoiceError('Catalog identity/count mismatch.')
    return dict(style='冷峻电影科幻', nonSpeechTarget=131, additionalVoiceLimit=24,
                budgetAFP=20000, cashBudget=0, requiredGate=list(PROBES),
                categories=dict(Counter(x['category'] for x in cues)), cues=cues)


def probe(identifier, round_number):
    if round_number not in (1, 2, 3):
        raise voice.VoiceError('At most an initial trial and two prompt revisions are authorized.')
    if identifier == 'CONTROL':
        if round_number != 1:
            raise voice.VoiceError('Control has one explicit take; no automatic retry.')
        return dict(id='CONTROL_R1', category='voice_control', targetSeconds=5,
                    text='Keep the channel open. Stay with me. We will bring you home.',
                    direction='A calm space-fleet commander speaking to one frightened colleague. Natural connected English, gentle urgency, about five seconds. Speak only the exact line, without music or added words.')
    label, description, seconds = PROBES[identifier]
    if round_number != 1:
        path = WORK / 'revisions.json'
        revisions = json.loads(path.read_text(encoding='utf-8')) if path.exists() else {}
        key = f'{identifier}_R{round_number}'
        if key not in revisions:
            raise voice.VoiceError('Author a revision after reviewing the previous output: ' + key)
        value = revisions[key]
        if not value.get('reason') or not value.get('text', '').strip() or not value.get('direction'):
            raise voice.VoiceError('Revision needs text, direction and observed reason.')
        return dict(id=key, category=identifier, targetSeconds=seconds, **value)
    # Keep the first two already-submitted Tim trials immutable. Remaining
    # Chinese trials use the Chinese TTS2 speaker in the official HTTP example.
    speaker = SPEAKER if identifier in ('LASER', 'METAL') else 'zh_female_vv_uranus_bigtts'
    return dict(id=f'{identifier}_R1', category=identifier, label=label, targetSeconds=seconds, speaker=speaker,
                text=description,
                direction=f'这是游戏声音素材制作，不是朗读。合成文本描述了声音事件，请直接呈现这个事件的声音：{description} 目标约{seconds}秒。只输出声音本身，不说出描述文字，不添加旁白，不发出人声口技。'+('只要纯器乐，不要歌词、吟唱或说话。' if identifier == 'MUSIC' else '不要音乐、说话或歌词。'))


def request_plan(item):
    match = re.fullmatch(r'(CONTROL|LASER|METAL|EXPLOSION|FLIGHT|CABIN|MUSIC)_R([1-3])', item['id'])
    if not match or (match[1] == 'CONTROL' and match[2] != '1'):
        raise voice.VoiceError('Request is outside the authorized capability trial IDs.')
    if item['category'] != ('voice_control' if match[1] == 'CONTROL' else match[1]):
        raise voice.VoiceError('Request category does not match its immutable trial ID.')
    args = argparse.Namespace(provider='seed-tts', id=[item['id']], all=False,
        text=item['text'], speaker=item.get('speaker', SPEAKER), direction=item['direction'],
        speech_rate=0, take='soundscape-capability-20260929', execute=False)
    line = voice.lines_for(args)[0]
    payload = voice.payload_for(args, line)
    digest = hashlib.sha256(json.dumps(payload, sort_keys=True, ensure_ascii=False).encode()).hexdigest()[:16]
    directory = voice.OUTPUT / 'seed-tts' / args.take / (item['id'] + '-' + digest)
    # Context is documented as unbilled. Reserve it anyway, plus 4x UTF-8 bytes,
    # as a conservative upper bound until actual server text_words is available.
    reserved_units = 4 * len((item['text'] + item['direction']).encode('utf-8'))
    return args, payload, directory, Decimal(reserved_units) * AFP_PER_CHARACTER


def validate_complete(directory, payload):
    path = directory / 'job.json'
    if not path.exists():
        return None
    record = json.loads(path.read_text(encoding='utf-8'))
    if record.get('status') != 'complete':
        raise voice.VoiceError('Existing submission is uncertain; do not retry: ' + str(path))
    audio = directory / 'voice.wav'
    if record.get('request') != payload or not audio.exists() or hashlib.sha256(audio.read_bytes()).hexdigest() != record.get('sha256'):
        raise voice.VoiceError('Completed cache does not match request/audio: ' + str(path))
    return record


def settled_cost(entry):
    actual = entry.get('serviceTextUnits')
    return Decimal(str(actual)) * AFP_PER_CHARACTER if actual is not None else Decimal(entry['reservedAFP'])


def write_ledger(path, ledger):
    known = sum((Decimal(str(x['serviceTextUnits'])) * AFP_PER_CHARACTER
                 for x in ledger['entries'] if x.get('serviceTextUnits') is not None), Decimal(0))
    unknown = sum((Decimal(x['reservedAFP']) for x in ledger['entries']
                   if x.get('serviceTextUnits') is None), Decimal(0))
    ledger.update(serviceUsageConvertedAFP=str(known), unconfirmedReservedAFP=str(unknown),
                  accountedAFP=str(known + unknown),
                  completedRequests=sum(x['status'] == 'complete' for x in ledger['entries']))
    write(path, ledger)


def execute_one(item):
    args, payload, directory, reserve = request_plan(item)
    # Validate/plan before any credential read or network request.
    cached = validate_complete(directory, payload)
    ledger_path = WORK / 'ledger.json'
    WORK.mkdir(parents=True, exist_ok=True)
    lock_path = WORK / 'generation.lock'
    try:
        lock = lock_path.open('x', encoding='utf-8')
    except FileExistsError:
        raise voice.VoiceError('A generation owner/unfinished lock exists; investigate before proceeding.') from None
    try:
        with lock:
            lock.write(now())
        ledger = json.loads(ledger_path.read_text(encoding='utf-8')) if ledger_path.exists() else dict(
            budgetAFP=str(BUDGET_AFP), afpPerCharacter=str(AFP_PER_CHARACTER),
            billingNote='AFP conversion uses service text_words when returned; otherwise a conservative reservation. Not a console invoice.',
            rateSource='https://docs.volcengine.com/docs/ark/agent-plan-personal-afp-credits-billing-rules?lang=zh', entries=[])
        if Decimal(ledger['budgetAFP']) != BUDGET_AFP:
            raise voice.VoiceError('Unexpected persisted budget; refusing to increase it.')
        existing = next((x for x in ledger['entries'] if x['id'] == item['id']), None)
        if existing:
            if existing['payload'] != payload:
                raise voice.VoiceError('Immutable trial ID has changed; use a reviewed revision.')
            if cached is None:
                raise voice.VoiceError('Reserved request without a complete cache; investigate, no resubmission.')
            entry = existing
        elif cached is not None:
            raise voice.VoiceError('Unledgered cache detected; reconcile before importing.')
        else:
            if any(x['status'] != 'complete' and not x.get('reviewedUncertainty') for x in ledger['entries']):
                raise voice.VoiceError('Prior uncertain/failed submission blocks new paid requests.')
            if sum((settled_cost(x) for x in ledger['entries']), Decimal(0)) + reserve > BUDGET_AFP:
                raise voice.VoiceError('Request would exceed the 20,000 AFP budget.')
            entry = dict(id=item['id'], category=item['category'], submittedAt=now(),
                payload=payload, job=str((directory / 'job.json').relative_to(ROOT)),
                reservedAFP=str(reserve), status='submission_unknown', contentAcceptance='not_reviewed')
            ledger['entries'].append(entry)
            write_ledger(ledger_path, ledger)
            try:
                # The existing client remains the sole transport/credential reader.
                args.execute = True
                voice.generate(args)
                cached = validate_complete(directory, payload)
                if cached is None:
                    raise voice.VoiceError('No complete job after synthesis.')
            except (voice.VoiceError, ValueError, KeyError, OSError) as error:
                # volc_voice sanitizes errors; never persist raw response headers/bodies.
                entry['error'] = str(error)
                if hasattr(error, 'stream_summary'):
                    entry['streamSummary'] = error.stream_summary
                write_ledger(ledger_path, ledger)
                raise
        usage = cached.get('usage') or {}
        units = usage.get('text_words')
        if isinstance(units, (int, float)) and not isinstance(units, bool) and units >= 0:
            entry['serviceTextUnits'] = units
        entry.update(status='complete', usage=usage, sha256=cached['sha256'], durationSeconds=cached['duration_seconds'])
        ledger['accountedAFP'] = str(sum((settled_cost(x) for x in ledger['entries']), Decimal(0)))
        ledger['completedRequests'] = sum(x['status'] == 'complete' for x in ledger['entries'])
        write_ledger(ledger_path, ledger)
        return entry
    finally:
        lock_path.unlink(missing_ok=True)


def analyze(path):
    import numpy as np
    with wave.open(str(path), 'rb') as wav:
        frames, rate, channels, width = wav.getnframes(), wav.getframerate(), wav.getnchannels(), wav.getsampwidth()
        if width != 2 or channels != 1 or rate != 24000:
            raise voice.VoiceError('Unexpected native PCM format: ' + str(path))
        signal = np.frombuffer(wav.readframes(frames), dtype='<i2').astype(np.float64) / 32768
    if signal.size == 0:
        raise voice.VoiceError('Empty WAV cannot pass signal QA: ' + str(path))
    peak = float(np.max(np.abs(signal))) if signal.size else 0
    rms = float(np.sqrt(np.mean(signal * signal))) if signal.size else 0
    active = np.flatnonzero(np.abs(signal) >= 10 ** (-50 / 20))
    edge_frames = min(frames, rate // 4)
    edge_db = lambda value: float(20 * np.log10(max(float(np.sqrt(np.mean(value * value))), 1e-12)))
    return dict(durationSeconds=frames / rate, sampleRate=rate, channels=channels,
        peakDbFS=float(20 * np.log10(max(peak, 1e-12))), rmsDbFS=float(20 * np.log10(max(rms, 1e-12))),
        clippedSamples=int(np.sum(np.abs(signal) >= 32767/32768)),
        leadingBelowMinus50Seconds=float(active[0] / rate) if active.size else frames / rate,
        trailingBelowMinus50Seconds=float((frames - active[-1] - 1) / rate) if active.size else frames / rate,
        first250msRmsDbFS=edge_db(signal[:edge_frames]), last250msRmsDbFS=edge_db(signal[-edge_frames:]),
        rawLoopEndpointStep=float(abs(signal[-1] - signal[0])),
        loopAcceptance='not_established_by_endpoint_measurement',
        sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        contentAcceptance='not_established_by_signal_analysis')


def audit(publish=False):
    path = WORK / 'ledger.json'
    ledger = json.loads(path.read_text(encoding='utf-8')) if path.exists() else {'entries': []}
    review_path = WORK / 'content-review.json'
    review = json.loads(review_path.read_text(encoding='utf-8')) if review_path.exists() else {}
    decisions = {x['id']: x for x in review.get('candidates', [])}
    checks, sections = [], []
    for entry in ledger['entries']:
        decision = decisions.get(entry['id'], {})
        if decision and decision.get('sha256') != entry.get('sha256'):
            raise voice.VoiceError('Content review is for different audio: ' + entry['id'])
        directory = (ROOT / entry['job']).parent
        try:
            record = validate_complete(directory, entry['payload'])
        except voice.VoiceError as error:
            checks.append(dict(id=entry['id'], generated=False, error=str(error),
                               reservedAFP=entry['reservedAFP']))
            sections.append(f'<article><h2>{html.escape(entry["id"])}</h2><p class="status">未产出完整音频</p><p>提交与计费状态保留待核对；原请求不会自动重发。</p></article>')
            continue
        if record is None:
            continue
        result = dict(id=entry['id'], **analyze(directory / 'voice.wav'), review=decision)
        checks.append(result)
        relative = Path(os.path.relpath(directory / 'voice.wav', WORK)).as_posix()
        text = entry['payload']['req_params']['text']
        direction = json.loads(entry['payload']['req_params']['additions'])['context_texts'][0]
        notes = ''.join(f'<li>{html.escape(x)}</li>' for x in decision.get('evidence', []))
        transcript = html.escape(decision.get('transcript', '未执行或无识别结果；不能证明无语音'))
        sections.append(f'<article><h2>{html.escape(entry["id"])}</h2>'
            f'<p class="status">{html.escape(decision.get("label", "内容待验收"))}</p>'
            f'<audio controls preload="none" src="{html.escape(relative)}"></audio>'
            f'<p>{result["durationSeconds"]:.3f} 秒 · 24 kHz / mono / PCM 16-bit · 削波样本 {result["clippedSamples"]}</p>'
            f'<ul>{notes}</ul><details><summary>请求、辅助转写与来源</summary>'
            f'<p>合成文本：{html.escape(text)}</p><p>context_texts：{html.escape(direction)}</p>'
            f'<p>离线转写：{transcript}</p><p>SHA-256：<code>{result["sha256"]}</code></p>'
            f'<a href="{html.escape(relative)}" download>原始 WAV</a></details></article>')
    result = dict(clips=checks, generated=sum('sha256' in x for x in checks), gatePassed=False,
                  acceptedNonSpeech=0, subjectiveListening='not_performed',
                  gateReason='No six-category acoustic acceptance; no batch generation or Unity publishing.',
                  serviceUsageConvertedAFP=ledger.get('serviceUsageConvertedAFP'),
                  unconfirmedReservedAFP=ledger.get('unconfirmedReservedAFP'),
                  accountedAFP=ledger.get('accountedAFP'))
    if publish:
        write(EVIDENCE / 'signal-qa.json', result)
        WORK.mkdir(parents=True, exist_ok=True)
        page = '''<!doctype html><html lang="zh-CN"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>Seed-TTS 2.0 能力实测</title>
<style>body{font:16px/1.7 system-ui;margin:0;background:#101923;color:#e0e9ee}main{max-width:960px;margin:40px auto;padding:0 20px}article{padding:24px;margin:18px 0;background:#182633;border:1px solid #344a5d;border-radius:12px}audio{width:100%}h1,h2{line-height:1.25}h2{font-size:21px}.status{color:#ffd08b}a{color:#9bd6ff}code{overflow-wrap:anywhere}summary{cursor:pointer}details{color:#b8cbd9}.budget{border-left:4px solid #efbd72;padding-left:18px}nav{display:flex;gap:18px;flex-wrap:wrap}</style></head><body><main>
<p>2026-09-29 · Seed-TTS 2.0 · 能力试验</p><h1>原始样例与验收证据</h1>
<p class="status">代表素材门槛未通过 · 批量制作和 Unity 发布未开始</p>
<p>共 REQUEST_COUNT 次真实请求，WAV_COUNT 个完整 WAV。每类最多三次，原样保留，没有裁切、调音或替代合成。自动转写只提供语音污染线索，不代表主观试听；短声音的空转写不能证明它是合格音效。</p>
<nav><a href="../../../docs/verification/SeedTTS-Soundscape-20260929/REPORT.md">验收报告</a><a href="ledger.json">AFP 账本</a><a href="catalog.json">131 项声音目录</a><a href="content-review.json">候选评价</a></nav>
'''
        page = page.replace('REQUEST_COUNT', str(len(ledger['entries']))).replace('WAV_COUNT', str(result['generated']))
        page += (f'<p class="budget">服务用量折算：{html.escape(str(ledger.get("serviceUsageConvertedAFP")))} AFP；'
                 f'未知用量预留：{html.escape(str(ledger.get("unconfirmedReservedAFP")))} AFP。'
                 f'预算占用合计 {html.escape(str(ledger.get("accountedAFP")))} / 20,000 AFP。预留不是已确认账单。</p>')
        page += ''.join(sections) + '''</main><script>document.addEventListener('play',e=>{if(e.target.tagName==='AUDIO')document.querySelectorAll('audio').forEach(a=>{if(a!==e.target)a.pause()})},true)</script></body></html>'''
        (WORK / 'listening.html').write_text(page, encoding='utf-8')
    return result


def review_uncertain(identifier, reason):
    if not reason.strip():
        raise voice.VoiceError('Explicit review reason required.')
    if (WORK / 'generation.lock').exists():
        raise voice.VoiceError('Cannot reconcile while generation is active.')
    path = WORK / 'ledger.json'
    ledger = json.loads(path.read_text(encoding='utf-8'))
    entry = next((x for x in ledger['entries'] if x['id'] == identifier), None)
    if entry is None or entry['status'] == 'complete':
        raise voice.VoiceError('Select an existing uncertain request.')
    entry['reviewedUncertainty'] = dict(at=now(), reason=reason,
        policy='Never resubmit this ID; reserve its full possible cost. Only separately authored requests may proceed.')
    ledger['accountedAFP'] = str(sum((settled_cost(x) for x in ledger['entries']), Decimal(0)))
    write_ledger(path, ledger)
    return dict(id=identifier, reservedAFP=entry['reservedAFP'], reviewed=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['catalog', 'preview', 'generate', 'audit', 'review-uncertain'])
    parser.add_argument('--ids', nargs='+', choices=['CONTROL', *PROBES], default=['CONTROL'])
    parser.add_argument('--round', type=int, choices=[1, 2, 3], default=1)
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--request-id')
    parser.add_argument('--reason')
    args = parser.parse_args()
    if args.command == 'review-uncertain':
        if not args.request_id or not args.reason:
            raise voice.VoiceError('--request-id and --reason are required.')
        print(json.dumps(review_uncertain(args.request_id, args.reason), ensure_ascii=False))
    elif args.command == 'catalog':
        value = catalog()
        if args.write:
            write(WORK / 'catalog.json', value)
        print(json.dumps({'target': value['nonSpeechTarget'], 'categories': value['categories'], 'written': args.write}, ensure_ascii=False))
    elif args.command == 'audit':
        print(json.dumps(audit(args.write), ensure_ascii=False, indent=2))
    else:
        if args.command == 'preview' and args.execute:
            raise voice.VoiceError('Preview cannot execute.')
        items = [probe(identifier, args.round) for identifier in args.ids]
        plans = [dict(id=x['id'], payload=request_plan(x)[1], reservedAFP=str(request_plan(x)[3])) for x in items]
        print(json.dumps(dict(execute=args.execute, requests=plans), ensure_ascii=False, indent=2), flush=True)
        if args.command == 'generate' and args.execute:
            if any(x['category'] != 'voice_control' for x in items):
                control = probe('CONTROL', 1)
                _, payload, directory, _ = request_plan(control)
                if validate_complete(directory, payload) is None:
                    raise voice.VoiceError('Complete the live dialogue control first.')
            for item in items:
                print(json.dumps(execute_one(item), ensure_ascii=False), flush=True)


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
    try:
        main()
    except (voice.VoiceError, ValueError, KeyError, OSError) as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(1)
