"""Offline checks for the revised Seed-TTS voice QA tool."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import wave

import numpy as np

import seed_voice_qa as qa


def write_voice(path: Path, amplitude: int = 10000) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    phase = np.linspace(0, 2 * np.pi * 300, 24000, endpoint=False)
    pcm = (np.sin(phase) * amplitude).astype("<i2")
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(24000)
        output.writeframes(pcm.tobytes())


def fixture(root: Path, line_id: str, take_id: str) -> tuple[dict, dict, Path]:
    line = {"id": line_id, "english": "Hold your position.",
            "speaker": "english-test-speaker", "speech_rate": 0,
            "context_texts": ["Speak naturally."], "group": "narrative", "stage": 0,
            "role": "commander"}
    payload = {"user": {"uid": "test"}, "req_params": {
        "text": line["english"], "speaker": line["speaker"],
        "audio_params": {"format": "pcm", "sample_rate": 24000, "speech_rate": 0},
        "additions": json.dumps({"context_texts": line["context_texts"]})}}
    request_sha = hashlib.sha256(json.dumps(payload, sort_keys=True, ensure_ascii=False).encode()).hexdigest()
    directory = root / "test-take" / (take_id + "-" + request_sha[:16])
    audio = directory / "voice.wav"
    write_voice(audio)
    digest = qa.sha256(audio)
    job = {"provider": "seed-tts", "resource_id": "seed-tts-2.0",
           "endpoint": "https://test.invalid/tts", "status": "complete", "request": payload,
           "line": {"id": take_id, "text": line["english"]},
           "sha256": digest, "duration_seconds": 1}
    (directory / "job.json").write_text(json.dumps(job), encoding="utf-8")
    entry = {"id": take_id, "status": "complete", "request": {
        "provider": "seed-tts", "resource_id": "seed-tts-2.0",
        "endpoint": "https://test.invalid/tts", "payload": payload},
        "requestSha256": request_sha, "job": str(directory / "job.json"),
        "sha256": digest, "durationSeconds": 1}
    return line, entry, audio


class VoiceQaTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.patch = patch.object(qa, "VOICE_ROOT", self.root)
        self.patch.start()
        self.addCleanup(self.patch.stop)

    def test_complete_voice_is_technically_ready_only(self):
        line, entry, _ = fixture(self.root, "E001", "E001")
        record = qa.inspect_line(line, entry, "test-take", self.root / "output")
        self.assertEqual("technically_ready_for_listening", record["status"])
        self.assertEqual("not_performed", record["contentListening"])
        self.assertEqual(0, record["metrics"]["clippedSamples"])

    def test_e033_replacement_keeps_authored_line_id(self):
        line, entry, _ = fixture(self.root, "E033", "E033_R2")
        record = qa.inspect_line(line, entry, "test-take", self.root / "output")
        self.assertEqual("E033", record["lineId"])
        self.assertEqual("E033_R2", record["takeId"])
        self.assertEqual("technically_ready_for_listening", record["status"])

    def test_changed_audio_fails_hash_check(self):
        line, entry, audio = fixture(self.root, "E001", "E001")
        write_voice(audio, amplitude=5000)
        record = qa.inspect_line(line, entry, "test-take", self.root / "output")
        self.assertEqual("technical_issue", record["status"])
        self.assertTrue(any("SHA-256" in error for error in record["errors"]))

    def test_wrong_pcm_rate_fails(self):
        path = self.root / "wrong.wav"
        path.parent.mkdir(parents=True, exist_ok=True)
        with wave.open(str(path), "wb") as output:
            output.setnchannels(1)
            output.setsampwidth(2)
            output.setframerate(22050)
            output.writeframes(np.zeros(22050, dtype="<i2").tobytes())
        with self.assertRaisesRegex(ValueError, "24000"):
            qa.pcm_metrics(path)


if __name__ == "__main__":
    unittest.main()
