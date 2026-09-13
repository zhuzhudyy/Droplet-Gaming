# 三体水滴 / Droplet Gaming

离线、单人、第三人称水滴战斗原型。最新正式场景保留 **2000 艘舰船**，包含剧情接近、舰船激光与水滴反射、贯穿延爆、事件无线电及舰船撤离。

## 下载试玩

下载 [Windows 可玩版 ZIP](Releases/Droplet-Gaming-Windows-NarrativeCombat.zip)，完整解压后运行 `Windows-NarrativeCombat/DropletPrototype.exe`。不要只复制 exe：同目录的 Data、DLL 和 Mono 运行库也必须保留。

- Enter 开始约60秒剧情，Tab 跳过，Esc 暂停/继续，R 直接战斗/重开，N 重播剧情。
- 鼠标转向，W/S 调速，A/D 横移，Shift 冲刺，Space 刹车。
- H 查看广播历史，-/+ 调整广播音量。

打包内容、校验值与验证范围见 [发行说明](Releases/README.md)。这是2026-09-12已验证构建的完整归档，不是本次推送重新编译的版本。

## 打开源工程

在 Unity Hub 添加仓库根目录，使用 **Unity 6000.5.10f1**，保留 URP **17.5.0**、Timeline **1.8.13**。打开 `Assets/_Project/Scenes/FleetAssault_NarrativeCombat.unity`，按 Play 并聚焦 Game。

源码、场景、Prefab、`.meta`、Packages、ProjectSettings、Blender源文件与测试均保留。Unity首次导入会自行重建Library。历史构建、迁移恢复包、重复基线备份及原始Profiler/运行日志仅保留本机；可玩ZIP、报告、测试代码和结构化验证证据在仓库中。部分历史报告链接指向这些本机诊断资料。

- [项目入口](START_HERE.md)
- [最新状态](docs/STATUS.md)
- [六项功能与实测报告](docs/NARRATIVE_COMBAT_REPORT.md)
- [环境版本](docs/ENVIRONMENT.md)
- [素材来源及限制](docs/ASSET_LICENSES.md)

原著参数与游戏放大尺度有明确区别；本项目不包含在线大模型或付费语音请求。未验证范围（物理键鼠手感、长时间热机等）见报告，不将短程自动验证当作全部人工验收。
