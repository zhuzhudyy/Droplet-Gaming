# 2000 艘规则舰阵与太阳强化交付报告

先打开 **`Assets/_Project/Scenes/FleetAssault_2000_Sun.unity`** 试玩。Play 后在 Ready 全景观察舰阵与左侧太阳，聚焦 Game 并按 Enter 开始；鼠标转向，W/S 调速，A/D 横移，Shift 加速，Space 刹车，Esc 暂停，R 重开。新场景基于 `FleetAssault_VisualUpgrade.unity` 复制，旧场景保留。

**文档状态：实施与中间验证记录已填写；最终优化、性能、Profiler/Frame Debugger、独立构建与 Player 结果仍为 PENDING，须由最终证据替换后才算完成交付。** 当前文件不是已通过全部验收的声明。

## 交付文件与实现

| 内容 | 路径 |
|---|---|
| 可编辑 Blender 舰阵 | `ArtSource/Blender/Fleet2000Sun/FleetLayout_2000_Sun.blend` |
| 权威布局与 bpy 脚本 | `Tools/Blender/Fleet2000Sun/layout_config.json`、`author_layout.py`、`README.md` |
| 布局导出与收据 | `ArtSource/Exports/Fleet2000Sun/FleetLayout_2000_Sun.json`、`FleetLayout_2000_Sun_manifest.json` |
| Unity 布局导入 | `Assets/_Project/Scripts/Editor/Fleet2000Sun/Fleet2000Pipeline.cs` |
| 运行分级与展示镜头 | `Assets/_Project/Scripts/Runtime/Fleet2000Sun/FleetRenderManager.cs`、`FleetOverviewCamera.cs` |
| 太阳接线与显示控制 | `Assets/_Project/Scripts/Editor/Fleet2000Sun/Sun2000Authoring.cs`、`Assets/_Project/Scripts/Runtime/Fleet2000Sun/SunDisplayRig.cs` |
| 太阳 Shader/材质/后处理 | `Assets/_Project/Art/Fleet2000Sun/Shaders/SolarPhotosphere.shader`、`SolarPhotosphere.mat`、`SunPost.asset`、`SunLighting.asset`、`SunLensFlare.asset` |
| 水滴太阳反射与任务配置 | 同目录 `PerfectChromeSun.mat`、`FleetMission_2000.asset` |
| 验证工具与原始证据 | `Assets/_Project/Scripts/Editor/Fleet2000Sun/`、`Assets/_Project/Scripts/Runtime/Fleet2000Sun/Fleet2000ValidationRunner.cs`、`docs/verification/Fleet2000Sun/` |

Blender **5.2.1 LTS / Python 3.13.13** 实际生成、保存并独立重开了 **50 列 × 20 行 × 2 层 = 2000 艘**。复用 FusionFrigate LOD2，2,000 个独立 Collection Instance 共用一个集合、12 个 Mesh；完整低精度舰体 1,396 三角形。没有重建舰船，没有重复导出 FBX，也没有复制参考图水印、按钮或像素。

实际舰长 **57.563997 m**，对应已完成 VisualUpgrade 的固定 2.6 倍模型尺度。个体均为单位缩放，舰首统一 +Z；横向/纵向/层间中心间距 **172.692 / 230.256 / 115.128 m**，即 3 L / 4 L / 2 L。第二层只增加 x=14.391、z=28.782 m 的固定错位。舰体整体范围约 **8495.487 × 131.278 × 4461.210 m**；透视收束来自真实三维位置和透视镜头，未人为压密远处舰船。

沿用既有 schema 1：每舰包含稳定 ID、模型 ID、位置、forward/up、四元数、单位缩放与米制说明。`Tools/Blender/Fleet2000Sun/layout_config.json` 是唯一姿态权威，ArtSource 导出和 Unity Assets 中的副本保持相同哈希；Blender 手工编辑须通过显式 `--export-saved` 回写同一权威，操作方法见工具 README。保存文件内存在舰阵，编辑模式可见。导入器复用现有 `FusionFrigate_VisualUpgrade.prefab`，仅维护生成舰队分支并提供 Undo；有未保存场景时拒绝覆盖。重复导入实际得到 **0 新建 / 2000 更新**，稳定身份与数量不变。

近景使用完整 Prefab、既有 LOD、九个独立命中体和既有池化摧毁表现；中远景使用 `Graphics.RenderMeshInstanced`，按空间块、LOD、共享 Mesh/材质/子网格分批绘制。2,000 艘始终保留独立 `ShipTarget` 身份和存活状态；分级切换只改变显示及交互激活，不创建替代舰船。唯一既有运行代码改动为 `DropletHitDetector` 在每段原有 overlap/sweep 前调用同步 `PrepareSweep`，覆盖本次完整路径及初始重叠，再交给既有精确命中查询判定；摧毁标志立即排除实例提交，重开恢复状态。没有把不可摧毁贴片算作有效目标，也没有修改水滴模型、方向、速度或贯穿权威。

## 太阳、阳光与任务范围

继续使用 **Unity 6000.5.10f1 / URP 17.5.0**。太阳展示角直径独立配置为 **34°**；现有 2.5 AU 战区的物理角直径约 **0.213162°**，艺术显示倍率约 **159.5 倍**。只改变既有太阳代理的显示尺度，宏观太阳系、战区映射及其他天体保持原坐标。

太阳使用 HDR 自发光核心、低成本动态表面颗粒及暖金边缘 Shader。单一 Directional Light 与太阳方向绑定，颜色接近白热色；新场景中其他方向主光停用。URP Bloom 当前强度 0.24、阈值 1.3、scatter 0.50，配合 ACES；关闭全局雾，避免整场橙色浓雾。Lens Flare (SRP) 启用深度遮挡与 32 采样，采样点位于不透明太阳表面前方，舰体遮挡中央光源时控制耀斑。保留原水滴 HDR 环境/局部反射探针/池化爆炸反射，只同步太阳反射角半径与主光方向。

出生点仍为 **(0,8,0)**，直前方 x=0/y=8 为 20 艘攻击走廊，第一舰 z=300。任务为 **5400 秒（90 分钟）**；活动中心 **(93.5415,65.564,2501.823)**，警告/恢复半径 **5900/6500 m**。独立计算全部舰船最远半径加完整舰半径及 400 m 转弯储备为 **5207.981 m**，距警告线尚余 **692.019 m**。

同一 458.685 km 蛇形全清路线，按原有 156 m/s Boost，加 297 秒换列、1000 秒对准预算，再留 15% 余量，估算 **4872.883 秒**。90 分钟预算依赖大量 Boost，未实跑整场通关；JSON 保留的 8940 秒是较慢的 60% 巡航路程情景建议，最终场景采用 5400 秒。详见 [布局、时限与保全复核](verification/Fleet2000Sun/layout-review.md)。

## 已执行的中间验证

| 检查 | 实际结果与证据 |
|---|---|
| Blender 保存/独立重开/共享 | 2000 唯一实例，单位缩放、统一朝向；JSON→Blender 最大位置误差 0.000302 m；`blender-generation.json`、`blender-reopen-verification.json` |
| 布局重复生成与穿插 | 权威/导出哈希相同，全部 1,999,000 舰对 AABB 无穿插；最小净距 98.978009 m；`blender-repeatability.json` |
| Unity 首次/重复导入 | 2000→2000，首次新建2000、重复新建0/更新2000，旧舰队未叠加；`first-import-audit.json`、`reimport-audit.json` |
| 实际 EditMode | **5/5**；`editmode-tests.json`；属于最终优化前记录 |
| 实际 PlayMode | **59/59**；`playmode-tests.json`；属于最终优化前记录 |
| 首轮完整 Editor 运行检查 | **229/229**，含跨分级高速长路径贯穿、计分、暂停、2000目标胜利、重开及池上限；`Editor/Final-20260908-142338-180/fleet2000-validation.json`；虽目录名为 Final，属于中间版本 |

完整首轮已保存真实 Unity 近/中/全景、太阳遮挡/无遮挡、连续爆炸、胜利及重开截图，位于上述 `Final-20260908-142338-180` 目录。首个 `Final-20260908-141635-748` 运行被中止，保留失败记录，不算通过。控制实验使用真实 Motor 查询，但包含受控攻击起点和超长诊断路径，不能冒充玩家自然连续飞行或 90 分钟实跑通关。

Blender 近/中/远真实布局预览分别为 `blender-near.png`、`blender-mid.png`、`blender-panorama.png`；它们只证明 Blender 布局展示，不作为 Unity 阳光或性能证据。

## 最终验证与性能待填

目标为 RTX 4060、**1920×1080 原生分辨率、无帧生成、约 80 FPS / 12.5 ms**，不是保证。本机已识别为 **Ryzen 9 7940HX / RTX 4060 Laptop GPU / Windows 11 10.0.26200 / Direct3D12**；Unity 报告总显存 7956 MB、系统内存 15575 MB。总显存容量不等于本游戏显存使用量。

| 最终验收项 | 结果 |
|---|---|
| 最终编译、优化后 EditMode/PlayMode、完整 Editor 检查 | **PENDING**：填写版本、通过数与证据 |
| 最终物理/合成输入飞行、朝向/相机/暂停/重开 | **PENDING**：填写真实执行范围和结果 |
| 最终 Unity Profiler 二进制采集与瓶颈结论 | **PENDING**：最终版本待核对；已有中间 raw 不作为最终通过 |
| 最终 Frame Debugger 实例绘制/Bloom/Flare 路径 | **PENDING**：最终版本待核对；不能从事件数推断 GPU 耗时 |
| 最终太阳遮挡、逆光可读性与截图 | **PENDING**：填写最终真实画面路径 |
| Windows 构建、入口、体积、Player 实际检查 | **PENDING**：独立于 Editor 结果 |
| 最终保全清单 | **PENDING**：最后一次修复与文档更新后复算 |

| 1080p 最终场景 | 平均 FPS / ms | P95 / P99 / 最大 ms | GPU / 显存实测 |
|---|---|---|---|
| 近景交互 | PENDING | PENDING | PENDING；未获得有效计数时写未测 |
| 中景舰阵 | PENDING | PENDING | PENDING；未获得有效计数时写未测 |
| 舰阵全景 | PENDING | PENDING | PENDING；未获得有效计数时写未测 |
| 连续爆炸 | PENDING | PENDING | PENDING；未获得有效计数时写未测 |
| 朝向太阳 | PENDING | PENDING | PENDING；未获得有效计数时写未测 |

每个阶段需注明 Editor/Player、画质、渲染比例、VSync/帧上限、焦点、样本数与时长。未测到的 GPU、显存或长期稳定性不得填零或称已达标。没有为安装体积添加无用资源，最终新增资源/构建容量为 **PENDING**。

## 保全与复查方式

中途对任务前 **818 个文件**重新计算 SHA-256：817 一致，唯一授权修改为 `DropletHitDetector.cs`，无缺失。114 个旧 ArtSource、60 个旧 Tools、包/ProjectSettings、旧场景/Prefab/美术及全部 328 个 `.meta` 保持原字节。旧构建目录存在，未用中途清单对其逐字节背书；最终以补充保全结果为准。没有升级 Unity/URP、安装重型插件或重做舰船、水滴及太阳系。

1. 打开交付场景，确认非 Play 状态已有规则舰阵。菜单 `DropletPrototype > Fleet 2000 Sun > 1 Create or Reimport Saved Scene` 可显式重导入；先保存自己的改动。菜单 `2 Inspect Saved Fleet` 检查身份、姿态和引用。
2. Play 后 Enter，从出生点保持向前并按 W+Shift，贯穿攻击列；观察每舰只计分一次，远处舰接近后完整显现，返回已毁舰位置无残留完整舰。
3. 查看近、中、远阵列，面向太阳确认舰船、水滴及 HUD 可辨认；让船体遮住太阳中央，比较耀斑变化。太阳大圆盘仍部分可见与光学耀斑被遮挡是不同现象。
4. 连续摧毁后 Esc 暂停，再 R 重开，确认 2000 目标、90:00、零分、出生方向、相机与特效池恢复。自然 90 分钟通关与长时间热稳定性仍需实测。
5. 开发复查菜单依次提供 `3 Start Rendered Validation`、`6 Live Input Flight Check`、`7 Capture Frame Debugger`、`8 Capture Unity Profiler`；运行时会暂时接管检查镜头/输入，应在独立验收会话使用。Profiler 与性能计时分开执行。

本轮范围止于 2000 舰阵、可达交互分级及太阳/阳光强化；最终待填项处理完后停止，不新增玩法或重做其他美术。
