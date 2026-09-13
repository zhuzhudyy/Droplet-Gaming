# 太阳系尺度重布局交付 — 2026-09-08

**试玩先打开 `Assets/_Project/Scenes/FleetAssault_SolarLayout.unity`，按 Play，再按 Enter。** 操作仍为鼠标转向、W/S 调速、Shift 冲刺、Space 刹车、A/D 平移、Esc 暂停、R 重开。40 舰、240 秒及战斗路线保留。

查看 Blender：`ArtSource/Blender/SpaceEnvironment/SpaceEnvironment_SolarLayout.blend`。场景下拉可切换 `SolarLayout_LocalBattle` 和 `SolarLayout_MacroOverview_DIAGRAM`；按小键盘 0 查看当前相机。局部默认 SpawnForward，宏观默认 MacroInnerTop，另有侧视与完整八轨道相机。宏观标签、十字、轨道线和480个样本均为编辑示意，不作为模型导出。

查看独立 Unity 环境：`Assets/_Project/Scenes/SolarSystemLayout_Review.unity`。可复用 Prefab：`Assets/_Project/Prefabs/Environment/SpaceEnvironment_SolarLayout.prefab`。全部模型、LOD 和基础摆放已保存，在 Edit 模式存在。选择环境根的 Inspector 可定位太阳/地球代理；地球默认尺寸不足一像素，这不是缺失模型。

独立 Windows 验证版：`Builds/Windows-SolarLayout-Validation-20260908/DropletPrototype.exe`，需保留同目录数据文件。它是本地验证构建，未发布、未覆盖旧构建。Editor 最终停在新版试玩场景，未进入 Play，没有未保存场景。

## A. 太阳系结构依据

| 对象 | 圆轨道半径/设计包络 |
|---|---:|
| 太阳 | 宏观参考原点 |
| 水星 / 金星 / 地球 / 火星 | 0.387 / 0.723 / 1.000 / 1.524 AU |
| 主小行星带 | 2.1–3.3 AU |
| 木星 / 土星 / 天王星 / 海王星 | 5.203 / 9.537 / 19.189 / 30.070 AU |

采用用户给出的近似半长轴，以 [JPL 行星近似位置表](https://ssd.jpl.nasa.gov/planets/approx_pos.html)核对；本轮没有执行该表的真实星历算法。JPL 表的地球条目为地月质心，本游戏把地球简化为1 AU对象。AU采用 [JPL 定义](https://ssd.jpl.nasa.gov/faq.html)的149,597,870.7 km；太阳半径695700 km依据 [NASA 术语表](https://science.nasa.gov/universe/glossary/)，地球半径取6371 km，参考 [JPL 平均半径表](https://ssd.jpl.nasa.gov/planets/phys_par.html)。

主带围绕太阳，位于火星与木星之间，径向有宽度，样本倾角0–8°、升交点分散。NASA Dawn资料描述主要区域约2.2–3.2 AU且非常稀疏，本游戏2.1–3.3 AU为稍宽设计包络。[NASA Dawn FAQ](https://science.nasa.gov/mission/dawn/faq/)

宏观图内太阳、地球、主带和战区共用配置及坐标系；地球轨道没有混入主带。只有太阳和地球使用既有实体天体模型，其他行星仅做编辑示意。未制作七套新模型。

## B. 游戏选择、坐标映射与显示代理

唯一权威是 `Tools/Blender/SolarSystemLayout/layout_config.json`，固定种子908260。宏观数据用AU和double；局部Unity坐标以米为单位。圆轨道使用X/Z平面、Y为上；相位与升交点从+X向+Z增长，正倾角使+Z半轨道抬升。Blender对应(x,z,y)基变换；岩石 `rotationDeg` 是 **Blender XYZ Euler**，Unity只读经过FBX转换后的完整姿态，不重解释为Unity Euler。

BattleAnchor选在2.5 AU、相位230°、倾角2°、升交点0°。其宏观位置约(-1.606969, -0.066836, -1.913944) AU，映射到原战场中心(0,20,260)米。所有相位、节点和本轮倾角分布是固定布局选择，不是原著准确坐标、当前星历或引力模拟。其他行星不共线。

实际观察者来自相机位置：

```text
observerAU = BattleAnchorAU + (cameraMetres - localOriginMetres) / 1000 / auKm
deltaAU = bodyAU - observerAU                 // double完成相减和归一化
angle = 2 * asin(radiusKm / distanceKm)
proxyDistance = 14000 + 4000 * distanceAU / (distanceAU + 1)
proxyPosition = cameraMetres + normalizedDelta * proxyDistance
proxyRadius = proxyDistance * radiusKm / distanceKm * readabilityMultiplier
```

球模型源半径为1；可读性倍率独立且默认 **1**。不合法半径、距离或非有限值会禁用相应Renderer，不向Transform写入NaN。代理深度随真实距离单调增加，保留太阳/地球远近关系；所有局部岩石都位于天体层之前。

从出生点附近看，太阳距离约2.5 AU，角直径 **0.213162°**；地球约3.456486 AU，角直径 **0.00141189°**。居中理想球在1080高、65°视野下的直径分别约 **3.15像素 / 0.021像素**（解析计算，非截图像素分割实测）。太阳实际代理距相机约16857米、半径31.36米；地球约17102米、半径0.211米。它们不能作为可飞抵的天体。

远景层在 ChaseCamera 更新之后重算，也在绑定相机渲染前同步。映射使用世界方向，不读取相机旋转去固定屏幕位置。天空只平移中心、保持方向；岩石保持固定世界位置，不随玩家补齐。天空壳22000米，far clip25000米；玩家/舰船/环境根没有统一放大。近裁剪仍0.1、战斗FOV仍65、跟随距离8米和高度2.2米。

已实现宏观背景/编辑总览至海王星约30 AU；实际连续飞行仅是下述局部球形地图。**没有真实尺度无缝太阳系、跨太阳系旅行、跃迁或原点重定位。**

## C. 实际飞行范围与战斗保留

| 项目 | 原FleetAssault | 新SolarLayout |
|---|---:|---:|
| 活动球半径 | 730 m | **5840 m，8倍** |
| 活动球直径 | 1460 m | 11680 m |
| 警告半径 | 610 m | 4880 m |
| 最高巡航 / 冲刺 | 52 / 156 m/s | 52 / 156 m/s |
| 中心至边界匀速巡航估算 | 14.04 s | 112.31 s |
| 中心至边界匀速冲刺估算 | 4.68 s | 37.44 s |
| 出生点朝+Z至边界的距离 | 约989.90 m | 约6099.99 m |

上表时间未计加速、刹车、转向。首舰中心仍离出生点100米；初速24 m/s时匀速约4.17秒，52 m/s约1.92秒。40舰中心范围335×135×420米，距战场中心最远约266.39米；舰船、碰撞体、水滴尺寸和目标路线未放大。

原MissionController已经从settings读取警告/回收条件，因此新场景绑定独立 `FleetMission_SolarLayout.asset`，同步motor、hit detector、score、camera和mission五处引用。只改变该配置的两个边界半径。安全回收仍到原出生点(0,8,0)，朝战场中心；调用既有ResetPose清理运动前后缓存、输入/扫掠、尾迹与相机。目标引导原本无最大距离限制，已实见4000米以上背后目标提示。菜单位置文案在新副本改为MAIN BELT，旧场景默认EARTH ORBIT保持原样。

**真实保存场景飞行实测**：通过临时合成键鼠设备驱动原Input System，真实FixedUpdate、正常TimeScale，从出生点持续W+Shift飞行；没有为路途加速时间或直接挪到新边界。约7.82秒越过730米，34.44秒出现新警告，40.66秒回收。采样最远 **5837.799米 / 7.996985倍**；采样离散步长为20ms，触发球半径为5840米。途中实际贯穿4舰、1000分；回收后保留，重开恢复40舰/0分/240秒。另有0.02秒逐步测试覆盖临界回收，重定位路径不误击测试通过。

证据：[实际键鼠与飞行记录](verification/SolarSystemLayout/live-input-flight.json)、[越过旧边界](verification/SolarSystemLayout/Game-BeyondOldBoundary.png)、[新边界警告](verification/SolarSystemLayout/Game-NewBoundaryWarning.png)、[安全回收](verification/SolarSystemLayout/Game-SafeReturn.png)、[暂停](verification/SolarSystemLayout/Game-Paused.png)、[重开](verification/SolarSystemLayout/Game-Ready-Restarted.png)。这些Game图片已经实际打开观察。

## D. 稀疏程度、画面和资源

| 环境资源/实例 | 旧几何环境 | 本轮 |
|---|---:|---:|
| 独立近中景岩石 | 12+160=172 | **32**：3参照、9中区、20外区 |
| 远景组合分区 | 8（共720小轮廓） | **0** |
| 环境布局实例（含2天体、天空） | 183 | **35** |
| 唯一Mesh，含所有LOD | 37 | **29**，全部引用旧资源 |
| 唯一三角形，含所有LOD | 38656 | **24256** |
| 共享材质 / 贴图 | 5 / 2 | 5 / 2 |
| Editor唯一Mesh原生大小 | 3590288 bytes | **1331216 bytes** |
| Editor唯一Texture原生大小 | 5681680 bytes | **5681680 bytes** |
| Mesh+Texture API合计 | 8.842 MiB | **6.688 MiB** |

旧几何环境是之前独立的SpaceEnvironment_Review；原FleetAssault使用另一套地球/星点背景，本轮仅在其副本移除那两个明确的旧背景对象，保留原照明。表格的资源前后比较针对几何环境版本，不能说原FleetAssault本来就有172块岩石。

岩石实例减少81.4%，不按扩大后的体积补数。配置要求最小中心间距650米；生成/重开检查实际净空和间距见 [Blender检查](verification/SolarSystemLayout/blender-selfcheck.json)。舰队核心保留半径800米的无岩石净空，包含原战斗路线和跟随相机余量。战区外是稀疏装饰区，岩石不参与物理、命中或计分；任意主动贴近装饰岩石仍可能穿过其表面，这是既有无碰撞背景语义。

正常出生视点只有少量小轮廓；反向、远离舰队后仍以开放空间为主。没有透明雾、尘埃、发光粒子、新星点或新高分辨率贴图填充空白。旧星空贴图复用，没有新增密集石带。75%–90%留白仅作设计检查起点，**本轮没有自动测得屏幕空白百分比**，也没有把黑色岩石当空白计数。

同一1280×720分辨率、FOV、相对于舰队位置的对照：

- 舰队FOV65、相机(0,10.2,-8)：[修改前](verification/SolarSystemLayout/Before-Fleet-Spawn.png) / [修改后](verification/SolarSystemLayout/After-Fleet-Spawn.png)。水滴/舰船相机参数相同，地球不再占据左侧大面积画面。
- 环境FOV55、旧ReferenceCamera(0,20,-380)：[旧环境](verification/SolarSystemLayout/Before-Environment-Reference.png) / [新环境](verification/SolarSystemLayout/Unity-ReferenceCamera.png)。旧太阳/地球屏高构图目标13.5%/17.5%已废止，新版本由真实角大小决定。
- [出生反向](verification/SolarSystemLayout/Unity-SpawnReverse.png)、[舰队外围](verification/SolarSystemLayout/Unity-FleetOutskirts.png)、[远处正向](verification/SolarSystemLayout/Unity-OpenSpaceForward.png)、[远处反向](verification/SolarSystemLayout/Unity-OpenSpaceReverse.png)。八个Unity相机图均已打开检查。
- 200米平移：[视差A](verification/SolarSystemLayout/Unity-ParallaxA.png) / [视差B](verification/SolarSystemLayout/Unity-ParallaxB.png)。实际相机投影中近岩约199像素位移；太阳float显示坐标位移测为0，double计算测试确认微小非零天文视差。这个0不表示宏观位置跟着镜头旋转。

实际原生LODUtility检查通过岩石LOD0/1/2/剔除，八个视点查询已记录。视锥内选中LOD的三角形资源估算约640–1920（不含未分组太阳/天空），是原生LOD选择加资源计数，**不是Frame Debugger实际GPU提交量**。本轮未重新做GPU逐事件抓帧；旧任务的绘制量不能搬作新版实测。诊断剔除距离超过常规活动球的部分已明确标注。[原生LOD与视差记录](verification/SolarSystemLayout/spatial-native-lod.json)

两张贴图仍为2048×1024、DXT1、12级mip、Read/Write关闭；网格Read/Write关闭，没有每实例Mesh/Material复制或静态批处理复制。64 MiB仍仅为环境Mesh+Texture参考上限。上述内存来自实际Editor/Editor Play Mode `Profiler.GetRuntimeMemorySizeLong`；包含全部LOD。LOD切换不代表卸载。这不是磁盘大小，也不是可归因显存。

独立Player可见运行的全局Unity分配器统计约192.0–193.7百万字节、保留290.5百万字节，来自 `Profiler.GetTotalAllocatedMemoryLong/GetTotalReservedMemoryLong`，不是操作系统进程工作集。它包含游戏/音效/界面等，不能与环境独占内存混算；GC逐帧计数器不可用，环境可归因Player内存/显存未单测。短时1080p前台正常/密集贯穿采样P95约1.85/2.11ms，仅为本机本次路径，未测GPU耗时、长时间游玩或跨硬件性能。

## E. 验证、修复、未测项

已执行并有结果：

1. Blender5.2.1 LTS实际生成、保存、独立进程重开，宏观俯视/侧视/完整总览，局部八方向，共享网格、净空、间距、固定配置及网格未改检查。原型网格来自上一版已检查的库，未重新雕刻。六张最终Blender关键视图有与源SHA256绑定的视觉放行记录。
2. 实际FBX导出32个小姿态标记，0个新模型/贴图导出；沿用已有不对称校准物和完整轴向变换。Unity逐项比对位置、forward、up、正缩放及共享引用。
3. Unity6000.5.10f1实际编译通过；新增数学EditMode **7/7**，完整PlayMode **30/30**。未运行会重写TestRange的旧SceneAuthoringTests。[数学测试](verification/SolarSystemLayout/editmode-math.json)、[PlayMode](verification/SolarSystemLayout/playmode-regression.json)
4. 两次重新导入、重复生成、外部手工哨兵保留、移除仅本次哨兵、关闭重开Review/Playable。40舰全套变换、碰撞体、共享网格与旧场景逐项一致；除了两个边界字段，全部序列化调参相同。[记录](verification/SolarSystemLayout/repeat-reopen-combat.json)
5. 新场景实际Input System持续飞行、边界提示/回收、刹车/鼠标转向、暂停、重开、计分及441个池对象稳定；旧高速多目标、初始重叠、满查询缓冲、复合碰撞去重和传送不误击回归通过。无新增原点重定位。
6. Windows StandaloneWindows64/Mono实际构建成功，103.814秒、0错误/1个既有可选Pipeline开发桥未配置警告；不是URP缺失。[构建报告](verification/SolarSystemLayout/windows-build.json)
7. 可见独立Player的20条继承记录通过（含7条截图写入），实际显示开始/普通飞行/40舰胜利及三次重开，截图已打开观察。[独立结果](verification/SolarSystemLayout/PlayerVisibleBenchmark/20260908-061837-942/benchmark.json)、[开始](verification/SolarSystemLayout/PlayerVisibleBenchmark/20260908-061837-942/01-ready.png)、[飞行](verification/SolarSystemLayout/PlayerVisibleBenchmark/20260908-061837-942/02-flight.png)、[胜利](verification/SolarSystemLayout/PlayerVisibleBenchmark/20260908-061837-942/04-results.png)。继承benchmark会在战斗检查中使用明确的ResetPose攻击起点，不能把它当连续飞到新边界的证据；边界以第5项独立记录为准。

实施中实际修复：Blender最后一个默认场景不能直接删除；UTF8 BOM读取；Unity Editor程序集显式引用已安装URP；Unity对象假null导致的LOD组件创建；修改assetId后更新自有LOD实例；构建入口要求权威/导出/导入哈希一致。增加Blender本地编辑回传防护，拒绝未回传的姿态差异及共享模型改动。

首次隐藏独立窗口只生成黑帧，虽然继承benchmark写了通过，本轮明确否决其渲染与性能结果；保留 [否决记录](verification/SolarSystemLayout/hidden-player-render-rejection.json)，以上只采用后续可见窗口结果。工具偶发域重载连接中断、Roslyn编码错误，以及截图工具不接受上级目录；通过编译后Editor菜单/临时脚本和明确输出路径完成，失败调用不算通过。

仍未验证：用户真实物理键鼠的主观手感/审美、听感、长时间自由飞行、跨硬件/分辨率矩阵、环境可归因显存、独立Player环境资源逐项内存、GPU逐事件绘制量和GPU耗时。未完成真实星历、重力或大世界旅行，这些不属本轮目标。主观的75%–90%留白没有伪装成测量。

保全：本轮开始先备份已有Scenes/Data，并对Assets、Tools、ArtSource、ProjectSettings、Packages建立哈希；旧场景（包括本轮开始状态的TestRange）、旧.blend、旧模型/材质/贴图和已有.meta不变。原PC_RPAsset在本轮经Unity原生处理写回，默认Renderer仍为0、资产/管线/包版本不变；未保存其任务前字节副本，不能给出可靠逐字段差异或声称它字节未变。其他原文件预期变更仅HudPresenter的默认保持不变的可配置位置文案及Editor asmdef对既有URP程序集的引用。[逐文件清单](verification/SolarSystemLayout/preservation-final.json)

历史STATUS记录上轮TestRange被旧测试改写且未找回原备份；本轮未覆盖该问题，也未声称恢复到更早版本。旧构建目录未写入。本轮没有重新建项目、升级工具、安装包、制作新光效或自动发布。

## 可调整与复现入口

主要新增文件：`SolarLayoutData.cs`、`SolarSystemBackdrop.cs`（运行时）；`SolarLayoutPipeline.cs`、`SolarLayoutInspector.cs`、`SolarLayoutVerification.cs`（Editor）；`SolarLayoutMathTests.cs`、`SolarFlightTests.cs`（测试）；`Tools/Blender/SolarSystemLayout/`（权威配置与工具），以及本文开头列出的源/Prefab/场景和 `FleetMission_SolarLayout.asset`。导入目录 `Assets/_Project/Art/Environment/SolarSystemLayout/` 仅含布局标记、配置、清单，没有重复模型。新增导出位于 `ArtSource/Blender/SpaceEnvironment/SolarLayoutExports/`，源备份是新源同目录的 `.before-<hash>.blend` 文件。本轮文档为TASK、REPORT，追加STATUS、ENVIRONMENT及HANDOFF；历史原文保留。[新增及变更文件清单](verification/SolarSystemLayout/changed-files.json)

先保存自己的Unity场景。保持 `layout_config.json` 为唯一权威；JSON导出副本与FBX用于Unity消费/校验，不应各自手工改一套位置。

```powershell
python Tools/Blender/SolarSystemLayout/run.py build
python Tools/Blender/SolarSystemLayout/run.py inspect
# 实际打开检查生成图，再更新与新 .blend SHA256 一致的 blender-visual-review.json
python Tools/Blender/SolarSystemLayout/run.py export
```

`build`只在独立background/factory进程运行，匹配生成收据才重建，保留带hash的旧源副本；手改源拒绝覆盖。支持的Blender局部岩石位置/旋转/均匀缩放手改可用 `run.py adopt-local` 显式回写权威配置：它备份配置及已编辑.blend，保留其他源内容、更新配置哈希，再运行inspect/视觉检查/export。该手改版本随后禁止整文件build重生成，防止抹掉其他手工内容；需全量重生成时在独立项目副本中操作。此回传接口已代码审查，未以真实用户手改文件做端到端验收。

Unity菜单 `DropletPrototype > Solar Layout`：1导入、2保存环境及检视、3保存可玩副本、4资源审计、5检视截图、6重复导入/重开/战斗对比、7在Play中的临时键鼠飞行检查。工具拒绝未保存场景和不一致的配置/源/导出。原生LOD工具可通过 `Tools/Blender/SolarSystemLayout/SolarSpatialProbe.cs` 的Run入口执行，只操作诊断相机并恢复。

手工复查建议：新版场景Play→Enter→W+Shift持续向前（约41秒回收）→观察警告/背后目标距离→Esc暂停→R重开；再转向观察小太阳、近岩视差和大片空白。源宏观场景检查主带围太阳，切换侧视看厚度；不要为了在一张截图中显示地球而放大它。

本轮到此停止；下一步仅为用户检查本轮布局和手感，不自动进入光效制作。
