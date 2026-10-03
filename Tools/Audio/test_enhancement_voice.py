import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import enhancement_content as content
import enhancement_voice as authoring


class EnhancementAudioTests(unittest.TestCase):
    def test_script_is_unique_four_stage_bilingual_and_event_complete(self):
        c=content.content()
        self.assertEqual(4,len(c['roles']))
        self.assertEqual(set(range(4)),{x['stage'] for x in c['narrative']})
        self.assertEqual(40,len({x['english'] for x in c['narrative']}))
        self.assertEqual(36,len(c['combat']))
        self.assertEqual(set(content.COMBAT),{x['eventKind'] for x in c['combat']})
        for line in c['narrative']+c['combat']:
            self.assertTrue(line['english'].isascii())
            self.assertTrue(any('\u4e00'<=x<='\u9fff' for x in line['text']))

    def test_preview_never_reads_credentials_or_submits(self):
        with patch.object(authoring.voice,'credential',side_effect=AssertionError('credential read')), patch.object(authoring.voice,'generate',side_effect=AssertionError('submitted')):
            authoring.generate(authoring.SAMPLES,False)

    def test_batch_cap_and_fixed_english_cast(self):
        c=content.content(); plans=list(authoring.plans(c,'all'))
        self.assertEqual(76,len(plans))
        for role in content.ROLES:
            self.assertEqual(1,len({a.speaker for l,a,_ in plans if l['role']==role}))
        c['narrative']*=2
        with self.assertRaises(authoring.voice.VoiceError):list(authoring.plans(c,'all'))

    def test_separate_local_signals_are_real_pcm_and_not_flat_silence(self):
        with tempfile.TemporaryDirectory() as d:
            for name in ['ConnectTone','InterruptTone','Alarm','EquipmentBed']:
                p=Path(d)/(name+'.wav');authoring.make_signal(p,name)
                import wave
                with wave.open(str(p),'rb') as w:
                    self.assertEqual((1,2,24000),(w.getnchannels(),w.getsampwidth(),w.getframerate()))
                    self.assertGreater(w.getnframes(),1000)
                    self.assertGreater(len(set(w.readframes(w.getnframes()))),20)

    def test_no_complete_english_manifest_published_from_missing_audio(self):
        c=content.content()
        self.assertTrue(all(x['duration']==0 for x in c['narrative']), 'Source timings must remain explicitly unmeasured until real audio exists.')
        with tempfile.TemporaryDirectory() as d:
            folder=Path(d)
            with patch.object(authoring,'DATA',folder/'data'), patch.object(authoring,'OUT',folder/'audio'), patch.object(authoring,'EVIDENCE',folder/'evidence'), patch.object(authoring.voice,'OUTPUT',folder/'missing-dry'), patch.object(authoring.shutil,'which',return_value='ffmpeg'):
                with self.assertRaises(authoring.voice.VoiceError):authoring.master()
            self.assertFalse((folder/'data/EnhancementEnglish.json').exists(), 'Missing audio must not be represented as a timed imported bank.')


if __name__=='__main__':unittest.main()
