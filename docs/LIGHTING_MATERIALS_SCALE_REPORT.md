# 天体尺度、太阳照明与材质升级交付 — 2026-09-08

**打开 `Assets/_Project/Scenes/FleetAssault_Lighting.unity`，按 Play，再按 Enter 或点击开始按钮即可查看效果并试玩。** 这是以最新稳定版 `FleetAssault_Expanded.unity` 为基础保存的新场景。120 艘静止可击毁星舰、540 秒任务保持；鼠标转向，W/S 调速，Shift 冲刺，Space 刹车，A/D 平移，Esc 暂停，R 重开。

先看出生点的水滴与前方舰队，再绕到第一艘星舰尾部观察 1 主、4 辅助引擎；朝太阳转向检查高光与背光面。地球在正常游戏视野中仍小于一个像素，属于远景尺度的预期结果。材质近照中的地球特写是明确标记的诊断视图，不能用作游戏比例证据。

**独立运行：打开 `Builds/Windows-Lighting-20260908/DropletPrototype.exe`，按 Enter 开始。** Windows 64 位构建成功，已实际启动并完成独立运行验证；详细记录见第 6 节。

## 1. 天体显示大小与距离关系

本轮检查的起点是扩编后的太阳系场景。它已经使用 AU/double 宏观位置与远景球体代理，保留了此前太阳系布局修正；本轮没有把舰船、水滴或战斗空间整体缩放。

宏观假设沿用战区位于 2.5 AU、相位 230°、倾角 2°的主小行星带附近。太阳位于宏观原点，地球处于约 1 AU 轨道；该固定相位布局不是当前星历或引力模拟。原始 `SolarLayoutConfig.json` 保留，新建 `SolarDisplayConfig.json` 只改显示倍率。

| 对象 | 物理角直径 | 原显示倍率 | 本轮显示倍率 | 本轮显示角直径 | 1080p、65° FOV 居中解析直径 |
|---|---:|---:|---:|---:|---:|
| 太阳 | 约 0.213162° | 1 | **2** | 约 **0.426324°** | 约 **6.31 px**，原约 3.15 px |
| 地球 | 约 0.00141189° | 1 | **1** | 约 **0.00141189°** | 约 **0.021 px** |

这里的像素值是球体居中投影的解析估算，不是截图像素分割实测。太阳的 2 倍艺术倍率用于让远方主光源保持可辨识；地球没有增加可读性倍率，没有重新变成压迫战区的近景大球。物理半径继续为太阳 695,700 km、地球 6,371 km，物理角大小保存在投影结果中，不被艺术倍率覆盖。

太阳代理距观察者约 16,857 m、显示半径约 62.71 m；地球代理距观察者约 17,102 m、半径约 0.211 m。太阳另有半径为显示太阳 5 倍的低强度透明日冕壳，它是辉光范围，不是太阳实体尺寸。星空壳半径 22,000 m，镜头近/远裁剪继续为 0.1/25,000 m；默认战斗 FOV 65°、镜头跟随距离 8 m、高度 2.2 m 不变。玩家的已保存 FOV 偏好仍由原设置系统读取。

舰长约 22.14 m、舰宽约 7.38 m、水滴缩放、120 个目标的身份/位置/旋转/命中体积、活动半径 5,840 m、警告半径 4,880 m、52/156 m/s 巡航/冲刺均保留。远景代理依观察者位置重算世界方向，不随相机转向粘在屏幕上；局部岩石位置保持。

最终三类视点对照：[出生点修改前](verification/LightingUpgrade/Editor/Before-20260908-112226-197/02-spawn-sun-facing.png) / [修改后](verification/LightingUpgrade/Editor/After-20260908-112306-011/02-spawn-sun-facing.png)，[舰队外围修改前](verification/LightingUpgrade/Editor/Before-20260908-112226-197/03-outskirts-sun-facing.png) / [修改后](verification/LightingUpgrade/Editor/After-20260908-112306-011/03-outskirts-sun-facing.png)，[开放空间修改前](verification/LightingUpgrade/Editor/Before-20260908-112226-197/04-open-space-sun-facing.png) / [修改后](verification/LightingUpgrade/Editor/After-20260908-112306-011/04-open-space-sun-facing.png)。自动数学检查也覆盖出生、外围和开放空间三个位置。

## 2. 太阳主导的照明

`SolarLightingRig` 读取同一套太阳投影方向，把主 Directional Light 的 forward 设为太阳方向的反向，并指定为 `RenderSettings.sun`。相机旋转不改变世界照明方向。旧 `Cold key`、`Blue fill`、`Rim` 三盏定向灯在新场景内禁用；原稳定场景中的灯保持。

| 设置 | 最终值与用途 |
|---|---|
| 主光 | 强度 **2.15**，颜色 RGB **(1, 0.945, 0.84)**，稍暖的太阳光 |
| 阴影 | Soft，强度 0.95，bias 0.035，normal bias 0.3 |
| 环境补光 | Flat，RGB (0.48, 0.61, 0.8) × **0.075**；只为背光面保留弱可读性 |
| 后处理 | HDR + ACES；Bloom intensity **0.16**、threshold 1.35、scatter 0.55、clamp 7 |
| 太阳本体 | HDR 自发光 RGB (5, 3.15, 1.1)，球面临边变暗、低幅度表面变化 |

这些强度是当前 URP 的渲染参数，未宣称经过物理照度或显示器亮度校准。舰船、水滴、岩石和地球使用相同主光方向；岩石 Renderer 重新启用投射/接收阴影。既有 PC 管线 **50 m 阴影距离**保留，因此远处舰船/岩石以表面受光明暗为主，不能声称整片 5.84 km 战区都有实时投影阴影。

太阳通过球面自发光、日冕薄壳和受控 Bloom 显亮，没有新增体积雾或强镜头耀斑。关闭后处理后，引擎结构与太阳实体仍由几何及 Shader 自身绘制。[不带后处理的尾部视图](verification/LightingUpgrade/After-EngineRear-NoPost.png)

## 3. 主引擎与四个辅助引擎

新 `FusionFrigate_Lighting.prefab` 保留原 FBX、9 个独立简单命中体积及校正后的 5 个尾部挂点。在 `VisualRoot/FusionDriveEffects` 下接入已保存的效果几何，全部进入原来的唯一 LODGroup。

每个喷口使用有厚度的球形能量内核；近景额外增加外层能量壳和薄曲面喷口内衬。内核以冷蓝边缘、较集中的暖白热区和低频循环纹理形成深度，表面有轻微脉动；辅助引擎使用同样的视觉结构，径向尺寸约为主引擎的 0.445 倍。没有长喷射柱、粒子流体、逐舰实时点光源或新 VFX Graph。

喷口内壁亮区来自共享发光内衬几何，是局部受热的视觉近似，没有伪称实时点光源照亮周边装甲。全部效果复用现有 `Earth_LOD2.fbx` 中的低面数球网格，新增 **3 个共享材质**，不生成逐舰 Mesh 或 Material 实例。

| LOD | 原舰体 Renderer | 本轮效果 Renderer | 合计 |
|---|---:|---:|---:|
| LOD0 | 13 | 5 内核 + 5 能量壳 + 5 内衬 | **28** |
| LOD1 | 13 | 5 内核 | **18** |
| LOD2 | 10 | 5 内核 | **15** |

每舰保存 25 个效果 Renderer，其中 15 个内核分布于三档 LOD；这不表示 25 个会同时绘制。新增几何继承原生 LOD/剔除；`FusionDriveVisuals` 只在质量变化时设置附加可见性遮罩，无逐舰 Update、材质实例化或物理权威。ShipTarget 摧毁时关闭整个 VisualRoot，所有 LOD 与火球一起隐藏；重开恢复原对象及选定质量。

最终尾部、三分之四和远距对照：[尾视前](verification/LightingUpgrade/Editor/Before-20260908-112226-197/05-engine-rear.png) / [尾视后](verification/LightingUpgrade/Editor/After-20260908-112306-011/05-engine-rear.png)，[三分之四后](verification/LightingUpgrade/Editor/After-20260908-112306-011/06-engine-three-quarter.png)，[远距后](verification/LightingUpgrade/Editor/After-20260908-112306-011/07-engine-far.png)。另有[水滴近距反射](verification/LightingUpgrade/Editor/After-20260908-112306-011/09-droplet-reflection.png)与[真实击毁后引擎关闭](verification/LightingUpgrade/Editor/After-20260908-112306-011/12-real-impact-engines-off.png)记录。

## 4. 五类表面材质

| 对象/材质 | 金属度 | 平滑度 | 实现 |
|---|---:|---:|---|
| 水滴 `MetalDroplet` | **1.0** | **0.985** | URP/Lit，冷银蓝基色；保留极光滑镜面金属表面，接收环境与局部反射 |
| 主装甲 `FF_Armor` | 0.82 | 0.65 | 明亮灰蓝金属，低幅表面颜色和平滑度变化 |
| 深结构 `FF_Structure` | 0.65 | 0.38 | 更暗、更粗糙的结构分区 |
| 引擎金属 `FF_EngineMetal` | 0.98 | 0.84 | 高金属度、较清晰的喷口及壳体反光 |
| 窗带 `FF_BlueGray` | 0.55 | 0.70 | 青蓝分区与克制的蓝色自发光 |
| 岩石 `RockBasalt` / `RockSlate` | **0** | 基值 0.12 / 0.10，变化限制在 0.06–0.16 | 高粗糙度，两档不规则色差、微法线扰动与原有几何坑洼/断面 |
| 地球 `EarthBase` | **0** | 陆地约 0.22，海洋约 0.86 | 复用原 2048×1024 地球贴图，以颜色启发式区分海洋/陆地/云；太阳受光侧加入微弱蓝色边缘光 |
| 太阳 `SunBase` | 不适用 | 不适用 | 独立自发光球面 Shader，保持炽热颜色与边缘层次 |

新 `SpaceSurface` Shader 使用安装版 URP 的 `UniversalFragmentPBR`、主光阴影及环境/探针反射路径。装甲与岩石的表面变化已改为两档平滑 value noise，分别形成低频斑驳与较细颗粒，替换会在表面产生规则条纹的周期正弦场。噪声按屏幕像素覆盖范围控制频率：远处或掠射角下无法分辨的细节逐渐退回均值，降低高频颗粒爬动和闪烁风险，没有逐帧随机重采样。

岩石微法线复用噪声同一组 8 个角点计算解析梯度，不另外多次采样高度场；梯度和切向投影先统一在物体空间计算，再把完成的法线转换到世界空间，避免旋转或非均匀缩放实例上的表面纹理方向不一致。岩石仍为金属度 0，平滑度上限 0.16。最新 Shader 已由实际 Unity 编译，向阳近照已确认不再呈现规则条纹：[修改前](verification/LightingUpgrade/Before-RockSunlitDetail.png) / [修改后](verification/LightingUpgrade/After-RockSunlitDetail.png)。这次视觉检查不等于所有距离、镜头角度和硬件上的闪烁都已排除。

地球的云层沿用同一张基础贴图，并非独立云几何或物理大气散射；实际游戏尺度下无法分辨大陆细节。

未增加 4K/8K 贴图，没有改变舰船模型、舰体 UV 或原 FBX 导入配置。新增太阳环境立方体为 256×256/面，局部实时反射为 128×128/面。共享岩石材质名称已经与原 Prefab 槽位对齐，避免只创建新材质却仍显示旧材质的接线错误。

## 5. 反射方案、质量开关与局限

最终采用 **定向 HDR 环境立方体 + 单个局部实时 Reflection Probe + PBR 镜面高光**。这是探针近似方案，未启用硬件光追或屏幕空间反射。实际机器为 RTX 4060 Laptop GPU、Direct3D12，硬件能力报告支持光追，但当前项目保持 Unity 6000.5.10f1 / URP 17.5.0；Unity 6000.5 功能表将 URP 的硬件光追反射及 SSR 标为不支持。本轮不迁移 HDRP、不安装第三方反射包。[Unity 6000.5 渲染管线功能表](https://docs.unity3d.com/6000.5/Documentation/Manual/render-pipelines-feature-comparison.html)

`SolarEnvironment.asset` 是太阳方向一致的 256 面分辨率 RGBAHalf HDR cubemap，替换新场景原有与太阳方向无关的宽冷光条反射。环境图中的太阳高亮经过有限展宽以适应低分辨率反射采样，是镜面反射的艺术近似；直接照明仍来自单一太阳主光。

局部探针只有 **1 个**：128 面分辨率、HDR、ViaScripting、IndividualFaces 分时更新、近/远裁剪 0.3/220 m、有效影响盒 500 m、混合距离 80 m、box projection 关闭。普通移动满足至少 25 m、且距离上次启动至少 3 秒才重捕获；毁船与重开会请求更新，并仍遵守捕获间隔和上一轮完成检查。探针能捕获附近舰体和环境，排除水滴及临时效果所用的 layer 2，以免自我反射或把一次性效果留在环境镜像里。

捕获期间不展示半更新的立方体。代码使用一个共享 Cube RenderTexture，等待 `IsFinishedRendering` 后才发布；失效时清空 `realtimeTexture` 并把影响盒置零，回退至太阳 HDR 环境。置零影响范围是必要的：实际 Unity 检查发现仅清空引用仍可能保留原生纹理后备，不能只凭字段为空宣称旧镜像消失。捕获中若再次毁船，结果会舍弃并重捕获。完成时显式通知纹理更新，供 URP 反射图集刷新。[RenderProbe API](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/ReflectionProbe.RenderProbe.html)

初次真实 Play 测试发现项目两档 QualitySettings 均关闭实时反射探针，导致捕获不完成；已修复为只在本场景运行时开启并在组件退出时恢复原值，没有把失败的 15 秒等待记录作为通过。最终测试实际等到了完成的捕获，并验证毁船/重开的即时失效。

| 质量/开关 | 引擎 | 后处理 Volume | 局部实时探针 |
|---|---|---|---|
| High | 内核、近景壳层及内衬 | 开 | 开 |
| Low | 仅各 LOD 的内核 | 开 | 关，保留环境反射 |
| Off | 引擎效果隐藏 | 关 | 关，保留基础材质与环境反射 |

`GeneratedLightingUpgrade` 上的 `SolarLightingRig` 还提供 `engineEffects`、`bloomEnabled`、`enhancedReflections` 独立开关。`bloomEnabled` 控制本轮整个后处理 Volume，因此同时关闭该 Volume 中的 ACES；运行时开关不会更改碰撞、计分或任务设置。

局限：探针是附近场景的低频快照，受捕获位置、220 m 范围与 3 秒间隔限制；不提供逐像素正确的镜面遮挡、自反射或多次反弹。更新期间会短暂回到环境反射，不能称为无延迟光追。远距火球只保留小内核，极远距离受正常像素/LOD 剔除限制。

## 6. 实际性能与运行证据

同机测量为 Windows 11、Ryzen 9 7940HX、RTX 4060 Laptop GPU、D3D12、1920×1080、PC/High、VSync 0、targetFrameRate -1。普通近景、全景、贯穿阶段各 45 帧预热、300 帧采样；近景相机 (19,18,70)，舰队全景相机 (950,720,-1150)。贯穿阶段用独立的安全接近位置加原 Motor 的 0.02 秒步进，不是人工连续飞行路线计时。

最终选择 [Before-20260908-112226-197](verification/LightingUpgrade/Editor/Before-20260908-112226-197/lighting-validation.json) 和采用最新表面 Shader 的 [After-20260908-112306-011](verification/LightingUpgrade/Editor/After-20260908-112306-011/lighting-validation.json)。两次均 `completed=true`、`allChecksPassed=true`；Before 为 28 条通过检查、17 张截图，After 为 **32 条通过检查、19 张截图**。以下按实际 `focusedFrames` 标明采样期窗口焦点，保留不利结果，不从不同轮次挑选更快数字。

| Editor 视角 | 修改前均值 / P95（ms） | 修改后均值 / P95（ms） | 修改前 / 后 focused 帧 |
|---|---:|---:|---:|
| 近景舰船 | 6.180 / 6.404 | **6.178 / 6.401** | **300/300 → 300/300** |
| 舰队全景 | 6.190 / 6.442 | **7.826 / 9.341** | **300/300 → 300/300** |
| 贯穿战斗 | 6.726 / 12.320 | **5.694 / 7.886** | **300/300 → 0/300** |

同为前台的近景 P95 基本持平；全景 P95 本次约增加 **45.0%**，且修改后全景 P99 为 **82.552 ms**，存在明显长帧，不能据此宣称稳定 60 FPS。贯穿阶段修改后窗口失焦，其较低帧时间不能解释为性能改善。表中的计时均为含 Editor 成本的整组结果，未隔离 Shader、透明引擎或探针各自占比。

| Editor 视角 | allocated 修改前 → 后（MiB） | reserved 修改前 → 后（MiB） | mono used 修改前 → 后（MiB） |
|---|---:|---:|---:|
| 近景舰船 | **1053.54 → 1069.77** | 1711.14 → 1721.94 | 809.55 → 810.11 |
| 舰队全景 | **1053.87 → 1069.39** | 1711.14 → 1721.94 | 811.46 → 813.07 |
| 贯穿战斗 | 1054.66 → 1069.53 | 1711.14 → 1721.94 | 814.79 → 816.42 |

这些是整进程 Unity tracked memory 的实际读取值，包括导入、测试和编辑器缓存，不能将差值全部归因于新增反射纹理，也不等于显存。普通阶段仅约 1.7–2.35 秒，不保证每段覆盖完整的 3 秒探针更新间隔；新版近景实际记录 1 次捕获请求，全景和贯穿各为 0，完整更新成本另看下面较长的两段采样。

`Before-20260908-094614-432`、`After-20260908-110005-419`、`After-20260908-110428-568` 为较早中间版本；`Before-20260908-112001-356` 与 `After-20260908-112058-756` 为混合焦点的中间重测。它们保留在证据目录中，但不作为本报告最终前后表的数据来源。

最新验证器新增同一近景相机下的 `ReflectionRefresh` 与 `ReflectionsOff` 两段，各 **2400 帧**，用于比较持续请求更新与关闭局部反射的运行代价。`ReflectionRefresh` 每 120 帧请求一次失效重捕获，生产设置 `probeInterval=3` 秒保持，上一轮未完成时不重入；报告的 `probeCaptures` 记录采样期实际启动的捕获次数，不能用 2400/120 推算实际捕获数。该阶段是受控反射更新压力对照，不是正常玩家移动频率或连续战斗路线。

| 最新 Editor 反射阶段 | 帧数 / focused 帧 | 实际捕获请求 | 均值 / P95（ms） | allocated（MiB） |
|---|---:|---:|---:|---:|
| `ReflectionRefresh` | 2400 / **151** | **6** | **6.624 / 10.688** | 1071.33 |
| `ReflectionsOff` | 2400 / **0** | **0** | **4.608 / 5.793** | 1073.41 |

开启段约 15.90 秒、关闭段约 11.06 秒，确实执行了 6 次原生捕获请求；但两段窗口焦点不同，**不能把这两个均值之差当作探针的准确开销**。这组结果用于确认更新过程得到实际采样，精确归因仍受焦点和 Editor 成本影响。

`ReflectionsOff` 只关闭局部探针的捕获与应用，保留已经分配的捕获 RenderTexture、基础环境反射、材质、引擎和后处理。它不是释放反射资源后的内存对照。两段进程级内存差异不能解释为探针纹理显存大小；单个捕获的 CPU/GPU 逐事件成本和显存归因仍未隔离。

Windows 64 位构建输出为 `Builds/Windows-Lighting-20260908/DropletPrototype.exe`。[构建记录](verification/LightingUpgrade/windows-build.json)为 **Succeeded、0 错误、1 警告、26.877 秒、129,179,970 bytes**。警告指向可选的 `RuntimePipelineConfig` 配置缺失，并非 URP 渲染器失效；该构建随后实际使用 D3D12 完成全部验证。构建工具时间戳与运行证据相差 8 小时，原始记录保留，未修改时间戳以制造一致性。

独立版实际运行记录为 [Player/After-20260908-112539-586](verification/LightingUpgrade/Player/After-20260908-112539-586/lighting-validation.json)：**非 Development、非 batch、D3D12、1920×1080、PC/High、VSync 0、不限帧**。五段采样全部保持前台焦点，`completed=true`、`allChecksPassed=true`，**32 条检查全部通过、19 张截图写入**，验证器完成后正常退出。

| 独立 Player 阶段 | 帧数 / focused 帧 | 实际捕获请求 | 均值 / P95（ms） | allocated / reserved / mono used（MiB） |
|---|---:|---:|---:|---:|
| 近景舰船 | 300 / 300 | 0 | **3.302 / 4.435** | **212.76 / 298.43 / 3.91** |
| 舰队全景 | 300 / 300 | 0 | **3.159 / 3.925** | **216.51 / 316.43 / 3.79** |
| 贯穿战斗 | 300 / 300 | 0 | **2.688 / 3.845** | 214.89 / 316.43 / 4.17 |
| `ReflectionRefresh` | 2400 / 2400 | **3** | **2.888 / 3.752** | 217.42 / 316.43 / 4.38 |
| `ReflectionsOff` | 2400 / 2400 | **0** | **2.871 / 3.702** | 217.27 / 332.43 / 4.77 |

独立版开启/关闭局部反射的均值相差约 **0.016 ms**，P95 相差约 **0.050 ms**；这只是同一新场景、同一视角、各约 6.9 秒、3 次实际捕获请求的短样本结果，不能作为探针逐事件成本或长期稳定帧率结论。普通三段各不足 1 秒，未覆盖探针刷新；贯穿段最大帧时间仍有 23.052 ms，反射开启段最大 20.741 ms。没有构建并测量同样 120 舰旧场景的独立 Player，因此**没有独立版修改前后提升结论**。Player 的 Main Thread 计数可用，GC 计数不可用；JSON 中该计数的零值不是零 GC 分配的证明。

已实际查看独立版[近景](verification/LightingUpgrade/Player/After-20260908-112539-586/performance-Near.png)、[舰队全景](verification/LightingUpgrade/Player/After-20260908-112539-586/performance-Panorama.png)、[水滴邻舰镜像](verification/LightingUpgrade/Player/After-20260908-112539-586/09-droplet-reflection.png)和[五喷口尾视](verification/LightingUpgrade/Player/After-20260908-112539-586/05-engine-rear.png)：渲染正常，没有大面积黑屏或粉色材质，邻舰镜像可见，五个内核具有层次。[Player 日志](verification/LightingUpgrade/Player/player.log)未发现运行期 Exception/Error；存在非阻塞原生日志 `d3d12: failed to query info queue interface (0x80004002)`，实际 D3D12 渲染与全部验证仍通过。

Editor 帧时间、Main Thread/GC 计数含编辑器自身成本；Unity tracked memory 是进程级统计，不能归因为本轮贴图显存或 GPU 耗时。截图写入及切换视点排除在采样之外。没有用估算、资产磁盘大小或 dotnet 编译代替 Player/GPU 性能验证。

## 7. 验证结果、已修复问题与未验证范围

| 检查 | 实际结果与证据 |
|---|---|
| Unity C# / Shader 编译 | 最新平滑噪声及岩石梯度 Shader 已由实际 Unity 编译，随后重新执行材质/资源与完整 PlayMode 检查；材质测试覆盖 shader 支持、ShaderHasError、错误粉色 Shader 与共享资产 |
| 新 EditMode | **5/5，通过，0 失败/跳过，4.26 秒**：[最新 Shader 后结果](verification/LightingUpgrade/editmode-shader-final-results.json) |
| 全部 PlayMode | **42/42，通过，0 失败/跳过，8.17 秒**：[最新 Shader 后完整结果](verification/LightingUpgrade/playmode-shader-final-results.json) |
| 新场景玩法保全 | 120 个目标身份、位置、缩放与原设置一致；真实 Motor 扫掠完成 120 舰并三次重开；材质/网格/效果对象和池数量不增长 |
| 五引擎生命期 | High/Low/Off × 三档 LOD 贯穿击毁、全部效果关闭、重开恢复；不影响命中体积与得分 |
| 主光/比例 | 三个位置的物理/显示角大小与世界方向通过；相机转向不带着光转；暂停保持游戏状态 |
| 反射 | 最终 Play 检查等到真实分时捕获完成，再验证摧毁立即撤回影响、重开失效、Low/独立关闭不调度捕获 |
| 实际 Input System 飞行 | [实测通过](verification/LightingUpgrade/live-input.json)：合成 Enter/W/Shift/Esc/R/Space/鼠标驱动真实 FixedUpdate，4.90 秒飞行约 532.04 m，最高 156 m/s，贯穿前 5 舰、1500 分，暂停/重开与 57 个池对象检查通过；不是物理键鼠主观验收 |
| 基线文件保全 | [最终快照对比](verification/LightingUpgrade/preservation-final.json)：609 个基线文件中 **4 条预期变化**，即两个程序集定义增加现有 URP/Core 引用，以及 `STATUS.md` / `ENVIRONMENT.md` 更新；其余 **605 个一致** |
| 保存场景及视觉 | 保存的新场景/Prefab 在 Play 前已存在；[最终 Editor 截图及流程检查](verification/LightingUpgrade/Editor/After-20260908-112306-011/lighting-validation.json)含 **19 张截图、32 条通过记录**；[最新向阳岩石近照](verification/LightingUpgrade/After-RockSunlitDetail.png)已确认无规则条纹；独立版同样完成 **32 条检查和 19 张截图**，实际观察见性能条目 |
| Windows 独立运行 | 构建成功后实际启动非 Development Player，120 舰贯穿至结算、暂停、三次重开与五喷口恢复通过，池保持 57；验证完成后正常退出，详见第 6 节原始记录 |

本轮实际修复了岩石新材质槽名不匹配、引擎内核大面积发白而缺少层次、实时探针全局质量开关关闭，以及清空探针引用仍有原生后备纹理。后续近照检查又发现装甲/岩石的周期条纹，已换为带宽受控的两档平滑噪声，并把岩石梯度与法线扰动统一在物体空间后再转换；实际 Unity 编译、向阳近照及重新运行的 5 项 EditMode / 42 项 PlayMode 确认通过。上面的最终表和截图目录对应修复后的材质，旧中间记录不混入最终结论。没有运行会调用 TestRangeBuilder 并重写 TestRange 的旧 `SceneAuthoringTests`；此前批次记载的 TestRange 历史保全问题继续保留，不在本轮擅自恢复。

尚未验证或不作通过声明：用户真实物理键鼠与主观手感/审美/听感；跨 GPU、分辨率和平台；长时间自由飞行/长时浸泡；可归因 GPU 时间及显存；地球物理大气、精确云层或真实空间辐射标定。自动脚本的成功不替代这些人工验收。

最终视觉观察和独立运行已完成，**没有阻碍本轮交付的工具问题**。上述可选 Pipeline 配置警告和 D3D12 信息队列查询日志保留在证据中；未测范围与短样本性能限制不作通过声明。

手工复核：打开新场景开始 → 绕尾观察 5 喷口 → 关闭后处理再观察结构 → 转向太阳比较舰船/水滴亮面 → 前往稀疏岩石区观察受光 → 击毁后检查尾焰清空 → Esc 暂停、R 重开确认全部恢复。诊断地球近照请与正常游戏尺度截图分开阅读。

## 8. 文件与资源清单

全部新场景/Prefab/配置/Shader/材质均附 Unity 生成的 `.meta`。旧 `.meta` 与原模型 GUID 保留；新文件使用新 GUID，未手写场景或 Prefab YAML。

| 新资产 | Unity GUID |
|---|---|
| `Assets/_Project/Scenes/FleetAssault_Lighting.unity` | `43ca104699d0fd549a939911de76b26d` |
| `Assets/_Project/Prefabs/Fleet/FusionFrigate_Lighting.prefab` | `a8f822cfebe944d41a6942db4de15792` |
| `Assets/_Project/Prefabs/Environment/SpaceEnvironment_Lighting.prefab` | `fb044e00d8ea8674d9b7fdd9446ae88b` |
| `Assets/_Project/Data/LightingQuality.asset` | `3bf860059fa548c46bd264ed7d92fbcb` |
| `Assets/_Project/Art/LightingUpgrade/SolarDisplayConfig.json` | `0995810a4a276824bbf2726fe42fa549` |
| `Assets/_Project/Art/LightingUpgrade/SolarEnvironment.asset` | `83ef90dc98f7c094e8516de58fb646c0` |
| `Assets/_Project/Art/LightingUpgrade/SolarPost.asset` | `0d5e278694b03a849bc8b8a3436e0035` |

`Assets/_Project/Art/LightingUpgrade/` 下本轮使用的 14 个共享材质：`MetalDroplet.mat`、`FF_Armor.mat`、`FF_Structure.mat`、`FF_EngineMetal.mat`、`FF_BlueGray.mat`、`RockBasalt.mat`、`RockSlate.mat`、`EarthBase.mat`、`SunBase.mat`、`SolarCorona.mat`、`SolarReflectionSky.mat`、`FusionCore.mat`、`FusionShell.mat`、`FusionLiner.mat`，各有同名 `.meta`。无引用的中间材质 `RockBase0.mat` / `RockBase1.mat` 已通过 AssetDatabase 删除，避免交付重复资源。

| Shader 文件，位于 `Assets/_Project/Art/Shaders/LightingUpgrade/` | Shader 名称 | Unity GUID |
|---|---|---|
| `SpaceSurface.shader` | `DropletPrototype/Lighting/SpaceSurface` | `96c40f3b7024a6c4c8709bdf2ad12167` |
| `SolarDisc.shader` | `DropletPrototype/Lighting/SolarDisc` | `dea94409d9f689e4fa40b4e288b4bc2c` |
| `SolarCorona.shader` | `DropletPrototype/Lighting/SolarCorona` | `9e83420dcb66177498821e9b38240647` |
| `FusionDriveCore.shader` | `DropletPrototype/FusionDriveCore` | `eec53d72a3291104aad92063ab223417` |
| `FusionDriveShell.shader` | `DropletPrototype/FusionDriveShell` | `186707d2a077c294598cf9569815dfcf` |

新运行时代码：`Scripts/Runtime/SolarLightingRig.cs`、`LightingQualityProfile.cs`、`FusionDriveVisuals.cs`、`LightingValidationRunner.cs`。新 Editor 代码：`Scripts/Editor/LightingUpgradePipeline.cs`、`FusionDriveAuthoring.cs`、`LightingUpgradeInspection.cs`、`LightingValidationCommands.cs`、`LightingLiveCheck.cs`。新测试：`Tests/EditMode/LightingUpgradeTests.cs`、`Tests/PlayMode/LightingUpgradePlayTests.cs`。以上相对路径均在 `Assets/_Project/` 下，各有同名 `.meta`。

既有代码配置只修改 `DropletPrototype.Runtime.asmdef` 与 `DropletPrototype.Editor.asmdef` 的安装版 URP/Core 程序集引用；没有新增包、升级 Editor、改变渲染管线或修改原玩法代码。原 Blender 源、FBX、稳定场景及旧构建保留。本文、`STATUS.md`、`ENVIRONMENT.md` 与 `docs/verification/LightingUpgrade/` 保存本轮交付记录。

显式重建菜单为 `DropletPrototype > Lighting Upgrade > 1 Create or Update Lighting Scene`，仅在停止 Play、无脏场景且关闭 Prefab Stage 时执行。普通试玩直接打开已保存的新场景，不需要运行生成菜单。新效果只在所属生成子树中维护，不需要重新运行旧 TestRange、太阳系或舰队布局生成器。

本轮到此停止，不自动开始新的玩法、移动 AI 或下一批资产开发。
