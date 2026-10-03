"""Validate actual generated/mixed files, publish provenance and a ready marker; no cloud calls."""
import json, shutil, subprocess, sys
from pathlib import Path
import cinematic_audio as ca

def run():
    c=ca.content();lines={x['id']:x for x in ca.all_lines(c)}
    provenance=[]
    for l in lines.values():
        args,directory,payload=ca.plan(l,c)
        job=json.loads((directory/'job.json').read_text(encoding='utf8'))
        assert job['status']=='complete',l['id']
        assert job['request']==payload,l['id']
        qa=ca.analyze(directory/'voice.wav');assert qa['sha256']==job['sha256']
        assert qa['duration']>.4 and qa['clippedSamples']==0 and qa['rmsDb']>-48,l['id']
        provenance.append(dict(id=l['id'],role=l['role'],text=l['english'],caption=l['text'],prompt=ca.direction(l),parameters=payload,job=str((directory/'job.json').relative_to(ca.ROOT)),duration=qa['duration'],status=job['status']))
    ca.write(ca.WORK/'generation-manifest.json',dict(provider='seed-tts-2.0',take=ca.TAKE,actualCompletedRequests=len(provenance),nonSpeechProbeRequests=1,automaticRetries=0,requestsUpperBound=ca.MAX_REQUESTS,characterUpperBound=ca.MAX_CHARS,voices=provenance))
    manifest=json.loads((ca.WORK/'manifest.json').read_text(encoding='utf8'))
    caption_count=0
    for r in manifest['scenes']:
        directory=ca.WORK/'Scenes'/r['id'];l=lines[r['id']]
        r['generation']=dict(prompt=ca.direction(l),parameters=ca.plan(l,c)[2],job=str((ca.plan(l,c)[1]/'job.json').relative_to(ca.ROOT)))
        r['dry_stems']=[]
        for kind in ('lead','background_people'):
            for index,track in enumerate(r['stems'][kind]):
                source=ca.ROOT/track['file'];target=directory/f'{kind}_dry_{index+1:02}.wav';shutil.copy2(source,target)
                r['dry_stems'].append(dict(file=str(target.relative_to(ca.ROOT)),source=track['file'],start=track['start'],kind=kind))
        for caption in r['captions']:
            assert caption['text'].strip() and caption['english'].strip(),r['id']
            assert caption['time']>=0 and caption['time']+caption['duration']<=r['duration']+.02,r['id']
            caption_count+=1
        qa=ca.analyze(directory/'radio_mix.wav');assert qa['clippedSamples']==0 and qa['rmsDb']>-40,r['id']
        assert abs(qa['duration']-r['duration'])<.002
        for name in ('lead','background_people','actions','environment','events','link','scene_mix','radio_mix'):
            assert (directory/(name+'.wav')).is_file(),(r['id'],name)
        if r['id'] in ('SA01','SE01'):
            for name in ('lead','background_people','actions','environment','events','link'):
                assert ca.analyze(directory/(name+'.wav'))['rmsDb']>-60,(r['id'],name)
        r['qa']=qa;ca.write(directory/'mix_recipe.json',r)
    manifest['generationManifest']='generation-manifest.json';ca.write(ca.WORK/'manifest.json',manifest)
    ca.qa_all()
    effects=json.loads((ca.WORK/'effects-manifest.json').read_text(encoding='utf8'))
    loop=next(x for x in effects if x['id']=='Equipment_1');assert loop['qa']['edgeDiscontinuity']<.001
    result=dict(status='actual_audio_assets_ready_for_root_Unity_import',realSeedVoiceRequests=len(provenance),completeSceneMixes=len(manifest['scenes']),subtitleCues=caption_count,openingDuration=manifest['openingDuration'],nonSpeechSeed='One actual supported-TTS empty-text probe returned 45002001; no supported configured non-speech Seed endpoint found, no sound-description text spoken',proceduralSource='Original modal heel/toe deck contacts, steel mechanisms, console, alarm, receiver and transient effects; not field recordings',audacity='Audacity installed; no scripting automation connected. FFmpeg used for all repeatable edits. No Audacity project claimed.',listening='待人工试听; no subjective acoustic/emotional acceptance asserted',ready=True)
    ca.write(ca.EVIDENCE/'READY.json',result);print(json.dumps(result,ensure_ascii=False,indent=2))

if __name__=='__main__':
    sys.stdout.reconfigure(encoding='utf8');run()
