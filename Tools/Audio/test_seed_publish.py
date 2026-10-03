"""Offline algorithm fixtures; not a Seed API request or subjective listening."""
import hashlib
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch
import wave

import numpy as np
import seed_publish as publish


def save_json(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(content, ensure_ascii=False), encoding="utf-8")


def fixture_wav(path, seconds, rate=48000, channels=2, leading=.0):
    """Generated only inside temporary unit-test directories."""
    path.parent.mkdir(parents=True, exist_ok=True)
    frames = round(seconds * rate)
    x = np.full((frames, channels), 3000, dtype="<i2")
    x[:round(leading * rate)] = 0
    with wave.open(str(path), "wb") as out:
        out.setnchannels(channels)
        out.setsampwidth(2)
        out.setframerate(rate)
        out.writeframes(x.tobytes())
    return hashlib.sha256(path.read_bytes()).hexdigest()


class SeedPublishTests(unittest.TestCase):
    def setUp(self):
        temp = TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.work = self.root / "ArtSource/Audio/SeedAudio20260929"
        config = {"ROOT": self.root, "WORK": self.work,
                  "CATALOG": self.work / "catalog.json",
                  "REVIEW": self.work / "capability-review.json",
                  "STAGING": self.work / "staging", "PUBLISHED": self.work / "published"}
        for key, value in config.items():
            p = patch.object(publish, key, value)
            p.start()
            self.addCleanup(p.stop)
        cues = [{"id": "Laser_01" if index == 0 else f"Cue_{index:03d}",
                 "category": "combat", "targetSeconds": .8,
                 "loop": False, "text_prompt": "catalog request" if index == 0 else "fixture"}
                for index in range(131)]
        save_json(self.work / "catalog.json", {"cues": cues})
        representatives = [{"category": name, "id": name + "_JOB", "seconds": 4,
                            "prompt": "representative request", "status": "generated_pending_listening"}
                           for name in ("LASER", "METAL", "EXPLOSION", "FLIGHT", "CABIN", "MUSIC")]
        save_json(self.work / "capability-review.json", {"representatives": representatives})
        for folder, cue_id, prompt in ((self.work / "raw/LASER_JOB", "LASER", "representative request"),
                                        (self.work / "raw-catalog/Laser_01", "Laser_01", "catalog request")):
            hash_value = fixture_wav(folder / "audio.wav", 2.0, leading=.3)
            save_json(folder / "job.json", {"status": "complete", "sha256": hash_value,
                      "request": {"model": "seed-audio-1.0", "text_prompt": prompt}})

    def test_stage_catalog_preserves_seed_provenance_and_trims_onset(self):
        cue = publish.inventory()["Laser_01"]
        candidate = publish.stage(cue)
        self.assertFalse(candidate["accepted"])
        self.assertGreater(candidate["recipe"]["startFrame"], 0)
        self.assertAlmostEqual(candidate["qa"]["durationSeconds"], .8, places=3)
        self.assertEqual(candidate["qa"]["channels"], 2)
        self.assertEqual(candidate["qa"]["clippedSamples"], 0)
        self.assertEqual(candidate["sha256"], publish.stage(cue)["sha256"])
        self.assertFalse((self.work / "published/Laser_01.wav").exists())

    def test_loop_crossfade_uses_only_source_and_has_quiet_seam(self):
        x = np.linspace(.1, .2, 48000, dtype=np.float32).reshape(-1, 1)
        out, recipe = publish.process(x, 48000, .6, True, False, "music")
        self.assertGreater(recipe["crossfadeFrames"], 0)
        self.assertEqual(out.shape[1], 1)
        self.assertLess(float(abs(out[0, 0] - out[-1, 0])), .001)
        self.assertTrue(np.isfinite(out).all())

    def test_representative_needs_review_then_publishes_double_hash(self):
        item = publish.inventory()["LASER"]
        publish.stage(item)
        with self.assertRaisesRegex(publish.PublishError, "awaits listening"):
            publish.publish([item], "Human listened to this entire sample")
        review = publish.read_json(self.work / "capability-review.json")
        review["representatives"][0]["status"] = "accepted_after_listening"
        save_json(self.work / "capability-review.json", review)
        paths = publish.publish([publish.inventory()["LASER"]], "Human listened to this entire sample")
        self.assertEqual(len(paths), 1)
        output = self.work / "published/LASER.wav"
        sidecar = publish.read_json(output.with_suffix(".json"))
        self.assertEqual(sidecar["sha256"], publish.sha256(output))
        self.assertEqual(sidecar["sourceSha256"], publish.sha256(self.work / "raw/LASER_JOB/audio.wav"))
        self.assertEqual(sidecar["model"], "seed-audio-1.0")
        self.assertTrue(sidecar["accepted"])
        self.assertEqual(publish.publish([publish.inventory()["LASER"]], "Human listened to this entire sample"), paths)

    def test_tamper_blocks_source_and_prior_stage(self):
        item = publish.inventory()["Laser_01"]
        plan = publish.proposal(item)
        publish.stage(item)
        with plan["output"].open("ab") as out:
            out.write(b"tamper")
        with self.assertRaisesRegex(publish.PublishError, "changed"):
            publish.stage(item)
        raw = self.work / "raw-catalog/Laser_01/audio.wav"
        with raw.open("ab") as out:
            out.write(b"tamper")
        with self.assertRaisesRegex(publish.PublishError, "SHA mismatch"):
            publish.proposal(item)


if __name__ == "__main__":
    unittest.main()
