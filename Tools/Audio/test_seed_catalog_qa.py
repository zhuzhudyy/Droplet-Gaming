"""Focused offline tests for the Seed catalog audit and listening index."""
from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest
import wave

import numpy as np

import seed_catalog_qa as qa


def cue(cue_id: str, prompt: str = "Original electronic sound.") -> dict:
    return {"id": cue_id, "category": "ui", "family": "Click", "trigger": "UI.Click",
            "targetSeconds": 1, "loop": False, "text_prompt": prompt}


def write_complete(work: Path, item: dict, data: bytes | None = None) -> Path:
    folder = work / "raw-catalog" / item["id"]
    folder.mkdir(parents=True, exist_ok=True)
    audio = folder / "audio.wav"
    if data is None:
        phase = np.linspace(0, 2 * np.pi * 400, 48000, endpoint=False)
        data = (np.sin(phase) * 10000).astype("<i2").tobytes()
    with wave.open(str(audio), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(48000)
        output.writeframes(data)
    metrics, _ = qa.pcm_metrics(audio)
    digest = qa.sha256(audio)
    stored_qa = {key: metrics[key] for key in
                 ("sampleRate", "channels", "sampleWidth", "frames", "clippedSamples", "durationSeconds",
                  "peakDbFS", "rmsDbFS")}
    stored_qa["silenceFraction"] = metrics["originalQaSilenceFraction"]
    stored_qa["sha256"] = digest
    (folder / "qa.json").write_text(json.dumps(stored_qa), encoding="utf-8")
    job = {"id": item["id"], "model": "seed-audio-1.0", "status": "complete",
           "request": {"model": "seed-audio-1.0", "text_prompt": item["text_prompt"],
                       "audio_config": {"format": "wav", "sample_rate": 48000}},
           "sha256": digest, "originalDurationSeconds": 1, "actualSeconds": 1}
    (folder / "job.json").write_text(json.dumps(job), encoding="utf-8")
    return audio


class SeedCatalogQaTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.work = Path(self.temp.name)

    def test_complete_pcm_passes_but_awaits_content_listening(self):
        item = cue("Click_01")
        write_complete(self.work, item)
        record, fingerprint = qa.inspect_cue(self.work, item)
        self.assertEqual("ready_for_listening", record["status"])
        self.assertEqual("not_performed", record["contentListening"])
        self.assertFalse(record["errors"])
        self.assertEqual(2048, len(fingerprint))

    def test_changed_file_and_clip_are_reported(self):
        item = cue("Click_01")
        audio = write_complete(self.work, item)
        with wave.open(str(audio), "wb") as output:
            output.setnchannels(1)
            output.setsampwidth(2)
            output.setframerate(48000)
            output.writeframes(np.full(48000, 32767, dtype="<i2").tobytes())
        record, _ = qa.inspect_cue(self.work, item)
        self.assertEqual("technical_issue", record["status"])
        self.assertTrue(any("SHA-256" in issue for issue in record["errors"]))
        self.assertTrue(any("clipped" in issue for issue in record["errors"]))

    def test_unknown_submission_is_never_called_a_failure(self):
        item = cue("Click_01")
        folder = self.work / "raw-catalog" / item["id"]
        folder.mkdir(parents=True)
        (folder / "job.json").write_text('{"status":"submission_unknown"}', encoding="utf-8")
        record, fingerprint = qa.inspect_cue(self.work, item)
        self.assertEqual("in_flight_or_unresolved", record["status"])
        self.assertIsNone(fingerprint)

    def test_explicit_replacement_source_preserves_authored_cue_id(self):
        item = cue("Laser_03", "Original laser sound.")
        item["sourceId"] = "Laser_03_R2"
        source = dict(item, id="Laser_03_R2")
        write_complete(self.work, source)
        record, fingerprint = qa.inspect_cue(self.work, item)
        self.assertEqual("Laser_03", record["id"])
        self.assertEqual("Laser_03_R2", record["sourceId"])
        self.assertEqual("ready_for_listening", record["status"])
        self.assertIn("Laser_03_R2/audio.wav", record["audioRelative"])
        self.assertIsNotNone(fingerprint)

    def test_same_waveform_variant_is_flagged_without_accepting_it(self):
        items = [cue("Click_01"), cue("Click_02", "Another sound.")]
        for item in items:
            write_complete(self.work, item)
        catalog = {"cues": items, "nonSpeechTarget": 2}
        review = {"representatives": [], "subjectiveListening": "not_performed"}
        result = qa.audit(self.work, catalog, review)
        self.assertEqual(2, result["statusCounts"]["ready_for_listening"])
        self.assertEqual("identical_file", result["possibleDuplicates"][0]["type"])
        self.assertEqual(0, result["representativesAcceptedByUser"])

    def test_listening_page_escapes_prompt(self):
        item = cue("Click_01", "<script>alert(1)</script>")
        write_complete(self.work, item)
        catalog = {"cues": [item], "nonSpeechTarget": 1}
        review = {"representatives": []}
        summary = qa.audit(self.work, catalog, review)
        page = qa.listening_html(summary, catalog, review)
        self.assertIn("&lt;script&gt;alert(1)&lt;/script&gt;", page)
        self.assertNotIn("<script>alert(1)</script>", page)


if __name__ == "__main__":
    unittest.main()
