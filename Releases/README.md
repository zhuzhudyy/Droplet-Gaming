# Windows Seed 音频版

下载 [Droplet-Gaming-Windows-SeedAudio-20260929.zip](https://github.com/zhuzhudyy/Droplet-Gaming/releases/download/seed-audio-20260929/Droplet-Gaming-Windows-SeedAudio-20260929.zip)。发行页面：[Seed 音频版](https://github.com/zhuzhudyy/Droplet-Gaming/releases/tag/seed-audio-20260929)。

- 构建：2026-09-29，Unity **6000.5.10f1**，Windows x64；2000 艘舰船，完整 Seed 音频。
- 打包：2026-10-03，从已有 `Builds/Windows-SeedAudio-20260929` 归档，没有重新编译或改变游戏文件。
- ZIP：**176,407,481 bytes**；解压后 **197** 个文件，**353,405,270 bytes**。
- 全部 ZIP 条目已逐一 SHA-256 对比源构建，一致。
- ZIP SHA-256：`1e5333009322b1f4472a47ce654081c060804261d0899b39ab363cbc20a11b74`。

完整解压，运行 `Windows-SeedAudio-20260929/DropletGaming.exe`。保留相邻 Data、DLL、D3D12 和 MonoBleedingEdge 文件；不需要 Unity Editor。包超过普通 Git 文件限制，因此通过 GitHub Release 提供完整下载。

9 月 29 日最终玩家自动检查 **34 通过 / 0 失败 / 3 人工待验**。本次执行的是归档完整性检查，没有重新运行 Unity 编译、PlayMode、玩家或 90 分钟路线。未完成人工验收的逐项试听、自然长时路线及真实键鼠体验见 [验收报告](../docs/SEED_AUDIO_ACCEPTANCE.md)。归档证据见 [archive-verification.json](../docs/verification/VersionCleanup-20261003/archive-verification.json)。

已从当前仓库移除旧 NarrativeCombat ZIP；其内容仍可通过 Git 历史恢复。本机保留新版完整玩家、前版 CinematicAudio 完整回退及 v0.2.2 精简 ZIP；11 个过时构建目录的删除被工具自动审核拒绝，仍在本机，详见 [本次整理报告](../docs/verification/VersionCleanup-20261003/REPORT.md)。
