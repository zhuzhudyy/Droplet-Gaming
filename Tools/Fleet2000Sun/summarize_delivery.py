"""Summarize actual Fleet2000Sun evidence without turning missing data into zero.

Run after the visible player has exited:
    python Tools/Fleet2000Sun/summarize_delivery.py

Only docs/verification/Fleet2000Sun/final-summary.json is written. --dry-run
prints that same JSON without writing. --label restricts selection to an exact
validation label. The newest completed Player report is selected even when its
checks failed; newer incomplete attempts remain visible in the selection audit.
Only Python's standard library is required.
"""
from __future__ import annotations

import argparse
import csv
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import statistics
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / "docs/verification/Fleet2000Sun"
PLAYER = EVIDENCE / "Player"
BUILD = ROOT / "Builds/Windows-Fleet2000Sun-20260908"
OUTPUT = EVIDENCE / "final-summary.json"
SCENE = "Assets/_Project/Scenes/FleetAssault_2000_Sun.unity"
MIB = 1024 * 1024


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def read_json(path: Path) -> tuple[dict[str, Any] | None, str | None]:
    if not path.is_file():
        return None, "missing"
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        if not isinstance(data, dict):
            return None, "JSON root is not an object"
        return data, None
    except (OSError, UnicodeError, ValueError) as exc:
        return None, str(exc)


def finite(value: Any) -> float | None:
    if value is None or isinstance(value, bool):
        return None
    try:
        number = float(value)
    except (ValueError, TypeError):
        return None
    return number if math.isfinite(number) else None


def utc_timestamp(value: Any) -> float | None:
    if not isinstance(value, str) or not value:
        return None
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
        if parsed.tzinfo is None:
            parsed = parsed.replace(tzinfo=timezone.utc)
        return parsed.timestamp()
    except ValueError:
        return None


def distribution(raw: Any, *, positive_mean: bool = False) -> dict[str, Any]:
    raw = raw if isinstance(raw, dict) else {}
    samples_value = finite(raw.get("samples"))
    samples = int(samples_value) if samples_value is not None and samples_value >= 0 else 0
    values = {key: finite(raw.get(key)) for key in ("mean", "p50", "p95", "p99", "maximum")}
    reason = None
    if samples == 0:
        reason = "No valid recorder/timing samples; zero-filled source statistics are not measurements."
    elif any(value is None for value in values.values()):
        reason = "Sample count exists but one or more statistics are missing or non-finite."
    elif positive_mean and values["mean"] <= 0:
        reason = "No positive timing result; zero GPU/frame timing is not a measured zero-cost frame."
    return dict(status="unmeasured" if reason else "measured", samples=samples,
        **({key: None for key in values} if reason else values), reason=reason)


def phase_summary(phase: dict[str, Any]) -> dict[str, Any]:
    frames = finite(phase.get("frames"))
    focused = finite(phase.get("focusedFrames"))
    timing = distribution(phase.get("renderedFrameMs"), positive_mean=True)
    fps = 1000.0 / timing["mean"] if timing["status"] == "measured" else None
    return dict(name=phase.get("name"), workload=phase.get("workload"),
        frames=int(frames) if frames is not None else None,
        focusedFrames=int(focused) if focused is not None else None,
        focusedFraction=focused / frames if focused is not None and frames and frames > 0 else None,
        allFramesFocused=focused == frames if focused is not None and frames and frames > 0 else None,
        seconds=finite(phase.get("seconds")), width=phase.get("width"), height=phase.get("height"),
        meanFps=fps, sourceMeanFps=finite(phase.get("meanFps")),
        renderedFrameMs=timing,
        mainThreadMs=distribution(phase.get("mainThreadMs"), positive_mean=True),
        gpuFrameMs=distribution(phase.get("gpuFrameMs"), positive_mean=True),
        drawCalls=distribution(phase.get("drawCalls")),
        setPassCalls=distribution(phase.get("setPassCalls")),
        triangles=distribution(phase.get("triangles")),
        gcBytes=distribution(phase.get("gcBytes")),
        destroyed=phase.get("destroyed"), activeEffectsPeak=phase.get("activeEffectsPeak"),
        reactorEffectsPeak=phase.get("reactorEffectsPeak"), realtimeLightsPeak=phase.get("realtimeLightsPeak"),
        particlesPeak=phase.get("particlesPeak"), probeCaptures=phase.get("probeCaptures"),
        reactorDropped=phase.get("reactorDropped"), legacyDropped=phase.get("legacyDropped"))


def select_player(label: str | None) -> tuple[Path | None, dict[str, Any] | None, list[dict[str, Any]]]:
    candidates = []
    if PLAYER.is_dir():
        for path in PLAYER.glob("*/fleet2000-validation.json"):
            data, error = read_json(path)
            if label is not None and data is not None and data.get("label") != label:
                continue
            eligible = data is not None and data.get("completed") is True
            reason = error
            if data is not None:
                if data.get("completed") is not True:
                    reason = "Player validation is incomplete."
                elif data.get("editor") is not False:
                    eligible, reason = False, "Report is not identified as a standalone Player."
                elif data.get("scene") != SCENE:
                    eligible, reason = False, "Report belongs to another scene."
            stamp = utc_timestamp(data.get("utc")) if data else None
            candidates.append((stamp if stamp is not None else path.stat().st_mtime, path, data,
                dict(path=rel(path), label=data.get("label") if data else None,
                    utc=data.get("utc") if data else None, completed=data.get("completed") if data else None,
                    allChecksPassed=data.get("allChecksPassed") if data else None,
                    eligible=eligible, reason=reason, error=data.get("error") if data else error)))
    candidates.sort(key=lambda item: (item[0], item[1].name), reverse=True)
    audit = [item[3] for item in candidates]
    selected = next((item for item in candidates if item[3]["eligible"]), None)
    return (selected[1], selected[2], audit) if selected else (None, None, audit)


def check_counts(checks: Any) -> dict[str, Any]:
    rows = checks if isinstance(checks, list) else []
    passed = sum(isinstance(row, dict) and row.get("passed") is True for row in rows)
    return dict(total=len(rows), passed=passed, nonpassing=len(rows)-passed,
        failures=[row for row in rows if not isinstance(row, dict) or row.get("passed") is not True])


def summarize_memory_rows(rows: Any, pid: int, start_utc: float | None,
                          end_utc: float | None) -> dict[str, Any]:
    dedicated, shared = [], []
    counters = dict(totalRows=0, matchingPidRows=0, positiveCounterRows=0,
        zeroCounterRows=0, invalidRows=0, outsideSelectedRunRows=0)
    timestamps = []
    for row in rows:
        counters["totalRows"] += 1
        row_pid, count = finite(row.get("pid")), finite(row.get("samples"))
        if row_pid != pid:
            continue
        counters["matchingPidRows"] += 1
        stamp = utc_timestamp(row.get("utc"))
        if (start_utc is not None or end_utc is not None) and stamp is None:
            counters["invalidRows"] += 1
            continue
        if (start_utc is not None and stamp < start_utc-2) or (end_utc is not None and stamp > end_utc+2):
            counters["outsideSelectedRunRows"] += 1
            continue
        if count is None or count <= 0:
            counters["zeroCounterRows"] += 1
            continue
        counters["positiveCounterRows"] += 1
        values = [finite(row.get("dedicatedBytes")), finite(row.get("sharedBytes"))]
        if any(value is None or value < 0 for value in values):
            counters["invalidRows"] += 1
        for value, output in zip(values, (dedicated, shared)):
            if value is not None and value >= 0:
                output.append(value / MIB)
        if stamp is not None:
            timestamps.append(stamp)

    def metric(values):
        return dict(status="measured" if values else "unmeasured", samples=len(values),
            mean=statistics.fmean(values) if values else None, maximum=max(values) if values else None)

    measured = bool(dedicated or shared)
    return dict(status="measured" if measured else "unmeasured", pid=pid, **counters,
        dedicatedMiB=metric(dedicated), sharedMiB=metric(shared),
        sampledUtcMin=datetime.fromtimestamp(min(timestamps), timezone.utc).isoformat() if timestamps else None,
        sampledUtcMax=datetime.fromtimestamp(max(timestamps), timezone.utc).isoformat() if timestamps else None,
        reason=None if measured else "No finite nonnegative memory values from samples>0 rows for the selected Player PID/run.")


def player_memory(path: Path | None, data: dict[str, Any] | None) -> dict[str, Any]:
    result = dict(status="unmeasured", pid=None, launchPath=None, csvPath=None,
        dedicatedMiB=dict(status="unmeasured", samples=0, mean=None, maximum=None),
        sharedMiB=dict(status="unmeasured", samples=0, mean=None, maximum=None),
        scope="Windows WDDM GPU Process Memory, exact selected Player PID, samples>0 only; sum across adapters. Dedicated allocation is not physical adapter capacity. Whole-run samples are not phase-specific VRAM.")
    if path is None or data is None:
        return dict(result, reason="No completed standalone Player report selected.")
    label = data.get("label")
    # Match names among actual siblings instead of interpreting a label as a path.
    siblings = {item.name: item for item in PLAYER.iterdir() if item.is_file()}
    launch_path = siblings.get(f"{label}-launch.json")
    memory_path = siblings.get(f"{label}-wddm-memory.csv")
    if launch_path is None:
        return dict(result, reason="Matching label launch receipt is missing; PID will not be guessed.")
    result["launchPath"] = rel(launch_path)
    launch, error = read_json(launch_path)
    pid_value = finite(launch.get("pid")) if launch else None
    if error or pid_value is None or pid_value <= 0 or pid_value != int(pid_value):
        return dict(result, reason=f"Missing/invalid launch PID: {error or 'pid'}")
    result["pid"] = int(pid_value)
    if memory_path is None:
        return dict(result, reason="Matching label WDDM CSV is missing.")
    result["csvPath"] = rel(memory_path)
    end_utc = path.stat().st_mtime
    # A label reused for a newer launch must not lend its PID to an older report.
    if launch_path.stat().st_mtime > end_utc+2:
        return dict(result, reason="Label launch receipt is newer than the selected completed report; PID/run match is unverified.")
    progress, _ = read_json(path.parent / "progress.json")
    if progress and progress.get("running") is False:
        progress_utc = utc_timestamp(progress.get("utc"))
        if progress_utc is not None:
            end_utc = progress_utc
    try:
        with memory_path.open(encoding="utf-8-sig", newline="") as stream:
            summary = summarize_memory_rows(csv.DictReader(stream), int(pid_value),
                utc_timestamp(data.get("utc")), end_utc)
        return dict(result, **{key: value for key, value in summary.items()})
    except (OSError, UnicodeError, csv.Error) as exc:
        return dict(result, reason=f"WDDM CSV could not be read: {exc}")


def source_inventory() -> dict[str, Any]:
    directories = ["Assets/_Project/Art/Fleet2000Sun", "Assets/_Project/Scripts/Editor/Fleet2000Sun",
        "Assets/_Project/Scripts/Runtime/Fleet2000Sun", "Tools/Blender/Fleet2000Sun", "Tools/Fleet2000Sun",
        "ArtSource/Blender/Fleet2000Sun", "ArtSource/Exports/Fleet2000Sun"]
    files = set()
    missing = []
    for name in directories:
        directory = ROOT / name
        if not directory.is_dir():
            missing.append(name)
            continue
        files.update(path for path in directory.rglob("*") if path.is_file())
        if directory.with_suffix(".meta").is_file():
            files.add(directory.with_suffix(".meta"))
    explicit = [SCENE, SCENE+".meta",
        "Assets/_Project/Tests/EditMode/Fleet2000AssetTests.cs",
        "Assets/_Project/Tests/EditMode/Fleet2000SunTests.cs",
        "Assets/_Project/Tests/PlayMode/Fleet2000SunTests.cs"]
    explicit += [name+".meta" for name in explicit if name.endswith(".cs")]
    for name in explicit:
        if (ROOT/name).is_file():
            files.add(ROOT/name)
        else:
            missing.append(name)
    entries = []
    for path in sorted(files):
        if "__pycache__" in path.parts or path.suffix in (".pyc", ".pyo", ".blend1", ".blend2"):
            continue
        if not path.resolve().is_relative_to(ROOT.resolve()):
            missing.append(rel(path)+" (outside-workspace link excluded)")
            continue
        content = path.read_bytes()
        entries.append(dict(path=rel(path), bytes=len(content), sha256=hashlib.sha256(content).hexdigest()))
    return dict(scope="New task-owned Assets/Tools/ArtSource, scene, tests and their .meta files only. Excludes Builds, Library, docs/evidence, old assets, backups and Python caches. These are authored/exported file bytes, not compressed installed-game size.",
        fileCount=len(entries), bytes=sum(item["bytes"] for item in entries), files=entries,
        missingExpectedPaths=missing,
        modifiedExistingSource="Assets/_Project/Scripts/Runtime/DropletHitDetector.cs (not counted as new resources)")


def build_inventory() -> dict[str, Any]:
    if not BUILD.is_dir():
        return dict(status="missing", directory=rel(BUILD), fileCount=None, bytes=None, executableExists=False)
    files = [path for path in BUILD.rglob("*") if path.is_file()]
    return dict(status="present", directory=rel(BUILD), fileCount=len(files),
        bytes=sum(path.stat().st_size for path in files),
        executableExists=(BUILD/"DropletPrototype.exe").is_file(),
        scope="Only this Windows-Fleet2000Sun-20260908 directory, recursively. File presence/size alone does not prove build success or that the selected Player used unchanged build bytes.")


def evidence_summary(filename: str, omit: tuple[str, ...] = ()) -> dict[str, Any]:
    path = EVIDENCE/filename
    data, error = read_json(path)
    if error:
        return dict(status="missing_or_unreadable", path=rel(path), reason=error)
    result = {key: value for key, value in data.items() if key not in omit}
    for key in omit:
        if isinstance(data.get(key), list):
            result[key+"Count"] = len(data[key])
    return dict(status="available", path=rel(path), data=result,
        scope="Copied from this evidence file. Its own passed/captured flags are authoritative; available is not a pass assertion.")


def build_summary(label: str | None = None) -> dict[str, Any]:
    path, data, candidates = select_player(label)
    player = dict(status="missing_or_incomplete", selectedReport=None, requestedLabel=label,
        reason="No completed matching standalone Player report is available.", phases=[], checks=None,
        candidates=candidates)
    if path is not None and data is not None:
        selected_index = next(i for i, item in enumerate(candidates) if item["path"] == rel(path))
        player.update(status="complete_passed" if data.get("allChecksPassed") is True else "complete_with_failures",
            selectedReport=rel(path), reason=None, label=data.get("label"), utc=data.get("utc"),
            completed=data.get("completed"), allChecksPassed=data.get("allChecksPassed"), error=data.get("error"),
            newerUnselectedAttempts=candidates[:selected_index],
            hardware={key: data.get(key) for key in ("cpu", "gpu", "graphicsApi", "operatingSystem",
                "processorCount", "graphicsMemoryMB", "systemMemoryMB")},
            settings={key: data.get(key) for key in ("unity", "scene", "width", "height", "editor",
                "developmentBuild", "batchMode", "inspectionOnly", "vSyncCount", "targetFrameRate", "totalTargets")},
            scope=data.get("scope"), checks=check_counts(data.get("checks")),
            phases=[phase_summary(phase) for phase in data.get("phases", []) if isinstance(phase, dict)],
            screenshotCount=len(data.get("screenshots", [])),
            frameCsv=rel(path.parent/"frames.csv") if (path.parent/"frames.csv").is_file() else None)
    return dict(schemaVersion=1, generatedUtc=datetime.now(timezone.utc).isoformat(),
        summaryTool=rel(Path(__file__)),
        evidenceRules="No missing sample is replaced by zero. Latest completed report is selected without filtering out failures. Incomplete/newer attempts remain listed. Per-stage means/percentiles are source measurements, not recomputed from screenshots. WDDM samples match label, launch PID and selected run time; no per-stage memory attribution is inferred.",
        player=player, playerGpuProcessMemory=player_memory(path, data),
        liveFlight=evidence_summary("live-flight.json", ("samples",)),
        frameDebugger=evidence_summary("frame-render-path.json", ("eventNames",)),
        sceneAudit=evidence_summary("scene-audit.json", ("ids",)),
        build=build_inventory(), newSourceArtifacts=source_inventory())


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--label", help="Select the latest completed Player report with this exact label.")
    parser.add_argument("--dry-run", action="store_true", help="Print the JSON without writing a file.")
    args = parser.parse_args()
    result = build_summary(args.label)
    serialized = json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False)+"\n"
    if args.dry_run:
        print(serialized, end="")
    else:
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_text(serialized, encoding="utf-8")
        print(json.dumps(dict(output=rel(OUTPUT), playerStatus=result["player"]["status"],
            playerReport=result["player"]["selectedReport"],
            memoryStatus=result["playerGpuProcessMemory"]["status"],
            buildBytes=result["build"]["bytes"], newSourceBytes=result["newSourceArtifacts"]["bytes"]), ensure_ascii=False))


if __name__ == "__main__":
    main()
