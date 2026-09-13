"""Run two independent marker exports/reopens and compare semantic receipts."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[3]
BLENDER = Path("C:/Program Files/Blender Foundation/Blender 5.2/blender.exe")
EVIDENCE = ROOT / "docs/verification/FleetExpansion"
EXPORT = ROOT / "ArtSource/Exports/FleetExpansion"
CONFIG = Path(__file__).with_name("layout_config.json")
config_before = CONFIG.read_bytes()
records = []
for run in (1, 2):
    for script in ("author_layout.py", "inspect_layout.py"):
        command = [str(BLENDER), "--background", "--factory-startup", "--python-exit-code", "1", "--python", str(Path(__file__).with_name(script))]
        if script == "author_layout.py":
            command += ["--", "--export"]
        result = subprocess.run(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        (EVIDENCE / f"layout-repeat{run}-{script[:-3]}.log").write_bytes(result.stdout)
        if result.returncode:
            raise RuntimeError(f"Blender {script} run {run} failed; inspect evidence log.")
    manifest = json.loads((EXPORT / "FleetLayout_Expanded_manifest.json").read_text(encoding="utf-8"))
    inspection = json.loads((EVIDENCE / "layout-source-roundtrip.json").read_text(encoding="utf-8"))
    records.append(dict(run=run, configSha256=manifest["configSha256"], markers=manifest["markers"],
                        measurements=manifest["measurements"], resourceCounts=manifest["resourceCounts"], inspection=inspection))
assert config_before == CONFIG.read_bytes(), "Export must never mutate authority JSON."
for key in ("configSha256", "markers", "measurements", "resourceCounts", "inspection"):
    assert records[0][key] == records[1][key], f"Non-deterministic {key}"
report = dict(passed=True, independentRuns=2, configUnchanged=True,
              configSha256=hashlib.sha256(config_before).hexdigest(), markerCount=len(records[0]["markers"]),
              stableIdsIdentical=True, positionsOrientationsScalesIdentical=True, measurementsIdentical=True,
              resourceCountsIdentical=True, sourceReopenAndFbxRoundtripPassedBothRuns=True,
              resourceCounts=records[0]["resourceCounts"],
              note="Semantic source/FBX poses and resources compared. Binary Blender/FBX files may include session metadata; byte identity is not required.")
(EVIDENCE / "layout-repeatability.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps(report, indent=2))
