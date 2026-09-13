"""Reproducible original DropletPrototype cues; Python standard library only.

No recordings, samples, external libraries, or sound services are used.
Run: python Tools/Audio/generate_audio.py
"""
from pathlib import Path
import hashlib
import json
import math
import random
import struct
import wave

RATE = 48000
PROJECT = Path(__file__).resolve().parents[2]
OUTPUT = PROJECT / "ArtSource" / "Audio" / "Exports"


def impact(duration, seed, weight):
    rng = random.Random(seed)
    low_noise = 0.0
    samples = []
    for index in range(round(duration * RATE)):
        t = index / RATE
        white = rng.uniform(-1, 1)
        low_noise += .075 * (white - low_noise)
        # A brief metal puncture, a restrained pressure thump, and decaying grit.
        thump_phase = 2 * math.pi * (42 * t + (110 + 28 * weight) * .045 * (1 - math.exp(-t / .045)))
        thump = math.sin(thump_phase) * math.exp(-t * (7.5 - weight * 1.5))
        puncture = sum(math.sin(2 * math.pi * (frequency * t - 38 * t * t)) / (part + 1)
                       for part, frequency in enumerate((710, 1163, 1777))) * math.exp(-t * 24)
        grit = (low_noise * 2.7 + white * .13) * math.exp(-t * (9 - weight * 2))
        attack = min(1, t / .0015)
        release = min(1, max(0, (duration - t) / .06))
        samples.append((thump * .56 + puncture * .23 + grit * .6) * attack * release)
    return samples


def flight_loop():
    # Integer cycles over two seconds avoid a loop discontinuity. This cue is
    # deliberately quiet in-game, with no engine/nozzle sound on the droplet.
    samples = []
    for index in range(RATE * 2):
        t = index / RATE
        shimmer = math.sin(2 * math.pi * 64 * t) + .33 * math.sin(2 * math.pi * 128 * t)
        shimmer += .12 * math.sin(2 * math.pi * 320 * t) * (1 + .2 * math.cos(2 * math.pi * .5 * t))
        samples.append(shimmer * .1)
    return samples


def save(name, samples, peak):
    amplitude = max(abs(sample) for sample in samples)
    samples = [sample * peak / amplitude for sample in samples]
    payload = b"".join(struct.pack("<h", round(max(-1, min(1, sample)) * 32767)) for sample in samples)
    path = OUTPUT / name
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(payload)
    return {"file": str(path.relative_to(PROJECT)), "sample_rate": RATE, "channels": 1,
            "pcm_bits": 16, "duration_seconds": len(samples) / RATE, "peak": peak,
            "rms": math.sqrt(sum(sample * sample for sample in samples) / len(samples)),
            "bytes": path.stat().st_size, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    assets = [save("Impact_Light.wav", impact(.72, 5001, 0), .72),
              save("Impact_Heavy.wav", impact(1.12, 5002, 1), .76),
              save("Flight_Resonance.wav", flight_loop(), .3)]
    manifest = {"generator": "Tools/Audio/generate_audio.py", "license": "Original project assets; CC0-1.0",
                "sources": "Mathematical synthesis only. No third-party samples.", "assets": assets}
    destination = OUTPUT.parent / "audio_manifest.json"
    destination.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
