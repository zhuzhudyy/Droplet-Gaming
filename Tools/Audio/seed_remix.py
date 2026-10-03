"""Offline Seed-only radio remixes. Never calls a cloud service or writes Unity Assets.

The 2026-09-19 scene recipes supply editorial timing only. Every waveform in a
new mix must resolve to a completed Seed-TTS job or an accepted Seed Audio cue.
Preview is read-only; render writes versioned drafts; publish requires an
explicit listening decision and creates immutable, provenance-checked files.
"""
from __future__ import annotations

import argparse
import hashlib
import html
import json
import math
import os
from pathlib import Path, PureWindowsPath
import shutil
import sys
import wave

import numpy as np
from scipy import ndimage, signal

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "ArtSource/Audio/SeedAudio20260929"
OLD_SCENES = ROOT / "ArtSource/Audio/CinematicAudio/Scenes"
VOICE_REVISIONS = WORK / "narrative-revision.json"
VOICE_LEDGER = WORK / "voice-ledger.json"
PUBLISHED_CUES = WORK / "published"
DRAFTS = WORK / "remixed/Scenes"
PUBLISHED_RADIO = WORK / "published-radio"
SAMPLE_RATE = 24000
SCHEMA = "droplet.seed-radio-remix.v1"
STEMS = ("lead", "background_people", "actions", "environment", "events", "link")
SPEECH_STEMS = ("lead", "background_people")
FAMILY_MAP = {
    "DeckSteps": "DeckSteps", "Console": "Console", "Hatch": "Hatch",
    "Impact": "Penetration", "Explosion": "Explosion", "Laser": "Laser",
    "Reflect": "Reflect", "Alarm": "Alarm", "Equipment": "CabinBed",
    "Connect": "Connect", "Disconnect": "Disconnect",
}
TARGET_DB = {"lead": -18.5, "background_people": -27.0,
             "actions": -29.5, "environment": -37.0, "events": -29.0,
             "link": -26.0}


class RemixError(RuntimeError):
    pass


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def canonical(data) -> bytes:
    return json.dumps(data, ensure_ascii=False, sort_keys=True,
                      separators=(",", ":"), allow_nan=False).encode("utf-8")


def relative(path: Path) -> str:
    return path.resolve().relative_to(ROOT.resolve()).as_posix()


def project_path(value: str) -> Path:
    path = Path(value)
    if not path.is_absolute():
        path = ROOT / path
    path = path.resolve()
    if not path.is_relative_to(ROOT.resolve()):
        raise RemixError("Source is outside this project: " + str(value))
    return path


def wav_info(path: Path) -> dict:
    with wave.open(str(path), "rb") as handle:
        if handle.getsampwidth() != 2 or handle.getcomptype() != "NONE":
            raise RemixError("Only source PCM16 WAV is supported: " + relative(path))
        frames, rate, channels = handle.getnframes(), handle.getframerate(), handle.getnchannels()
    if frames <= 0 or rate < 8000 or channels not in (1, 2):
        raise RemixError("Invalid Seed WAV: " + relative(path))
    return {"frames": frames, "rate": rate, "channels": channels,
            "seconds": frames / rate}


def _job_for_voice(line_id: str, lines: dict, entries: dict) -> dict:
    # The first E033 request is explicitly unresolved. Its user-authorized R2
    # remake speaks the exact same line; never read or replay the unknown take.
    entry_id = "E033_R2" if line_id == "E033" else line_id
    if line_id not in lines or entry_id not in entries:
        raise RemixError("Revised Seed-TTS voice not yet complete: " + line_id)
    if line_id == "E033" and entries.get("E033", {}).get("status") != "submission_unknown":
        raise RemixError("E033 source status changed; review the explicit R2 mapping")
    entry = entries[entry_id]
    if entry.get("status") != "complete":
        raise RemixError("Seed-TTS request unresolved: " + line_id)
    job_path = project_path(entry["job"])
    path = job_path.with_name("voice.wav")
    if not job_path.is_file() or not path.is_file():
        raise RemixError("Missing Seed-TTS job/audio: " + line_id)
    job = read_json(job_path)
    actual_hash = digest(path)
    expected = lines[line_id]
    request_params = entry.get("request", {}).get("payload", {}).get("req_params", {})
    if (request_params.get("text") != expected["english"] or
            request_params.get("speaker") != expected.get("speaker") or
            json.loads(request_params.get("additions", "{}")) .get("context_texts") != expected.get("context_texts")):
        raise RemixError("Seed-TTS voice does not match revised text/speaker/direction: " + line_id)
    if (job.get("status") != "complete" or job.get("provider") != "seed-tts"
            or job.get("resource_id") != "seed-tts-2.0"
            or job.get("sha256") != actual_hash or entry.get("sha256") != actual_hash
            or job.get("request") != entry.get("request", {}).get("payload")):
        raise RemixError("Seed-TTS provenance/hash mismatch: " + line_id)
    info = wav_info(path)
    return {"id": line_id, "path": path, "job": job_path, "sha256": actual_hash,
            "model": "seed-tts-2.0", "seconds": info["seconds"],
            "accepted": False}


def _cue_source(cue_id: str) -> dict:
    path = PUBLISHED_CUES / (cue_id + ".wav")
    sidecar = PUBLISHED_CUES / (cue_id + ".json")
    if not path.is_file() or not sidecar.is_file():
        raise RemixError("Accepted Seed Audio cue not yet published: " + cue_id)
    meta = read_json(sidecar)
    if meta.get("accepted") is not True or meta.get("model") != "seed-audio-1.0":
        raise RemixError("Seed Audio cue not accepted: " + cue_id)
    actual_hash = digest(path)
    if actual_hash != meta.get("sha256"):
        raise RemixError("Published cue WAV hash mismatch: " + cue_id)
    job_path = project_path(meta["sourceJob"])
    if not job_path.is_file() or job_path.name != "job.json":
        raise RemixError("Missing original Seed Audio job: " + cue_id)
    job = read_json(job_path)
    raw_path = job_path.with_name("audio.wav")
    if (job.get("status") != "complete" or job.get("request", {}).get("model") != "seed-audio-1.0"
            or not raw_path.is_file() or digest(raw_path) != job.get("sha256")
            or meta.get("sourceSha256") != job.get("sha256")):
        raise RemixError("Original Seed Audio provenance/hash mismatch: " + cue_id)
    info = wav_info(path)
    return {"id": cue_id, "path": path, "job": job_path,
            "sha256": actual_hash, "sourceSha256": job["sha256"],
            "model": "seed-audio-1.0", "seconds": info["seconds"],
            "accepted": True}


def _old_voice_id(track: dict) -> str:
    value = PureWindowsPath(track["file"]).parent.name
    line_id = value.split("-", 1)[0]
    if not line_id or not line_id[0].isalpha():
        raise RemixError("Unexpected old voice template path")
    return line_id


def _cue_id(track: dict, profile: str) -> str:
    stem = PureWindowsPath(track["file"]).stem
    if "_" not in stem:
        raise RemixError("Unexpected procedural template: " + stem)
    family, number = stem.rsplit("_", 1)
    if family not in FAMILY_MAP or not number.isdigit():
        raise RemixError("Non-Seed sound cannot be mapped: " + stem)
    if family == "Equipment":
        number = {"studio": 1, "calm": 1, "attack": 2,
                  "evacuation": 3}[profile]
    return f"{FAMILY_MAP[family]}_{int(number):02d}"


def _scene_ordinal(scene_id: str) -> int:
    digits = "".join(character for character in scene_id if character.isdigit())
    if digits:
        return max(1, int(digits))
    return 1 + int.from_bytes(hashlib.sha256(scene_id.encode("utf-8")).digest()[:4], "big")


def _scene_variant(cue_id: str, scene_id: str, occurrence: int = 0) -> str:
    """Rotate existing authored link/alarm events, never invent new event times."""
    family = cue_id.rsplit("_", 1)[0]
    if family not in ("Alarm", "Connect", "Disconnect"):
        return cue_id
    variant = (_scene_ordinal(scene_id) - 1 + occurrence) % 3 + 1
    return f"{family}_{variant:02d}"


def _interference_id(scene_id: str) -> str:
    """One quiet signal detail per authored alarm scene, cycling four Seed takes."""
    return f"Interference_{(_scene_ordinal(scene_id) - 1) % 4 + 1:02d}"


def _voice_inventory() -> tuple[dict, dict]:
    revision = read_json(VOICE_REVISIONS)
    ledger = read_json(VOICE_LEDGER)
    if revision.get("voiceProvider") != "Seed-TTS 2.0" or \
            revision.get("take") != ledger.get("take"):
        raise RemixError("Revised voice inventory identity mismatch")
    lines = {line["id"]: line for line in revision["lines"]}
    entries = {entry["id"]: entry for entry in ledger["entries"]}
    return lines, entries


def _timing(speech: list[dict], old_duration: float) -> tuple[list[dict], float, list[tuple[float, float]]]:
    """Keep authored gaps; expand/contract speech blocks to actual new dry lengths."""
    speech.sort(key=lambda x: x["oldStart"])
    anchors = [(0.0, 0.0)]
    previous_old_end = previous_new_end = 0.0
    for item in speech:
        old_start = item["oldStart"]
        old_end = old_start + item["oldSeconds"]
        if old_start + 1e-5 < previous_old_end:
            raise RemixError("Overlapping old dialogue template needs manual editing")
        new_start = previous_new_end + max(0.0, old_start - previous_old_end)
        new_end = new_start + item["source"]["seconds"]
        item["start"] = new_start
        anchors.extend(((old_start, new_start), (old_end, new_end)))
        previous_old_end, previous_new_end = old_end, new_end
    duration = max(previous_new_end + 0.25,
                   previous_new_end + max(0.0, old_duration - previous_old_end))
    anchors.append((old_duration, duration))
    return speech, duration, anchors


def warp_time(old_time: float, anchors: list[tuple[float, float]]) -> float:
    if old_time <= anchors[0][0]:
        return anchors[0][1]
    for (a, b), (c, d) in zip(anchors, anchors[1:]):
        if old_time <= c:
            return b if c <= a else b + (old_time - a) * (d - b) / (c - a)
    return anchors[-1][1] + old_time - anchors[-1][0]


def plan_scene(scene_id: str) -> dict:
    recipe_path = OLD_SCENES / scene_id / "mix_recipe.json"
    if not recipe_path.is_file():
        raise RemixError("Unknown old radio scene: " + scene_id)
    template = read_json(recipe_path)
    if set(template["stems"]) != set(STEMS):
        raise RemixError("Unexpected template stems: " + scene_id)
    lines, entries = _voice_inventory()
    all_tracks = []
    missing = []
    speech = []
    variant_counts = {}
    first_alarm = None
    for stem in STEMS:
        for track in template["stems"][stem]:
            if stem in SPEECH_STEMS:
                source_id = _old_voice_id(track)
            else:
                base_id = _cue_id(track, template["profile"])
                family = base_id.rsplit("_", 1)[0]
                occurrence = variant_counts.get(family, 0)
                source_id = _scene_variant(base_id, scene_id, occurrence)
                variant_counts[family] = occurrence + 1
                if family == "Alarm" and first_alarm is None:
                    first_alarm = float(track["start"])
            try:
                source = (_job_for_voice(source_id, lines, entries) if stem in SPEECH_STEMS
                          else _cue_source(source_id))
            except (RemixError, KeyError, OSError) as exc:
                missing.append(f"{stem}: {source_id}: {exc}")
                source = None
            item = {"stem": stem, "sourceId": source_id, "source": source,
                    "oldStart": float(track["start"]), "oldGain": float(track["gain"]),
                    "perspective": track["perspective"]}
            if stem in SPEECH_STEMS and source:
                old_path = project_path(track["file"].replace("\\", "/"))
                item["oldSeconds"] = wav_info(old_path)["seconds"]
                speech.append(item)
            all_tracks.append(item)
    if first_alarm is not None:
        # Alarm is an existing authored event. The brief interference layer is
        # attached to its first onset, in the speech-ducked environment stem.
        source_id = _interference_id(scene_id)
        try:
            source = _cue_source(source_id)
        except (RemixError, KeyError, OSError) as exc:
            missing.append(f"environment: {source_id}: {exc}")
            source = None
        all_tracks.append({"stem": "environment", "sourceId": source_id,
                           "source": source, "oldStart": first_alarm,
                           "oldGain": .12, "perspective": "far"})
    if missing:
        return {"id": scene_id, "ready": False, "missing": missing,
                "template": relative(recipe_path)}
    speech, duration, anchors = _timing(speech, float(template["duration"]))
    for item in all_tracks:
        if item not in speech:
            item["start"] = warp_time(item["oldStart"], anchors)
    # A single sustained Seed cabin recording replaces each four-second
    # procedural equipment restart. It is repeated with a crossfade in render.
    equipment = [item for item in all_tracks if item["sourceId"].startswith("CabinBed_")]
    if equipment:
        first = min(equipment, key=lambda item: item["start"])
        first["start"] = 0.0
        first["loop"] = True
        all_tracks = [item for item in all_tracks if item not in equipment or item is first]
    caption_rows = []
    for item in speech:
        line = lines[item["sourceId"]]
        caption_rows.append({"time": round(item["start"], 6),
                             "duration": round(item["source"]["seconds"], 6),
                             "english": line["english"], "text": line["text"],
                             "role": line["role"]})
    normalized = []
    for item in all_tracks:
        source = item["source"]
        normalized.append({"stem": item["stem"], "sourceId": item["sourceId"],
                           "sourceFile": relative(source["path"]), "sourceJob": relative(source["job"]),
                           "sha256": source["sha256"],
                           "sourceSha256": source.get("sourceSha256", source["sha256"]),
                           "model": source["model"],
                           "start": round(item["start"], 6), "oldGain": item["oldGain"],
                           "perspective": item["perspective"], "loop": item.get("loop", False)})
    identity = {"schema": SCHEMA, "scene": scene_id, "templateSha256": digest(recipe_path),
                "revisionSha256": digest(VOICE_REVISIONS), "duration": round(duration, 6),
                "tracks": normalized, "captions": caption_rows,
                "recipeVersion": "seed-source-only-radio-2"}
    fingerprint = hashlib.sha256(canonical(identity)).hexdigest()
    return {"id": scene_id, "ready": True, "fingerprint": fingerprint,
            "directory": DRAFTS / scene_id / fingerprint[:16], "identity": identity,
            "tracks": all_tracks, "duration": duration, "template": template,
            "captions": caption_rows}


def decode(path: Path) -> np.ndarray:
    with wave.open(str(path), "rb") as handle:
        rate, channels, width = handle.getframerate(), handle.getnchannels(), handle.getsampwidth()
        if width != 2 or channels not in (1, 2):
            raise RemixError("Unsupported Seed PCM format: " + relative(path))
        x = np.frombuffer(handle.readframes(handle.getnframes()), dtype="<i2").astype(np.float32)
    x = x.reshape(-1, channels).mean(axis=1) / 32768.0
    if rate != SAMPLE_RATE:
        common = math.gcd(rate, SAMPLE_RATE)
        x = signal.resample_poly(x, SAMPLE_RATE // common, rate // common).astype(np.float32)
    return x


def perspective_audio(x: np.ndarray, perspective: str) -> np.ndarray:
    bands = {"near": (95, 8200), "far": (240, 3100), "room": (80, 6200)}
    if perspective not in bands:
        raise RemixError("Unknown perspective: " + perspective)
    low, high = bands[perspective]
    x = signal.sosfilt(signal.butter(2, low, "highpass", fs=SAMPLE_RATE, output="sos"), x)
    x = signal.sosfilt(signal.butter(2, high, "lowpass", fs=SAMPLE_RATE, output="sos"), x)
    if perspective != "near":
        dry = x.copy()
        for delay_ms, gain in ((23, .09), (43, .05)) if perspective == "room" else ((31, .15), (57, .08)):
            delay = round(delay_ms * SAMPLE_RATE / 1000)
            x[delay:] += dry[:-delay] * gain
    fade = min(len(x) // 2, round(.004 * SAMPLE_RATE))
    if fade:
        x[:fade] *= np.linspace(0, 1, fade, dtype=np.float32)
        x[-fade:] *= np.linspace(1, 0, fade, dtype=np.float32)
    return x.astype(np.float32)


def loop_audio(x: np.ndarray, frames: int, crossfade_seconds: float = .35) -> np.ndarray:
    """Repeat only input samples, blending matching ends without adding a tone."""
    if len(x) >= frames:
        return x[:frames]
    fade = min(round(crossfade_seconds * SAMPLE_RATE), len(x) // 6)
    if fade < 2:
        return np.resize(x, frames)
    result = np.zeros(frames, dtype=np.float32)
    first = min(len(x), frames)
    result[:first] = x[:first]
    cursor = len(x)
    while cursor < frames:
        start = cursor - fade
        n = min(len(x), frames - start)
        overlap = min(fade, n)
        a = np.linspace(1, 0, overlap, dtype=np.float32)
        result[start:start + overlap] = result[start:start + overlap] * a + x[:overlap] * (1 - a)
        if n > overlap:
            result[start + overlap:start + n] = x[overlap:n]
        cursor = start + n
    return result


def mix_tracks(plan: dict, background_gain: float = 1.0) -> np.ndarray:
    frames = round(plan["duration"] * SAMPLE_RATE)
    stems = {stem: np.zeros(frames, dtype=np.float32) for stem in STEMS}
    cache = {}
    peak_old_gain = {}
    for item in plan["tracks"]:
        key = (item["stem"], item["sourceId"])
        peak_old_gain[key] = max(peak_old_gain.get(key, 0), item["oldGain"])
    for item in plan["tracks"]:
        source = item["source"]
        key = (source["sha256"], item["perspective"])
        if key not in cache:
            cache[key] = perspective_audio(decode(source["path"]), item["perspective"])
        x = cache[key]
        start = round(item["start"] * SAMPLE_RATE)
        if start >= frames:
            continue
        available = frames - start
        x = loop_audio(x, available) if item.get("loop") else x[:available]
        rms = float(np.sqrt(np.mean(x * x))) if len(x) else 0.0
        if rms <= 1e-8:
            continue
        desired = 10 ** (TARGET_DB[item["stem"]] / 20)
        relative_gain = item["oldGain"] / max(peak_old_gain[(item["stem"], item["sourceId"])], 1e-6)
        gain = min(16.0, desired / rms) * relative_gain
        stems[item["stem"]][start:start + len(x)] += x * gain
    lead = stems["lead"]
    background = sum((stems[name] for name in ("background_people", "actions", "environment", "events")),
                     np.zeros(frames, dtype=np.float32)) * background_gain
    # Speech-driven ducking is one authoring-stage mix, not a second Unity bus.
    rms_window = max(1, round(.05 * SAMPLE_RATE))
    envelope = np.sqrt(np.maximum(0, ndimage.uniform_filter1d(lead * lead, rms_window)))
    duck = 1 - .64 * np.clip(envelope / .017, 0, 1)
    duck = signal.lfilter([.008], [1, -.992], duck).astype(np.float32)
    scene = lead + background * duck
    scene = signal.sosfilt(signal.butter(2, 230, "highpass", fs=SAMPLE_RATE, output="sos"), scene)
    scene = signal.sosfilt(signal.butter(2, 4300, "lowpass", fs=SAMPLE_RATE, output="sos"), scene)
    scene = np.tanh(scene * 1.18).astype(np.float32) * .78
    out = scene + stems["link"]
    peak = float(np.max(np.abs(out))) if len(out) else 0
    if peak > .84:
        out *= .84 / peak
    # A whole-scene cue ends on silence even if its Seed cabin bed is sustained.
    # This fades only existing samples; no synthetic sound is introduced.
    head = min(len(out) // 2, round(.005 * SAMPLE_RATE))
    tail = min(len(out) // 2, round(.080 * SAMPLE_RATE))
    if head:
        out[:head] *= np.linspace(0, 1, head, dtype=np.float32)
    if tail:
        out[-tail:] *= np.linspace(1, 0, tail, dtype=np.float32)
    return out.astype(np.float32)


def write_wav(path: Path, samples: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(".wav.tmp")
    with wave.open(str(temp), "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(SAMPLE_RATE)
        handle.writeframes((np.clip(samples, -.999, .999) * 32767).astype("<i2").tobytes())
    os.replace(temp, path)


def qa(path: Path) -> dict:
    x = decode(path)
    peak = float(np.max(np.abs(x)))
    rms = float(np.sqrt(np.mean(x * x)))
    return {"sha256": digest(path), "seconds": len(x) / SAMPLE_RATE,
            "sampleRate": SAMPLE_RATE, "channels": 1,
            "peakDbFS": 20 * math.log10(max(peak, 1e-10)),
            "rmsDbFS": 20 * math.log10(max(rms, 1e-10)),
            "clippedSamples": int(np.sum(np.abs(x) >= .999)),
            "edgeDiscontinuity": float(abs(x[0] - x[-1]))}


def output_names(plan: dict) -> list[str]:
    names = ["radio_mix.wav"]
    if (OLD_SCENES / plan["id"] / "radio_low.wav").is_file():
        names += ["radio_low.wav", "radio_high.wav"]
    return names


def render_scene(plan: dict) -> dict:
    if not plan["ready"]:
        raise RemixError("Missing accepted Seed sources for " + plan["id"])
    directory = plan["directory"]
    recipe_path = directory / "mix_recipe.json"
    if recipe_path.is_file():
        cached = read_json(recipe_path)
        if cached.get("fingerprint") != plan["fingerprint"]:
            raise RemixError("Versioned output fingerprint mismatch: " + plan["id"])
        if all((directory / name).is_file() and digest(directory / name) == cached["outputs"][name]["sha256"]
               for name in output_names(plan)):
            return cached
        raise RemixError("Incomplete/corrupt draft; inspect before rebuilding: " + plan["id"])
    if directory.exists() and any(directory.iterdir()):
        raise RemixError("Unfinished draft exists; inspect before rebuilding: " + str(directory))
    directory.mkdir(parents=True, exist_ok=True)
    gains = {"radio_mix.wav": 1.0, "radio_low.wav": .42,
             "radio_high.wav": 1.35}
    outputs = {}
    for name in output_names(plan):
        target = directory / name
        write_wav(target, mix_tracks(plan, gains[name]))
        outputs[name] = qa(target)
        if outputs[name]["clippedSamples"] or outputs[name]["rmsDbFS"] < -45:
            raise RemixError("Draft QA failed: " + relative(target))
    result = {"schema": SCHEMA, "id": plan["id"],
              "fingerprint": plan["fingerprint"], "identity": plan["identity"],
              "outputs": outputs, "subjectiveListening": "not_performed",
              "accepted": False, "oldProgramSourcesUsed": 0}
    recipe_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return result


def publish_scene(plan: dict, review: str, acceptance_basis: str = "individual_listening") -> list[str]:
    if not plan["ready"]:
        raise RemixError("Missing accepted Seed sources for " + plan["id"])
    recipe_path = plan["directory"] / "mix_recipe.json"
    if not recipe_path.is_file():
        raise RemixError("Render the Seed radio draft before publishing: " + plan["id"])
    recipe = read_json(recipe_path)
    if recipe.get("fingerprint") != plan["fingerprint"] or recipe.get("oldProgramSourcesUsed") != 0:
        raise RemixError("Draft source identity changed: " + plan["id"])
    publication = []
    source_pairs = sorted({(track["sourceJob"], track["sourceSha256"])
                           for track in plan["identity"]["tracks"]})
    for name in output_names(plan):
        source = plan["directory"] / name
        if not source.is_file() or digest(source) != recipe["outputs"][name]["sha256"]:
            raise RemixError("Draft WAV hash mismatch: " + plan["id"])
        suffix = "" if name == "radio_mix.wav" else "_low" if name == "radio_low.wav" else "_high"
        target = PUBLISHED_RADIO / (plan["id"] + suffix + ".wav")
        sidecar = target.with_suffix(".json")
        meta = {"accepted": True, "sha256": digest(source),
                "model": "seed-tts-2.0+seed-audio-1.0",
                "sourceJob": relative(recipe_path), "sourceSha256": digest(recipe_path),
                "durationSeconds": recipe["outputs"][name]["seconds"],
                "english": plan["captions"][0]["english"] if plan["captions"] else "",
                "text": plan["captions"][0]["text"] if plan["captions"] else "",
                "captions": plan["captions"],
                "sourceJobs": [job for job, _ in source_pairs],
                "sourceHashes": [sha for _, sha in source_pairs],
                "sources": [{"job": track["sourceJob"], "sha256": track["sourceSha256"],
                             "usedSha256": track["sha256"], "model": track["model"]}
                            for track in plan["identity"]["tracks"]],
                "review": review, "sourceOnly": "Seed-TTS 2.0 and Seed Audio 1.0",
                "acceptanceBasis": acceptance_basis,
                "subjectiveListening": ("accepted" if acceptance_basis == "individual_listening"
                                        else "not_individually_performed")}
        if target.exists() or sidecar.exists():
            if not (target.is_file() and sidecar.is_file() and
                    digest(target) == meta["sha256"] and
                    read_json(sidecar) == meta):
                raise RemixError("Published radio already exists with different content: " + target.name)
        publication.append((source, target, sidecar, meta))
    # Validate every variant before creating even one published file.
    PUBLISHED_RADIO.mkdir(parents=True, exist_ok=True)
    published = []
    for source, target, sidecar, meta in publication:
        if not target.exists():
            temporary = target.with_suffix(".wav.tmp")
            shutil.copyfile(source, temporary)
            os.replace(temporary, target)
            sidecar.write_text(json.dumps(meta, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        published.append(relative(target))
    return published


def listening_page(plans: list[dict]) -> Path:
    page = ["<!doctype html><html lang='zh-CN'><meta charset='utf-8'><title>Seed 通讯暂存试听</title>",
            "<style>body{font:16px system-ui;max-width:900px;margin:2em auto;background:#101725;color:#eef2f5}article{border:1px solid #455269;padding:1em;margin:1em 0}audio{width:100%}small{color:#9ac}</style>",
            "<h1>Seed 通讯暂存试听</h1><p>这些是离线重混候选，未经过主观试听验收，不是 Unity 发布资源。</p>"]
    for plan in plans:
        if not plan["ready"] or not (plan["directory"] / "mix_recipe.json").is_file():
            continue
        recipe = read_json(plan["directory"] / "mix_recipe.json")
        if recipe.get("fingerprint") != plan["fingerprint"]:
            continue
        rel = "Scenes/" + plan["id"] + "/" + plan["fingerprint"][:16]
        page.append("<article><h2>" + html.escape(plan["id"]) + "</h2>")
        for name in output_names(plan):
            src = rel + "/" + name
            page.append("<p><small>" + name + "</small><audio controls preload='none' src='" + src + "'></audio></p>")
        page.append("<p>" + html.escape(" / ".join(row["english"] for row in plan["captions"])) + "</p></article>")
    page.append("</html>")
    path = WORK / "remixed/listening.html"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(page), encoding="utf-8")
    return path


def technical_preview() -> Path:
    """A real-source audition sketch; the raw cabin candidate remains unaccepted.

    This uses the revised SC01/SC02 dry voices and one completed Seed Audio raw
    candidate. It deliberately omits absent console/link cues and cannot be
    published by publish_scene or imported as a finished Unity radio scene.
    """
    scene_id = "SC01"
    template = read_json(OLD_SCENES / scene_id / "mix_recipe.json")
    lines, entries = _voice_inventory()
    speech = []
    for stem in SPEECH_STEMS:
        for track in template["stems"][stem]:
            line_id = _old_voice_id(track)
            source = _job_for_voice(line_id, lines, entries)
            old_path = project_path(track["file"].replace("\\", "/"))
            speech.append({"stem": stem, "sourceId": line_id, "source": source,
                           "oldStart": float(track["start"]),
                           "oldSeconds": wav_info(old_path)["seconds"],
                           "oldGain": float(track["gain"]),
                           "perspective": track["perspective"]})
    speech, duration, _ = _timing(speech, float(template["duration"]))
    raw_job = WORK / "raw/CABIN_SERVICE_ACTIVE_397e1ed9/job.json"
    raw_audio = raw_job.with_name("audio.wav")
    job = read_json(raw_job)
    if (job.get("status") != "complete" or job.get("request", {}).get("model") != "seed-audio-1.0"
            or not raw_audio.is_file() or digest(raw_audio) != job.get("sha256")):
        raise RemixError("The raw cabin candidate is incomplete or its hash changed")
    source = {"id": "CABIN_SERVICE_ACTIVE_397e1ed9", "path": raw_audio, "job": raw_job,
              "sha256": job["sha256"], "model": "seed-audio-1.0",
              "seconds": wav_info(raw_audio)["seconds"], "accepted": False}
    tracks = speech + [{"stem": "environment", "sourceId": source["id"],
                        "source": source, "start": 0.0, "oldGain": .65,
                        "perspective": "room", "loop": True}]
    identity = {"scene": scene_id, "kind": "unaccepted technical audition",
                "processingVersion": 2,
                "sourceHashes": [item["source"]["sha256"] for item in tracks],
                "duration": round(duration, 6)}
    folder = WORK / "remixed/technical-preview" / (scene_id + "-" + hashlib.sha256(canonical(identity)).hexdigest()[:16])
    output = folder / "radio_mix.wav"
    review = folder / "preview.json"
    if output.is_file() and review.is_file():
        prior = read_json(review)
        if prior.get("sha256") == digest(output) and prior.get("identity") == identity:
            return output
        raise RemixError("Existing technical preview changed; inspect before rebuilding")
    if folder.exists() and any(folder.iterdir()):
        raise RemixError("Incomplete technical preview exists; inspect before rebuilding")
    folder.mkdir(parents=True, exist_ok=True)
    write_wav(output, mix_tracks({"duration": duration, "tracks": tracks}))
    result = {"accepted": False, "subjectiveListening": "not_performed",
              "technicalOnly": True, "notForUnity": True,
              "omitted": ["console", "connect", "disconnect"],
              "identity": identity, "sha256": digest(output), "qa": qa(output),
              "sources": [{"job": relative(item["source"]["job"]),
                           "sha256": item["source"]["sha256"], "model": item["source"]["model"]}
                          for item in tracks]}
    review.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return output


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("preview", "render", "publish", "pages", "technical-preview"))
    choice = parser.add_mutually_exclusive_group()
    choice.add_argument("--ids", nargs="+", help="Old scene IDs, e.g. E001 SC01 B025 HELP")
    choice.add_argument("--all", action="store_true")
    approval = parser.add_mutually_exclusive_group()
    approval.add_argument("--accept-listening", action="store_true",
                          help="Individual scene was actually heard and accepted")
    approval.add_argument("--accept-technical", action="store_true",
                          help="Provisional Unity release after source/timing QA; individual listening remains pending")
    parser.add_argument("--review", help="Truthful record of acceptance basis and remaining listening limits")
    args = parser.parse_args(argv)
    if args.action == "technical-preview":
        if args.ids or args.all:
            parser.error("technical-preview uses its fixed SC01 Seed-source sketch")
        print(technical_preview())
        return 0
    if not args.ids and not args.all:
        parser.error("select --ids or --all")
    ids = sorted(path.parent.name for path in OLD_SCENES.glob("*/mix_recipe.json")) if args.all else args.ids
    if len(ids) != len(set(ids)):
        raise RemixError("Duplicate scene IDs")
    plans = [plan_scene(scene_id) for scene_id in ids]
    if args.action == "preview":
        for plan in plans:
            print(json.dumps({"id": plan["id"], "ready": plan["ready"],
                              "missing": plan.get("missing", []),
                              "draft": str(plan.get("directory", ""))}, ensure_ascii=False))
        return 0
    if args.action == "render":
        for plan in plans:
            result = render_scene(plan)
            print(json.dumps({"id": plan["id"], "draft": relative(plan["directory"]),
                              "outputs": result["outputs"]}, ensure_ascii=False))
        listening_page(plans)
        return 0
    if args.action == "pages":
        print(listening_page(plans))
        return 0
    if not (args.accept_listening or args.accept_technical) or not args.review or len(args.review.strip()) < 8:
        raise RemixError("Publish requires an explicit acceptance basis and review note")
    for plan in plans:
        basis = "individual_listening" if args.accept_listening else "technical_provisional"
        print(json.dumps({"id": plan["id"], "published": publish_scene(plan, args.review.strip(), basis)},
                         ensure_ascii=False))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RemixError, KeyError, ValueError, wave.Error) as error:
        print("Seed remix: " + str(error), file=sys.stderr)
        raise SystemExit(2)
