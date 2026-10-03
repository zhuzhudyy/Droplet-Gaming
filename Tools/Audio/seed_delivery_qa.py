"""Read-only QA for staged/published Seed Audio 1.0 game cues.

This report complements seed_catalog_qa.py's raw model-output check. It never
generates sound or publishes/changes the staged and Unity-facing WAVs. A file
level spectral margin is only a masking proxy; the actual Unity mix and human
listening remain separate acceptance checks.
"""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone
import html
import json
import math
import os
from pathlib import Path
import sys
import wave

import numpy as np

import seed_catalog_qa as base


ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / "ArtSource/Audio/SeedAudio20260929"
OUTPUT = WORK / "catalog-qa"
MIX_BAND_HZ = (300, 3400)
MASK_MARGIN_DB = 12.0
BED_CATEGORIES = {"music", "ambience", "flight", "cabin", "combat"}


def identity_hash(value: dict) -> str:
    import hashlib
    encoded = json.dumps(value, ensure_ascii=False, sort_keys=True,
                         separators=(",", ":"), allow_nan=False).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def midband_dbfs(path: Path) -> float:
    """Mean 300–3400 Hz RMS of three short positions, each channel separately."""
    with wave.open(str(path), "rb") as source:
        channels, width = source.getnchannels(), source.getsampwidth()
        rate, frames = source.getframerate(), source.getnframes()
        if (source.getcomptype() != "NONE" or width != 2 or channels not in (1, 2)
                or rate < 8000 or frames < 1):
            raise ValueError("Expected mono/stereo PCM16 WAV for spectral check")
        n = min(frames, round(rate * .5))
        starts = sorted({round((frames - n) * fraction) for fraction in (.15, .5, .85)})
        frequencies = np.fft.rfftfreq(n, 1 / rate)
        mask = (frequencies >= MIX_BAND_HZ[0]) & (frequencies <= MIX_BAND_HZ[1])
        if not np.any(mask):
            raise ValueError("Too few frames for midband estimate")
        weights = np.full(len(frequencies), 2.0)
        weights[0] = 1.0
        if n % 2 == 0:
            weights[-1] = 1.0
        powers = []
        for start in starts:
            source.setpos(start)
            raw = source.readframes(n)
            if len(raw) != n * channels * width:
                raise ValueError("Truncated PCM in spectral sample")
            signal = np.frombuffer(raw, dtype="<i2").astype(np.float64).reshape(n, channels) / 32768
            spectrum = np.fft.rfft(signal, axis=0)
            powers.append(float(np.mean(np.sum(np.abs(spectrum[mask]) ** 2 *
                                               weights[mask, None], axis=0) / (n * n))))
    return base.dbfs(math.sqrt(float(np.mean(powers))))


def edge_step_ratio(path: Path) -> float:
    """Loop boundary step relative to normal local sample-to-sample steps."""
    with wave.open(str(path), "rb") as source:
        frames, rate, channels = source.getnframes(), source.getframerate(), source.getnchannels()
        n = min(frames, round(rate * .1))
        first = np.frombuffer(source.readframes(n), dtype="<i2").astype(np.int32).reshape(-1, channels)
        source.setpos(frames - n)
        last = np.frombuffer(source.readframes(n), dtype="<i2").astype(np.int32).reshape(-1, channels)
    if len(first) < 2 or len(last) < 2:
        return 0.0
    boundary = float(np.max(np.abs(first[0] - last[-1])))
    local = np.concatenate((np.abs(np.diff(first, axis=0)).ravel(),
                            np.abs(np.diff(last, axis=0)).ravel()))
    return round(boundary / max(float(np.quantile(local, .99)), 1.0), 3)


def voice_reference(work: Path) -> dict | None:
    summary_path = work / "voice-qa" / "summary.json"
    if not summary_path.is_file():
        return None
    summary = base.load_json(summary_path)
    readings = []
    for line in summary.get("lines", []):
        if line.get("status") != "technically_ready_for_listening" or not line.get("audioRelative"):
            continue
        path = (summary_path.parent / line["audioRelative"]).resolve()
        if not path.is_file():
            continue
        readings.append(midband_dbfs(path))
    if not readings:
        return None
    return {"sampleCount": len(readings), "percentile25DbFS": round(float(np.percentile(readings, 25)), 3),
            "medianDbFS": round(float(np.median(readings)), 3),
            "bandHz": list(MIX_BAND_HZ),
            "note": "Dry speech-file reference; Unity bus gains, spatial attenuation and ducking are not included"}


def compare_stored_qa(stored: dict, measured: dict, label: str, errors: list[str]) -> None:
    for name in ("sampleRate", "channels", "clippedSamples"):
        if stored.get(name) != measured[name]:
            errors.append(label + " QA differs for " + name)
    for name in ("durationSeconds", "peakDbFS", "rmsDbFS", "edgeDiscontinuity"):
        if name not in stored or abs(float(stored[name]) - measured[name]) > (.001 if name != "edgeDiscontinuity" else .00001):
            errors.append(label + " QA differs for " + name)


def assess_processed(cue: dict, metrics: dict, step_ratio: float,
                     bed_band: float | None, voice_ref: dict | None) -> tuple[list[str], list[str], dict | None]:
    errors, warnings = [], []
    target = float(cue["targetSeconds"])
    duration = metrics["durationSeconds"]
    if duration < target * .75 or duration > target * 1.10 + .1:
        errors.append("Processed duration substantially misses authored target")
    elif duration < target * .90:
        warnings.append("Processed duration is more than 10% shorter than target")
    if metrics["clippedSamples"]:
        errors.append("Processed PCM clips near full scale")
    if metrics["rmsDbFS"] < -60 or metrics["silenceFraction"] > .95:
        errors.append("Processed audio is effectively silent")
    if cue["loop"]:
        if metrics["edgeDiscontinuity"] > .05 and step_ratio > 8:
            errors.append("Large loop-boundary step after processing")
        elif metrics["edgeDiscontinuity"] > .01 and step_ratio > 5:
            warnings.append("Audible-click risk at processed loop boundary")
        if abs(metrics["edgeWindowRmsRatioDb"]) > 9:
            warnings.append("First and last 250 ms have substantially different RMS")
    else:
        if metrics["edgeDiscontinuity"] > .01:
            warnings.append("One-shot begins/ends with a sizable waveform step")
        if metrics["leadingSilenceSeconds"] > max(.1, target * .2):
            warnings.append("Long leading silence remains after onset trim")
        if metrics["trailingSilenceSeconds"] > max(.25, target * .5):
            warnings.append("Long trailing silence remains after tail fade")
    mask = None
    if cue["loop"] and cue["category"] in BED_CATEGORIES and bed_band is not None and voice_ref:
        margin = voice_ref["percentile25DbFS"] - bed_band
        mask = {"bedMidbandDbFS": round(bed_band, 3),
                "dryVoiceReferenceDbFS": voice_ref["percentile25DbFS"],
                "unmixedVoiceToBedMarginDb": round(margin, 3),
                "illustrativeDuckDbFor12dBMargin": round(max(0, MASK_MARGIN_DB - margin), 3),
                "interpretation": "File-level proxy only; actual Unity bus gains and voice ducking not applied"}
        if margin < 6:
            warnings.append("File-level midband separation under 6 dB; verify comms ducking in Unity")
    return errors, warnings, mask


def inspect_delivery(work: Path, output: Path, cue: dict, voice_ref: dict | None) -> dict:
    cue_id = cue["id"]
    source_id = cue.get("sourceId", cue_id)
    result = {"id": cue_id, "category": cue["category"], "family": cue.get("family"),
              "sourceId": source_id,
              "loop": bool(cue["loop"]), "targetSeconds": cue["targetSeconds"],
              "status": "raw_not_complete", "errors": [], "warnings": [],
              "contentListening": "not_individually_performed"}
    raw = work / "raw-catalog" / source_id
    job_path, audio_path = raw / "job.json", raw / "audio.wav"
    if not job_path.is_file():
        return result
    try:
        job = base.load_json(job_path)
        if job.get("status") != "complete":
            result["status"] = "raw_in_flight_or_unresolved"
            return result
        if not audio_path.is_file():
            result["status"] = "technical_issue"
            result["errors"].append("Complete Seed job lacks raw audio.wav")
            return result
        raw_sha = base.sha256(audio_path)
        if (raw_sha != job.get("sha256") or job.get("id") != source_id or
                job.get("request", {}).get("text_prompt") != cue["text_prompt"]):
            result["errors"].append("Raw source SHA or prompt differs from completed Seed job")
        result["rawSha256"] = raw_sha
        result["rawAudioRelative"] = os.path.relpath(audio_path, output).replace("\\", "/")
        stage_root = work / "staging" / cue_id
        matching = []
        if stage_root.is_dir():
            for record_path in stage_root.glob("*/candidate.json"):
                candidate = base.load_json(record_path)
                identity = candidate.get("identity", {})
                if (identity.get("id") == cue_id and identity.get("sourceSha256") == raw_sha and
                        identity.get("targetSeconds") == float(cue["targetSeconds"]) and
                        identity.get("loop") == bool(cue["loop"])):
                    matching.append((int(identity.get("version", -1)), record_path, candidate))
        if not matching:
            result["status"] = ("stage_in_progress_or_incomplete" if stage_root.is_dir() and
                                any(stage_root.glob("*/audio.wav")) else "not_staged")
            if (work / "published" / (cue_id + ".wav")).exists() or \
                    (work / "published" / (cue_id + ".json")).exists():
                result["errors"].append("Published cue lacks a matching staged candidate")
            if result["errors"]:
                result["status"] = "technical_issue"
            return result
        matching.sort(key=lambda value: value[0], reverse=True)
        top_version = matching[0][0]
        if sum(value[0] == top_version for value in matching) > 1:
            result["errors"].append("Multiple staged candidates share current process version")
        _, record_path, candidate = matching[0]
        identity = candidate["identity"]
        if record_path.parent.name != identity_hash(identity)[:16]:
            result["errors"].append("Candidate folder does not match processing identity")
        stage_audio = record_path.parent / "audio.wav"
        if not stage_audio.is_file():
            result["errors"].append("Staged candidate lacks audio.wav")
            result["status"] = "technical_issue"
            return result
        stage_sha = base.sha256(stage_audio)
        result["stageAudioRelative"] = os.path.relpath(stage_audio, output).replace("\\", "/")
        if stage_sha != candidate.get("sha256") or candidate.get("sourceSha256") != raw_sha:
            result["errors"].append("Staged WAV SHA/source hash differs from candidate")
        if not str(candidate.get("sourceJob", "")).replace("\\", "/").endswith(
                "/raw-catalog/" + source_id + "/job.json"):
            result["errors"].append("Staged candidate source job path differs")
        metrics, _ = base.pcm_metrics(stage_audio)
        result["metrics"] = metrics
        result["stageSha256"] = stage_sha
        compare_stored_qa(candidate.get("qa", {}), metrics, "Staged", result["errors"])
        recipe = candidate.get("recipe", {})
        if recipe.get("outputFrames") != metrics["frames"] or recipe.get("rate") != metrics["sampleRate"]:
            result["errors"].append("Processing recipe frames/rate differ from staged PCM")
        raw_qa_path = raw / "qa.json"
        if not raw_qa_path.is_file() or recipe.get("sourceFrames") != base.load_json(raw_qa_path).get("frames"):
            result["errors"].append("Processing recipe source frames differ from raw Seed WAV")
        if cue["loop"] and not 1 < int(recipe.get("crossfadeFrames", 0)) < metrics["frames"]:
            result["errors"].append("Loop recipe lacks a valid circular crossfade")
        if not cue["loop"] and (recipe.get("fadeInFrames", 0) <= 0 or recipe.get("fadeOutFrames", 0) <= 0):
            result["errors"].append("One-shot recipe lacks edge fades")
        step_ratio = edge_step_ratio(stage_audio)
        result["edgeStepToLocalP99Ratio"] = step_ratio
        band = midband_dbfs(stage_audio) if cue["loop"] and cue["category"] in BED_CATEGORIES else None
        derived_errors, derived_warnings, mask = assess_processed(cue, metrics, step_ratio, band, voice_ref)
        result["errors"].extend(derived_errors)
        result["warnings"].extend(derived_warnings)
        if mask:
            result["commsMaskingProxy"] = mask
        result["status"] = "staged_ready"
        published_audio = work / "published" / (cue_id + ".wav")
        published_meta = published_audio.with_suffix(".json")
        if published_audio.is_file() and not published_meta.is_file():
            result["status"] = "publish_in_progress_or_incomplete"
        elif published_meta.is_file() and not published_audio.is_file():
            result["errors"].append("Published metadata exists without WAV")
        elif published_audio.is_file() and published_meta.is_file():
            metadata = base.load_json(published_meta)
            pub_sha = base.sha256(published_audio)
            if pub_sha != stage_sha or pub_sha != metadata.get("sha256"):
                result["errors"].append("Published WAV differs from staged candidate or sidecar")
            if (metadata.get("model") != "seed-audio-1.0" or
                    metadata.get("sourceSha256") != raw_sha or
                    metadata.get("sourcePrompt") != cue["text_prompt"] or
                    metadata.get("sourceCueId") != source_id or
                    metadata.get("processing") != recipe):
                result["errors"].append("Published source/model/processing metadata differs")
            compare_stored_qa(metadata.get("qa", {}), metrics, "Published", result["errors"])
            result["publishedAudioRelative"] = os.path.relpath(published_audio, output).replace("\\", "/")
            result["publishedSha256"] = pub_sha
            result["publishedSubjectiveListening"] = metadata.get("subjectiveListening")
            result["status"] = "published_ready"
        if result["errors"]:
            result["status"] = "technical_issue"
        return result
    except (OSError, ValueError, KeyError, TypeError, wave.Error, OverflowError) as error:
        result["status"] = "technical_issue"
        result["errors"].append("Cannot verify delivery candidate: " + str(error))
        return result


def duplicate_findings(records: list[dict], fingerprints: dict[str, np.ndarray]) -> list[dict]:
    ready = [record for record in records if record["id"] in fingerprints and
             record["status"] in ("staged_ready", "published_ready")]
    findings = []
    for i, left in enumerate(ready):
        x = fingerprints[left["id"]]
        x = x - np.mean(x)
        norm_x = float(np.linalg.norm(x))
        for right in ready[i + 1:]:
            if left["stageSha256"] == right["stageSha256"]:
                findings.append({"ids": [left["id"], right["id"]], "kind": "exact_same_wav",
                                 "crossCategory": left["category"] != right["category"]})
                continue
            a, b = left["metrics"]["durationSeconds"], right["metrics"]["durationSeconds"]
            if abs(a - b) > max(.1, min(a, b) * .01) or norm_x < 1e-5:
                continue
            y = fingerprints[right["id"]]
            y = y - np.mean(y)
            norm_y = float(np.linalg.norm(y))
            if norm_y < 1e-5:
                continue
            score = float(abs(np.dot(x, y) / (norm_x * norm_y)))
            if score >= .997:
                findings.append({"ids": [left["id"], right["id"]],
                                 "kind": "possible_gain_variant", "correlation": round(score, 5),
                                 "crossCategory": left["category"] != right["category"]})
    return findings


def raw_duplicate_findings(records: list[dict]) -> list[dict]:
    """Exact reuse can be established without a subjective similarity claim."""
    known = {}
    findings = []
    for record in records:
        digest = record.get("rawSha256")
        if not digest:
            continue
        for earlier in known.get(digest, []):
            findings.append({"ids": [earlier["id"], record["id"]],
                             "kind": "exact_same_raw_wav",
                             "crossCategory": earlier["category"] != record["category"]})
        known.setdefault(digest, []).append(record)
    return findings


def audit(work: Path, output: Path, catalog: dict) -> dict:
    cues = catalog["cues"]
    if (len(cues) != 131 or len({cue["id"] for cue in cues}) != 131 or
            len({cue.get("sourceId", cue["id"]) for cue in cues}) != 131):
        raise ValueError("Expected 131 unique catalog cues and source IDs")
    reference = voice_reference(work)
    records = [inspect_delivery(work, output, cue, reference) for cue in cues]
    fingerprints = {}
    for record in records:
        if record["status"] in ("staged_ready", "published_ready"):
            stage_path = (output / record["stageAudioRelative"]).resolve()
            _, fingerprint = base.pcm_metrics(stage_path)
            fingerprints[record["id"]] = fingerprint
    duplicates = raw_duplicate_findings(records) + duplicate_findings(records, fingerprints)
    for finding in duplicates:
        if finding["kind"] in ("exact_same_wav", "exact_same_raw_wav"):
            for record in records:
                if record["id"] in finding["ids"]:
                    record["errors"].append("Exact Seed source or staged WAV reused by another catalog cue")
                    record["status"] = "technical_issue"
    return {"schema": "droplet.seed-delivery-qa.v1",
            "scannedAtUtc": datetime.now(timezone.utc).isoformat(),
            "catalogTarget": len(cues),
            "statusCounts": dict(Counter(record["status"] for record in records)),
            "technicalIssues": sum(bool(record["errors"]) for record in records),
            "warningCount": sum(len(record["warnings"]) for record in records),
            "possibleDuplicates": duplicates,
            "voiceReference": reference,
            "maskingProxyScope": "Dry voice and processed bed files at unity gain; not Unity mixer or subjective intelligibility",
            "contentListening": "Individual catalog cues have not been accepted by human listening",
            "cues": records}


def listening_html(report: dict, catalog: dict) -> str:
    esc = html.escape
    items = {cue["id"]: cue for cue in catalog["cues"]}
    counts = report["statusCounts"]
    rows = ["<!doctype html><html lang='zh-CN'><meta charset='utf-8'>",
            "<meta name='viewport' content='width=device-width, initial-scale=1'>",
            "<title>Seed 音频成品试听与 QA</title>",
            "<style>body{font:16px system-ui,sans-serif;background:#101724;color:#eff5fc;max-width:1080px;margin:2em auto;padding:0 1em}article{border:1px solid #3d5269;border-radius:10px;padding:1em;margin:1em 0;background:#172336}audio{width:100%}small{color:#b7c8d7}input,select{font:inherit;padding:.5em;background:#26384b;color:white;border:1px solid #678}li.error{color:#ffaea7}li.warning{color:#ffda94}</style>",
            "<h1>Seed 音频成品试听</h1>",
            "<p>可对照原始模型音频与处理后暂存/发布音频。技术通过不等于内容或游戏内混音通过；六类代表已有用户听感认可，目录内每条声音未逐一试听验收。</p>",
            "<p>已发布 " + str(counts.get("published_ready", 0)) + "；已暂存 " +
            str(counts.get("staged_ready", 0)) + "；技术问题 " +
            str(counts.get("technical_issue", 0)) + "。通讯遮蔽栏是未经 Unity 音量/压低处理的文件级代理值。</p>",
            "<label>搜索 <input id='search' placeholder='编号、类别、触发事件'></label> <label>状态 <select id='status'><option value=''>全部</option><option value='published_ready'>已发布</option><option value='staged_ready'>已暂存</option><option value='technical_issue'>技术问题</option><option value='not_staged'>未暂存</option></select></label>"]
    for record in report["cues"]:
        cue = items[record["id"]]
        search = " ".join((record["id"], record["category"], str(cue.get("trigger", ""))))
        rows.append("<article data-search='" + esc(search.lower(), quote=True) +
                    "' data-status='" + esc(record["status"], quote=True) + "'>")
        rows.append("<h3>" + esc(record["id"]) +
                    (" ← " + esc(record["sourceId"]) if record["sourceId"] != record["id"] else "") +
                    " <small>" + esc(record["status"]) +
                    " · " + esc(record["category"]) + " · " + esc(str(cue.get("trigger", ""))) + "</small></h3>")
        if record.get("rawAudioRelative"):
            rows.append("<p><small>Seed 原始音频</small><audio controls preload='none' src='" +
                        esc(record["rawAudioRelative"], quote=True) + "'></audio></p>")
        processed = record.get("publishedAudioRelative") or record.get("stageAudioRelative")
        if processed:
            label = "发布成品" if record.get("publishedAudioRelative") else "处理后暂存"
            rows.append("<p><small>" + label + "</small><audio controls preload='none' src='" +
                        esc(processed, quote=True) + "'></audio></p>")
        if record.get("metrics"):
            m = record["metrics"]
            rows.append("<p><small>" + str(m["durationSeconds"]) + " 秒（目标 " +
                        str(record["targetSeconds"]) + " 秒）· 峰值 " + str(m["peakDbFS"]) +
                        " dBFS · RMS " + str(m["rmsDbFS"]) + " dBFS · 接缝跳变 " +
                        str(m["edgeDiscontinuity"]) + "</small></p>")
        if record.get("commsMaskingProxy"):
            mask = record["commsMaskingProxy"]
            rows.append("<p><small>未混音对白/背景中频余量 " +
                        str(mask["unmixedVoiceToBedMarginDb"]) + " dB；达到 12 dB 文件级余量的示意压低量 " +
                        str(mask["illustrativeDuckDbFor12dBMargin"]) + " dB。须在 Unity 中实测。</small></p>")
        if record["errors"] or record["warnings"]:
            rows.append("<ul>" + "".join("<li class='error'>" + esc(x) + "</li>" for x in record["errors"]) +
                        "".join("<li class='warning'>" + esc(x) + "</li>" for x in record["warnings"]) + "</ul>")
        rows.append("<details><summary>生成提示词</summary><p>" + esc(cue["text_prompt"]) + "</p></details></article>")
    rows.append("<script>const q=document.querySelector('#search'),s=document.querySelector('#status');function filter(){for(const e of document.querySelectorAll('article[data-status]'))e.hidden=!e.dataset.search.includes(q.value.toLowerCase())||(s.value&&e.dataset.status!==s.value)}q.oninput=s.onchange=filter</script></html>")
    return "\n".join(rows) + "\n"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, default=WORK)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args(argv)
    catalog = base.load_json(args.work / "catalog.json")
    result = audit(args.work, args.output, catalog)
    base.write_atomic(args.output / "delivery-summary.json", json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    base.write_atomic(args.output / "delivery-listening.html", listening_html(result, catalog))
    print(json.dumps({"statusCounts": result["statusCounts"],
                      "technicalIssues": result["technicalIssues"],
                      "warningCount": result["warningCount"],
                      "possibleDuplicates": len(result["possibleDuplicates"]),
                      "voiceReference": result["voiceReference"],
                      "listening": str(args.output / "delivery-listening.html")}, ensure_ascii=False))
    return 0 if result["technicalIssues"] == 0 else 2


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, TypeError, wave.Error) as error:
        print("Seed delivery QA: " + str(error), file=sys.stderr)
        raise SystemExit(2)
