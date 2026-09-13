# FusionFrigate 导入与舰队扩编

2026-09-08。**打开 `Assets/_Project/Scenes/FleetAssault_Expanded.unity`，按 Play，再按 Enter 开始。** 120舰、Prefab、540秒任务均已保存，Play前可编辑。W/Shift加速、Space刹车、鼠标转向、Esc暂停、R重开；原稳定场景保留。

## 数量、间距和范围

以下为真实ShipTarget与保存场景的Unity测量，非LOD/预览计数：[原基线](verification/FleetExpansion/baseline-scene.json)、[扩大版](verification/FleetExpansion/unity-layout.json)。

| 指标 | 原布局 | 扩大版 |
|---|---:|---:|
| 独立可摧毁舰船 | 40 | **120，6群各20** |
| 最近邻中心距离最小值 | 37.483330 m | **110.699951 m** |
| 最近邻中心距离中位数 | 51.778374 m | **110.887985 m** |
| 最近邻最大值 | 68.124886 m | **110.888107 m** |
| 中位数与原布局之比 | 1 | **2.141589倍** |
| 中位数 / 统一参考舰长22.14 m | 约2.339 L | **5.008491 L** |
| 舰船中心范围 X×Y×Z | 335×135×420 m | **1424.350952×437.692017×1455.824951 m** |
| 完整新舰队LOD0视觉范围 | — | **1431.730835×443.903595×1477.965088 m** |
| 活动 / 警告半径 | 5840 / 4880 m | **5840 / 4880 m** |

最小舰群视觉AABB净空 **216.987762 m，约9.8 L**，高于6 L；舰对包围盒重叠为0。仅改位置，不缩放舰队根、玩家、相机或太阳系。第一舰为 **(0,8,100)**，玩家从 **(0,8,0)** 接近。

## 实际模型与共享资源

实际检查源 `ArtSource/Blender/Ships/FusionFrigate/FusionFrigate.blend` 与导出 `ArtSource/Exports/Ships/FusionFrigate/FusionFrigate.fbx`存在且哈希匹配，直接复用FBX。[资产复查](verification/FleetExpansion/model-audit-summary.json)、[Unity实测](verification/FleetExpansion/unity-model.json)：

| LOD | 整舰实际三角形 | Renderer / 唯一Mesh | Unity宽×高×长（m，取近似） |
|---|---:|---:|---|
| LOD0 | **17,224** | 13 / 7 | 7.380000×6.211534×22.139997 |
| LOD1 | **5,400** | 13 / 7 | 7.380000×6.211534×22.139997 |
| LOD2 | **1,396** | 10 / 5 | 7.380000×6.211534×22.139999 |

以上为本轮实测，非旧预算。三档原点、方向、尺寸一致；舰首+Z、上方+Y，根单位缩放，22.14 m沿用旧护卫舰尺度。共 **19唯一Mesh、4共享Material、0舰船贴图、36个Renderer**；三档资源合计不能当同时绘制量。

导入目录 `Assets/_Project/Art/Models/Ships/FusionFrigate/`只有一份FBX；`Materials/`的 `FF_Armor / FF_Structure / FF_EngineMetal / FF_BlueGray`用URP/Lit保留装甲、结构、引擎、窗带区别。关闭相机、灯光、动画、BlendShapes、自动Collider和Mesh Read/Write，导入法线并忽略源隐藏状态。玩法使用简单Collider，摧毁和通用效果不读取舰模顶点，故无需Read/Write。未新增贴图或逐舰资源。

Unity实查FBX的5个Empty喷口挂点朝 **+Z舰首**，与预期相反。Prefab统一设置 `localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up)`朝−Z舰尾，保留位置/单位比例；**未改源、FBX或几何**，最终挂点EditMode通过。

每Prefab实测 **60 Transform（含根）/36 Renderer**；120舰共4320 Renderer、1080 Collider，共享19 Mesh/4Material。唯一Mesh原生大小 **1,923,344 byte（1.834 MiB）**、Material **12,784 byte**，含所有LOD，非显存归因。无逐舰Mesh/Material复制；全舰队未启用Static Batching，四材质GPUInstancing=false。

## Prefab、LOD和战斗接线

`Assets/_Project/Prefabs/Fleet/FusionFrigate.prefab`沿用 `ShipTarget/DestructionPresenter`及独立 `VisualRoot/HitVolumes/Sockets`。移除FBX自动LODGroup，仅保留一个正式组，分配13/13/10 Renderer，阈值 **0.12/0.035/0.001**，不交叉淡化；低LOD可用，碰撞独立。

每舰 **9个简单Collider=4 Box+5 Capsule**：三段主体、舰桥及五引擎。比建议3～8多一个，以紧贴喷口而避免大盒覆盖引擎间空气；无逐装甲/天线Collider或MeshCollider，子体积均指向同一ShipTarget。

首次摧毁禁用全部HitVolumes与整个VisualRoot，重开统一恢复。单舰0.02秒Motor步骤接近、贯穿、计分、重开通过：[单舰结果](verification/FleetExpansion/single-ship-play.json)。各LOD及关闭Renderer时命中一致也通过PlayMode。

新场景 `MissionEffects.wreckPrefabs`置空，不套旧舰型残骸；复用已有闪光/火花/音效池，无新光效或碎裂。移除旧残骸使池Transform **441→57**，并发上限不变，贯穿效果峰值16；3轮胜利/重开后资源、池与状态稳定。

## 权威布局、任务与再生成

权威 `Tools/Blender/FleetExpansion/layout_config.json`固定40→120、seed908261、120 pose及ID `SPAWN_Small_FF001`～`FF120`，不重复乘数量。每群4列×5纵深，上下分层错列、统一舰首，保留一条5舰直线。左右 **106.272 m=4.8 L**、前后 **110.7 m=5 L**、上下 **55.35 m=2.5 L**，使实际最近邻超过原值2倍。

Blender副本 `ArtSource/Blender/FleetExpansion/FleetLayout_Expanded.blend`只有120 Empty与根，无舰模/材质/纹理/相机/灯。导出 `ArtSource/Exports/FleetExpansion/FleetLayout_Expanded.fbx`复用已校准的临时四顶点姿态代理，Unity仅消费Transform。两次生成、重开、回导的ID/pose/资源一致，JSON不变，最大位置误差 **0.000056014 m**。[重复性](verification/FleetExpansion/layout-repeatability.json)、[回导](verification/FleetExpansion/layout-source-roundtrip.json)、[命令说明](../Tools/Blender/FleetExpansion/README.md)。

`author_layout.py`默认验证pose，`--reflow`显式重排，后台Blender `--export`导出；手改副本哈希不匹配会拒绝覆盖。Unity菜单 `DropletPrototype > Fleet Expansion`提供 `1 Import Fusion Frigate`、`2 Save Gameplay Prefab`、`3 Create or Update Expanded Fleet`、`4 Validate Fleet`，保护未保存场景/Prefab Stage，使用Unity API和Undo。

新场景从最新太阳系稳定场景复制，保存 `GeneratedFleet/Squadron_01`～`06`，替换旧计分舰队；Play前已存在。两次更新、重开后的120 ID/位置/引用一致，手工内容和环境保留：[结果](verification/FleetExpansion/repeat-reopen.json)。只在新场景移除带旧ID/数量假设的G09基准组件。

独立 `Assets/_Project/Data/FleetMission_Expanded.asset`为 **540秒**，不改旧240秒或52/156 m/s速度；开始前完整注册120目标，视觉不决定成员。配置内代表蛇形航线 **18,377.249 m**；60%距离巡航、40%冲刺，等效 **70.909 m/s→259.166 s**。加23次转向×3秒及120舰对准×0.5秒，乘1.3得 **504.616 s**，向整分钟取整540。此为估算，非人工完整航线验收。

不移动32颗太阳系岩石，保守分离净空最小 **404.760 m**。目标中心最大局部半径 **1507.611 m**，含舰体及250 m转向余量仍在4880 m警告半径内，保留5840 m范围和安全回收。

## 实际验证

沿用Unity **6000.5.10f1**、URP **17.5.0**、Input System **1.20.0**和Blender **5.2.1 LTS**，没有升级安装。实际Editor完成编译、资产生成与测试，没有用dotnet代替Unity。

| 检查 | 结果与证据 |
|---|---|
| 安全EditMode | **13通过、0失败**：Fusion资产/场景5项、Solar数学7项、Score1项；[Fusion 5项结果](verification/FleetExpansion/editmode-fusion.json)。未运行会重建TestRange的旧作者测试。 |
| 完整PlayMode | **38通过、0失败、0跳过**；[结果](verification/FleetExpansion/playmode-regression.json)。 |
| 单舰 / 全LOD | 接近、真实扫掠命中、一次计分、全部LOD消失、重开恢复；模型不可见时仍是有效目标。 |
| 命中体积 | 主体、舰首、舰桥、主引擎及四辅助引擎分别扫掠命中；舰外与窄舰首侧面空气路径不误命中。 |
| 120舰胜利与3重开 | 真实Motor扫掠逐舰Destroy，每舰/胜利各一次；目标、分数、时间、引用、池清理稳定。设置攻击起点不是人工连续航线证明。 |
| 查询容量边界 | 8/25命中、初始重叠，及 **4096/4097实际Collider**边界（含复合身份和4097初始重叠）通过；验证扩容及完整查询回退，没有仅把缓冲改为120。 |
| 任务 / 飞行 | 薄目标4倍冲刺、转角、暂停、超时、回收无瞬移伤害及旧场景回归通过；540秒经公开Step推进，不改剩余量。 |
| 保存 / 再生成 | 120稳定ID、1个完整任务成员集合、1套舰队，2次生成与重开一致；手工环境保留。 |

连续Input System复测**通过**：4.839996秒/528.4377 m，最大156 m/s，**5舰/1500分**，末位置(0,8,533.7177)、前向(0,0,1)；暂停、R恢复120/540秒/0分、刹车转向和57池稳定。[结果](verification/FleetExpansion/live-input.json)。首轮诊断设备未锁定而漂移、仅3击中，保留[失败记录](verification/FleetExpansion/live-input-attempt1.json)，修正后重测；不是物理键鼠体验验收。

最终场景为已保存、无脏标记、退出Play的 `FleetAssault_Expanded`，菜单验证再次通过。Console保留旧 `PresentationRegressionTests` 故意注入的 `G09 deliberate optional presentation fault` 异常，用于验证表现故障不能阻断计分；该预期日志对应通过的测试，不是未解决的运行故障。

最终对照809个[基线文件](verification/FleetExpansion/baseline-files.json)，仅预期的 `docs/STATUS.md`、`docs/ENVIRONMENT.md`追加本轮记录，其余807个文件哈希一致，无缺失或意外变更：[最终保全](verification/FleetExpansion/preservation-final.json)。旧稳定场景、原布局/模型/太阳系源、本轮开始状态的TestRange、包与版本保留；更早批次TestRange问题仍如历史记录，不据本轮保全声称已恢复。

## 性能结果与限制

[修改前](verification/FleetExpansion/Before-performance.json)/[后](verification/FleetExpansion/After-performance.json)：**Editor Play、1920×1080、各300帧**，Ryzen9 7940HX / RTX4060 Laptop / D3D12，同近舰/全景视点及相近贯穿条件。沿用PC质量、VSync0、AA0、lodBias2。帧时间和GC含Editor工作，非Player成本。

| 视点 | 平均ms 前→后 | P95 ms 前→后 | P99 ms 前→后 | GC标记均值KiB/帧 前→后 |
|---|---|---|---|---|
| 近舰 | 6.618→**6.165** | 6.427→**6.370** | 12.844→**6.477** | 13.538→12.348 |
| 全景 | 6.358→**6.157** | 6.634→**6.358** | 14.084→**6.495** | 12.494→12.347 |
| 贯穿 | 6.696→**6.695** | 12.305→**12.266** | 12.541→**12.489** | 13.157→13.095 |

| 采样记录 | 近舰 前→后 | 全景 前→后 | 贯穿 前→后 |
|---|---:|---:|---:|
| 全帧Draw Calls | 316→4229 | 372→6052 | 540→1611 |
| 全帧Triangle计数 | 158386→571710 | 199132→350870 | 138420→256684 |
| 选中LOD+视锥舰模三角形估计 | 73676→266340 | 91652→167520 | 22088→114424 |
| 扩编后选中LOD0/1/2舰数 | 8 / 58 / 54 | 0 / 0 / 120 | 10 / 41 / 44 |
| 全Editor分配内存MiB | 892.412→935.950 | 892.542→936.177 | 893.715→940.251 |

远景实际LOD2；贯穿采样已毁25舰。全帧计数含环境/效果/通道，视锥估算非GPU提交，4320非同时可见数。Draw Call显著增加，Editor帧耗时相近不能证明Player/GPU全面达标。全Editor内存非舰船/显存归因，磁盘大小不作内存指标。

实际Frame Debugger采集 **52事件，含19个SRPBatch**，覆盖Shadow/DepthNormal/Opaque的DrawSRPBatcher，确认SRP路径：[记录](verification/FleetExpansion/frame-render-path.json)。仅证明路径，不推算提交三角形/GPU时间。**Standalone未构建/运行，Player性能、GPU耗时、显存归因和长期跨硬件未测。** 贯穿基准以ResetPose设起点，调用既有Mission.Step推进真实Motor固定步扫掠；连续键鼠测试另由正常FixedUpdate驱动，两者均不代表9分钟自然航线。

## 交付、截图和边界

新增模型/4材质、游戏Prefab、独立MissionSettings、扩大场景、布局导入、`FusionFleetPipeline.cs`及安全验证工具；作者源/导出/配置和证据路径如上。完整路径见[变更清单](verification/FleetExpansion/changed-files.json)。既有玩法、太阳系宏观内容、旧配置/场景和旧构建没有为扩编重写。

实际截图：[单舰](verification/FleetExpansion/Single-Ship-Inspection.png)、[舰队全景](verification/FleetExpansion/Fleet-Panorama.png)、[群间距](verification/FleetExpansion/Squadron-Spacing.png)、[开局](verification/FleetExpansion/Expanded-Spawn.png)、[Game近舰](verification/FleetExpansion/After-Near-Game.png)、[Game贯穿](verification/FleetExpansion/After-Penetrations-Game.png)、[连续5击中](verification/FleetExpansion/Expanded-Live-Five-Hits.png)、[暂停](verification/FleetExpansion/Expanded-Live-Paused.png)、[重开](verification/FleetExpansion/Expanded-Live-Restarted.png)。

人工验收：查看五喷口/LOD，飞5舰通道观察HUD，Esc暂停、R重开3次并检查Console。主观手感、美术与完整540秒自然航线仍待试玩。

停止本轮模型接入与舰队扩编范围，不自动继续尾焰、光效、AI、移动舰船或额外发布。
