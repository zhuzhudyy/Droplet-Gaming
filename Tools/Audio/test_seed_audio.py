"""Offline budget/idempotency tests; never live API evidence."""
import base64
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import wave

import seed_audio as audio


def wav_bytes():
    result = io.BytesIO()
    with wave.open(result, 'wb') as stream:
        stream.setnchannels(2)
        stream.setsampwidth(2)
        stream.setframerate(48000)
        stream.writeframes(b'\x01\x00\x02\x00' * 4800)
    return result.getvalue()


class SeedAudioTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.work = patch.object(audio, 'WORK', Path(self.temporary.name) / 'batch')
        self.work.start()

    def tearDown(self):
        self.work.stop()
        self.temporary.cleanup()

    def test_preview_has_no_io_or_credential(self):
        with patch.object(audio.voice, 'credential', side_effect=AssertionError('secret read')):
            plans = [audio.payload(audio.probe(category)) for category in audio.PROBES]
        self.assertEqual(6, len(plans))
        self.assertFalse(audio.WORK.exists())
        self.assertTrue(all('duration' not in plan for plan in plans))

    def test_full_maximum_reserved_not_requested_duration(self):
        self.assertEqual(audio.RESERVE, audio.MAX_SECONDS * audio.RATE_PER_MINUTE / 60)
        self.assertEqual(audio.cost({'reservedCNY': '2'}), 2)
        self.assertEqual(str(audio.cost({'originalDurationSeconds': '30'})), '0.500000')

    def test_budget_blocks_before_credentials_or_network(self):
        ledger = audio.load_ledger()
        ledger['entries'] = [dict(id=str(i), status='complete', originalDurationSeconds='120') for i in range(30)]
        audio.save_ledger(ledger)
        with patch.object(audio.voice, 'credential', side_effect=AssertionError('secret read')):
            with self.assertRaisesRegex(audio.voice.VoiceError, 'budget'):
                audio.execute(audio.probe('LASER'))

    def test_unknown_request_durable_and_never_resubmitted(self):
        with patch.object(audio.voice, 'credential', return_value='test-secret'), patch.object(audio, 'submit', side_effect=audio.voice.VoiceError('timeout')) as call:
            with self.assertRaises(audio.voice.VoiceError):
                audio.execute(audio.probe('LASER'))
            with self.assertRaises(audio.voice.VoiceError):
                audio.execute(audio.probe('LASER'))
            with self.assertRaises(audio.voice.VoiceError):
                audio.execute(audio.probe('METAL'))
        self.assertEqual(call.call_count, 1)
        ledger = audio.load_ledger()
        self.assertEqual(ledger['accountedCNY'], '2')
        self.assertEqual(ledger['entries'][0]['status'], 'submission_unknown')
        self.assertFalse((audio.WORK / 'audio-generation.lock').exists())

    def test_complete_cache_verifies_request_and_hash(self):
        response = dict(code=0, original_duration=.1, audio=base64.b64encode(wav_bytes()).decode())
        with patch.object(audio.voice, 'credential', return_value='test-secret'), patch.object(audio, 'submit', return_value=response) as call:
            first = audio.execute(audio.probe('LASER'))
            cached = audio.execute(audio.probe('LASER'))
            self.assertFalse(first['contentAccepted'])
            self.assertTrue(cached['cached'])
            self.assertEqual(call.call_count, 1)
            (audio.WORK / 'raw/LASER_R1/audio.wav').write_bytes(b'tampered')
            with self.assertRaisesRegex(audio.voice.VoiceError, 'hash'):
                audio.execute(audio.probe('LASER'))
        self.assertEqual(audio.load_ledger()['accountedCNY'], '0.016667')

    def test_reservation_persisted_before_network(self):
        def observe(*args):
            ledger = audio.load_ledger()
            self.assertEqual(ledger['accountedCNY'], '2')
            self.assertEqual(ledger['entries'][0]['status'], 'submission_unknown')
            raise audio.voice.VoiceError('deliberate')
        with patch.object(audio.voice, 'credential', return_value='test-secret'), patch.object(audio, 'submit', side_effect=observe):
            with self.assertRaises(audio.voice.VoiceError):
                audio.execute(audio.probe('METAL'))

    def test_source_channels_preserved(self):
        info = audio.inspect_wav(wav_bytes())
        self.assertEqual(info['channels'], 2)
        self.assertFalse(info['contentAccepted'])
        self.assertEqual(info['subjectiveListening'], 'not_performed')

    def test_errors_redact_key_and_signed_url(self):
        data = json.dumps(dict(code=45000010, message='Invalid test-secret at https://host/file?secret=abc')).encode()
        result = json.dumps(audio.safe_error(data, 'test-secret'))
        self.assertNotIn('test-secret', result)
        self.assertNotIn('https://', result)
        code_result = audio.safe_error(json.dumps(dict(code='test-secret', message='bad')).encode(), 'test-secret')
        self.assertNotIn('test-secret', json.dumps(code_result))

    def test_fourth_take_rejected(self):
        with self.assertRaises(audio.voice.VoiceError):
            audio.probe('MUSIC', 4)

    def test_success_response_can_omit_code(self):
        response = unittest.mock.MagicMock()
        response.__enter__.return_value.read.return_value = json.dumps(dict(audio='AAAA', original_duration=1)).encode()
        opener = unittest.mock.MagicMock()
        opener.open.return_value = response
        with patch.object(audio.urllib.request, 'build_opener', return_value=opener):
            result = audio.submit(audio.payload(audio.probe('LASER')), 'test-key', 'test-request')
        self.assertEqual(result['original_duration'], 1)

    def test_legacy_auth_uses_app_and_access_headers(self):
        response = unittest.mock.MagicMock()
        response.__enter__.return_value.read.return_value = json.dumps(dict(audio='AAAA', original_duration=1)).encode()
        opener = unittest.mock.MagicMock()
        opener.open.return_value = response
        with patch.object(audio.urllib.request, 'build_opener', return_value=opener):
            audio.submit(audio.payload(audio.probe('LASER')), 'test-access', 'test-request', app_id='test-app')
        headers = dict((key.lower(), value) for key, value in opener.open.call_args.args[0].header_items())
        self.assertEqual(headers['x-api-app-id'], 'test-app')
        self.assertEqual(headers['x-api-access-key'], 'test-access')
        self.assertEqual(headers['x-api-resource-id'], 'volc.service_type.10074')
        self.assertNotIn('x-api-key', headers)

    def test_auth_review_keeps_reserve_and_requires_different_key(self):
        item = dict(id='LASER_R1', status='submission_unknown', reservedCNY='2',
                    credentialVariable='ARK_API_KEY', diagnostic=dict(httpStatus=401, serviceCode=45000010))
        ledger = audio.load_ledger()
        ledger['entries'].append(item)
        audio.save_ledger(ledger)
        with patch.object(audio.voice, 'credential', return_value='same-key'):
            with self.assertRaisesRegex(audio.voice.VoiceError, 'unchanged'):
                audio.review_auth_rejection('LASER_R1', 'user configured speech key')
        with patch.object(audio.voice, 'credential', side_effect=lambda name: name + '-test-key'):
            result = audio.review_auth_rejection('LASER_R1', 'user configured speech key')
        self.assertTrue(result['reviewed'])
        self.assertEqual(audio.load_ledger()['accountedCNY'], '2')
        self.assertEqual(audio.load_ledger()['entries'][0]['status'], 'submission_unknown')


if __name__ == '__main__':
    unittest.main()
