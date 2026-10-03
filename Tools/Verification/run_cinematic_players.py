"""Run the rendered, full-fleet local player sequentially; no cloud or asset authoring.

The opt-in C# runner uses ordinary Input System keys for flight and separately
labels direct-damage stress. Neither automated audio meters nor screenshots
establish subjective listening. Each invocation requires a fresh evidence folder.
"""
import argparse
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("output", type=Path)
    parser.add_argument("--speed", type=float, default=100)
    parser.add_argument("--skip-opening", action="store_true")
    args = parser.parse_args()
    player = ROOT / "Builds/Windows-CinematicAudio-20260919/DropletGaming.exe"
    if not player.is_file():
        parser.error("Build the cinematic Windows player first.")
    destination = args.output.resolve()
    destination.mkdir(parents=True, exist_ok=True)
    runs = [("PlayerFinal", "-cinematic-validation", ["-cinematic-slow-route", "-cinematic-route-speed", str(args.speed)])]
    if not args.skip_opening:
        runs.append(("Opening", "-cinematic-opening", []))
    if any((destination / name).exists() for name, _, _ in runs):
        parser.error("Evidence folder already contains a run; choose a fresh output folder.")
    failed = False
    for name, mode, extra in runs:
        folder = destination / name
        folder.mkdir()
        command = [str(player), "-screen-fullscreen", "0", "-screen-width", "1920", "-screen-height", "1080",
                   mode, str(folder), *extra, "-logFile", str(destination / (name + ".log"))]
        print("Starting", name, flush=True)
        process = subprocess.run(command, cwd=ROOT)
        (folder / "process-exit.json").write_text(json.dumps({"exitCode": process.returncode}), encoding="utf-8")
        failed |= process.returncode != 0
        print(name, "actual process exit", process.returncode, flush=True)
    return int(failed)


if __name__ == "__main__":
    sys.exit(main())
