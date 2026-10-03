# Seed 音频交付与验收 — 2026-09-29

当前可玩版本使用 [`FleetAssault_SeedAudio.unity`](../Assets/_Project/Scenes/FleetAssault_SeedAudio.unity)；Windows 玩家在 [`Builds/Windows-SeedAudio-20260929`](../Builds/Windows-SeedAudio-20260929)。这是由 Unity Editor API 从原正式场景另存的音频增强版，也是唯一启用的默认构建场景；旧场景作为禁用回退项保留，原音频和 GUID 不变。玩家不访问云服务。

## 已交付

| 内容 | 完成数量 | 来源和听感状态 |
|---|---:|---|
| 非语音成品 | 131/131 | `seed-audio-1.0`；激光、贯穿、爆炸、飞行、舱室、配乐六类代表已获用户认可，其余独立条目待逐条试听 |
| 新版英语对白 | 87/87 | Seed-TTS 2.0；技术检查通过，逐条角色表演待试听 |
| 重混通讯 | 80 主版 + 78 强度变体 | 只混合上述 Seed 声源；技术暂发，完整场景待试听 |
| Unity 新版音频 | 131 单条 + 158 通讯 | 全部由实际 Editor 导入并保存于新场景；旧 25 个程序声效源未进入新版通讯混音 |

试听总入口为 [`ArtSource/Audio/SeedAudio20260929/index.html`](../ArtSource/Audio/SeedAudio20260929/index.html)。[完整素材目录](../ArtSource/Audio/SeedAudio20260929/catalog.json)记录用途、触发、提示词和变体；每条 [`published`](../ArtSource/Audio/SeedAudio20260929/published) 与 [`published-radio`](../ArtSource/Audio/SeedAudio20260929/published-radio) 旁侧 JSON 保存模型、请求、来源和成品哈希。`Laser_03` 使用成功的独立来源 `Laser_03_R2`，`E033` 使用 `E033_R2`；两条首次结果不明的请求保留原记录，不被当作成功素材或自动重发。长音乐及环境素材在 Unity 中以流式加载并保留双声道。

四幕新版英语开场的 40 条干声合计 521.499 秒；加场景间隔后主片约 561.44 秒。90 分钟任务时长、2000 艘可独立识别的舰船和原玩法参数未改。阶段配乐轮替、环境、巡航/冲刺/操控边沿、战斗事件、舰内、通讯、界面和任务反馈均由新目录触发；12 路世界声源上限保留。

## 已执行验证

- 六类代表样音由用户试听确认。原声 [131 条 QA](../ArtSource/Audio/SeedAudio20260929/catalog-qa/summary.json) 与成品 [131 条 QA](../ArtSource/Audio/SeedAudio20260929/catalog-qa/delivery-summary.json) 均为 0 技术问题、0 近重复候选；[87 条干声 QA](../ArtSource/Audio/SeedAudio20260929/voice-qa/summary.json) 技术就绪。成品 QA 的 25 项非阻断提醒包括 22 条文件级通讯遮蔽代理、两条连击声起音偏迟和一条滑条声偏短；Unity 玩家已证实通讯时会压低背景，实际可懂度仍须听评。
- 音频工具离线回归 **64/64** 通过；Unity 实际 EditMode 作者工具 **3/3**、PlayMode 运行时 **6/6**、新场景集成 **3/3**、持续世界音源 **6/6** 通过。后者验证反应堆和撤离可同舰并行循环、同类去重、舰爆/逃脱停止以及槽位复用。证据见 [`docs/verification/SeedAudio-20260929`](verification/SeedAudio-20260929)。测试使用真实已导入场景；单元测试自身的临时 WAV 不计为云生成音频。
- 实际 Unity 6000.5.10f1 Windows 构建 [`build.json`](verification/SeedAudio-20260929/build.json)：Succeeded，0 错误、1 警告，玩家约 353 MB；最终持续音修复已进入重建的玩家 DLL。
- 在 1920 × 1080 最新实际玩家中，[2000 舰报告](verification/SeedAudio-20260929/player-validation-release/report.json)记录 **34 项自动检查通过、0 失败、3 项需人工完成**；另在 1280 × 720 运行一次。检查包含 2000 唯一舰 ID、131 条导入音频、四幕选曲和舱室切换、通讯压低、暂停保留播放位置、巡航/冲刺区分、贯穿至反应堆和延爆事件链、12 路世界源上限、三次连续重开及加速结果。六段玩家监听器输出 WAV 皆非静音，报告与截图、事件轨迹同目录。四幕检查验证每幕代表 cue 的路由，未代表完整自然时间轴播放。

## 尚需人工完成

1. 从试听目录逐条听 131 个非语音变体、87 条对白及 158 条通讯，重点检查误读、角色表演、起音尾音、重复感、对白可懂度、背景循环接缝与各场景响度。技术暂发是为了形成可玩版本，不等于逐条艺术验收。
2. 在发布玩家中自然播放完整四幕，并用实际操控攻击，检查字幕、转场与声音在正常时速下同步。
3. 在发布玩家中分别完成完整 90 分钟倒计时和获胜路线。现有结果检查使用不改任务设置的加速步进，不能替代完整时长验证。

运行玩家：打开 [`DropletGaming.exe`](../Builds/Windows-SeedAudio-20260929/DropletGaming.exe)。关卡使用离线素材；按游戏现有界面开始、暂停/继续、跳过剧情和重开。若听到具体问题，在试听目录记下 cue 或通讯 ID，可由原始 Seed 请求与成品侧车定位并只修订该条。

本批非语音生成脚本按每条成功请求的原始时长及未确定请求的上限预留计，当前最大计入约 **32.83 / 60 元**；这不是服务商实际账单。对白累计计入 **2235.195 / 20000 AFP**（含历史预留）。未充值或启用额外付费。
