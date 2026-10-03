"""Finite Seed-TTS 2.0 authoring and deterministic, editable communications mixes.

Only generate/probe --execute can access the cloud. Everything played by Unity is local.
No provider aliases, undocumented endpoints, automatic retries, time stretching or recharge.
"""
import argparse, array, hashlib, html, json, math, os, shutil, subprocess, sys, wave
from pathlib import Path
from types import SimpleNamespace
import numpy as np
from scipy.signal import lfilter
import volc_voice as voice

ROOT=voice.ROOT
SOURCE=ROOT/'Tools/Audio/EnhancementEnglish.json'
WORK=ROOT/'ArtSource/Audio/CinematicAudio'
ASSETS=ROOT/'Assets/_Project/Audio/CinematicAudio'
DATA=ROOT/'Assets/_Project/Data/CinematicAudio'
EVIDENCE=ROOT/'docs/verification/CinematicAudio'
TAKE='cinematic-radio-en-20260919'
SR=24000
MAX_REQUESTS=88
MAX_CHARS=16000
EXTRA=[
 ('SC01','commander','Helm, hold our position. Keep that corridor open.','舵手，保持位置。让那条通道保持畅通。','calm'),
 ('SC02','comms','Course held. Batteries standing by.','航向已保持。炮组待命。','calm'),
 ('SA01','commander','We have a hit on the bridge! Seal the forward doors. Stay at your stations.','舰桥遭到撞击！封闭前舱门。坚守岗位。','urgent'),
 ('SA02','engineer','Fire under the console! Cutting power now!','控制台下面起火了！正在切断电源！','urgent'),
 ('SE01','commander','All hands, abandon the bridge! Move!','全体人员，撤离舰桥！快！','urgent'),
 ('SE02','engineer','The hatch is jammed!','舱门卡住了！','urgent'),
 ('SE03','commander','Use the aft passage! Keep moving!','走后方通道！不要停！','urgent'),
 ('BG01','comms','Aye, checking the panel.','收到，正在检查面板。','calm'),
 ('BG02','engineer','Get back!','退后！','urgent'),
 ('BG03','comms','This way! Keep together!','这边！跟紧！','urgent'),
 ('HELP','engineer','Reactor breach! Get clear!','反应堆破损！快离开！','urgent'),
]

def write(path, data):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')

def content():
    c=json.loads(SOURCE.read_text(encoding='utf8'))
    c['extra']=[dict(id=i,role=r,english=e,text=t,direction=d,channel=next(x['channel'] for x in c['roles'] if x['role']==r)) for i,r,e,t,d in EXTRA]
    return c

def direction(l):
    role={'anchor':'a public broadcaster talking quietly to people watching at home',
          'commander':'a fleet commander at a bridge microphone, operating the tactical console and addressing nearby officers',
          'engineer':'an engineer checking reactor instruments and talking to the bridge over a handheld communicator',
          'comms':'a communications officer adjusting a receiver, reporting to the commander'}[l['role']]
    emotion={'calm':'Measured, curious but cautious. Connected conversational phrasing, small natural breaths; a gentle pause before the final thought.',
             'tense':'Concern grows into controlled urgency. Short breath at a clause boundary, stress the actionable words; remain conversational.',
             'urgent':'An immediate emergency. A quick intake of breath, decisive short clauses and controlled fear; do not shout every word.'}[l['direction']]
    return f'You are {role} in an original fictional space drama. {emotion} Speak the exact English dialogue naturally, never as a newsreader. No added words, music, sound effects or stage-direction speech.'

def plan(l,c):
    roles={r['role']:r['speaker'] for r in c['roles']}
    args=SimpleNamespace(id=[l['id']],text=l['english'],all=False,speaker=roles[l['role']],direction=direction(l),speech_rate=0,take=TAKE,execute=False)
    payload=voice.payload_for(args,dict(id=l['id'],text=l['english']))
    digest=hashlib.sha256(json.dumps(payload,sort_keys=True,ensure_ascii=False).encode()).hexdigest()[:16]
    return args,voice.OUTPUT/'seed-tts'/TAKE/(l['id']+'-'+digest),payload

def all_lines(c): return c['narrative']+c['combat']+c['extra']

def generate(execute,representatives=False):
    c=content(); lines=all_lines(c)
    assert len(lines)<=MAX_REQUESTS and sum(len(l['english']) for l in lines)<=MAX_CHARS
    if representatives: lines=c['extra']+[c['narrative'][0]]
    preview=[dict(id=l['id'],role=l['role'],text=l['english'],caption=l['text'],prompt=direction(l),request=plan(l,c)[2]) for l in lines]
    write(EVIDENCE/('representatives-preview.json' if representatives else 'batch-preview.json'),dict(execute=execute,requestLimit=MAX_REQUESTS,charLimit=MAX_CHARS,plannedRequests=len(lines),characters=sum(len(l['english']) for l in lines),lines=preview))
    print(json.dumps(dict(execute=execute,planned=len(lines),characters=sum(len(l['english']) for l in lines),take=TAKE)),flush=True)
    if not execute:return
    lock=WORK/'generation.lock';lock.parent.mkdir(parents=True,exist_ok=True)
    with lock.open('x') as f:f.write(str(os.getpid()))
    try:
        for l in lines:
            args,directory,_=plan(l,c);args.execute=True
            voice.generate(args)
            if not (directory/'voice.wav').exists(): raise voice.VoiceError('Unresolved previous job: '+l['id']+'; no retry.')
            qa=analyze(directory/'voice.wav');write(directory/'qa.json',qa)
            print('QA',l['id'],json.dumps(qa),flush=True)
    finally:lock.unlink(missing_ok=True)

def probe(execute):
    # Supported speech API, empty text: do not feed a sound description into spoken text.
    payload={'user':{'uid':'droplet-offline-authoring'},'req_params':{'text':'','speaker':'en_male_tim_uranus_bigtts','audio_params':{'format':'pcm','sample_rate':24000},'additions':json.dumps({'context_texts':['Produce three metallic relay clicks, no speech, no music, no narration.']})}}
    p=EVIDENCE/'non-speech-capability.json'
    if p.exists(): print(p.read_text(encoding='utf8'));return
    result=dict(request=payload,endpoint=voice.TTS,execute=execute,documentation=['https://www.volcengine.com/docs/6561/1598757','https://www.volcengine.com/docs/6561/1871062'],finding='Installed connector exposes text-to-speech only. No configured supported Seed non-speech endpoint found. Do not use community invented endpoints.')
    print(json.dumps(result))
    if not execute:return
    result['status']='submission_unknown';write(p,result)
    try:
        with voice.open_request(voice.TTS,{'Content-Type':'application/json','X-Api-Key':voice.credential('VOLC_SPEECH_API_KEY'),'X-Api-Resource-Id':'seed-tts-2.0'},payload) as response:
            pcm,usage=voice.decode_tts(response)
        candidate=WORK/'CapabilityProbe/unaccepted.wav';candidate.parent.mkdir(parents=True,exist_ok=True);candidate.write_bytes(voice.make_wav(pcm))
        result.update(status='returned_unverified_candidate',usage=usage,accepted=False,candidate=str(candidate.relative_to(ROOT)))
    except voice.VoiceError as e:result.update(status='no_non_speech_audio',error=str(e),accepted=False)
    write(p,result)

def analyze(path):
    with wave.open(str(path),'rb') as w:
        assert w.getsampwidth()==2
        sr=w.getframerate();channels=w.getnchannels();x=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(float)/32768
    rms=float(np.sqrt(np.mean(x*x)));peak=float(np.max(np.abs(x)))
    chunks=[float(np.sqrt(np.mean(y*y))) for y in np.array_split(x,max(1,int(len(x)/sr/channels*10)))];maxsilent=run=0
    for y in chunks:
        run=run+1 if y<.0002 else 0;maxsilent=max(maxsilent,run)
    return dict(duration=len(x)/sr/channels,sampleRate=sr,channels=channels,peakDb=20*math.log10(max(1e-10,peak)),rmsDb=20*math.log10(max(1e-10,rms)),clippedSamples=int(np.sum(np.abs(x)>=.999)),maxSilenceSeconds=maxsilent*.1,edgeDiscontinuity=float(abs(x[0]-x[-1])),sha256=hashlib.sha256(path.read_bytes()).hexdigest())

def wav(path,x):
    path.parent.mkdir(parents=True,exist_ok=True)
    x=np.nan_to_num(x);peak=np.max(np.abs(x))
    if peak>.96:x=x*(.96/peak)
    with wave.open(str(path),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(SR);w.writeframes((np.clip(x,-.999,.999)*32767).astype('<i2').tobytes())

def modal(t,freqs,decay):
    return sum(np.sin(2*np.pi*f*t)*np.exp(-t*(decay+i*2))/(i+1) for i,f in enumerate(freqs))

def synthesize():
    """Authored modal/tonal effects. Foot contacts are heel/toe resonances, never noise stand-ins."""
    d=WORK/'Effects';manifest=[]
    modes={'DeckSteps':4.2,'Console':1.4,'Hatch':1.5,'Impact':1.1,'Explosion':2.7,'Laser':.24,'Reflect':.32,'Alarm':2.0,'Equipment':4.0,'Connect':.14,'Disconnect':.2}
    for mode,length in modes.items():
        count=3 if mode in ('DeckSteps','Console','Hatch','Impact','Explosion','Laser','Reflect') else 1
        for variant in range(count):
            rng=np.random.default_rng(719+variant*31+sum(map(ord,mode)));t=np.arange(round(SR*length))/SR;x=np.zeros_like(t)
            if mode=='DeckSteps':
                for n,start in enumerate(np.arange(.12,length-.25,.31+variant*.017)):
                    start+=rng.uniform(-.035,.035);local=t-start;mask=local>=0;u=local[mask]
                    heel=modal(u,[83+variant*7,173,319,617],21)*(.34 if n%2 else .42)
                    toe=np.maximum(0,u-.066);toe=modal(toe,[112,261,709],30)*(u>=.066)*.19
                    x[mask]+=(heel+toe)*(1-.62*start/length)
            elif mode=='Console':
                for start in [.08,.27,.58,.79,1.05]:
                    u=np.maximum(0,t-start);x+=(t>=start)*modal(u,[530+variant*35,1411,2380],80)*.20
                x+=.04*np.sin(2*np.pi*940*t)*((t>.84)&(t<.96))
            elif mode=='Hatch':
                x=.07*np.sin(2*np.pi*(126*t+40*t*t))*((t>.15)&(t<.95))
                for start,amp in [(.08,.42),(1.04,.58),(1.13,.15)]:
                    u=np.maximum(0,t-start);x+=(t>=start)*modal(u,[59+variant*9,129,277,520],14)*amp
            elif mode in ('Impact','Explosion'):
                noise=lfilter([.09,.09],[1,-.82],rng.standard_normal(len(t)))
                x=.55*modal(t,[39+variant*7,76,127,283],4 if mode=='Explosion' else 10)
                x+=noise*np.exp(-t*(2.7 if mode=='Explosion' else 15))*.6
                if mode=='Explosion':
                    for start in [.13,.3,.54]:
                        u=np.maximum(0,t-start);x+=(t>=start)*modal(u,[155,337+variant*12,783],18)*.10
            elif mode in ('Laser','Reflect'):
                f=2400+variant*330 if mode=='Laser' else 3900+variant*210
                x=(np.sin(2*np.pi*(f*t-2800*t*t))+.25*np.sin(2*np.pi*61*t))*np.exp(-t*16)*.38
            elif mode=='Alarm':x=.105*np.sin(2*np.pi*(660*t+45*np.sin(2*np.pi*2*t)))*(np.sin(np.pi*2*t)**6)
            elif mode=='Equipment':x=.028*np.sin(2*np.pi*72*t)+.010*np.sin(2*np.pi*144*t)+.005*np.sin(2*np.pi*317*t)
            elif mode=='Connect':x=.16*np.sin(2*np.pi*np.where(t<.065,870,1170)*t)
            else:x=.11*np.sin(2*np.pi*(1050*t-1900*t*t))+rng.standard_normal(len(t))*.023*np.exp(-t*18)
            fade=np.minimum(1,t/.004)*np.minimum(1,(length-t)/.025);x*=fade
            path=d/f'{mode}_{variant+1}.wav';wav(path,x)
            manifest.append(dict(id=path.stem,file=str(path.relative_to(ROOT)),source='Original procedural synthesis, deterministic seed; no third-party recording',model='heel/toe impulses exciting modal steel deck resonances' if mode=='DeckSteps' else 'tonal/modal oscillator and shaped transient synthesis',prompt=f'{mode}, steel ship compartment, close mechanical onset and short room decay, {length} seconds, mono, no speech, no music, no narration; '+('several irregular hurried foot contacts receding from microphone' if mode=='DeckSteps' else 'one-shot' if mode!='Equipment' else 'seamless loop with zero-valued edges'),qa=analyze(path),listening='待人工试听'))
    write(WORK/'effects-manifest.json',manifest)
    ASSETS.mkdir(parents=True,exist_ok=True)
    for m in manifest:
        if m['id'].startswith(('Laser_','Reflect_','Impact_','Explosion_','Connect_','Disconnect_')):shutil.copy2(ROOT/m['file'],ASSETS/(m['id']+'.wav'))

def ffmpeg(inputs,graph,out,duration=None):
    exe=shutil.which('ffmpeg');assert exe,'FFmpeg unavailable'
    args=[exe,'-hide_banner','-loglevel','error','-y']
    for path in inputs:args+=['-i',str(path)]
    args+=['-filter_complex',graph,'-map','[out]','-ar',str(SR),'-ac','1','-c:a','pcm_s16le']
    if duration is not None:args+=['-t',str(duration)]
    args+=[str(out)];subprocess.run(args,check=True)
    return args

def stem(events,duration,path):
    inputs=[];graph=[];recipe=[]
    for i,e in enumerate(events):
        source,start,gain,space=e;inputs.append(source)
        process={'near':'highpass=f=95,lowpass=f=8200','far':'highpass=f=240,lowpass=f=3100,aecho=0.8:0.65:31|57:0.16|0.08','room':'highpass=f=80,lowpass=f=6200,aecho=0.8:0.7:23|43:0.09|0.05'}[space]
        graph.append(f'[{i}:a]atrim=0,asetpts=PTS-STARTPTS,{process},volume={gain},alimiter=limit=0.9:level=0,afade=t=in:d=0.004,adelay={round(start*1000)}:all=1[s{i}]')
        recipe.append(dict(file=str(source.relative_to(ROOT)),start=round(start,6),gain=gain,perspective=space))
    if not events:
        wav(path,np.zeros(round(duration*SR)));return [],[]
    graph.append(''.join(f'[s{i}]' for i in range(len(events)))+f'amix=inputs={len(events)}:normalize=0:dropout_transition=0,apad,atrim=duration={duration}[out]')
    command=ffmpeg(inputs,';'.join(graph),path,duration)
    return recipe,command

def make_scene(l,c,lookup,profile,representative=False):
    sid=l['id'];d=WORK/'Scenes'/sid;d.mkdir(parents=True,exist_ok=True)
    dry=lookup[sid];leadlen=analyze(dry)['duration'];variant=sum(map(ord,sid))%3+1
    effects=WORK/'Effects';e=lambda n:effects/f'{n}_{variant if n in ("DeckSteps","Console","Hatch","Impact") else 1}.wav'
    lead_gain=lambda path:min(8,10**((-18.5-analyze(path)['rmsDb'])/20))
    background_gain=lambda path:min(3,10**((-27-analyze(path)['rmsDb'])/20))
    lead=[(dry,.20,lead_gain(dry),'near')];bg=[];actions=[];environment=[];events=[]
    duration=leadlen+.50
    captions=[dict(time=.20,duration=leadlen,text=l['text'],english=l['english'],role=l['role'])]
    if representative:
        if sid=='SC01':bid='SC02'; start=.2+leadlen+.24
        elif sid=='SA01':bid='SA02'; start=.2+leadlen+.15
        else:bid='SE02'; start=.2+leadlen+.25
        blen=analyze(lookup[bid])['duration'];bg=[(lookup[bid],start,background_gain(lookup[bid]),'far')];captions.append(dict(time=start,duration=blen,text=next(x['text'] for x in c['extra'] if x['id']==bid),english=next(x['english'] for x in c['extra'] if x['id']==bid),role='background crew'))
        duration=start+blen+.75
        if sid=='SE01':
            start=duration-.45;last=next(x for x in c['extra'] if x['id']=='SE03');ll=analyze(lookup['SE03'])['duration'];lead.append((lookup['SE03'],start,lead_gain(lookup['SE03']),'near'));captions.append(dict(time=start,duration=ll,text=last['text'],english=last['english'],role='commander'));duration=start+ll+1.5
    elif profile!='studio' and sid!='HELP' and leadlen>2.7 and (not sid.startswith('E') or int(sid[1:])%5==0):
        bid='BG01' if profile=='calm' else 'BG03' if profile=='evacuation' else 'BG02';start=.20+leadlen+.1
        bg=[(lookup[bid],start,background_gain(lookup[bid])*.8,'far')];blen=analyze(lookup[bid])['duration'];duration=start+blen+.3
        b=next(x for x in c['extra'] if x['id']==bid);captions.append(dict(time=start,duration=blen,text=b['text'],english=b['english'],role='background crew'))
    if profile=='studio':
        actions=[(e('Console'),max(.1,leadlen*.65),.10,'far')]
    else:
        environment=[(e('Equipment'),at,.65 if profile=='calm' else .9,'room') for at in np.arange(0,duration,4)]
        actions=[(e('Console'),.05,.27,'room'),(e('Console'),max(.9,leadlen*.57),.16,'far')]
    if profile in ('attack','evacuation'):
        environment += [(e('Alarm'),at,.24 if profile=='attack' else .32,'far') for at in np.arange(.12,duration,2.6)]
        events=[(e('Impact'),max(.5,leadlen*.45),.38,'room')]
        actions.append((e('Hatch'),min(duration-1.5,leadlen+.25),.45,'room'))
    if profile=='evacuation':actions +=[(e('DeckSteps'),max(.5,leadlen*.8),.50,'room'),(e('DeckSteps'),max(1,duration-3.8),.26,'far')]
    link=[(effects/'Connect_1.wav',0,.40,'near'),(effects/'Disconnect_1.wav',duration-.21,.35,'near')]
    stems={};commands=[]
    for name,tracks in [('lead',lead),('background_people',bg),('actions',actions),('environment',environment),('events',events),('link',link)]:
        rec,cmd=stem(tracks,duration,d/(name+'.wav'));stems[name]=rec
        if cmd:commands.append(cmd)
    graph='[0:a]asplit=2[lead][sc];[1:a][2:a][3:a][4:a]amix=inputs=4:normalize=0:dropout_transition=0[bg];[bg][sc]sidechaincompress=threshold=0.026:ratio=3:attack=14:release=180[duck];[lead][duck]amix=inputs=2:normalize=0:dropout_transition=0,alimiter=limit=0.85:level=0[out]'
    commands.append(ffmpeg([d/(n+'.wav') for n in ('lead','background_people','actions','environment','events')],graph,d/'scene_mix.wav',duration))
    # Entire cabin goes through the same microphone/radio bus BEFORE receiver link sounds.
    graph='[0:a]highpass=f=230,lowpass=f=4300,acompressor=threshold=0.18:ratio=2.2:attack=9:release=100:makeup=1.15,asoftclip=type=tanh:threshold=0.82:output=0.95[cabin];[cabin][1:a]amix=inputs=2:normalize=0:dropout_transition=0,alimiter=limit=0.84:level=0[out]'
    commands.append(ffmpeg([d/'scene_mix.wav',d/'link.wav'],graph,d/'radio_mix.wav',duration))
    target=ASSETS/(sid+'.wav');shutil.copy2(d/'radio_mix.wav',target)
    if l.get('eventKind') or representative:
        for variant_name,bg_gain in [('low',.42),('high',1.35)]:
            variant_graph=f'[0:a]asplit=2[lead][sc];[1:a]volume={bg_gain}[people];[2:a]volume={bg_gain}[actions];[3:a]volume={bg_gain}[env];[4:a]volume={bg_gain}[events];[people][actions][env][events]amix=inputs=4:normalize=0:dropout_transition=0[bg];[bg][sc]sidechaincompress=threshold=0.026:ratio=3:attack=14:release=180[duck];[lead][duck]amix=inputs=2:normalize=0:dropout_transition=0,highpass=f=230,lowpass=f=4300,acompressor=threshold=0.18:ratio=2.2:attack=9:release=100:makeup=1.15,asoftclip=type=tanh:threshold=0.82:output=0.95[cabin];[cabin][5:a]amix=inputs=2:normalize=0:dropout_transition=0,alimiter=limit=0.84:level=0[out]'
            commands.append(ffmpeg([d/(n+'.wav') for n in ('lead','background_people','actions','environment','events','link')],variant_graph,d/f'radio_{variant_name}.wav',duration))
            shutil.copy2(d/f'radio_{variant_name}.wav',ASSETS/f'{sid}_{variant_name}.wav')
    recipe=dict(id=sid,profile=profile,sourceShip='Runtime authoritative event speaker ID; narrative fleet command',trigger=l.get('eventKind','NarrativeCue'),english=l['english'],caption=l['text'],role=l['role'],generation=dict(prompt=direction(l),parameters=plan(l,c)[2],job=str((plan(l,c)[1]/'job.json').relative_to(ROOT))),duration=duration,stems=stems,captions=captions,commands=commands,mode='single whole-scene premix; never concurrently play stems',interrupt='source explosion stops entire clip including cabin tail; receiver-only 0.2s disconnect is allowed',candidate='v1',selected=str(target.relative_to(ROOT)),listening='待人工试听',qa=analyze(target))
    write(d/'mix_recipe.json',recipe)
    l['duration']=duration;l['captions']=captions;l['premixed']=True
    return recipe

def mix(representatives=False):
    c=content();lookup={l['id']:plan(l,c)[1]/'voice.wav' for l in all_lines(c)}
    selected=[next(x for x in c['extra'] if x['id']==i) for i in ('SC01','SA01','SE01')] if representatives else c['narrative']+c['combat']+[next(x for x in c['extra'] if x['id']==i) for i in ('SC01','SA01','SE01','HELP')]
    manifest=[];cursor=0
    for l in selected:
        sid=l['id'];kind=l.get('eventKind','');profile='calm'
        if sid=='SA01' or kind in ('HullPenetrated','ReactorUnstable','RescueRequested','LaserReflected'):profile='attack'
        if sid=='SE01' or kind=='RetreatOrdered':profile='evacuation'
        if l['role']=='anchor':profile='studio'
        recipe=make_scene(l,c,lookup,profile,sid in ('SC01','SA01','SE01'))
        manifest.append(recipe)
        if sid.startswith('E'):l['time']=cursor;cursor+=l['duration']+.16
        print('MIX',sid,round(l['duration'],3),flush=True)
    c['duration']=cursor+.25;c['sceneExtras']=[x for x in selected if x['id'] in ('SC01','SA01','SE01','HELP')]
    write(WORK/('representatives-manifest.json' if representatives else 'manifest.json'),dict(provider='seed-tts-2.0',roles=c['roles'],scenes=manifest,openingDuration=c['duration'],listening='待人工试听',sourceQuality='24 kHz native PCM; no claimed upsampling improvement'))
    if not representatives:write(DATA/'CinematicRadioContent.json',c)
    render_listening_page(manifest,representatives)
    write(EVIDENCE/('representatives-qa.json' if representatives else 'audio-qa.json'),dict(generated=len(list(voice.OUTPUT.glob('seed-tts/'+TAKE+'/*/voice.wav'))),mixed=len(manifest),openingDuration=c['duration'],ffmpeg=subprocess.run(['ffmpeg','-version'],capture_output=True,text=True).stdout.splitlines()[0],clips=[dict(id=r['id'],**r['qa']) for r in manifest],listening='待人工试听'))

def render_listening_page(scenes,representatives=False):
    """HTML-only authoring: no synthesis, remix, analysis or audio-file writes."""
    featured={'SC01':'平静舰桥','SA01':'舰桥受袭','SE01':'紧急撤离'}
    ordered=[r for sid in featured for r in scenes if r['id']==sid]
    ordered.extend(r for r in scenes if r['id'] not in featured)
    page=['<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>舰队通讯试听</title><style>body{background:#101920;color:#d4e5ed;font:16px system-ui;max-width:960px;margin:40px auto;padding:0 20px;line-height:1.6}article{border-top:1px solid #345;padding:24px 0;scroll-margin-top:20px}small{color:#9bb}a{color:#7cd7ee}nav{border:1px solid #345;padding:16px;margin:24px 0}nav a{display:inline-block;margin:4px 14px 4px 0}.stems{display:flex;flex-wrap:wrap;gap:8px 20px;padding:0;list-style:none}audio{max-width:100%;width:440px}.featured{color:#aee9d3}</style><h1 id="top">完整舰内通讯 · Seed-TTS 2.0</h1><p>实际生成与混音；主观情绪、跑动真实感与混音均<strong>待人工试听</strong>。单声道舰内收音经统一通讯总线。点击播放，不自动发声。</p><nav aria-label="通讯场景跳转"><strong>优先复核三个代表场景</strong><br>']
    for sid,label in featured.items():
        if any(r['id']==sid for r in ordered):page.append(f'<a href="#{sid}">{sid} · {label}</a>')
    if not representatives:
        page.append('<details><summary>全部场景索引</summary>')
        page.extend(f'<a href="#{html.escape(r["id"])}">{html.escape(r["id"])}</a>' for r in ordered)
        page.append('</details>')
    page.append('</nav>')
    labels={'lead':'主角','background_people':'背景人物','actions':'人物动作','environment':'设备与环境','events':'场景事件','link':'通讯链路'}
    for r in ordered:
        sid=html.escape(r['id']);base=(Path('Scenes')/r['id']).as_posix()
        title=featured.get(r['id'],r['profile'])
        page.append(f'<article id="{sid}"><h2>{sid} · {html.escape(title)} · {r["duration"]:.2f}s</h2><p>{html.escape(r["english"])}</p><p>{html.escape(r["caption"])}</p><audio controls preload="none" src="{base}/radio_mix.wav"></audio><p><a href="{base}/radio_mix.wav">完整通讯成品</a> · <a href="{base}/scene_mix.wav">舰内母版</a> · <a href="{base}/mix_recipe.json">配方与逐句字幕</a></p><p>实际分轨：</p><ul class="stems">')
        for stem_name,label in labels.items():
            unused=' <small>（本段未使用，保留空轨）</small>' if not r.get('stems',{}).get(stem_name) else ''
            page.append(f'<li><a href="{base}/{stem_name}.wav">{label} · {stem_name}</a>{unused}</li>')
        page.append('</ul><small>待人工试听。主角/背景人物链接为编排后的分轨；原始干声由配方关联，保存在同目录。</small><p><a href="#top">返回索引</a></p></article>')
    page.append('</html>')
    (WORK/('representatives.html' if representatives else 'listening.html')).write_text('\n'.join(page),encoding='utf8')

def rebuild_listening_pages():
    scenes=json.loads((WORK/'manifest.json').read_text(encoding='utf8'))['scenes']
    render_listening_page(scenes)
    render_listening_page([r for r in scenes if r['id'] in ('SC01','SA01','SE01')],True)

def qa_all():
    result=[]
    for p in sorted(ASSETS.glob('*.wav')):
        subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-i',str(p),'-f','null','-'],check=True)
        q=analyze(p);q['file']=str(p.relative_to(ROOT));result.append(q)
    write(EVIDENCE/'decode-qa.json',dict(clips=result,allDecode=True,zeroClippedSamples=all(q['clippedSamples']==0 for q in result),nonSilent=all(q['rmsDb']>-55 for q in result),listening='待人工试听'))
    print(json.dumps(dict(clips=len(result),clipped=sum(q['clippedSamples'] for q in result),maxSilence=max(q['maxSilenceSeconds'] for q in result))))

if __name__=='__main__':
    sys.stdout.reconfigure(encoding='utf8');sys.stderr.reconfigure(encoding='utf8')
    p=argparse.ArgumentParser();p.add_argument('action',choices=['probe','generate','synthesize','mix','qa','pages']);p.add_argument('--execute',action='store_true');p.add_argument('--representatives',action='store_true');a=p.parse_args()
    if a.action=='probe':probe(a.execute)
    elif a.action=='generate':generate(a.execute,a.representatives)
    elif a.action=='synthesize':synthesize()
    elif a.action=='mix':mix(a.representatives)
    elif a.action=='pages':rebuild_listening_pages()
    else:qa_all()
