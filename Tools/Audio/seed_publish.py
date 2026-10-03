"""Seed Audio PCM post-processing: preview, versioned staging, explicit publish.

No network, cloud credential, Unity Asset, or synthesized sound source is used.
All edits are trim, level reduction, fade, and crossfade of verified Seed WAVs.
"""
from __future__ import annotations

import argparse
import hashlib
import html
import json
import math
import os
from pathlib import Path
import shutil
import sys
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "ArtSource/Audio/SeedAudio20260929"
CATALOG = WORK / "catalog.json"
REVIEW = WORK / "capability-review.json"
STAGING = WORK / "staging"
PUBLISHED = WORK / "published"
SCHEMA = "droplet.seed-audio-publish.v1"
PROCESS_VERSION = 3
LOOP_RMS_DB = {"music": -26, "ambience": -36, "flight": -24,
               "combat": -26, "cabin": -36}
ONESHOT_PEAK_DB = {"music": -7, "ambience": -12, "flight": -8,
                   "combat": -4, "laser": -4, "metal": -4,
                   "explosion": -4, "cabin": -12, "ui": -10, "mission": -7}


class PublishError(RuntimeError):
    pass


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rel(path: Path) -> str:
    return path.resolve().relative_to(ROOT.resolve()).as_posix()


def identity_hash(value: dict) -> str:
    data = json.dumps(value, ensure_ascii=False, sort_keys=True,
                      separators=(",", ":"), allow_nan=False).encode("utf-8")
    return hashlib.sha256(data).hexdigest()


def inventory() -> dict[str, dict]:
    catalog = read_json(CATALOG)
    cues = catalog["cues"]
    if len(cues) != 131 or len({cue["id"] for cue in cues}) != 131:
        raise PublishError("Expected the complete, unique 131-cue catalog")
    items = {cue["id"]: {"id": cue["id"], "sourceId": cue.get("sourceId", cue["id"]), "kind": "catalog",
                              "category": cue["category"], "targetSeconds": float(cue["targetSeconds"]),
                              "loop": bool(cue["loop"]), "prompt": cue["text_prompt"],
                              "rawDirectory": WORK / "raw-catalog" / cue.get("sourceId", cue["id"])}
             for cue in cues}
    review = read_json(REVIEW)
    representatives = review["representatives"]
    if len(representatives) != 6 or len({item["category"] for item in representatives}) != 6:
        raise PublishError("Expected six representative Seed Audio jobs")
    for entry in representatives:
        name = entry["category"]
        if name in items:
            raise PublishError("Representative and catalog IDs collide")
        items[name] = {"id": name, "sourceId": entry["id"], "kind": "representative",
                       "category": name.lower(), "targetSeconds": float(entry["seconds"]),
                       "loop": name in ("FLIGHT", "CABIN", "MUSIC"),
                       "prompt": entry["prompt"],
                       "rawDirectory": WORK / "raw" / entry["id"],
                       "reviewStatus": entry.get("status")}
    return items


def selected(items: dict, ids: list[str] | None, all_cues: bool, representatives: bool) -> list[dict]:
    if sum((bool(ids), all_cues, representatives)) != 1:
        raise PublishError("Choose --ids, --all, or --representatives")
    if all_cues:
        return [item for item in items.values() if item["kind"] == "catalog"]
    if representatives:
        return [item for item in items.values() if item["kind"] == "representative"]
    if len(set(ids)) != len(ids) or set(ids) - set(items):
        raise PublishError("Unknown or repeated cue ID")
    return [items[item_id] for item_id in ids]


def read_pcm(path: Path) -> tuple[np.ndarray, int, int]:
    with wave.open(str(path), "rb") as handle:
        channels, width = handle.getnchannels(), handle.getsampwidth()
        rate, frames = handle.getframerate(), handle.getnframes()
        if width != 2 or channels not in (1, 2) or rate < 8000 or frames < 1:
            raise PublishError("Expected nonempty mono/stereo PCM16 Seed WAV: " + rel(path))
        raw = handle.readframes(frames)
    if len(raw) != frames * channels * 2:
        raise PublishError("Truncated Seed WAV: " + rel(path))
    samples = np.frombuffer(raw, dtype="<i2").astype(np.float32).reshape(frames, channels) / 32768.0
    return samples, rate, channels


def source(item: dict) -> dict:
    folder = item["rawDirectory"]
    job_path, audio_path = folder / "job.json", folder / "audio.wav"
    if not job_path.is_file() or not audio_path.is_file():
        raise PublishError("Raw Seed result not complete: " + item["id"])
    job = read_json(job_path)
    actual_sha = sha256(audio_path)
    if (job.get("status") != "complete" or job.get("request", {}).get("model") != "seed-audio-1.0"
            or job.get("sha256") != actual_sha
            or job.get("request", {}).get("text_prompt") != item["prompt"]):
        raise PublishError("Seed request/output identity or SHA mismatch: " + item["id"])
    samples, rate, channels = read_pcm(audio_path)
    return {"job": job_path, "audio": audio_path, "sha256": actual_sha,
            "seconds": len(samples) / rate, "rate": rate, "channels": channels,
            "samples": samples}


def detect_onset(samples: np.ndarray, rate: int) -> int:
    energy = np.max(np.abs(samples), axis=1)
    peak = float(np.max(energy))
    if peak < .0003:
        raise PublishError("Seed audio is effectively silent")
    window = max(1, round(.005 * rate))
    # A short block maximum sees quiet leading silence without erasing attacks.
    blocks = np.maximum.reduceat(energy, np.arange(0, len(energy), window))
    active = np.flatnonzero(blocks >= max(.0003, peak * .025))
    if not len(active):
        raise PublishError("No identifiable sound onset")
    return max(0, int(active[0]) * window - round(.025 * rate))


def process(samples: np.ndarray, rate: int, target_seconds: float,
            loop: bool, representative: bool, category: str) -> tuple[np.ndarray, dict]:
    if not 0 < target_seconds <= 120:
        raise PublishError("Invalid target length")
    n = len(samples)
    if n == 0:
        raise PublishError("Empty source")
    recipe = {"operation": "trim/edge fades" if not loop else "trim/circular tail crossfade",
              "sourceFrames": n, "rate": rate, "targetSeconds": target_seconds,
              "representative": representative}
    if loop:
        crossfade = min(round(rate * (1.5 if target_seconds >= 40 else .35)), n // 8)
        if crossfade < 2:
            raise PublishError("Seed loop is too short for a clean seam")
        # Use as much of the model output as requested, keeping a full tail
        # overlap. The result starts at the original tail and ends adjacent to
        # that same tail: repeated playback does not click at the sample edge.
        length = min(n, round(target_seconds * rate) + crossfade)
        if length <= crossfade * 2:
            raise PublishError("Seed loop has insufficient material")
        raw = samples[:length]
        out = raw[:-crossfade].copy()
        blend = np.linspace(0, 1, crossfade, dtype=np.float32)[:, None]
        out[:crossfade] = raw[-crossfade:] * (1 - blend) + raw[:crossfade] * blend
        recipe.update(startFrame=0, usedSourceFrames=length,
                      crossfadeFrames=crossfade, outputFrames=len(out))
    else:
        onset = 0 if representative else detect_onset(samples, rate)
        wanted = max(1, round(target_seconds * rate))
        start = min(onset, max(0, n - 1)) if n > wanted else 0
        out = samples[start:min(n, start + wanted)].copy()
        head = min(len(out) // 2, round(.005 * rate))
        tail = min(len(out) // 2, round(min(.08, max(.012, target_seconds * .08)) * rate))
        if head:
            out[:head] *= np.linspace(0, 1, head, dtype=np.float32)[:, None]
        if tail:
            out[-tail:] *= np.linspace(1, 0, tail, dtype=np.float32)[:, None]
        recipe.update(startFrame=start, usedSourceFrames=len(out),
                      fadeInFrames=head, fadeOutFrames=tail, outputFrames=len(out))
    peak = float(np.max(np.abs(out)))
    rms = float(np.sqrt(np.mean(out * out)))
    if rms < 1e-8 or peak < 1e-8:
        raise PublishError("Processed Seed audio became silent")
    if loop:
        target_rms = 10 ** (LOOP_RMS_DB.get(category, -30) / 20)
        gain = min(8.0, target_rms / rms, .90 / peak)
    else:
        target_peak = 10 ** (ONESHOT_PEAK_DB.get(category, -8) / 20)
        gain = min(8.0, target_peak / peak, .90 / peak)
    out *= gain
    recipe["linearGain"] = gain
    recipe["targetLevelDbFS"] = (LOOP_RMS_DB.get(category, -30) if loop
                                 else ONESHOT_PEAK_DB.get(category, -8))
    return out.astype(np.float32), recipe


def write_wav(path: Path, samples: np.ndarray, rate: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".wav.tmp")
    with wave.open(str(temporary), "wb") as handle:
        handle.setnchannels(samples.shape[1])
        handle.setsampwidth(2)
        handle.setframerate(rate)
        handle.writeframes((np.clip(samples, -.999, .999) * 32767).astype("<i2").tobytes())
    os.replace(temporary, path)


def qa(path: Path) -> dict:
    x, rate, channels = read_pcm(path)
    peak = float(np.max(np.abs(x)))
    rms = float(np.sqrt(np.mean(x * x)))
    return {"sha256": sha256(path), "durationSeconds": len(x) / rate,
            "sampleRate": rate, "channels": channels,
            "peakDbFS": 20 * math.log10(max(peak, 1e-10)),
            "rmsDbFS": 20 * math.log10(max(rms, 1e-10)),
            "clippedSamples": int(np.sum(np.abs(x) >= .999)),
            "edgeDiscontinuity": float(np.max(np.abs(x[0] - x[-1])))}


def proposal(item: dict) -> dict:
    raw = source(item)
    identity = {"schema": SCHEMA, "version": PROCESS_VERSION, "id": item["id"],
                "sourceJob": rel(raw["job"]), "sourceSha256": raw["sha256"],
                "targetSeconds": item["targetSeconds"], "loop": item["loop"],
                "kind": item["kind"]}
    folder = STAGING / item["id"] / identity_hash(identity)[:16]
    return {"item": item, "source": raw, "identity": identity,
            "folder": folder, "output": folder / "audio.wav",
            "record": folder / "candidate.json"}


def stage(item: dict) -> dict:
    plan = proposal(item)
    folder, output, record = plan["folder"], plan["output"], plan["record"]
    if record.is_file() and output.is_file():
        cached = read_json(record)
        if (cached.get("identity") == plan["identity"] and
                cached.get("sha256") == sha256(output) and
                cached.get("qa") == qa(output)):
            return cached
        raise PublishError("Staged candidate changed; inspect before rebuilding: " + item["id"])
    if folder.exists() and any(folder.iterdir()):
        raise PublishError("Incomplete staged candidate exists; inspect before rebuilding: " + item["id"])
    processed, recipe = process(plan["source"]["samples"], plan["source"]["rate"],
                                item["targetSeconds"], item["loop"],
                                item["kind"] == "representative", item["category"])
    folder.mkdir(parents=True, exist_ok=True)
    write_wav(output, processed, plan["source"]["rate"])
    metrics = qa(output)
    if metrics["clippedSamples"] or metrics["rmsDbFS"] < -60:
        raise PublishError("Staged candidate failed technical check: " + item["id"])
    result = {"id": item["id"], "identity": plan["identity"],
              "sourceJob": plan["identity"]["sourceJob"],
              "sourceSha256": plan["identity"]["sourceSha256"],
              "sha256": metrics["sha256"], "recipe": recipe, "qa": metrics,
              "accepted": False, "subjectiveListening": "not_performed"}
    record.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return result


def publish(items: list[dict], review_note: str) -> list[str]:
    candidates = []
    review = read_json(REVIEW)
    status = {entry["category"]: entry.get("status") for entry in review["representatives"]}
    for item in items:
        if item["kind"] == "representative" and status[item["id"]] != "accepted_after_listening":
            raise PublishError("Representative still awaits listening acceptance: " + item["id"])
        plan = proposal(item)
        if not plan["record"].is_file() or not plan["output"].is_file():
            raise PublishError("Stage and listen to cue before publishing: " + item["id"])
        candidate = read_json(plan["record"])
        if (candidate.get("identity") != plan["identity"] or
                candidate.get("sha256") != sha256(plan["output"]) or
                candidate.get("qa") != qa(plan["output"])):
            raise PublishError("Staged candidate identity/hash mismatch: " + item["id"])
        if candidate["qa"].get("clippedSamples") != 0:
            raise PublishError("Cannot publish clipped cue: " + item["id"])
        target = PUBLISHED / (item["id"] + ".wav")
        sidecar = target.with_suffix(".json")
        metadata = {"id": item["id"], "accepted": True, "model": "seed-audio-1.0",
                    "sha256": candidate["sha256"],
                    "sourceJob": candidate["sourceJob"],
                    "sourceSha256": candidate["sourceSha256"],
                    "sourcePrompt": item["prompt"], "sourceCueId": item["sourceId"],
                    "processing": candidate["recipe"], "qa": candidate["qa"],
                    "review": review_note,
                    "acceptanceBasis": ("user_listened_representative" if item["kind"] == "representative"
                                        else "user_accepted_six_categories_and_technical_qa"),
                    "subjectiveListening": ("accepted" if item["kind"] == "representative"
                                            else "not_individually_performed")}
        if target.exists() or sidecar.exists():
            if not (target.is_file() and sidecar.is_file() and
                    sha256(target) == metadata["sha256"] and read_json(sidecar) == metadata):
                raise PublishError("Existing published cue differs; do not overwrite: " + item["id"])
        candidates.append((plan["output"], target, sidecar, metadata))
    # All candidates and collisions are checked before writing any published file.
    PUBLISHED.mkdir(parents=True, exist_ok=True)
    paths = []
    for original, target, sidecar, metadata in candidates:
        if not target.exists():
            temporary = target.with_suffix(".wav.tmp")
            shutil.copyfile(original, temporary)
            os.replace(temporary, target)
            sidecar.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        paths.append(rel(target))
    return paths


def listening_page(items: list[dict]) -> Path:
    rows = ["<!doctype html><html lang='zh-CN'><meta charset='utf-8'><title>Seed 音效暂存试听</title>",
            "<style>body{font:16px system-ui;background:#101725;color:#eef2f5;max-width:920px;margin:2em auto}article{padding:1em;border:1px solid #455269;margin:1em 0}audio{width:100%}small{color:#9ac}</style>",
            "<h1>Seed 音效暂存试听</h1><p>此页仅列已生成候选；技术处理不代表听感或内容验收。</p>"]
    for item in items:
        try:
            plan = proposal(item)
        except PublishError:
            continue
        if not plan["record"].is_file() or not plan["output"].is_file():
            continue
        record = read_json(plan["record"])
        if record.get("sha256") != sha256(plan["output"]):
            continue
        url = item["id"] + "/" + plan["folder"].name + "/audio.wav"
        rows.append("<article><h2>" + html.escape(item["id"]) + "</h2><p><small>" +
                    html.escape(item["category"]) + " · " +
                    str(round(record["qa"]["durationSeconds"], 2)) + " s · 未验收</small></p>" +
                    "<audio controls preload='none' src='" + html.escape(url) + "'></audio></article>")
    rows.append("</html>")
    STAGING.mkdir(parents=True, exist_ok=True)
    path = STAGING / "listening.html"
    path.write_text("\n".join(rows), encoding="utf-8")
    return path


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("preview", "stage", "publish", "pages"))
    select = parser.add_mutually_exclusive_group(required=True)
    select.add_argument("--ids", nargs="+")
    select.add_argument("--all", action="store_true", help="All 131 catalog cues")
    select.add_argument("--representatives", action="store_true", help="Six live service samples")
    parser.add_argument("--accept", action="store_true", help="Explicitly release reviewed candidates for Unity")
    parser.add_argument("--review", help="Truthful acceptance basis and any listening limits, at least 8 characters")
    args = parser.parse_args(argv)
    items = selected(inventory(), args.ids, args.all, args.representatives)
    if args.action == "preview":
        for item in items:
            try:
                plan = proposal(item)
                status = {"id": item["id"], "ready": True, "source": rel(plan["source"]["audio"]),
                          "sourceSeconds": plan["source"]["seconds"], "targetSeconds": item["targetSeconds"],
                          "loop": item["loop"], "staging": rel(plan["folder"])}
            except (PublishError, KeyError, wave.Error) as error:
                status = {"id": item["id"], "ready": False, "reason": str(error)}
            print(json.dumps(status, ensure_ascii=False))
        return 0
    if args.action == "stage":
        for item in items:
            result = stage(item)
            print(json.dumps({"id": item["id"], "staged": True, "qa": result["qa"]}, ensure_ascii=False))
        listening_page(items)
        return 0
    if args.action == "pages":
        print(listening_page(items))
        return 0
    if not args.accept or not args.review or len(args.review.strip()) < 8:
        raise PublishError("Publish requires explicit --accept and a review note")
    print(json.dumps({"published": publish(items, args.review.strip())}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (PublishError, OSError, KeyError, ValueError, wave.Error) as error:
        print("Seed publish: " + str(error), file=sys.stderr)
        raise SystemExit(2)
