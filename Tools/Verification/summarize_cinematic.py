"""Summarize completed, real evidence without running Unity or changing assets."""
import hashlib
import html
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs/verification/CinematicAudio-20260919"


def read(path):
    # Original PowerShell receipts may be UTF-16; json detects Unicode BOMs.
    return json.loads(path.read_bytes())


def tool_result(name):
    result = read(OUT / name)["data"]["result"]
    return json.loads(result) if isinstance(result, str) else result


def main():
    tests = {}
    for name in ("editmode-results.json", "playmode-final-results.json", "shots-final-results.json"):
        result = tool_result(name)
        assert result["status"] == "completed", name
        tests.update({case["FullName"]: case["Status"] for case in result["results"]})
    players = {}
    for name in ("PlayerFinal", "Opening"):
        report = read(OUT / name / "report.json")
        assert report["completed"] and report["allChecksPassed"], name
        assert read(OUT / name / "process-exit.json")["exitCode"] == 0, name
        players[name] = {key: report[key] for key in ("ships", "identities", "maximumVoices", "maximumWorldSources", "nonzeroOutputSamples", "peakOutput", "checks", "shots")}
    natural = [shot for shot in players["PlayerFinal"]["shots"] if shot["phase"] == "Normal input slower flight"]
    kinds = sorted({shot["kind"] for shot in natural})
    assert kinds == ["Attacker", "DropletContact", "Explosion", "Penetration"], kinds
    assert all(value == "Passed" for value in tests.values())
    hashes = {}
    for relative in ("Assets/_Project/Scenes/FleetAssault_CinematicAudio_Cubic.unity",
                     "Assets/_Project/Data/CinematicAudio/CinematicRadioContent.json",
                     "Assets/_Project/Data/CinematicFleet/FleetLayout_Cubic.json",
                     "Builds/Windows-CinematicAudio-20260919/DropletGaming_Data/Managed/DropletPrototype.Runtime.dll"):
        hashes[relative] = hashlib.sha256((ROOT / relative).read_bytes()).hexdigest()
    record = {"status": "technical_acceptance_passed_subjective_listening_pending", "uniqueEditorTests": len(tests),
              "passedEditorTests": sum(value == "Passed" for value in tests.values()), "build": read(OUT / "build.json"),
              "naturalShotKinds": kinds, "players": players, "sha256": hashes,
              "listening": "待人工试听。Technical signal/output checks do not verify acting, intelligibility or Foley realism."}
    (OUT / "delivery-audit.json").write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
    parts = ['<!doctype html><meta charset="utf-8"><title>2000舰实际验收证据</title>',
             '<style>body{font:16px system-ui;background:#101820;color:#dce8ef;max-width:1100px;margin:36px auto;padding:0 20px;line-height:1.6}a{color:#75d5ed}img{width:100%;height:auto}figure{margin:30px 0}code{color:#abd}</style>',
             '<h1>完整2000舰 · 实际运行证据</h1>',
             f'<p>实际Unity测试{len(tests)}/{len(tests)}；Windows运行{len(players["PlayerFinal"]["checks"])}/{len(players["PlayerFinal"]["checks"])}；完整英语开场{len(players["Opening"]["checks"])}/{len(players["Opening"]["checks"])}，两个进程退出码均0。</p>',
             '<p>四类特写来自正常S键降速后的真实飞行事件；集中伤害压力阶段另行记录。音频已生成、混音与技术检查，主观听感仍待人工试听。</p>',
             '<p><a href="../../CINEMATIC_AUDIO_ACCEPTANCE.md">总验收报告</a> · <a href="delivery-audit.json">结构化汇总</a> · <a href="PlayerFinal/report.json">玩家原始报告</a> · <a href="Opening/report.json">开场原始报告</a> · <a href="../../../ArtSource/Audio/CinematicAudio/listening.html">真实通讯试听与分轨</a></p>']
    gallery = [("攻击者舰体与发射", "PlayerFinal/shot-002-Attacker.png"),
               ("水滴接触与反射", "PlayerFinal/shot-001-DropletContact.png"),
               ("实际贯穿", "PlayerFinal/shot-005-Penetration.png"),
               ("爆炸中段：完整场景Editor正常300 UU/s飞行；Editor暂时静音避免与开场玩家叠播", "EditorVisual/shot-006-Explosion-frame2.png"),
               ("实际英文开场/中文字幕（140秒）", "Opening/opening-140.png")]
    for label, path in gallery:
        assert (OUT / path).is_file(), path
        parts.append(f'<figure><figcaption>{html.escape(label)}</figcaption><a href="{path}"><img loading="lazy" src="{path}"></a></figure>')
    (OUT / "index.html").write_text("\n".join(parts), encoding="utf-8")
    print(json.dumps({"uniqueTests": len(tests), "playerChecks": len(players["PlayerFinal"]["checks"]),
                      "openingChecks": len(players["Opening"]["checks"]), "naturalKinds": kinds}, ensure_ascii=False))


if __name__ == "__main__":
    main()
