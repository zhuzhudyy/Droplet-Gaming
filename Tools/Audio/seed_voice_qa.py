"""Offline technical QA and listening index for the 87 revised Seed-TTS lines.

The immutable ledger, voice files, and authored narrative are read-only. A
passed PCM check does not establish pronunciation, performance or content QA.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
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
OUTPUT = WORK / "voice-qa"
VOICE_ROOT = ROOT / "ArtSource/Audio/Generated/Volcengine/seed-tts"
REPLACEMENTS = {"E033": "E033_R2"}


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def dbfs(value: float) -> float:
    return round(20 * math.log10(max(value, 1e-10)), 3)


def pcm_metrics(path: Path) -> dict:
    with wave.open(str(path), "rb") as source:
        channels = source.getnchannels()
        rate = source.getframerate()
        width = source.getsampwidth()
        frames = source.getnframes()
        if (source.getcomptype() != "NONE" or channels != 1 or rate != 24000 or
                width != 2 or frames < 1):
            raise ValueError("Expected nonempty mono PCM16 WAV at 24000 Hz")
        raw = source.readframes(frames)
        if len(raw) != frames * width or source.readframes(1):
            raise ValueError("Truncated or overlong PCM payload")
    x = np.frombuffer(raw, dtype="<i2").astype(np.int32)
    absolute = np.abs(x)
    peak = int(np.max(absolute)) / 32768.0
    rms = float(np.sqrt(np.mean((x.astype(np.float64) / 32768.0) ** 2)))
    active = np.flatnonzero(absolute >= 100)  # About -50 dBFS, above PCM dither.
    onset = int(active[0]) if len(active) else frames
    last = int(active[-1]) if len(active) else -1
    edge_frames = min(frames, round(rate * .02))
    return {
        "channels": channels,
        "sampleRate": rate,
        "sampleWidth": width,
        "frames": frames,
        "durationSeconds": frames / rate,
        "peakDbFS": dbfs(peak),
        "rmsDbFS": dbfs(rms),
        "clippedSamples": int(np.count_nonzero(absolute >= 32735)),
        "silenceFraction": round(float(np.count_nonzero(absolute <= 16)) / frames, 6),
        "leadingSilenceSeconds": round(onset / rate, 4),
        "trailingSilenceSeconds": round((frames - 1 - last) / rate, 4),
        "first20msPeakDbFS": dbfs(float(np.max(absolute[:edge_frames])) / 32768.0),
        "last20msPeakDbFS": dbfs(float(np.max(absolute[-edge_frames:])) / 32768.0),
        "edgeDiscontinuity": round(float(abs(x[0] - x[-1])) / 32768.0, 6),
    }


def expected_path(take: str, entry: dict) -> Path:
    return VOICE_ROOT / take / (entry["id"] + "-" + entry["requestSha256"][:16])


def inspect_line(line: dict, entry: dict | None, take: str, output: Path) -> dict:
    line_id = line["id"]
    take_id = REPLACEMENTS.get(line_id, line_id)
    record = {
        "lineId": line_id,
        "takeId": take_id,
        "group": line.get("group"),
        "stage": line.get("stage"),
        "role": line.get("role"),
        "status": "missing",
        "contentListening": "not_performed",
        "errors": [],
        "warnings": [],
    }
    if entry is None:
        record["errors"].append("No ledger entry for selected take")
        return record
    if entry.get("status") != "complete":
        record["status"] = "incomplete"
        record["errors"].append("Selected ledger entry is not complete")
        return record
    record["status"] = "technically_ready_for_listening"
    try:
        folder = expected_path(take, entry)
        job_path = folder / "job.json"
        audio_path = folder / "voice.wav"
        if not job_path.is_file() or not audio_path.is_file():
            raise ValueError("Complete ledger entry lacks job.json or voice.wav")
        job = read_json(job_path)
        request = entry.get("request", {})
        payload = request.get("payload", {})
        params = payload.get("req_params", {})
        canonical_payload = json.dumps(payload, sort_keys=True, ensure_ascii=False).encode("utf-8")
        request_sha = hashlib.sha256(canonical_payload).hexdigest()
        if request_sha != entry.get("requestSha256"):
            record["errors"].append("Request payload SHA-256 differs from ledger")
        if (request.get("provider") != "seed-tts" or
                request.get("resource_id") != "seed-tts-2.0" or
                job.get("provider") != "seed-tts" or job.get("resource_id") != "seed-tts-2.0" or
                job.get("status") != "complete" or job.get("request") != payload or
                job.get("endpoint") != request.get("endpoint")):
            record["errors"].append("Job provider, model or request differs from ledger")
        if (params.get("text") != line["english"] or
                params.get("speaker") != line["speaker"] or
                params.get("audio_params") != {"format": "pcm", "sample_rate": 24000,
                                                "speech_rate": line.get("speech_rate", 0)} or
                json.loads(params.get("additions", "{}")) != {"context_texts": line["context_texts"]}):
            record["errors"].append("Synthesized text, speaker, rate or direction differs from narrative")
        if job.get("line", {}).get("text") != line["english"]:
            record["errors"].append("Job line text differs from authored narrative")
        if job.get("line", {}).get("id") != take_id:
            record["errors"].append("Job line ID differs from selected take")
        if entry.get("job") and Path(entry["job"]).resolve() != job_path.resolve():
            record["warnings"].append("Ledger absolute job path differs from current checkout")
        digest = sha256(audio_path)
        record["sha256"] = digest
        if digest != job.get("sha256") or digest != entry.get("sha256"):
            record["errors"].append("WAV SHA-256 differs from job or ledger")
        metrics = pcm_metrics(audio_path)
        record["metrics"] = metrics
        record["audioRelative"] = os.path.relpath(audio_path, output).replace("\\", "/")
        if abs(metrics["durationSeconds"] - float(job.get("duration_seconds", -1))) > .001 or \
                abs(metrics["durationSeconds"] - float(entry.get("durationSeconds", -1))) > .001:
            record["errors"].append("Decoded WAV duration differs from job or ledger")
        if metrics["clippedSamples"]:
            record["errors"].append("PCM contains near-full-scale clipped samples")
        if metrics["peakDbFS"] < -60 or metrics["rmsDbFS"] < -65 or metrics["silenceFraction"] > .95:
            record["errors"].append("Voice is effectively silent")
        if metrics["leadingSilenceSeconds"] > 1.5:
            record["warnings"].append("Unusually long leading silence; inspect speech onset")
        if metrics["trailingSilenceSeconds"] > 1.5:
            record["warnings"].append("Unusually long trailing silence; inspect subtitle timing")
        if metrics["edgeDiscontinuity"] > .05 or metrics["first20msPeakDbFS"] > -15 or \
                metrics["last20msPeakDbFS"] > -15:
            record["warnings"].append("Strong waveform at file edge; inspect edit boundary")
        if not .25 <= metrics["durationSeconds"] <= 120:
            record["warnings"].append("Unusual speech duration")
        if record["errors"]:
            record["status"] = "technical_issue"
        return record
    except (OSError, ValueError, KeyError, TypeError, wave.Error) as error:
        record["status"] = "technical_issue"
        record["errors"].append("Cannot verify selected voice: " + str(error))
        return record


def audit(manifest: dict, ledger: dict, output: Path) -> dict:
    lines = manifest["lines"]
    entries = ledger["entries"]
    if len(lines) != 87 or len({line["id"] for line in lines}) != 87:
        raise ValueError("Expected 87 unique revised narrative lines")
    if manifest["take"] != ledger["take"]:
        raise ValueError("Narrative take differs from voice ledger")
    by_id = {entry["id"]: entry for entry in entries}
    if len(by_id) != len(entries):
        raise ValueError("Duplicate voice ledger ID")
    if set(by_id) - ({line["id"] for line in lines} | set(REPLACEMENTS.values())):
        raise ValueError("Unexpected voice ledger entries")
    original = by_id.get("E033")
    substitute = by_id.get("E033_R2")
    if original is None or original.get("status") != "submission_unknown" or substitute is None:
        raise ValueError("Expected unresolved E033 and explicit E033_R2 replacement")
    if original.get("request") != substitute.get("request"):
        raise ValueError("E033 replacement changes the intended synthesis request")
    records = [inspect_line(line, by_id.get(REPLACEMENTS.get(line["id"], line["id"])),
                            manifest["take"], output) for line in lines]
    counts = dict(Counter(record["status"] for record in records))
    opening = [record for record in records if record["group"] == "narrative"]
    stage_totals = defaultdict(float)
    for record in opening:
        if "metrics" in record:
            stage_totals[str(record["stage"])] += record["metrics"]["durationSeconds"]
    old_opening = sum(float(line.get("original", {}).get("dryDurationSeconds", 0))
                      for line in lines if line["group"] == "narrative")
    return {
        "schema": "droplet.seed-voice-qa.v1",
        "scannedAtUtc": datetime.now(timezone.utc).isoformat(),
        "take": manifest["take"],
        "expectedLines": 87,
        "selectedCompleteTakes": sum(record["status"] in
                                     ("technically_ready_for_listening", "technical_issue")
                                     and "metrics" in record for record in records),
        "technicallyReady": counts.get("technically_ready_for_listening", 0),
        "statusCounts": counts,
        "warningCount": sum(len(record["warnings"]) for record in records),
        "openingDrySeconds": round(sum(stage_totals.values()), 3),
        "openingDryMinutes": round(sum(stage_totals.values()) / 60, 3),
        "oldOpeningDrySeconds": round(old_opening, 3),
        "stageDrySeconds": {stage: round(seconds, 3) for stage, seconds in sorted(stage_totals.items())},
        "openingLineCount": len(opening),
        "replacement": {"authoredId": "E033", "selectedTake": "E033_R2",
                        "originalStatus": original["status"]},
        "subjectiveListening": "not_performed_for_87_voices",
        "interpretation": "PCM integrity only; actual opening timeline also contains gaps/transitions and requires Unity verification",
        "lines": records,
    }


def listening_html(report: dict, manifest: dict) -> str:
    line_map = {line["id"]: line for line in manifest["lines"]}
    esc = html.escape
    rows = ["<!doctype html><html lang='zh-CN'><meta charset='utf-8'>",
            "<meta name='viewport' content='width=device-width, initial-scale=1'>",
            "<title>Seed-TTS 新版对白试听</title>",
            "<style>body{font:16px system-ui,sans-serif;background:#101724;color:#eff5fc;max-width:1050px;margin:2em auto;padding:0 1em}article{border:1px solid #3d5269;border-radius:10px;padding:1em;margin:1em 0;background:#172336}audio{width:100%}small{color:#b7c8d7}input{font:inherit;padding:.5em;background:#26384b;color:white;border:1px solid #678}li.error{color:#ffaea7}li.warning{color:#ffda94}</style>",
            "<h1>Seed-TTS 新版对白试听</h1>",
            "<p>87 条对白已按剧情顺序排列。页面显示的是干声客观检查；语言准确性、表演和字幕同步尚需真人试听与 Unity 播放核对。</p>",
            "<p>技术可用 " + str(report["technicallyReady"]) + "/87；开场 40 条干声共 " +
            str(report["openingDrySeconds"]) + " 秒（" + str(report["openingDryMinutes"]) +
            " 分钟）。E033 选用 E033_R2。</p>",
            "<label>搜索 <input id='search' placeholder='编号、角色或对白'></label>"]
    current_section = None
    for record in report["lines"]:
        line = line_map[record["lineId"]]
        section = ("第 " + str(int(line["stage"]) + 1) + " 幕" if line["group"] == "narrative" else
                   "战斗通讯" if line["group"] == "combat" else "补充短句")
        if section != current_section:
            rows.append("<h2>" + esc(section) + "</h2>")
            current_section = section
        search = " ".join((record["lineId"], str(record["role"]), line["english"], line["text"]))
        rows.append("<article data-search='" + esc(search.lower(), quote=True) + "'>")
        rows.append("<h3>" + esc(record["lineId"]) +
                    (" → " + esc(record["takeId"]) if record["lineId"] != record["takeId"] else "") +
                    " <small>" + esc(record["status"]) + " · " + esc(str(record["role"])) + "</small></h3>")
        rows.append("<p>" + esc(line["english"]) + "<br><small>" + esc(line["text"]) + "</small></p>")
        if "audioRelative" in record:
            rows.append("<audio controls preload='none' src='" + esc(record["audioRelative"], quote=True) + "'></audio>")
        if "metrics" in record:
            m = record["metrics"]
            rows.append("<p><small>" + str(round(m["durationSeconds"], 3)) +
                        " 秒 · 峰值 " + str(m["peakDbFS"]) + " dBFS · 前静音 " +
                        str(m["leadingSilenceSeconds"]) + " 秒 · 后静音 " +
                        str(m["trailingSilenceSeconds"]) + " 秒</small></p>")
        if record["errors"] or record["warnings"]:
            rows.append("<ul>" + "".join("<li class='error'>" + esc(x) + "</li>" for x in record["errors"]) +
                        "".join("<li class='warning'>" + esc(x) + "</li>" for x in record["warnings"]) + "</ul>")
        rows.append("</article>")
    rows.append("<script>const q=document.querySelector('#search');q.oninput=()=>{for(const e of document.querySelectorAll('article[data-search]'))e.hidden=!e.dataset.search.includes(q.value.toLowerCase())}</script></html>")
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
    manifest = read_json(args.work / "narrative-revision.json")
    ledger = read_json(args.work / "voice-ledger.json")
    report = audit(manifest, ledger, args.output)
    write_atomic(args.output / "summary.json", json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    write_atomic(args.output / "listening.html", listening_html(report, manifest))
    print(json.dumps({"technicallyReady": report["technicallyReady"],
                      "statusCounts": report["statusCounts"],
                      "warningCount": report["warningCount"],
                      "openingDrySeconds": report["openingDrySeconds"],
                      "stageDrySeconds": report["stageDrySeconds"],
                      "listening": str(args.output / "listening.html")}, ensure_ascii=False))
    return 0 if report["technicallyReady"] == 87 else 2


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, TypeError, wave.Error) as error:
        print("Seed voice QA: " + str(error), file=sys.stderr)
        raise SystemExit(2)
