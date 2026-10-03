"""Offline fixtures for staged/published Seed audio delivery checks."""
from __future__ import annotations

import json
from pathlib import Path
import shutil
import tempfile
import unittest
import wave

import numpy as np

import seed_catalog_qa as raw_qa
import seed_delivery_qa as qa


def save_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value), encoding="utf-8")


def wav(path: Path, hz: int = 500, seconds: float = 1.0, gain: float = .1,
        rate: int = 48000, fade: bool = False) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    frames = round(seconds * rate)
    x = np.sin(np.arange(frames) * 2 * np.pi * hz / rate) * gain
    if fade:
        ramp = np.ones(frames)
        head = min(frames // 2, round(.005 * rate))
        tail = min(frames // 2, round(.08 * rate))
        ramp[:head] = np.linspace(0, 1, head)
        ramp[-tail:] = np.linspace(1, 0, tail)
        x *= ramp
    pcm = (x * 32767).astype("<i2")
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(rate)
        output.writeframes(pcm.tobytes())


def fixture(work: Path, cue_id: str = "Laser_03", source_id: str = "Laser_03_R2",
            publish: bool = False) -> dict:
    cue = {"id": cue_id, "sourceId": source_id, "category": "combat", "family": "Laser",
           "targetSeconds": 1.0, "loop": False, "text_prompt": "One cold laser sound."}
    raw_audio = work / "raw-catalog" / source_id / "audio.wav"
    wav(raw_audio)
    raw_sha = raw_qa.sha256(raw_audio)
    raw_metrics, _ = raw_qa.pcm_metrics(raw_audio)
    save_json(raw_audio.with_name("job.json"), {"id": source_id, "status": "complete",
              "sha256": raw_sha, "request": {"text_prompt": cue["text_prompt"]}})
    save_json(raw_audio.with_name("qa.json"), {"frames": raw_metrics["frames"]})
    identity = {"schema": "droplet.seed-audio-publish.v1", "version": 3,
                "id": cue_id, "sourceJob": "ArtSource/Audio/SeedAudio20260929/raw-catalog/" +
                source_id + "/job.json", "sourceSha256": raw_sha,
                "targetSeconds": 1.0, "loop": False, "kind": "catalog"}
    directory = work / "staging" / cue_id / qa.identity_hash(identity)[:16]
    stage_audio = directory / "audio.wav"
    wav(stage_audio, fade=True)
    metrics, _ = raw_qa.pcm_metrics(stage_audio)
    stage_sha = raw_qa.sha256(stage_audio)
    stored_qa = {field: metrics[field] for field in
                 ("sampleRate", "channels", "clippedSamples", "durationSeconds",
                  "peakDbFS", "rmsDbFS", "edgeDiscontinuity")}
    stored_qa["sha256"] = stage_sha
    recipe = {"rate": 48000, "sourceFrames": raw_metrics["frames"], "outputFrames": 48000, "fadeInFrames": 240,
              "fadeOutFrames": 3840}
    save_json(directory / "candidate.json", {"id": cue_id, "identity": identity,
              "sourceJob": identity["sourceJob"], "sourceSha256": raw_sha,
              "sha256": stage_sha, "qa": stored_qa, "recipe": recipe})
    if publish:
        published = work / "published" / (cue_id + ".wav")
        published.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(stage_audio, published)
        save_json(published.with_suffix(".json"), {"id": cue_id, "model": "seed-audio-1.0",
                  "sha256": stage_sha, "sourceSha256": raw_sha,
                  "sourcePrompt": cue["text_prompt"], "sourceCueId": source_id,
                  "processing": recipe, "qa": stored_qa,
                  "subjectiveListening": "not_individually_performed"})
    return cue


class DeliveryQaTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.work = Path(self.temp.name)
        self.output = self.work / "catalog-qa"

    def test_replacement_source_staging_and_published_identity(self):
        cue = fixture(self.work, publish=True)
        result = qa.inspect_delivery(self.work, self.output, cue, None)
        self.assertEqual("published_ready", result["status"])
        self.assertEqual("Laser_03_R2", result["sourceId"])
        self.assertIn("Laser_03_R2/audio.wav", result["rawAudioRelative"])
        self.assertEqual([], result["errors"])

    def test_corrupt_staged_wav_is_rejected(self):
        cue = fixture(self.work)
        staged = next((self.work / "staging" / cue["id"]).glob("*/audio.wav"))
        wav(staged, hz=900, fade=True)
        result = qa.inspect_delivery(self.work, self.output, cue, None)
        self.assertEqual("technical_issue", result["status"])
        self.assertTrue(any("SHA" in error for error in result["errors"]))

    def test_loop_seam_and_duration_checks(self):
        cue = {"category": "music", "loop": True, "targetSeconds": 10}
        metrics = {"durationSeconds": 7, "clippedSamples": 0, "rmsDbFS": -25,
                   "silenceFraction": 0, "edgeDiscontinuity": .1,
                   "edgeWindowRmsRatioDb": 10}
        errors, warnings, proxy = qa.assess_processed(cue, metrics, 10, None, None)
        self.assertTrue(any("duration" in error for error in errors))
        self.assertTrue(any("boundary" in error for error in errors))
        self.assertTrue(any("RMS" in warning for warning in warnings))
        self.assertIsNone(proxy)

    def test_cross_category_exact_raw_reuse_is_exposed(self):
        records = [{"id": "A", "category": "music", "rawSha256": "abc"},
                   {"id": "B", "category": "combat", "rawSha256": "abc"}]
        findings = qa.raw_duplicate_findings(records)
        self.assertEqual("exact_same_raw_wav", findings[0]["kind"])
        self.assertTrue(findings[0]["crossCategory"])

    def test_midband_proxy_is_frequency_sensitive(self):
        low = self.work / "low.wav"
        high = self.work / "high.wav"
        wav(low, hz=500)
        wav(high, hz=6000)
        self.assertGreater(qa.midband_dbfs(low) - qa.midband_dbfs(high), 25)
        cue = {"category": "music", "loop": True, "targetSeconds": 1}
        metrics = {"durationSeconds": 1, "clippedSamples": 0, "rmsDbFS": -25,
                   "silenceFraction": 0, "edgeDiscontinuity": 0,
                   "edgeWindowRmsRatioDb": 0}
        _, warnings, proxy = qa.assess_processed(
            cue, metrics, 1, -30, {"percentile25DbFS": -34})
        self.assertEqual(-4, proxy["unmixedVoiceToBedMarginDb"])
        self.assertEqual(16, proxy["illustrativeDuckDbFor12dBMargin"])
        self.assertTrue(any("Unity" in warning for warning in warnings))


if __name__ == "__main__":
    unittest.main()
