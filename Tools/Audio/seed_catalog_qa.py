"""Offline, repeatable technical audit and listening index for Seed Audio cues.

The source catalog and raw model results are read-only. This tool cannot call
the provider or accept content on behalf of a listener. Run it at any point in
a batch; an unknown submission remains 'in flight or unresolved' until the
generating process is checked separately.
"""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import html
import json
import math
import os
from pathlib import Path
import sys
import wave

import numpy as np


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "ArtSource/Audio/SeedAudio20260929"
OUTPUT = WORK / "catalog-qa"
SAMPLE_COUNT = 2048


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def dbfs(amplitude: float) -> float:
    return round(20 * math.log10(max(amplitude, 1e-10)), 3)


def pcm_metrics(path: Path) -> tuple[dict, np.ndarray]:
    """Read PCM in bounded chunks; retain only seam and duplicate samples."""
    with wave.open(str(path), "rb") as source:
        channels = source.getnchannels()
        width = source.getsampwidth()
        rate = source.getframerate()
        frames = source.getnframes()
        if (source.getcomptype() != "NONE" or width != 2 or
                channels not in (1, 2) or rate != 48000 or frames < 1):
            raise ValueError("Expected nonempty mono/stereo PCM16 at 48000 Hz")
        selections = np.linspace(0, frames - 1, SAMPLE_COUNT).astype(np.int64)
        fingerprint = np.zeros(SAMPLE_COUNT, dtype=np.float32)
        head = np.empty((0, channels), dtype=np.float32)
        tail = np.empty((0, channels), dtype=np.float32)
        seam_frames = min(frames, round(rate * .25))
        peak = 0
        sum_squares = 0.0
        sample_count = 0
        silence_frames = 0
        legacy_silent_samples = 0
        clipped_samples = 0
        first_active = None
        last_active = None
        offset = 0
        while offset < frames:
            count = min(rate, frames - offset)
            raw = source.readframes(count)
            if len(raw) != count * channels * width:
                raise ValueError("Truncated PCM data")
            pcm = np.frombuffer(raw, dtype="<i2").reshape(count, channels)
            values = pcm.astype(np.float32) / 32768.0
            peak = max(peak, int(np.max(np.abs(pcm.astype(np.int32)))))
            sum_squares += float(np.sum(values.astype(np.float64) ** 2))
            sample_count += values.size
            silence_frames += int(np.count_nonzero(np.max(np.abs(pcm.astype(np.int32)), axis=1) <= 16))
            legacy_silent_samples += int(np.count_nonzero(np.abs(pcm.astype(np.int32)) <= 3))
            clipped_samples += int(np.count_nonzero(np.abs(pcm.astype(np.int32)) >= 32735))
            active = np.flatnonzero(np.max(np.abs(pcm.astype(np.int32)), axis=1) >= 100)
            if len(active):
                if first_active is None:
                    first_active = offset + int(active[0])
                last_active = offset + int(active[-1])
            if offset < seam_frames:
                head = np.concatenate((head, values[:max(0, min(count, seam_frames - offset))]), axis=0)
            if offset + count >= frames - seam_frames:
                tail = np.concatenate((tail, values[max(0, frames - seam_frames - offset):]), axis=0)
            chosen = (selections >= offset) & (selections < offset + count)
            if np.any(chosen):
                fingerprint[chosen] = np.mean(values[selections[chosen] - offset], axis=1)
            offset += count
        if source.readframes(1):
            raise ValueError("PCM has more frames than declared")
    rms = math.sqrt(sum_squares / max(1, sample_count))
    result = {
        "sampleRate": rate,
        "channels": channels,
        "sampleWidth": width,
        "frames": frames,
        "durationSeconds": round(frames / rate, 5),
        "peakDbFS": dbfs(peak / 32768.0),
        "rmsDbFS": dbfs(rms),
        "clippedSamples": clipped_samples,
        "silenceFraction": round(silence_frames / frames, 6),
        "originalQaSilenceFraction": round(legacy_silent_samples / sample_count, 6),
        "leadingSilenceSeconds": round((first_active if first_active is not None else frames) / rate, 5),
        "trailingSilenceSeconds": round((frames - 1 - last_active if last_active is not None else frames) / rate, 5),
        "edgeDiscontinuity": round(float(np.max(np.abs(head[0] - tail[-1]))), 6),
        "first20msPeakDbFS": dbfs(float(np.max(np.abs(head[:min(len(head), round(rate * .02))])))),
        "last20msPeakDbFS": dbfs(float(np.max(np.abs(tail[-min(len(tail), round(rate * .02)):])))),
        "edgeWindowRmsRatioDb": round(
            20 * math.log10(max(float(np.sqrt(np.mean(head * head))), 1e-10) /
                            max(float(np.sqrt(np.mean(tail * tail))), 1e-10)), 3),
    }
    return result, fingerprint


def inspect_cue(work: Path, cue: dict) -> tuple[dict, np.ndarray | None]:
    cue_id = cue["id"]
    source_id = cue.get("sourceId", cue_id)
    folder = work / "raw-catalog" / source_id
    record = {
        "id": cue_id,
        "sourceId": source_id,
        "category": cue["category"],
        "family": cue.get("family", cue_id.split("_")[0]),
        "trigger": cue.get("trigger", ""),
        "targetSeconds": cue["targetSeconds"],
        "loop": bool(cue["loop"]),
        "status": "not_generated",
        "contentListening": "not_performed",
        "warnings": [],
        "errors": [],
    }
    job_path = folder / "job.json"
    if not job_path.is_file():
        if folder.exists():
            record["status"] = "incomplete_directory"
            record["errors"].append("Directory exists without a job record")
        return record, None
    try:
        job = load_json(job_path)
    except (ValueError, OSError) as error:
        record["status"] = "technical_issue"
        record["errors"].append("Unreadable job record: " + str(error))
        return record, None
    if job.get("status") != "complete":
        record["status"] = ("in_flight_or_unresolved" if job.get("status") == "submission_unknown"
                            else "failed_or_incomplete")
        record["jobStatus"] = job.get("status")
        return record, None
    record["status"] = "ready_for_listening"
    record["audioRelative"] = "../raw-catalog/" + source_id + "/audio.wav"
    expected_request = {
        "model": "seed-audio-1.0",
        "text_prompt": cue["text_prompt"],
        "audio_config": {"format": "wav", "sample_rate": 48000},
    }
    if job.get("id") != source_id or job.get("model") != "seed-audio-1.0" or job.get("request") != expected_request:
        record["errors"].append("Job ID, model or request differs from authored catalog")
    audio_path = folder / "audio.wav"
    qa_path = folder / "qa.json"
    if not audio_path.is_file() or not qa_path.is_file():
        record["errors"].append("Complete job lacks audio.wav or qa.json")
        record["status"] = "technical_issue"
        return record, None
    try:
        actual_sha = sha256(audio_path)
        record["sha256"] = actual_sha
        if job.get("sha256") != actual_sha:
            record["errors"].append("WAV SHA-256 differs from completed job")
        stored_qa = load_json(qa_path)
        if stored_qa.get("sha256") != actual_sha:
            record["errors"].append("WAV SHA-256 differs from original QA record")
        metrics, fingerprint = pcm_metrics(audio_path)
        record["metrics"] = metrics
        for field in ("sampleRate", "channels", "sampleWidth", "frames", "clippedSamples"):
            if stored_qa.get(field) != metrics[field]:
                record["errors"].append("Original QA field differs: " + field)
        for field in ("peakDbFS", "rmsDbFS"):
            if field not in stored_qa or abs(float(stored_qa[field]) - metrics[field]) > .002:
                record["errors"].append("Original QA field differs: " + field)
        if ("silenceFraction" not in stored_qa or
                abs(float(stored_qa["silenceFraction"]) - metrics["originalQaSilenceFraction"]) > .00001):
            record["errors"].append("Original QA field differs: silenceFraction")
        if abs(float(stored_qa.get("durationSeconds", -1)) - metrics["durationSeconds"]) > .001:
            record["errors"].append("Original QA duration differs from decoded PCM")
        if abs(float(job.get("actualSeconds", -1)) - metrics["durationSeconds"]) > .001:
            record["errors"].append("Job duration differs from decoded PCM")
        if not 0 < float(job.get("originalDurationSeconds", -1)) <= 120:
            record["errors"].append("Provider duration is missing or invalid")
        if metrics["clippedSamples"]:
            record["errors"].append("PCM contains near-full-scale clipped samples")
        if metrics["peakDbFS"] < -60 or metrics["rmsDbFS"] < -65:
            record["errors"].append("Audio is effectively silent")
        if metrics["silenceFraction"] > .95:
            record["warnings"].append("More than 95% of frames are near silent")
        target = float(cue["targetSeconds"])
        if abs(metrics["durationSeconds"] - target) > max(2, target * .25):
            record["warnings"].append("Model duration differs substantially from cue target")
        if cue["loop"] and metrics["durationSeconds"] >= 5:
            if metrics["edgeDiscontinuity"] > .05 or abs(metrics["edgeWindowRmsRatioDb"]) > 9:
                record["warnings"].append(
                    "Raw loop endpoints differ; inspect seam after crossfade processing")
        if record["errors"]:
            record["status"] = "technical_issue"
        return record, fingerprint
    except (ValueError, OSError, wave.Error, OverflowError) as error:
        record["status"] = "technical_issue"
        record["errors"].append("Cannot verify complete WAV/QA: " + str(error))
        return record, None


def duplicate_findings(records: list[dict], fingerprints: dict[str, np.ndarray]) -> list[dict]:
    """High-confidence gain-invariant waveform comparison within cue families."""
    candidates = [record for record in records if record["status"] == "ready_for_listening"
                  and record["id"] in fingerprints]
    findings = []
    for i, left in enumerate(candidates):
        x = fingerprints[left["id"]]
        x = x - np.mean(x)
        x_norm = float(np.linalg.norm(x))
        if x_norm < 1e-5:
            continue
        for right in candidates[i + 1:]:
            if left["family"] != right["family"] or left["category"] != right["category"]:
                continue
            a = left["metrics"]["durationSeconds"]
            b = right["metrics"]["durationSeconds"]
            if abs(a - b) > max(.1, min(a, b) * .01):
                continue
            if left["sha256"] == right["sha256"]:
                findings.append({"ids": [left["id"], right["id"]], "type": "identical_file"})
                continue
            y = fingerprints[right["id"]]
            y = y - np.mean(y)
            y_norm = float(np.linalg.norm(y))
            if y_norm < 1e-5:
                continue
            similarity = float(abs(np.dot(x, y) / (x_norm * y_norm)))
            if similarity >= .997:
                findings.append({"ids": [left["id"], right["id"]],
                                 "type": "possible_gain_variant", "waveformCorrelation": round(similarity, 5)})
    return findings


def audit(work: Path, catalog: dict, review: dict) -> dict:
    cues = catalog["cues"]
    if (len(cues) != catalog.get("nonSpeechTarget") or
            len({cue["id"] for cue in cues}) != len(cues) or
            len({cue.get("sourceId", cue["id"]) for cue in cues}) != len(cues)):
        raise ValueError("Catalog target count, cue IDs or Seed source IDs are not unique")
    records = []
    fingerprints = {}
    for cue in cues:
        record, fingerprint = inspect_cue(work, cue)
        records.append(record)
        if fingerprint is not None:
            fingerprints[record["id"]] = fingerprint
    accepted = [entry for entry in review.get("representatives", [])
                if entry.get("status") == "accepted_after_listening"]
    counts = Counter(record["status"] for record in records)
    categories = {category: dict(Counter(record["status"] for record in records
                                         if record["category"] == category))
                  for category in sorted({record["category"] for record in records})}
    return {
        "schema": "droplet.seed-catalog-qa.v1",
        "scannedAtUtc": datetime.now(timezone.utc).isoformat(),
        "catalogTarget": len(cues),
        "statusCounts": dict(counts),
        "categoryStatusCounts": categories,
        "technicalIssues": sum(bool(record["errors"]) for record in records),
        "possibleDuplicates": duplicate_findings(records, fingerprints),
        "representativesAcceptedByUser": len(accepted) if review.get("subjectiveListening") == "accepted" else 0,
        "contentListening": "Only the six representatives have user acceptance; catalog cues await listening",
        "unknownSubmissionNote": "Check the batch process before treating in_flight_or_unresolved as a failed request; never replay automatically",
        "cues": records,
    }


def listening_html(summary: dict, catalog: dict, review: dict) -> str:
    catalog_by_id = {cue["id"]: cue for cue in catalog["cues"]}
    esc = html.escape
    counts = summary["statusCounts"]
    rows = ["<!doctype html><html lang='zh-CN'><meta charset='utf-8'>",
            "<meta name='viewport' content='width=device-width, initial-scale=1'>",
            "<title>Seed Audio 批次试听与技术检查</title>",
            "<style>body{font:16px system-ui,sans-serif;background:#101724;color:#eff5fc;max-width:1080px;margin:2em auto;padding:0 1em}a{color:#97d7ff}article{border:1px solid #3d5269;border-radius:10px;padding:1em;margin:1em 0;background:#172336}audio{width:100%}small,summary{color:#b7c8d7}.tag{padding:.15em .5em;border-radius:5px;background:#32445a}input,select{font:inherit;padding:.5em;background:#26384b;color:white;border:1px solid #678}code{overflow-wrap:anywhere}li.error{color:#ffaea7}li.warning{color:#ffda94}</style>",
            "<h1>Seed Audio 批次试听</h1>",
            "<p>这是原始生成音频的客观检查。只有下方六类代表样音获得用户听感认可；目录内其余声音仍需逐一试听，技术检查不代表内容验收。</p>",
            "<p>目录目标：" + str(summary["catalogTarget"]) + "；可试听：" + str(counts.get("ready_for_listening", 0)) +
            "；技术问题：" + str(counts.get("technical_issue", 0)) +
            "；生成中或状态待核：" + str(counts.get("in_flight_or_unresolved", 0)) + ".</p>",
            "<label>搜索 <input id='search' placeholder='名称、类别或触发事件'></label> <label>状态 <select id='status'><option value=''>全部</option><option value='ready_for_listening'>可试听</option><option value='technical_issue'>技术问题</option><option value='in_flight_or_unresolved'>生成中或待核</option><option value='not_generated'>未生成</option></select></label>",
            "<h2>批次目录</h2>"]
    for record in summary["cues"]:
        cue = catalog_by_id[record["id"]]
        status = record["status"]
        search = " ".join((record["id"], record["category"], record["trigger"]))
        rows.append("<article data-status='" + esc(status, quote=True) + "' data-search='" + esc(search.lower(), quote=True) + "'>")
        rows.append("<h3>" + esc(record["id"]) +
                    (" ← " + esc(record["sourceId"]) if record["sourceId"] != record["id"] else "") +
                    " <small class='tag'>" + esc(status) + "</small></h3>")
        rows.append("<p><small>" + esc(record["category"]) + " · " + esc(record["trigger"]) +
                    " · 目标 " + str(record["targetSeconds"]) + " 秒" +
                    (" · 循环" if record["loop"] else "") + "</small></p>")
        if "audioRelative" in record:
            rows.append("<audio controls preload='none' src='" + esc(record["audioRelative"], quote=True) + "'></audio>")
        if "metrics" in record:
            m = record["metrics"]
            rows.append("<p><small>实际 " + str(m["durationSeconds"]) + " 秒 · " +
                        str(m["channels"]) + " 声道 · 峰值 " + str(m["peakDbFS"]) +
                        " dBFS · 削波样本 " + str(m["clippedSamples"]) + "</small></p>")
        if record["errors"] or record["warnings"]:
            rows.append("<ul>" + "".join("<li class='error'>" + esc(x) + "</li>" for x in record["errors"]) +
                        "".join("<li class='warning'>" + esc(x) + "</li>" for x in record["warnings"]) + "</ul>")
        rows.append("<details><summary>生成提示词</summary><p>" + esc(cue["text_prompt"]) + "</p></details></article>")
    rows.append("<h2>用户已认可的六类代表样音</h2>")
    for entry in review.get("representatives", []):
        if entry.get("status") != "accepted_after_listening":
            continue
        path = "../" + entry["relativeAudio"]
        rows.append("<article><h3>" + esc(entry["label"]) + " <small class='tag'>用户听感通过</small></h3>" +
                    "<audio controls preload='none' src='" + esc(path, quote=True) + "'></audio></article>")
    rows.append("<script>const q=document.querySelector('#search'),s=document.querySelector('#status');function filter(){for(const e of document.querySelectorAll('article[data-status]'))e.hidden=!e.dataset.search.includes(q.value.toLowerCase())||(s.value&&e.dataset.status!==s.value)}q.oninput=s.onchange=filter</script></html>")
    return "\n".join(rows) + "\n"


def write_atomic(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(content, encoding="utf-8")
    os.replace(temporary, path)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, default=WORK)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args(argv)
    catalog = load_json(args.work / "catalog.json")
    review = load_json(args.work / "capability-review.json")
    summary = audit(args.work, catalog, review)
    write_atomic(args.output / "summary.json", json.dumps(summary, ensure_ascii=False, indent=2) + "\n")
    write_atomic(args.output / "listening.html", listening_html(summary, catalog, review))
    print(json.dumps({"statusCounts": summary["statusCounts"],
                      "technicalIssues": summary["technicalIssues"],
                      "possibleDuplicates": len(summary["possibleDuplicates"]),
                      "listening": str(args.output / "listening.html")}, ensure_ascii=False))
    return 0 if summary["technicalIssues"] == 0 else 2


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, wave.Error) as error:
        print("Seed catalog QA: " + str(error), file=sys.stderr)
        raise SystemExit(2)
