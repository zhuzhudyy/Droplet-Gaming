# Fleet Assault v0.2.2 交付索引

打开 `Assets/_Project/Scenes/FleetAssault.unity`，按 Play，再按 Enter 或点击 BEGIN SORTIE。独立运行目录是 `Builds/Windows-v0.2.2-G09`，启动其中的 `DropletPrototype.exe`。解压完整包后运行，不要单独复制 exe。操作与人工检查见 [PLAYTEST_FLEET.md](PLAYTEST_FLEET.md)。

## 资产和文件改动

| 范围 | 实际交付内容 |
|---|---|
| ArtSource/Blender | calibration.blend、fleet_assets.blend、fleet_layout.blend；9个实际FBX、校准/40布局期望JSON、网格审计、5张预览；[复现说明](../ArtSource/Blender/DELIVERY.md) |
| Tools/Blender | build_fleet_assets.py及执行、检查和预览辅助脚本；已实际运行 |
| ArtSource/Audio、Tools/Audio | 3个原创48kHz PCM16 WAV、音频生成器、manifest与来源说明 |
| Assets/_Project/Art | Models/Fleet的显式FBX及导入设置；FleetMaterials的共享URP材质、静态反射Cubemap和星场Mesh；3个原创shader |
| Assets/_Project/Audio/Fleet | 实际轻/重贯穿和飞行共鸣WAV，已接入音源 |
| Assets/_Project/Prefabs/Fleet | 水滴、护卫舰、巡洋舰、指挥变体和3种六块残骸Prefab；单位游戏根、VisualRoot、独立HitVolumes |
| Assets/_Project/Scenes、Data | FleetAssault.unity、FleetMission.asset、FleetEffects.asset；40舰、240秒，独立配置 |
| Scripts/Runtime（新增） | ShipHitContext、FleetSceneRoot、PlayerOptions、FleetBenchmark；Presentation下4个有上限的表现组件 |
| Scripts/Runtime（更新） | ShipTarget、DropletHitDetector、DropletMotor、MissionController、ChaseCamera、HudPresenter；在既有玩法上增加命中上下文、异常隔离事件、重置通知、轻微镜头反馈、设置和完整UI |
| Scripts/Editor（新增） | FleetAssetPipeline、FleetLayoutImporter、FleetEnvironmentBuilder、FleetAssaultBuilder；Unity API保存资产与正式场景 |
| Tests（新增） | FleetAssetTests、PresentationRegressionTests；旧测试保留 |
| ProjectSettings | EditorBuildSettings：FleetAssault/TestRange/SampleScene；PlayerSettings：0.2.2、1920×1080窗口；Unity原生构建可能保存现有URP预过滤/序列化数据，不代表切换管线 |
| 文档 | AGENTS、IMPLEMENTATION_PLAN的批次例外；ASSET_PIPELINE、ENVIRONMENT、STATUS；BATCH_G05_G09、PLAYTEST_FLEET、ASSET_LICENSES、本索引、PERFORMANCE_G09与verification证据 |
| Builds | 新版完整Windows目录与ZIP；旧Windows、v0.2.0、v0.2.1候选目录均保留 |

所有Unity资产包含原生生成/保留的.meta。场景、Prefab和配置通过Unity Editor API写入，没有手写序列化YAML。完整文件hash清单见 `verification/G05-G09/delivery-manifest.json`；该清单是交付时快照，不是虚构的Git基线差异。

项目目前是父仓库 `C:/学习/玩` 内的未跟踪目录，无法用HEAD得出本批精确逐文件diff。未暂存、提交、重置Git，未修改父仓库配置；既有.gitignore已覆盖构建/缓存及Blender备份。TestRange及SampleScene、旧灰盒Prefab/配置用途保留，包版本、Editor版本和活动PC_RPAsset沿用。

## 验收证据

- 基线实际Unity旧回归：EditMode 3/3、PlayMode18/18。
- 本批最终全套：`verification/G05-G09/release-editor.json` 15/15，`release-playmode.json` 27/27，0失败/跳过；先前最终XML也保留。涉及高速薄目标、多目标、饱和查询、复合去重、暂停、超时/同一步最后击杀优先判胜、瞬移无伤、重复导入、保存重开及三次正式舰队重开。
- Blender校准：`calibration-unity.txt`；模型尺寸/法线、40个源布局完整位姿与Unity逐项对照通过。
- 关闭/耗尽表现预算、错误表现订阅者、残骸无Collider/ShipTarget、音源/尾迹/事件重开清理均经过实际PlayMode验证。
- 最终原生构建和独立玩家证据、性能条件及局限见 [PERFORMANCE_G09.md](PERFORMANCE_G09.md)。只在显式CLI参数下启用自动路线，普通启动是可操作的Ready界面。
- 实际查看Ready、Game普通飞行、连穿、冲击/残骸、Results、Settings画面。冲击近景使用明确标注的临时检查镜头；其余为实际追尾镜头。截图证明画面，行为由测试和实际玩家路线验证。
- 所有原始失败报告保留：校准前两次方向/依赖图问题、测试临时场景fixture、早期自动路线选错目标、一次工具编码导致构建失败，均修复后实际重跑，没有删除原有断言。

## 需要人工检查 / 已知局限

1. 在独立窗口用真实键盘 Enter、Esc、R，并试玩鼠标转向、W/S、A/D、Shift、Space。自动化注入Enter/Esc未稳定产生预期状态，不能据此断言真实硬件输入已通过；Input System逻辑和已有Editor合成输入验证通过，鼠标菜单点击实际生效。
2. 主观飞行舒适度、整局240秒难度/路线、最后一舰引导和音量听感留给用户集中试玩。WAV和AudioSource实际存在/播放状态已验证，但没有以自动工具声称完成听感验收。
3. 尾迹在贴近追尾镜头时有折线/结状外观；可把Effects设LOW/OFF。预制残骸短暂经过镜头，未实现遮挡淡出；无物理阻挡或计分影响。星球是程序化风格化地球，非真实地理纹理。
4. 独立版GC逐帧分配计数器不可用；未做长时间内存浸泡、其他硬件/分辨率/30–144Hz矩阵或GPU时间测量。短路线结果不能替代这些检查。
5. 构建的一个警告是可选Pipeline开发桥没有运行时配置，因此玩家禁用该桥；离线游戏无需它。D3D12日志的info-queue接口提示没有造成观察到的画面失败。没有未解决的编译、构建或必需资源阻塞。

本批停止在G09。后续只待用户集中反馈，不自动启动下一阶段。
