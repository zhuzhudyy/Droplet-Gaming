"""Offline tests of spending/cache boundaries, not evidence of model capability."""
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import seed_soundscape as sound


class SoundscapeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.patches = [patch.object(sound, 'ROOT', self.root),
                        patch.object(sound, 'WORK', self.root / 'batch'),
                        patch.object(sound.voice, 'OUTPUT', self.root / 'generated'),
                        patch('sys.stdout', new_callable=io.StringIO)]
        for item in self.patches:
            item.start()

    def tearDown(self):
        for item in reversed(self.patches):
            item.stop()
        self.temp.cleanup()

    def response(self):
        return io.BytesIO(b'{"code":0,"data":"AAAAAA=="}\n{"code":20000000,"usage":{"text_words":60}}')

    def test_preview_and_catalog_do_not_read_secrets_call_network_or_write(self):
        with patch.object(sound.voice, 'credential') as secret, patch.object(sound.voice, 'open_request') as request:
            self.assertEqual(sound.catalog()['nonSpeechTarget'], 131)
            for identifier in ['CONTROL', *sound.PROBES]:
                _, payload, _, _ = sound.request_plan(sound.probe(identifier, 1))
                self.assertTrue(payload['req_params']['text'].strip())
            secret.assert_not_called()
            request.assert_not_called()
            self.assertEqual(list(self.root.iterdir()), [])

    def test_durable_success_cache_does_not_submit_or_charge_twice(self):
        with patch.object(sound.voice, 'credential', return_value='offline-secret'), \
             patch.object(sound.voice, 'open_request', return_value=self.response()) as request:
            first = sound.execute_one(sound.probe('CONTROL', 1))
            second = sound.execute_one(sound.probe('CONTROL', 1))
            self.assertEqual(first['sha256'], second['sha256'])
            self.assertEqual(request.call_count, 1)
            ledger = json.loads((sound.WORK / 'ledger.json').read_text(encoding='utf-8'))
            self.assertEqual(len(ledger['entries']), 1)
            self.assertEqual(ledger['accountedAFP'], '8.100')
            self.assertNotIn('offline-secret', (sound.WORK / 'ledger.json').read_text(encoding='utf-8'))

    def test_uncertain_submission_stops_same_and_other_requests(self):
        with patch.object(sound.voice, 'credential', return_value='offline-secret'), \
             patch.object(sound.voice, 'open_request', side_effect=sound.voice.VoiceError('timeout')) as request:
            for identifier in ['CONTROL', 'CONTROL', 'LASER']:
                with self.assertRaises(sound.voice.VoiceError):
                    sound.execute_one(sound.probe(identifier, 1))
            self.assertEqual(request.call_count, 1)
            self.assertFalse((sound.WORK / 'generation.lock').exists())

    def test_budget_is_checked_before_credential_or_transport(self):
        sound.write(sound.WORK / 'ledger.json', dict(budgetAFP='20000', entries=[dict(
            id='previous', status='complete', reservedAFP='20000')]))
        with patch.object(sound.voice, 'credential') as secret, patch.object(sound.voice, 'open_request') as request:
            with self.assertRaisesRegex(sound.voice.VoiceError, 'budget'):
                sound.execute_one(sound.probe('CONTROL', 1))
            secret.assert_not_called()
            request.assert_not_called()

    def test_corrupted_success_is_never_replaced_by_paid_regeneration(self):
        item = sound.probe('CONTROL', 1)
        with patch.object(sound.voice, 'credential', return_value='offline-secret'), \
             patch.object(sound.voice, 'open_request', return_value=self.response()) as request:
            sound.execute_one(item)
            _, _, directory, _ = sound.request_plan(item)
            (directory / 'voice.wav').write_bytes(b'corrupt')
            with self.assertRaises(sound.voice.VoiceError):
                sound.execute_one(item)
            self.assertEqual(request.call_count, 1)

    def test_revisions_require_an_observed_reason_and_explicit_input(self):
        with self.assertRaises(sound.voice.VoiceError):
            sound.probe('LASER', 2)
        sound.write(sound.WORK / 'revisions.json', {'LASER_R2': dict(text='test', direction='test')})
        with self.assertRaises(sound.voice.VoiceError):
            sound.probe('LASER', 2)

    def test_round_four_and_disguised_additional_trials_are_rejected(self):
        with self.assertRaises(sound.voice.VoiceError):
            sound.probe('LASER', 4)
        item = sound.probe('CONTROL', 1)
        with patch.object(sound.voice, 'credential') as secret, patch.object(sound.voice, 'open_request') as request:
            for change in [dict(id='CONTROL_R2'), dict(id='LASER_R4'), dict(category='LASER')]:
                with self.subTest(change=change), self.assertRaises(sound.voice.VoiceError):
                    sound.execute_one(dict(item, **change))
            secret.assert_not_called()
            request.assert_not_called()

    def test_reviewed_empty_stream_still_reserves_cost_and_cannot_be_resent(self):
        empty = io.BytesIO(b'{"code":0}\n{"code":20000000}')
        item = sound.probe('CONTROL', 1)
        reserve = sound.request_plan(item)[3]
        with patch.object(sound.voice, 'credential', return_value='offline-secret'), \
             patch.object(sound.voice, 'open_request', return_value=empty) as request:
            with self.assertRaises(sound.voice.VoiceError):
                sound.execute_one(item)
            ledger = json.loads((sound.WORK / 'ledger.json').read_text(encoding='utf-8'))
            self.assertEqual(ledger['entries'][0]['streamSummary']['pcm_bytes'], 0)
            self.assertEqual(ledger['serviceUsageConvertedAFP'], '0')
            self.assertEqual(ledger['unconfirmedReservedAFP'], str(reserve))
            sound.review_uncertain('CONTROL_R1', 'Inspected empty stream; retain reservation.')
            with self.assertRaises(sound.voice.VoiceError):
                sound.execute_one(item)
            self.assertEqual(request.call_count, 1)
            request.return_value = self.response()
            sound.execute_one(sound.probe('LASER', 1))
            ledger = json.loads((sound.WORK / 'ledger.json').read_text(encoding='utf-8'))
            self.assertEqual(ledger['serviceUsageConvertedAFP'], '8.100')
            self.assertEqual(ledger['unconfirmedReservedAFP'], str(reserve))
            self.assertEqual(ledger['accountedAFP'], str(reserve + sound.Decimal('8.100')))

    def test_wav_success_and_empty_transcript_cannot_pass_content_gate(self):
        with patch.object(sound.voice, 'credential', return_value='offline-secret'), \
             patch.object(sound.voice, 'open_request', return_value=self.response()):
            entry = sound.execute_one(sound.probe('CONTROL', 1))
        decision = dict(id=entry['id'], sha256=entry['sha256'], transcript='', decision='unverified')
        sound.write(sound.WORK / 'content-review.json', dict(candidates=[decision]))
        result = sound.audit()
        self.assertEqual(result['generated'], 1)
        self.assertFalse(result['gatePassed'])
        self.assertEqual(result['acceptedNonSpeech'], 0)
        decision['sha256'] = 'wrong-audio'
        sound.write(sound.WORK / 'content-review.json', dict(candidates=[decision]))
        with self.assertRaises(sound.voice.VoiceError):
            sound.audit()


if __name__ == '__main__':
    unittest.main()
