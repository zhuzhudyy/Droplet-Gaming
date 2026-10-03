"""Offline contract/failure checks; never calls a cloud service."""
import argparse
import base64
import io
import json
from pathlib import Path
import tempfile
import subprocess
import sys
import unittest
from unittest.mock import patch
import wave

import volc_voice as voice


class VoiceTests(unittest.TestCase):
    def args(self, **changes):
        values = dict(provider="seed-tts", id=["C01"], all=False, text=None,
                      speaker="test-speaker", direction="克制", take="test",
                      speech_rate=0, execute=False)
        values.update(changes)
        return argparse.Namespace(**values)

    def test_stream_reassembles_pcm_and_wav(self):
        pcm = b"\x01\x00\xff\x7f"
        events = [{"code": 0, "data": base64.b64encode(pcm[:1]).decode()},
                  {"code": 0, "sentence": {"text": "测试"}},
                  {"code": 0, "data": base64.b64encode(pcm[1:]).decode()},
                  {"code": 20000000, "usage": {"text_words": 2}}]
        actual, usage = voice.decode_tts(io.BytesIO(b"\n".join(json.dumps(e).encode() for e in events)))
        self.assertEqual(actual, pcm)
        self.assertEqual(usage["text_words"], 2)
        with wave.open(io.BytesIO(voice.make_wav(actual))) as wav:
            self.assertEqual((wav.getnchannels(), wav.getsampwidth(), wav.getframerate()), (1, 2, 24000))
            self.assertEqual(wav.readframes(2), pcm)

    def test_failed_truncated_empty_and_invalid_streams_rejected(self):
        for stream in [b'{"code":45000000}', b'{"code":0,"data":"AAAA"}',
                       b'{"code":20000000}', b'{"code":0,"data":"@@@"}',
                       b'{"code":0,"data":"AA=="}\n{"code":20000000}']:
            with self.subTest(stream=stream), self.assertRaises((voice.VoiceError, ValueError)):
                voice.decode_tts(io.BytesIO(stream))

    def test_completed_empty_pcm_records_metadata_without_remote_text_or_key(self):
        events = b'{"code":0,"message":"untrusted-private-response"}\n{"code":20000000}'
        with tempfile.TemporaryDirectory() as folder, patch.object(voice, "OUTPUT", Path(folder)), \
                patch.object(voice, "credential", return_value="test-secret-value"), \
                patch.object(voice, "open_request", return_value=io.BytesIO(events)), \
                patch("sys.stdout", new_callable=io.StringIO):
            with self.assertRaises(voice.VoiceError):
                voice.generate(self.args(execute=True))
            job_text = next(Path(folder).rglob("job.json")).read_text(encoding="utf-8")
            record = json.loads(job_text)
            self.assertEqual(record["status"], "submission_unknown")
            self.assertEqual(record["stream_summary"], dict(event_count=2, codes=[0, 20000000],
                                                          complete=True, pcm_bytes=0, usage=None))
            self.assertNotIn("untrusted-private-response", job_text)
            self.assertNotIn("test-secret-value", job_text)
            self.assertEqual(list(Path(folder).rglob("voice.wav")), [])

    def test_preview_never_reads_key_calls_network_or_writes(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(voice, "OUTPUT", Path(folder)), \
                patch.object(voice, "open_request") as request, patch.object(voice, "credential") as key, \
                patch("sys.stdout", new_callable=io.StringIO):
            voice.generate(self.args())
            request.assert_not_called()
            key.assert_not_called()
            self.assertEqual(list(Path(folder).iterdir()), [])

    def test_uncertain_submission_is_not_automatically_repeated(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(voice, "OUTPUT", Path(folder)), \
                patch.object(voice, "credential", return_value="test-key"), \
                patch.object(voice, "open_request", side_effect=voice.VoiceError("timeout")) as request, \
                patch("sys.stdout", new_callable=io.StringIO):
            with self.assertRaises(voice.VoiceError):
                voice.generate(self.args(execute=True))
            voice.generate(self.args(execute=True))
            self.assertEqual(request.call_count, 1)
            job = next(Path(folder).rglob("job.json"))
            self.assertEqual(json.loads(job.read_text(encoding="utf-8"))["status"], "submission_unknown")
            self.assertNotIn("test-key", job.read_text(encoding="utf-8"))

    def test_success_cache_preserves_audio_without_second_request(self):
        response = b'{"code":0,"data":"AAAAAA=="}\n{"code":20000000}'
        with tempfile.TemporaryDirectory() as folder, patch.object(voice, "OUTPUT", Path(folder)), \
                patch.object(voice, "credential", return_value="test-key"), \
                patch.object(voice, "open_request", return_value=io.BytesIO(response)) as request, \
                patch("sys.stdout", new_callable=io.StringIO):
            voice.generate(self.args(execute=True))
            voice.generate(self.args(execute=True))
            self.assertEqual(request.call_count, 1)
            self.assertEqual(len(list(Path(folder).rglob("voice.wav"))), 1)

    def test_all_existing_dialogue_ids_unique_and_selectable(self):
        lines = voice.lines_for(self.args(id=None, all=True))
        self.assertEqual(len(lines), 48)
        self.assertEqual(len({line["id"] for line in lines}), 48)
        with self.assertRaises(voice.VoiceError):
            voice.lines_for(self.args(id=["missing"]))
        with self.assertRaises(voice.VoiceError):
            voice.identifier("../escape")

    def test_authenticated_requests_never_follow_redirect(self):
        with self.assertRaises(voice.VoiceError):
            voice.NoRedirect().redirect_request(None, None, 302, "redirect", {}, "https://example.com")

    def test_cli_rejects_removed_video_options(self):
        for options in [("generate", "--provider", "seedance"), ("collect",),
                        ("generate", "--model", "video-model"),
                        ("generate", "--duration", "5")]:
            with self.subTest(options=options):
                result = subprocess.run([sys.executable, "-B", str(Path(voice.__file__)), *options],
                                        capture_output=True, text=True)
                self.assertEqual(result.returncode, 2)

    def test_tts_request_uses_only_speech_service(self):
        response = b'{"code":0,"data":"AAAAAA=="}\n{"code":20000000}'
        with tempfile.TemporaryDirectory() as folder, patch.object(voice, "OUTPUT", Path(folder)), \
                patch.object(voice, "credential", return_value="test-key") as key, \
                patch.object(voice, "open_request", return_value=io.BytesIO(response)) as request, \
                patch("sys.stdout", new_callable=io.StringIO):
            voice.generate(self.args(execute=True))
            key.assert_called_once_with("VOLC_SPEECH_API_KEY")
            url, headers, payload = request.call_args.args
            self.assertEqual(url, "https://openspeech.bytedance.com/api/v3/plan/tts/unidirectional")
            self.assertEqual(headers["X-Api-Resource-Id"], "seed-tts-2.0")
            self.assertEqual(payload["req_params"]["speaker"], "test-speaker")
            self.assertNotIn("Authorization", headers)


if __name__ == "__main__":
    unittest.main()
