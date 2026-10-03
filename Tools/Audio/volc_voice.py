"""Development-time Volcengine voice generation. Python standard library only.

See docs/VOLCENGINE_VOICE.md. No credentials or cloud calls in the Unity player.
"""
import argparse
import base64
import hashlib
import io
import json
import os
from pathlib import Path
import re
import sys
import urllib.error
import urllib.request
import uuid
import wave

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "ArtSource/Audio/Generated/Volcengine"
CONTENT = ROOT / "Assets/_Project/Data/NarrativeCombat/RadioContent.json"
TTS = "https://openspeech.bytedance.com/api/v3/plan/tts/unidirectional"


class VoiceError(Exception):
    pass


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise VoiceError("Unexpected HTTP redirect; no credentials forwarded.")


def credential(name):
    value = os.environ.get(name, "").strip()
    if not value and sys.platform == "win32":
        import winreg
        try:
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as key:
                value = str(winreg.QueryValueEx(key, name)[0]).strip()
        except FileNotFoundError:
            pass
    if not value:
        raise VoiceError(f"Configure {name}; see docs/VOLCENGINE_VOICE.md. Never paste keys into chat.")
    return value


def open_request(url, headers=None, payload=None):
    data = None if payload is None else json.dumps(payload, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers=headers or {})
    try:
        return urllib.request.build_opener(NoRedirect()).open(req, timeout=60)
    except urllib.error.HTTPError as error:
        # Do not print raw error bodies, signed URLs or request headers.
        raise VoiceError(f"HTTP {error.code}; check service activation, credentials and quota. No automatic retry.") from None
    except (urllib.error.URLError, TimeoutError, OSError):
        raise VoiceError("Network request failed. Submission may have reached the service; do not blindly resubmit.") from None


def write_json(path, value):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def identifier(value):
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,63}", value):
        raise VoiceError("IDs/takes must contain 1-64 ASCII letters, numbers, underscores or hyphens.")
    return value


def lines_for(args):
    if args.text is not None:
        if not args.id or len(args.id) != 1 or args.all:
            raise VoiceError("--text requires exactly one --id and cannot use --all.")
        lines = [{"id": args.id[0], "text": args.text}]
    else:
        source = json.loads(CONTENT.read_text(encoding="utf-8-sig"))
        lines = source["combat"] + source["narrative"]
        if not args.all:
            if not args.id:
                raise VoiceError("Select --id C01 [N01 ...] or explicitly use --all.")
            unknown = set(args.id) - {line["id"] for line in lines}
            if unknown:
                raise VoiceError("Unknown line IDs: " + ", ".join(sorted(unknown)))
            lines = [line for line in lines if line["id"] in args.id]
    for line in lines:
        identifier(line["id"])
        if not line["text"].strip():
            raise VoiceError("Empty text cannot be synthesized.")
    return lines


def payload_for(args, line):
    if not args.speaker:
        raise VoiceError("seed-tts requires --speaker from the official TTS 2.0 voice list.")
    return {"user": {"uid": "droplet-offline-authoring"}, "req_params": {
        "text": line["text"], "speaker": args.speaker,
        "audio_params": {"format": "pcm", "sample_rate": 24000, "speech_rate": args.speech_rate},
        "additions": json.dumps({"context_texts": [args.direction]}, ensure_ascii=False),
    }}


def decode_tts(stream):
    chunks = []
    usage = None
    complete = False
    event_count = 0
    codes = []
    for raw in stream:
        if not raw.strip():
            continue
        event = json.loads(raw)
        event_count += 1
        code = event.get("code")
        if code not in codes:
            codes.append(code)
        if code not in (0, 20000000):
            raise VoiceError(f"TTS service error {code}; check voice/resource permissions. No automatic retry.")
        if event.get("data"):
            chunks.append(base64.b64decode(event["data"], validate=True))
        if code == 20000000:
            complete = True
            usage = event.get("usage")
            break
    pcm = b"".join(chunks)
    if not complete or not pcm or len(pcm) % 2:
        error = VoiceError("Incomplete/invalid PCM stream; no audio file published.")
        # Metadata only; never persist raw service text, response headers or keys.
        error.stream_summary = dict(event_count=event_count, codes=codes,
                                    complete=complete, pcm_bytes=len(pcm), usage=usage)
        raise error
    return pcm, usage


def make_wav(pcm):
    output = io.BytesIO()
    with wave.open(output, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(24000)
        wav.writeframes(pcm)
    return output.getvalue()


def generate(args):
    identifier(args.take)
    lines = lines_for(args)
    plans = []
    for line in lines:
        payload = payload_for(args, line)
        digest = hashlib.sha256(json.dumps(payload, sort_keys=True, ensure_ascii=False).encode()).hexdigest()[:16]
        directory = OUTPUT / "seed-tts" / args.take / (line["id"] + "-" + digest)
        plans.append((line, payload, directory))
    print(json.dumps({"execute": args.execute, "provider": "seed-tts",
                      "lines": [{"id": x[0]["id"], "text": x[0]["text"], "output": str(x[2])} for x in plans],
                      "characters": sum(len(x[0]["text"]) for x in plans)}, ensure_ascii=False, indent=2))
    if not args.execute:
        return
    key = credential("VOLC_SPEECH_API_KEY")
    for line, payload, directory in plans:
        job = directory / "job.json"
        if job.exists():
            existing = json.loads(job.read_text(encoding="utf-8"))
            if existing.get("status") == "complete":
                audio = directory / "voice.wav"
                if not audio.exists() or hashlib.sha256(audio.read_bytes()).hexdigest() != existing.get("sha256"):
                    raise VoiceError(f"Cached audio is missing/modified: {directory}")
            print(f"Existing job: {job}. Use a new --take for an intentional new generation.")
            continue
        directory.mkdir(parents=True, exist_ok=True)
        record = {"provider": "seed-tts", "line": line, "request": payload,
                  "resource_id": "seed-tts-2.0", "endpoint": TTS,
                  "status": "submission_unknown", "request_id": str(uuid.uuid4())}
        # Exclusive journal before the request prevents accidental double billing on reruns.
        with job.open("x", encoding="utf-8") as handle:
            json.dump(record, handle, ensure_ascii=False, indent=2)
        headers = {"Content-Type": "application/json"}
        headers.update({"X-Api-Key": key, "X-Api-Resource-Id": "seed-tts-2.0",
                        "X-Api-Request-Id": record["request_id"], "X-Control-Require-Usage-Tokens-Return": "*"})
        try:
            with open_request(TTS, headers, payload) as response:
                pcm, usage = decode_tts(response)
        except VoiceError as error:
            record["error"] = str(error)
            if hasattr(error, "stream_summary"):
                record["stream_summary"] = error.stream_summary
            write_json(job, record)
            raise
        audio = make_wav(pcm)
        (directory / "voice.wav").write_bytes(audio)
        record.update(status="complete", sha256=hashlib.sha256(audio).hexdigest(),
                      duration_seconds=len(pcm) / 48000, usage=usage)
        write_json(job, record)
        print(f"{record['status']}: {job}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    generate_parser = commands.add_parser("generate", help="Preview by default; --execute submits paid requests.")
    generate_parser.add_argument("--provider", choices=("seed-tts",), default="seed-tts")
    generate_parser.add_argument("--id", nargs="+")
    generate_parser.add_argument("--all", action="store_true")
    generate_parser.add_argument("--text")
    generate_parser.add_argument("--speaker")
    generate_parser.add_argument("--speech-rate", type=int, default=0, choices=range(-50, 101))
    generate_parser.add_argument("--direction", default="用清晰、克制的中文舰队广播语气说话")
    generate_parser.add_argument("--take", default="take01")
    generate_parser.add_argument("--execute", action="store_true")
    generate_parser.set_defaults(run=generate)
    args = parser.parse_args()
    args.run(args)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    try:
        main()
    except (VoiceError, ValueError, KeyError, OSError) as error:
        # Known local errors have no credential content; remote bodies are never included.
        print(f"ERROR: {error}", file=sys.stderr)
        sys.exit(1)
