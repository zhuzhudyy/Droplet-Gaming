# 三体水滴 / Trysolar Drip

这是完整工作工程，统一路径：`C:\学习\玩\Unity\Trysolar Drip`。

## 打开与试玩

- 双击根目录 `Open-Project.cmd`，或在 Unity Hub 打开这个文件夹。
- 使用已有的 Unity **6000.5.10f1**，URP **17.5.0**。
- 最新正式场景：`Assets/_Project/Scenes/FleetAssault_NarrativeCombat.unity`，包含 2000 艘舰船。
- Unity 中按 Play，再聚焦 Game 并按 Enter 开始剧情；Tab 跳过，R 直接战斗/重开，Esc 暂停。
- 已有独立版：`Builds/Windows-NarrativeCombat/DropletPrototype.exe`。运行时保留整个相邻构建目录。
- 鼠标转向，W/S 调速，A/D 横移，Shift 冲刺，Space 刹车，H 广播历史。

## 目录用途

| 路径 | 内容 |
|---|---|
| `Assets/_Project` | 游戏脚本、场景、预制体、导入美术、测试 |
| `Assets/美术` | 从早期模板保留的参考图及原始元数据 |
| `ArtSource` | Blender 源工程、导出及原始参考 |
| `Tools` | 美术生成、验证与辅助脚本 |
| `Builds` | 最新独立版及保留的历史构建 |
| `docs/STATUS.md` | 最新交付状态、历史记录及验证限制 |
| `docs/ProjectCleanup-20260913` | 整理记录、文件哈希及旧模板恢复包 |

## 本次整理

2026-09-13 将原 `新建文件夹/DropletPrototype` 的完整内容迁入本目录，保留 122 个原有脚本、15 个场景、源模型、历史构建与 Git 历史。早期模板只有 SampleScene 和教程脚本；它的独有参考图已合并，其他源文件与 Git 快照保存在 `InitialTemplate-and-Git.zip` 中。旧模板缓存及只含启动日志的 `Trysolar` 已清理，`2DLearn` 保持原样。

恢复早期模板时，将该 ZIP 解压到一个新的空文件夹，再用同版本 Unity 打开；Library 会自动生成。不要将恢复包覆盖到当前工作工程。

Codex 的 `unity` MCP 连接固定到本目录。当前会话的旧连接已断开，重新打开 Codex 后会按新配置建立连接。旧的 `127.0.0.1:8080/mcp` 配置已从 Codex 移除。
