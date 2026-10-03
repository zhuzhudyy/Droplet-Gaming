"""Bounded single-owner Seed-TTS queue and reproducible local radio mastering."""
import argparse, array, hashlib, json, math, os, shutil, subprocess, sys, wave
from pathlib import Path
from types import SimpleNamespace
import volc_voice as voice

ROOT=voice.ROOT
SOURCE=ROOT/'Tools/Audio/EnhancementEnglish.json'
TAKE='enhancement-en-20260919'
OUT=ROOT/'Assets/_Project/Audio/EnhancementEnglish'
DATA=ROOT/'Assets/_Project/Data/Enhancement'
EVIDENCE=ROOT/'docs/verification/Enhancement-20260919/audio'
SAMPLES=['E001','E011','E012','E021']
# One finite pass only. Existing server-side quotas are unchanged, no refill or retry.
MAX_CHARACTERS=14000
MAX_REQUESTS=76
DIRECTIONS={
 'calm':'Speak in natural conversational English, calm and thoughtful, with connected phrases and subtle human hesitation. This is an original fictional radio drama. No music or sound effects.',
 'tense':'Speak in natural English as a trained professional under rising pressure, controlled but worried. Emphasize the important words, varied intonation. Original fictional radio drama. No music or sound effects.',
 'urgent':'Speak in natural English as an urgent radio warning during a fictional space emergency. Short breaths, forceful clear projection, quick decisive phrasing, controlled fear. No music or sound effects.',
}

def plans(c,ids):
    roles={r['role']:r['speaker'] for r in c['roles']}
    lines=c['narrative']+c['combat']
    if len(lines)>MAX_REQUESTS or sum(len(l['english']) for l in lines)>MAX_CHARACTERS: raise voice.VoiceError('Batch exceeds bounded authoring limits.')
    selected=[l for l in lines if ids=='all' or l['id'] in ids]
    for l in selected:
        args=SimpleNamespace(id=[l['id']],text=l['english'],all=False,speaker=roles[l['role']],direction=DIRECTIONS[l['direction']],speech_rate=5 if l['direction']=='calm' else 8,take=TAKE,execute=False)
        payload=voice.payload_for(args, {'id':l['id'],'text':l['english']})
        digest=hashlib.sha256(json.dumps(payload,sort_keys=True,ensure_ascii=False).encode()).hexdigest()[:16]
        yield l,args,voice.OUTPUT/'seed-tts'/TAKE/(l['id']+'-'+digest)

def analyze(path):
    with wave.open(str(path),'rb') as w:
        assert w.getnchannels()==1 and w.getsampwidth()==2
        sr=w.getframerate(); v=array.array('h',w.readframes(w.getnframes()))
    rms=math.sqrt(sum(x*x for x in v)/len(v))/32768
    peak=max(abs(x) for x in v)/32768
    duration=len(v)/sr
    active=sum(abs(x)>327 for x in v)/len(v)
    result=dict(duration=duration,peakDb=20*math.log10(max(peak,1e-9)),rmsDb=20*math.log10(max(rms,1e-9)),clippedSamples=sum(abs(x)>=32760 for x in v),activeRatio=active)
    if duration<.5 or rms<.001 or peak>=.9999 or active<.05: raise voice.VoiceError('Audio signal QA failed: '+str(path))
    return result

def generate(ids,execute):
    c=json.loads(SOURCE.read_text(encoding='utf8'))
    selected=list(plans(c,ids))
    print(json.dumps(dict(execute=execute,resource='seed-tts-2.0',take=TAKE,requests=len(selected),characters=sum(len(l['english']) for l,_,_ in selected),maxBatchCharacters=MAX_CHARACTERS,maxBatchRequests=MAX_REQUESTS,lines=[dict(id=l['id'],role=l['role'],speaker=a.speaker,rate=a.speech_rate) for l,a,_ in selected]),indent=2))
    if not execute:return
    if ids=='all':
        for _,_,d in plans(c,SAMPLES):
            if not (d/'voice.wav').exists():raise voice.VoiceError('Generate and signal-QA four role samples before full library.')
            analyze(d/'voice.wav')
    # Process lock prevents a second queue owner from accidentally overlapping requests.
    EVIDENCE.mkdir(parents=True,exist_ok=True)
    lock=EVIDENCE/'queue.lock'
    with lock.open('x') as f:f.write(str(os.getpid()))
    try:
        for l,a,d in selected:
            a.execute=True
            voice.generate(a)
            if not (d/'voice.wav').exists():raise voice.VoiceError('Unresolved prior submission: '+l['id']+'. Stop; no blind retry.')
            result=analyze(d/'voice.wav')
            print(l['id'],json.dumps(result),flush=True)
    finally: lock.unlink(missing_ok=True)

def make_signal(path,mode):
    sr=24000;length={'ConnectTone':.14,'InterruptTone':.19,'Alarm':1.2,'EquipmentBed':4.0}[mode];data=array.array('h')
    for i in range(int(sr*length)):
        t=i/sr;fade=min(1,t/.012,(length-t)/.022)
        if mode=='ConnectTone':v=.15*math.sin(2*math.pi*(860 if t<.07 else 1180)*t)
        elif mode=='InterruptTone':v=.12*math.sin(2*math.pi*(720-1800*t)*t)
        elif mode=='Alarm':v=.15*math.sin(2*math.pi*(660 if int(t*4)%2==0 else 880)*t)*(1 if t%.3<.22 else 0)
        else:v=.012*(math.sin(2*math.pi*90*t)+.3*math.sin(2*math.pi*180*t))
        data.append(int(max(-1,min(1,v*fade))*32767))
    with wave.open(str(path),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(sr);w.writeframes(data.tobytes())

def master():
    ffmpeg=shutil.which('ffmpeg')
    if not ffmpeg:raise voice.VoiceError('Configured FFmpeg unavailable')
    OUT.mkdir(parents=True,exist_ok=True);DATA.mkdir(parents=True,exist_ok=True);EVIDENCE.mkdir(parents=True,exist_ok=True)
    c=json.loads(SOURCE.read_text(encoding='utf8'));qa=[];cursor=0
    for l,a,d in plans(c,'all'):
        dry=d/'voice.wav'; target=OUT/(l['id']+'.wav')
        if not dry.exists():raise voice.VoiceError('Missing real Seed-TTS audio: '+l['id'])
        dryqa=analyze(dry)
        filters='highpass=f=190,lowpass=f=6200,acompressor=threshold=0.13:ratio=2.4:attack=8:release=110:makeup=1.3,loudnorm=I=-18:TP=-2:LRA=8'
        subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-y','-i',str(dry),'-af',filters,'-ar','24000','-ac','1','-c:a','pcm_s16le',str(target)],check=True)
        radioqa=analyze(target);l['duration']=round(radioqa['duration'],6)
        if l['id'].startswith('E'):
            l['time']=round(cursor,6);cursor+=l['duration']+.16
        qa.append(dict(id=l['id'],role=l['role'],speaker=a.speaker,english=l['english'],caption=l['text'],dry=str(dry.relative_to(ROOT)),radio=str(target.relative_to(ROOT)),dryQA=dryqa,radioQA=radioqa,listening='待人工试听',sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
    c['duration']=round(cursor+.25,6)
    for name in ['ConnectTone','InterruptTone','Alarm','EquipmentBed']:make_signal(OUT/(name+'.wav'),name)
    (DATA/'EnhancementEnglish.json').write_text(json.dumps(c,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    summary=dict(provider='Seed-TTS 2.0',aiSynthesized=True,ffmpeg=subprocess.run([ffmpeg,'-version'],capture_output=True,text=True).stdout.splitlines()[0],openingDuration=c['duration'],openingVoiceSeconds=sum(q['radioQA']['duration'] for q in qa if q['id'].startswith('E')),interlineGap=.16,lines=qa,listening='待人工试听 / no acoustic listening capability in this agent')
    (EVIDENCE/'audio-qa.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    (EVIDENCE/'listening-list.md').write_text('# English AI voice listening list\n\nAll voices: Seed-TTS 2.0 AI synthesis. Pending human listening (待人工试听).\n\n| ID | Role | Seconds | Audio |\n|---|---|---:|---|\n'+''.join(f'| {q["id"]} | {q["role"]} | {q["radioQA"]["duration"]:.2f} | [{q["id"]}](../../../../{q["radio"]}) |\n' for q in qa),encoding='utf8')
    print(json.dumps(dict(openingDuration=c['duration'],voiceFiles=len(qa),separateSignals=4,withinFourToSixMinutes=240<=c['duration']<=360)))
    if not 240<=c['duration']<=360:raise voice.VoiceError('Real opening duration outside 240-360 seconds. Edit unique content; do not pad or loop.')

if __name__=='__main__':
    sys.stdout.reconfigure(encoding='utf8');sys.stderr.reconfigure(encoding='utf8')
    p=argparse.ArgumentParser();p.add_argument('action',choices=['samples','all','master']);p.add_argument('--execute',action='store_true');p.add_argument('--take',default=TAKE);args=p.parse_args()
    try:
        TAKE=voice.identifier(args.take)
        if args.action=='master':master()
        else:generate(SAMPLES if args.action=='samples' else 'all',args.execute)
    except (voice.VoiceError,ValueError,OSError,subprocess.CalledProcessError) as e:print('ERROR:',e,file=sys.stderr);sys.exit(1)
