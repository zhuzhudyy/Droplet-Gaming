# 三体水滴 / Droplet Gaming

离线、单人、第三人称水滴战斗原型。最新 Seed 音频版保留 **2000 艘舰船**，包含四幕剧情、舰船激光与水滴反射、贯穿延爆、事件特写、英语无线电及舰船撤离。

## 下载试玩

下载 [最新 Windows Seed 音频版 ZIP](https://github.com/zhuzhudyy/Droplet-Gaming/releases/download/seed-audio-20260929/Droplet-Gaming-Windows-SeedAudio-20260929.zip)，完整解压后运行 `Windows-SeedAudio-20260929/DropletGaming.exe`。不要只复制 exe：同目录的 Data、DLL 和 Mono 运行库也必须保留。

- Enter 开始约9分钟的四幕英语剧情（中文字幕），Tab 跳过，Esc 暂停/继续，R 直接战斗/重开。
- 鼠标转向，W/S 调速，A/D 横移，Shift 冲刺，Space 刹车。
- H 查看广播历史，-/+ 调整广播音量。
- Q 返回主视角，F8 切换特写关闭/低频/标准。

打包内容、校验值与验证范围见 [发行说明](Releases/README.md)。这是2026-09-29已验证构建的完整归档，本次整理没有重新编译。34项玩家自动检查通过；自然长时路线、逐项试听和真实键鼠体验仍待人工验收。

## 打开源工程

在 Unity Hub 添加仓库根目录，使用 **Unity 6000.5.10f1**，保留 URP **17.5.0**、Timeline **1.8.13**。打开 `Assets/_Project/Scenes/FleetAssault_SeedAudio.unity`，按 Play 并聚焦 Game。此场景是默认启用的构建场景。

源码、场景、Prefab、`.meta`、Packages、ProjectSettings、Blender源文件、音频制作源材与测试均保留。Unity首次导入会自行重建Library。可玩ZIP在GitHub Release；报告、测试代码和结构化验证证据在仓库中。历史构建、迁移恢复包、重复基线备份及原始Profiler/运行日志仅保留本机，部分历史报告链接指向这些本机资料。

- [项目入口](START_HERE.md)
- [最新状态](docs/STATUS.md)
- [Seed音频交付与验收](docs/SEED_AUDIO_ACCEPTANCE.md)
- [环境版本](docs/ENVIRONMENT.md)
- [素材来源及限制](docs/ASSET_LICENSES.md)

原著参数与游戏放大尺度有明确区别；玩家完全离线，云端音频生成仅限开发工具。未验证范围见验收报告，不将短程自动验证当作全部人工验收。
