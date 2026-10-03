"""Offline fixtures only; these tests are not content listening or live Seed calls."""
import hashlib
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
import wave
from unittest.mock import patch

import numpy as np

import seed_remix as remix


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")


def fixture_wav(path, seconds, rate=24000, channels=1):
    """A simple test vector; never a source for production audio."""
    path.parent.mkdir(parents=True, exist_ok=True)
    count = round(seconds * rate)
    x = np.full((count, channels), 5000, dtype="<i2")
    with wave.open(str(path), "wb") as out:
        out.setnchannels(channels)
        out.setsampwidth(2)
        out.setframerate(rate)
        out.writeframes(x.tobytes())
    return hashlib.sha256(path.read_bytes()).hexdigest()


class SeedRemixTests(unittest.TestCase):
    def setUp(self):
        self.temp = TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.work = self.root / "ArtSource/Audio/SeedAudio20260929"
        self.old = self.root / "ArtSource/Audio/CinematicAudio/Scenes"
        changes = {"ROOT": self.root, "WORK": self.work, "OLD_SCENES": self.old,
                   "VOICE_REVISIONS": self.work / "narrative-revision.json",
                   "VOICE_LEDGER": self.work / "voice-ledger.json",
                   "PUBLISHED_CUES": self.work / "published",
                   "DRAFTS": self.work / "remixed/Scenes",
                   "PUBLISHED_RADIO": self.work / "published-radio"}
        for name, value in changes.items():
            p = patch.object(remix, name, value)
            p.start()
            self.addCleanup(p.stop)
        old_voice_root = self.root / "ArtSource/Audio/Generated/Volcengine/seed-tts/old"
        new_voice_root = self.root / "ArtSource/Audio/Generated/Volcengine/seed-tts/new"
        lines, entries = [], []
        for line_id, old_seconds, new_seconds in (("X001", 1.0, 2.0), ("BG01", .5, .6)):
            fixture_wav(old_voice_root / (line_id + "-old") / "voice.wav", old_seconds)
            directory = new_voice_root / (line_id + "-new")
            sha = fixture_wav(directory / "voice.wav", new_seconds)
            payload = {"user": {"uid": "fixture"},
                       "req_params": {"text": line_id + " line", "speaker": "fixture-voice",
                                      "additions": json.dumps({"context_texts": ["fixture direction"]})}}
            write_json(directory / "job.json", {"status": "complete", "provider": "seed-tts",
                        "resource_id": "seed-tts-2.0", "request": payload, "sha256": sha})
            lines.append({"id": line_id, "english": line_id + " line", "text": line_id + " 字幕",
                          "role": "comms", "speaker": "fixture-voice",
                          "context_texts": ["fixture direction"]})
            entries.append({"id": line_id, "status": "complete", "job": str(directory / "job.json"),
                            "sha256": sha, "request": {"payload": payload}})
        write_json(self.work / "narrative-revision.json", {"voiceProvider": "Seed-TTS 2.0",
                   "take": "fixture", "lines": lines})
        write_json(self.work / "voice-ledger.json", {"take": "fixture", "entries": entries})
        for cue in ("Console_01", "CabinBed_01", "Connect_01", "Disconnect_01"):
            raw_dir = self.work / "raw-catalog" / cue
            raw_hash = fixture_wav(raw_dir / "audio.wav", 1.0, 48000, 2)
            write_json(raw_dir / "job.json", {"status": "complete",
                       "request": {"model": "seed-audio-1.0"}, "sha256": raw_hash})
            published = self.work / "published" / (cue + ".wav")
            published.parent.mkdir(parents=True, exist_ok=True)
            published.write_bytes((raw_dir / "audio.wav").read_bytes())
            write_json(published.with_suffix(".json"), {"accepted": True,
                       "model": "seed-audio-1.0", "sha256": remix.digest(published),
                       "sourceJob": remix.relative(raw_dir / "job.json"),
                       "sourceSha256": raw_hash})
        old_base = "ArtSource/Audio/Generated/Volcengine/seed-tts/old"
        effects_base = "ArtSource/Audio/CinematicAudio/Effects"
        def t(name, start, gain=1.0, perspective="near"):
            return {"file": name, "start": start, "gain": gain, "perspective": perspective}
        stems = {
            "lead": [t(f"{old_base}/X001-old/voice.wav", .2)],
            "background_people": [t(f"{old_base}/BG01-old/voice.wav", 1.3, .5, "far")],
            "actions": [t(f"{effects_base}/Console_1.wav", .5, .3, "room")],
            "environment": [t(f"{effects_base}/Equipment_1.wav", 0, .4, "room"),
                            t(f"{effects_base}/Equipment_1.wav", 1.0, .4, "room")],
            "events": [],
            "link": [t(f"{effects_base}/Connect_1.wav", 0, .4),
                     t(f"{effects_base}/Disconnect_1.wav", 1.9, .4)],
        }
        write_json(self.old / "X001/mix_recipe.json", {"id": "X001", "profile": "calm",
                   "duration": 2.1, "stems": stems})

    def test_render_uses_only_verified_seed_and_expands_dialogue(self):
        plan = remix.plan_scene("X001")
        self.assertTrue(plan["ready"])
        self.assertAlmostEqual(plan["captions"][1]["time"], 2.3, places=3)
        self.assertAlmostEqual(plan["duration"], 3.2, places=3)
        self.assertEqual(len([x for x in plan["tracks"] if x["sourceId"] == "CabinBed_01"]), 1)
        result = remix.render_scene(plan)
        output = plan["directory"] / "radio_mix.wav"
        self.assertTrue(output.is_file())
        self.assertEqual(result["oldProgramSourcesUsed"], 0)
        self.assertEqual(result["outputs"]["radio_mix.wav"]["clippedSamples"], 0)
        self.assertEqual(remix.render_scene(plan)["outputs"], result["outputs"])
        self.assertTrue(all("Effects" not in t["sourceFile"] for t in result["identity"]["tracks"]))

    def test_hash_corruption_blocks_source_and_cache(self):
        plan = remix.plan_scene("X001")
        remix.render_scene(plan)
        with (plan["directory"] / "radio_mix.wav").open("ab") as out:
            out.write(b"corrupt")
        with self.assertRaisesRegex(remix.RemixError, "corrupt draft"):
            remix.render_scene(plan)
        published = self.work / "published/Console_01.wav"
        with published.open("ab") as out:
            out.write(b"corrupt")
        blocked = remix.plan_scene("X001")
        self.assertFalse(blocked["ready"])
        self.assertTrue(any("hash mismatch" in item for item in blocked["missing"]))

    def test_publish_writes_accepted_sidecar_without_old_asset_change(self):
        plan = remix.plan_scene("X001")
        remix.render_scene(plan)
        paths = remix.publish_scene(plan, "Human listened to entire fixture scene; accepted.")
        self.assertEqual(len(paths), 1)
        sidecar = remix.read_json(self.work / "published-radio/X001.json")
        self.assertTrue(sidecar["accepted"])
        self.assertEqual(sidecar["sha256"], remix.digest(self.work / "published-radio/X001.wav"))
        self.assertEqual(sidecar["model"], "seed-tts-2.0+seed-audio-1.0")
        self.assertTrue(sidecar["sourceJobs"])
        self.assertEqual(len(sidecar["sourceJobs"]), len(sidecar["sourceHashes"]))
        for job_name, source_hash in zip(sidecar["sourceJobs"], sidecar["sourceHashes"]):
            self.assertEqual(remix.read_json(self.root / job_name)["sha256"], source_hash)
        self.assertEqual(set(sidecar["captions"][0]), {"time", "duration", "english", "text", "role"})
        self.assertEqual(sidecar["english"], "X001 line")
        self.assertEqual(sidecar["text"], "X001 字幕")
        self.assertEqual(remix.publish_scene(plan, "Human listened to entire fixture scene; accepted."), paths)

    def test_unresolved_e033_uses_only_completed_r2_matching_same_line(self):
        revision_file = self.work / "narrative-revision.json"
        ledger_file = self.work / "voice-ledger.json"
        revision, ledger = remix.read_json(revision_file), remix.read_json(ledger_file)
        line = {"id": "E033", "english": "The same revised words.", "text": "同一句修订字幕。",
                "speaker": "fixture-voice", "context_texts": ["fixture direction"], "role": "engineer"}
        revision["lines"].append(line)
        ledger["entries"].append({"id": "E033", "status": "submission_unknown"})
        directory = self.root / "ArtSource/Audio/Generated/Volcengine/seed-tts/new/E033_R2-new"
        sha = fixture_wav(directory / "voice.wav", 1.2)
        payload = {"req_params": {"text": line["english"], "speaker": line["speaker"],
                    "additions": json.dumps({"context_texts": line["context_texts"]})}}
        write_json(directory / "job.json", {"status": "complete", "provider": "seed-tts",
                    "resource_id": "seed-tts-2.0", "request": payload, "sha256": sha})
        ledger["entries"].append({"id": "E033_R2", "status": "complete",
                                  "job": str(directory / "job.json"), "sha256": sha,
                                  "request": {"payload": payload}})
        write_json(revision_file, revision)
        write_json(ledger_file, ledger)
        lines, entries = remix._voice_inventory()
        chosen = remix._job_for_voice("E033", lines, entries)
        self.assertEqual(chosen["job"], directory / "job.json")
        self.assertEqual(chosen["sha256"], sha)

    def test_laser_r2_cue_can_be_used_under_stable_published_id(self):
        raw_dir = self.work / "raw-catalog/Laser_03_R2"
        source_sha = fixture_wav(raw_dir / "audio.wav", .8, 48000, 2)
        write_json(raw_dir / "job.json", {"status": "complete",
                   "request": {"model": "seed-audio-1.0"}, "sha256": source_sha})
        published = self.work / "published/Laser_03.wav"
        published.write_bytes((raw_dir / "audio.wav").read_bytes())
        write_json(published.with_suffix(".json"), {"accepted": True,
                   "model": "seed-audio-1.0", "sha256": remix.digest(published),
                   "sourceJob": remix.relative(raw_dir / "job.json"),
                   "sourceSha256": source_sha})
        chosen = remix._cue_source("Laser_03")
        self.assertEqual(chosen["job"], raw_dir / "job.json")
        self.assertEqual(chosen["sourceSha256"], source_sha)

    def test_loop_is_seed_input_blend_not_new_oscillator(self):
        source = np.linspace(.1, .2, 4000, dtype=np.float32)
        out = remix.loop_audio(source, 12500, .02)
        self.assertEqual(len(out), 12500)
        self.assertTrue(np.isfinite(out).all())
        self.assertAlmostEqual(float(out[0]), .1, places=5)
        self.assertGreater(float(out[-1]), .1)

    def test_authored_alarm_gets_one_seed_interference_without_changing_template(self):
        recipe_path = self.old / "X001/mix_recipe.json"
        original = recipe_path.read_bytes()
        recipe = remix.read_json(recipe_path)
        recipe["stems"]["environment"].append({
            "file": "ArtSource/Audio/CinematicAudio/Effects/Alarm_1.wav",
            "start": .45, "gain": .35, "perspective": "room"})
        write_json(recipe_path, recipe)
        authored = recipe_path.read_bytes()
        for cue in ("Alarm_01", "Interference_01"):
            raw_dir = self.work / "raw-catalog" / cue
            source_hash = fixture_wav(raw_dir / "audio.wav", .7, 48000, 2)
            write_json(raw_dir / "job.json", {"status": "complete",
                       "request": {"model": "seed-audio-1.0"}, "sha256": source_hash})
            published = self.work / "published" / (cue + ".wav")
            published.write_bytes((raw_dir / "audio.wav").read_bytes())
            write_json(published.with_suffix(".json"), {"accepted": True,
                       "model": "seed-audio-1.0", "sha256": remix.digest(published),
                       "sourceJob": remix.relative(raw_dir / "job.json"),
                       "sourceSha256": source_hash})
        plan = remix.plan_scene("X001")
        self.assertTrue(plan["ready"], plan.get("missing"))
        alarms = [track for track in plan["tracks"] if track["sourceId"].startswith("Alarm_")]
        details = [track for track in plan["tracks"] if track["sourceId"].startswith("Interference_")]
        self.assertEqual(len(alarms), 1)
        self.assertEqual(len(details), 1)
        self.assertEqual(details[0]["sourceId"], "Interference_01")
        self.assertEqual(details[0]["stem"], "environment")
        self.assertAlmostEqual(details[0]["start"], alarms[0]["start"])
        self.assertEqual(recipe_path.read_bytes(), authored)
        self.assertNotEqual(original, authored)
        self.assertTrue(all("Effects" not in t["sourceFile"] for t in plan["identity"]["tracks"]))


class SeedRemixRepositoryContractTests(unittest.TestCase):
    def test_25_old_effects_have_distinct_seed_catalog_equivalents_and_158_outputs(self):
        root = Path(__file__).resolve().parents[2]
        effects = json.loads((root / "ArtSource/Audio/CinematicAudio/effects-manifest.json").read_text(encoding="utf-8"))
        catalog = json.loads((root / "ArtSource/Audio/SeedAudio20260929/catalog.json").read_text(encoding="utf-8"))
        cue_ids = {cue["id"] for cue in catalog["cues"]}
        mapped = [remix._cue_id({"file": entry["file"]}, "calm") for entry in effects]
        self.assertEqual(len(effects), 25)
        self.assertEqual(len(set(mapped)), 25)
        self.assertTrue(set(mapped).issubset(cue_ids))
        scenes = list((root / "ArtSource/Audio/CinematicAudio/Scenes").glob("*/mix_recipe.json"))
        self.assertEqual(len(scenes), 80)
        self.assertEqual(sum((entry.parent / "radio_low.wav").is_file() for entry in scenes), 39)
        self.assertEqual(sum((entry.parent / "radio_high.wav").is_file() for entry in scenes), 39)
        self.assertEqual(80 + 39 + 39, 158)

    def test_all_link_alarm_and_signal_variants_have_authored_events(self):
        root = Path(__file__).resolve().parents[2]
        scenes = root / "ArtSource/Audio/CinematicAudio/Scenes"
        consumed = {}
        for path in scenes.glob("*/mix_recipe.json"):
            recipe = remix.read_json(path)
            counters = {}
            first_alarm = None
            for stem, tracks in recipe["stems"].items():
                if stem in remix.SPEECH_STEMS:
                    continue
                for track in tracks:
                    base = remix._cue_id(track, recipe["profile"])
                    family = base.rsplit("_", 1)[0]
                    count = counters.get(family, 0)
                    cue_id = remix._scene_variant(base, path.parent.name, count)
                    counters[family] = count + 1
                    if family in ("Alarm", "Connect", "Disconnect"):
                        consumed.setdefault(cue_id, set()).add(path.parent.name)
                    if family == "Alarm" and first_alarm is None:
                        first_alarm = track["start"]
            if first_alarm is not None:
                consumed.setdefault(remix._interference_id(path.parent.name), set()).add(path.parent.name)
        expected = {f"{family}_{number:02d}" for family in ("Alarm", "Connect", "Disconnect")
                    for number in range(1, 4)}
        expected.update(f"Interference_{number:02d}" for number in range(1, 5))
        self.assertEqual(set(consumed), expected)
        self.assertTrue(all(consumed.values()))


if __name__ == "__main__":
    unittest.main()
