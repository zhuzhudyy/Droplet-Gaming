"""Summarize actual visual-upgrade validation JSONs without inventing missing counters.

Usage: python Tools/VisualUpgrade/summarize_validation.py BEFORE_JSON AFTER_JSON
       [--output docs/verification/VisualUpgrade/performance-summary.md]
Only the explicitly requested output file is written; source reports are read-only.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def number(value, digits=2) -> str:
    return f"{value:.{digits}f}"


def marker(phase: dict, name: str, field="mean") -> str:
    measured = phase.get(name, {})
    if not measured.get("samples", 0):
        return "未测得"
    return number(measured[field])


def failures(report: dict) -> list[str]:
    items = [f"{c['name']}：{c.get('detail', '')}" for c in report.get("checks", []) if not c.get("passed")]
    if report.get("error"):
        items.append(report["error"])
    return items


def summarize(before: dict, after: dict, before_path: Path, after_path: Path) -> str:
    category = lambda r: "Editor 测量" if r["editor"] else "独立运行测量"
    lines = [
        "视觉升级性能比较（自动提取实际 JSON）", "",
        f"修改前：{category(before)}；{before['gpu']}；{before['width']}×{before['height']}；{before['scene']}。",
        f"修改后：{category(after)}；{after['gpu']}；{after['width']}×{after['height']}；{after['scene']}。", "",
    ]
    incompatible = [key for key in ("editor", "gpu", "width", "height", "vSyncCount", "targetFrameRate", "developmentBuild") if before.get(key) != after.get(key)]
    if incompatible:
        lines += ["注意：以下测量条件不同，不能当作严格同条件对照：" + "、".join(incompatible) + "。", ""]
    lines += [
        "| 场景 | 前 FPS | 后 FPS | 前 P95 ms | 后 P95 ms | 前/后 GPU 均值 ms | 前/后实际帧数 | 前/后焦点帧比例 | 前/后摧毁数 |",
        "|---|---:|---:|---:|---:|---|---|---|---|",
    ]
    prior = {phase["name"]: phase for phase in before["phases"]}
    for phase in after["phases"]:
        name = phase["name"]
        if name not in prior:
            continue
        old = prior[name]
        focus = lambda p: f"{p['focusedFrames']}/{p['frames']}"
        lines.append(
            f"| {name} | {number(old['meanFps'])} | {number(phase['meanFps'])} | "
            f"{marker(old, 'renderedFrameMs', 'p95')} | {marker(phase, 'renderedFrameMs', 'p95')} | "
            f"{marker(old, 'gpuFrameMs')} / {marker(phase, 'gpuFrameMs')} | {old['frames']} / {phase['frames']} | "
            f"{focus(old)} / {focus(phase)} | {old['destroyed']} / {phase['destroyed']} |"
        )
    lines += ["", "每阶段先暖机 60 帧，再采样至少 480 帧且至少 6 秒。双方 VSync=0、帧率不封顶。集中爆炸按每 0.16 秒一次真实 Motor 扫掠；目标起点重设用于控制实验，不代表自然操作节奏。若帧率低于 80，达到 480 帧所需时间可能超过 6 秒，攻击总数也可能不同，摧毁数已经单独列出。", "",
              "| 阶段 | 前/后 SetPass 均值 | 前/后三角形均值 | 前/后 GC 字节/帧 | 后爆炸峰值/容量 | 前/后实时灯峰值 | 前/后粒子峰值 | 前/后探针捕获数 |",
              "|---|---|---|---|---|---|---|---|"]
    for phase in after["phases"]:
        if phase["name"] not in prior:
            continue
        old = prior[phase["name"]]
        lines.append(
            f"| {phase['name']} | {marker(old, 'setPassCalls')} / {marker(phase, 'setPassCalls')} | "
            f"{marker(old, 'triangles')} / {marker(phase, 'triangles')} | {marker(old, 'gcBytes')} / {marker(phase, 'gcBytes')} | "
            f"{phase['reactorEffectsPeak']} / {phase['before']['reactorCapacity']} | {old['realtimeLightsPeak']} / {phase['realtimeLightsPeak']} | "
            f"{old['particlesPeak']} / {phase['particlesPeak']} | {old['probeCaptures']} / {phase['probeCaptures']} |"
        )
    lines += ["", "| 库存（第一阶段开始） | 修改前 | 修改后 |", "|---|---:|---:|"]
    names = {
        "sceneRenderers": "场景全部 Renderer（含关闭的池和 LOD）", "uniqueSharedMaterials": "场景共享材质种类",
        "instanceNamedMaterials": "名称含 Instance 的场景材质", "legacyPoolTransforms": "旧特效池 Transform 数",
        "reactorPoolTransforms": "反应堆池 Transform 数", "reactorCapacity": "同时反应堆爆炸预算", "probes": "反射探针数",
        "enabledRealtimeProbes": "启用的实时反射探针", "particleSystems": "ParticleSystem 数",
    }
    for key, label in names.items():
        lines.append(f"| {label} | {before['phases'][0]['before'].get(key, '未记录')} | {after['phases'][0]['before'].get(key, '未记录')} |")
    lines += ["", "未取得样本的 Profiler 标记显示‘未测得’，不能解释为零成本。GPU 值来自 FrameTimingManager 的可用样本；总帧时包含当前进程与 Editor（如有）的开销，不等于纯 GPU 时间。共享材质库存不等于完整 GPU 显存占用。", ""]
    for label, report, path in (("前", before, before_path), ("后", after, after_path)):
        lines.append(f"{label}验证：completed={report['completed']}，allChecksPassed={report['allChecksPassed']}；证据：{path.as_posix()}。")
        for issue in failures(report):
            lines.append(f"- 未通过：{issue}")
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before", type=Path)
    parser.add_argument("after", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    text = summarize(load(args.before), load(args.after), args.before, args.after)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text, encoding="utf-8")
        print(args.output)
    else:
        print(text)


if __name__ == "__main__":
    main()
