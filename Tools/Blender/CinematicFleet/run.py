"""Argument-safe background runner; records real Blender exit/output and repeatability."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "ArtSource/Exports/CinematicFleet"
BLENDER = Path(r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe")


def execute(label, flags):
    OUT.mkdir(parents=True, exist_ok=True)
    command = [str(BLENDER), "--background", "--factory-startup", "--python-exit-code", "1", "--python",
               str(Path(__file__).with_name("author_layout.py")), "--", *flags]
    log = OUT / (label + ".log")
    with log.open("wb") as stream:
        result = subprocess.run(command, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT, check=False)
    print(f"{label}: exit={result.returncode}; log={log}")
    if result.returncode:
        print(log.read_text(encoding="utf-8", errors="replace")[-5000:])
        raise SystemExit(result.returncode)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=["generate", "verify", "render", "repeatability"])
    args = parser.parse_args()
    if args.phase == "repeatability":
        export = OUT / "FleetLayout_Cubic.json"
        before = hashlib.sha256(export.read_bytes()).hexdigest()
        execute("repeat-generation", ["--generate", "--verify"])
        after = hashlib.sha256(export.read_bytes()).hexdigest()
        assert before == after, "Repeated generation changed the authoritative pose export."
        report = dict(passed=True, method="Actual second background Blender generation and saved-source roundtrip.",
                      layoutBeforeSha256=before, layoutAfterSha256=after, unchanged=True)
        (OUT / "repeatability.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(report))
    else:
        execute(args.phase, ["--" + args.phase] + (["--verify"] if args.phase == "generate" else []))
