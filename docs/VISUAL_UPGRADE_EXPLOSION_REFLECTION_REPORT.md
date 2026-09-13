打开 [Assets/_Project/Scenes/FleetAssault_VisualUpgrade.unity](../Assets/_Project/Scenes/FleetAssault_VisualUpgrade.unity)，在 Unity 点击 Play，再按 Enter 开始试玩。鼠标转向，W / S 调速，A / D 横移，Shift 加速，Space 刹车，Esc 暂停，R 重开；沿出生点正前方加速可连续贯穿舰队并查看反应堆爆炸。

本轮是在现有正式可玩场景 `FleetAssault_Droplet_Rebuilt` 上制作独立视觉版本。旧场景、新场景和独立 Prefab 同时保留；没有改变舰队成员、计分规则、任务时长或操控参数。当前正式舰队场景原有任务时间为 **540 秒**，本轮保持这一现状；早期 G01–G04 的 120 秒批次记录不是当前舰队场景的配置。

日期：2026-09-08。环境保持 Unity **6000.5.10f1**、URP **17.5.0**、`PC_RPAsset` 和 Unity Test Framework **1.7.0**。本轮复用既有 **Blender 5.2.1** 源文件及导出网格，未运行 Blender 重建或重新导出；没有重做整套舰船，也没有升级 Editor、包或渲染管线。

也可以直接运行 [Windows 独立版 DropletPrototype.exe](../Builds/Windows-VisualUpgrade-20260908/DropletPrototype.exe)。实际 Windows 构建和修改前/后的独立运行检查均已完成，Editor 与独立运行结果分别列于第 8、9 项。

**1. 飞船尺寸与水滴比例**

以现有水滴网格为基准，将 FusionFrigate 的视觉、命中体和挂点子树分别放大 **2.6 倍**。各舰船根节点仍保持单位缩放，120 艘舰的身份、世界位置、方向和舰队间距保持原样。舰队根节点没有参与缩放。

| 对象 | 原尺寸，宽 × 高 × 长 | 新尺寸，宽 × 高 × 长 |
|---|---:|---:|
| FusionFrigate 舰体网格包围盒 | 7.380 × 6.212 × 22.140 m | 19.188 × 16.150 × 57.564 m |
| 水滴 | 0.800 × 0.800 × 2.400 m | 保持不变 |
| 舰长 / 水滴长 | 9.225 倍 | 23.985 倍 |

这些数值是场景/网格包围盒测量，不是从截图估算。新舰队最近中心距离约 **110.700 m**，保守世界包围盒之间最小间隙约 **53.136 m**。每舰 9 个独立命中体，共 1,080 个，随所属子树同步扩大；碰撞仍由原来的 `DropletMotor` 路径扫掠处理，不会把水滴挡停。

保留 3 级原生 LOD，重新计算放大后的 LODGroup 包围范围，屏幕高度阈值为 0.12 / 0.035 / 0.001，每级分别包含 28 / 18 / 15 个 Renderer。主引擎和四个辅助引擎仍存在于每级 LOD。尾部爆心使用 `Sockets/ReactorDetonationOrigin`，其放大前局部位置是 `(0, -0.05, -7)`，随 Sockets 子树一同变换，避免模型扩大而爆心留在旧位置。

证据：[场景审计](verification/VisualUpgrade/scene-audit.json)、[同深度舰船/水滴对照图](verification/VisualUpgrade/Editor/After-20260908-134145-326/01-ship-droplet-size.png)。该截图把水滴放在舰体投影边界外，并保持相同相机深度，避免被巨舰遮挡；没有临时放大水滴。

**2. 水滴从暗淡表面提升到镜面金属外观**

原场景已经使用金属方向材质，但在实际近景中，大部分曲面只得到很暗的蓝灰环境，舰船和火光反射不足，轮廓仅有小面积白色高光。因此只把 Metallic 调高并不能解决塑料感。原始表现已保存在 [修改前材质截图](verification/VisualUpgrade/Editor/Before-20260908-132623-784/02-chrome-before-explosion.png)。

新 `PerfectChrome` 保持现有重建水滴网格、法线、头尾方向和 0.8 × 0.8 × 2.4 m 尺寸。材质使用银灰基色 `(0.86, 0.88, 0.90)`、Metallic=1、Smoothness=0.985，保留 URP 的镜面 BRDF、环境反射和太阳直接高光，并新增有方向的太阳/反应堆/爆炸反射响应。没有给水滴加入表面噪点、粗糙法线或颗粒贴图。

新增 256 像素/面的 HDR 环境 Cubemap，提供中性的明暗带、暗部和与太阳方向一致的亮区，使镜面曲率有可反射的亮度结构。这是低频美术环境场，不是完整舰队照片，也没有把爆炸火光固定画进贴图。世界空间反射向量使高光随观察方向滑动。近处舰船、反应堆和爆炸另外通过下述动态来源参与反射。

最终截图中能看到连续的银色高光、暗色反射区和清晰曲面，没有粉材质或黑面错误。本轮没有重新拓扑水滴；保留原网格的长锥尾形状，其背向爆炸的真实镜面反光会天然集中到很小区域，这也是第 6 项需要增加可控近似响应的原因。

**3. 太阳光效和统一主光方向**

`SolarLightingRig` 根据现有太阳布局和观察位置，令唯一主 Directional Light 朝向太阳的反方向。舰船、水滴、岩石、地球使用同一个太阳受光方向。主光强度为 2.6、颜色 `(1, 0.98, 0.95)`，环境补光强度为 0.085；其余保存的补光没有成为同时启用的主光。

太阳本体使用 `SolarStar`：自发光光球、解析噪声生成的颗粒/活动区、从中心到边缘的亮度变化以及暖色边缘。保留已有低成本日冕结构。后期使用 ACES 和节制的 Bloom：Intensity=0.19、Threshold=1.25、Scatter=0.52、Clamp=8。没有新增全屏 Lens Flare，也没有用全屏过曝掩盖材质。

原太阳系布局和游戏中太阳的角尺寸不变，因此常规战斗镜头中的太阳仍较小，不能在几像素的日面上观察精细颗粒。[正对太阳的实际战斗图](verification/VisualUpgrade/Editor/After-20260908-134145-326/performance-FacingSun.png) 用于验证目标可读性；[太阳材质近看诊断图](verification/VisualUpgrade/Editor/After-20260908-134145-326/08a-solar-surface-DIAGNOSTIC-NOT-GAME-SCALE.png) 仅临时冻结代理映射并移动检查相机，清楚标记为 **非游戏比例画面**，没有把这个巨大日面保存进场景。

**4. 主反应堆与四个辅助引擎**

复用 FusionFrigate 已有的核心球体、蓝色外层、喷口金属和衬里。主反应堆和四个辅助引擎共享同一套材质语言；较小引擎保持较小尺寸，没有给每艘舰增加五盏灯或五套 ParticleSystem。

`ReactorCore` 用蓝白边缘、较集中的白热内核以及低频流动/轻微脉动表现受约束的高能等离子体。正常状态不会持续剧烈闪烁，也不会延伸成巨大蓝色喷焰。金属喷口的太阳高光、蓝色内圈与高亮核心形成材质分层。

击穿后，`ReactorDestructionPresenter` 用 MaterialPropertyBlock 提高 `_Instability`，核心短暂增强并失稳；0.16 秒后完整舰体和引擎一同退出显示。重开时恢复原来的属性块、舰体和引擎状态。正常运转没有每舰实时点光；局部高亮主要来自发光几何、材质和反射，爆炸期间才使用预算内的共享局部灯。

**5. 反应堆爆炸的四个阶段**

| 阶段 | 时间 | 实际表现 |
|---|---|---|
| 贯穿 | 命中瞬间 | 原有 Motor 扫掠提交摧毁和分数；命中点出现短促穿透闪光、方向性热火花 |
| 失稳 | 0–0.16 s | 已摧毁目标的纯视觉舰体短暂保留，反应堆/引擎增强；命中体已经关闭，不能再次计分 |
| 主爆炸 | 约 0.16–0.90 s | 从固定反应堆挂点爆出，多层火球、冲击环、沿舰体方向的能量喷涌、合并金属碎片和暖白强光 |
| 余波 | 约 0.90–2.70 s | 热残光与碎片逐步衰减；到期回收，无永久火球或常驻残骸 |

主爆心半径参数为 16 m，实际各层形状随时间扩展；这不是额外的伤害范围。爆炸只表现已被击穿舰船的毁灭，不新增范围伤害玩法。所有碎片是纯视觉几何，没有 Collider、Rigidbody 或 ShipTarget，不能挡住水滴或成为计分目标。

`ShipTarget` 的死亡、去重和计分不等待 VFX。特效关闭、池满或表现资源缺失时，玩法仍然成立。失稳时临时重新显示的子树也要先通过“纯视觉、无碰撞和无目标身份”检查。[最终主爆炸画面](verification/VisualUpgrade/Editor/After-20260908-134145-326/05-main-reactor-blast.png) 和 [120 次同帧命中压力画面](verification/VisualUpgrade/Editor/After-20260908-134145-326/10-bounded-120-hit-burst.png) 均来自真实 Motor 扫掠，不是直接调用摧毁接口摆拍。

**6. 水滴如何接收到爆炸火光**

`ReactorExplosionPool` 是唯一爆炸时钟。它把最多 4 个优先爆炸的世界位置、亮度、颜色和范围交给 `DropletReflectionResponse`，后者只更新唯一水滴 Renderer 的属性块。颜色按同一阶段由失稳蓝白过渡到爆炸橙白，再衰减；没有另起一个容易残留的火光计时器。

材质先计算世界空间反射方向上的光源亮斑和白热核心，再增加受爆心方向、距离、观察方向和曲面法线限制的弧形掠射反光带。这个补充项用于解决长锥尾在典型追尾视角下只有极少像素能看到严格镜面火光的问题。它是明确的美术近似；没有把整个水滴基色改成橙色，也没有改变普通状态的银灰材质。

用相同暂停时刻、相同相机、相同光源和同一曝光，分别关闭/开启 `explosionReflections`，检查实际渲染 PNG。最终近看画面保留中间的冷暗反射区，边缘/肩部出现暖色反光；追尾视角也出现可见的橙白亮度变化。以下数据是 RGB8 截图像素差，不是主观评分或光学精度声明。

| 最终 Editor A/B | ROI | 红色增加 >2/255 的像素 | ROI 平均变化 R / G / B，0–255 | 最大通道变化 |
|---|---|---:|---:|---:|
| 近看 | `(820,454)–(1120,600)` | 23,279 | 25.68 / 15.18 / 8.89 | 183 |
| 典型追尾 | `(910,660)–(1008,780)` | 5,724 | 35.13 / 21.84 / 13.21 | 165 |

证据：[追尾关闭](verification/VisualUpgrade/Editor/After-20260908-134145-326/07c-frozen-CHASE-explosion-response-OFF.png)、[追尾开启](verification/VisualUpgrade/Editor/After-20260908-134145-326/07d-frozen-CHASE-explosion-response-ON.png)、[近看关闭](verification/VisualUpgrade/Editor/After-20260908-134145-326/07a-frozen-chrome-explosion-response-OFF.png)、[近看开启](verification/VisualUpgrade/Editor/After-20260908-134145-326/07b-frozen-chrome-explosion-response-ON.png)、[追尾像素数据](verification/VisualUpgrade/Editor/After-20260908-134145-326/reflection-chase-pixels.json)、[近看像素数据](verification/VisualUpgrade/Editor/After-20260908-134145-326/reflection-close-pixels.json)。

每帧覆盖有效光源数组、清空未使用项；重开和组件停用清空爆炸来源。暂停冻结池内年龄，A/B 只改变可视反射开关。实际检查包含多次连续命中、自然衰减和三次重开，未发现永久发橙或前一轮爆炸污染下一轮。

**7. 反射方案选择、其他材质与资源上限**

本轮采用 **URP 镜面 BRDF + HDR 环境 Cubemap + 一个局部 Reflection Probe + 最多 3 个附近反应堆方向源 + 最多 4 个爆炸方向源 + 至多 2 盏共享爆炸灯**。Unity 6.5 官方功能表将 URP 的屏幕空间反射和光线追踪反射列为不支持，因此本轮保留当前管线，采用项目内可控制的混合方案。[Unity 6000.5 官方渲染管线功能对比](https://docs.unity3d.com/6000.5/Documentation/Manual/render-pipelines-feature-comparison.html)

没有新增 SSR、光线追踪或六面逐帧抓取。慢速局部探针适合附近舰体和环境；玩家身后短暂爆炸的及时反射由同源事件数据保证，避免等待探针更新或屏幕中必须存在爆心。

局部探针为 128 像素/面，原有分时捕获，刷新间隔至少 3 秒，移动阈值 25 m。水滴和临时特效所在的 Layer 2 被排除，避免自反射和把已结束火光留在 Cubemap 中。它保留舰船/环境反射功能；瞬时火光响应不依赖这张探针何时更新。

FusionFrigate 主装甲采用 Metallic=0.9、Smoothness=0.73 和受控表面变化；深色结构、舱段和引擎金属各自保留差异。岩石使用非金属、约 0.12 光滑度和程序化粗糙变化，与水滴区分。地球保留现有海陆云层贴图，按表面类型区分海洋高光与陆地粗糙度，加入受太阳方向限制的边缘亮感；本轮没有替换天文布局或增加超高分辨率地图。

| 项目 | High | Low | Off |
|---|---:|---:|---:|
| 同时反应堆爆炸 | 12 | 6 | 0 |
| 共享爆炸点光 | 2 | 1 | 0 |
| 单爆炸火球层 | 最多 5 | 使用同一预热池并控制表现 | 0 |
| ParticleSystem 数 | 0 | 0 | 0 |

池一次预热 **135 个 Transform**，后续重复使用。每爆炸有 8 块合并绘制的装甲碎片以及合并火花几何，没有给每碎片增加行为组件。池满时按距离/可见性优先级保留更重要效果，必要时回收旧槽或放弃表现，不丢失任何逻辑摧毁。旧 MissionEffects 在新场景保留音频用途，几何特效容量设为 0，避免重复叠加两套爆炸。

最终 Editor 实测全场景共享材质种类为 18、名称含 `(Instance)` 的场景材质为 0；舰体自身共享材质为 7。128/256 级 Cubemap 和程序化效果控制了纹理规模，没有新增 4K/8K 特效图。本轮 `Art/VisualUpgrade` 目录在报告草拟时约 **8.42 MB**（文件大小合计，包含该目录资源与元数据，不等于运行内存或安装包体）。最终独立构建大小另列，25–30 GB 是控制预算，没有为了接近这一容量而填充资源。

**8. 修改前后性能**

实际设备是 **NVIDIA GeForce RTX 4060 Laptop GPU**。这是本机笔记本版本的测量，不泛化为所有桌面 RTX 4060。两类测量均为 1920×1080，双方 VSync=0、帧率上限=-1；每个阶段先暖机 60 帧，再测量至少 480 帧且至少 6 秒。所有测量帧均处于聚焦状态。

最终 **Windows 独立运行，非 Development Build**：

| 视角 | 修改前 FPS | 修改后 FPS | 修改前 P95 ms | 修改后 P95 ms | 前 / 后采样帧数 |
|---|---:|---:|---:|---:|---:|
| 典型舰船近景 | 329.70 | 219.46 | 3.89 | 6.27 | 1,979 / 1,317 |
| 舰队中景 | 250.49 | 189.75 | 5.42 | 6.59 | 1,503 / 1,139 |
| 集中爆炸 | 346.12 | 259.66 | 3.96 | 5.15 | 2,077 / 1,558 |
| 面向太阳 | 300.97 | 241.16 | 4.41 | 5.28 | 1,806 / 1,447 |

本机这四段独立运行短程测试的平均帧率为 **189.75–259.66 FPS**，明显超过约 80 FPS 目标，P95 也低于 12.5 ms；升级的实际成本仍然存在，不能把性能余量解释成免费效果。集中爆炸修改前命中 38 艘，修改后命中 37 艘，差异来自 0.16 秒实时间隔在不同帧率下的离散触发。两边均为相同目标选择与真实 Motor 路径流程。

需保留的限制是：修改后集中爆炸的 Main Thread P99 约 **22.27 ms**，最大约 **57.93 ms**。压力脚本在单个渲染帧内执行多次 Motor 模拟步完成一次整舰贯穿，确实增加同步工作；但排除毁舰计数变化帧及紧后一帧后，P99 仍约 **20.69 ms**，不能把全部尖峰归因于诊断扫掠。当前数据没有隔离探针、表现更新和进程各项成本，因此不宣称“锁定 80 FPS”或已经确定某一个 GPU 瓶颈。原始逐帧数据保留，可以进一步定位；不为隐藏尖峰而删掉这些样本。

独立版没有取得 GPU、GC Allocated In Frame 和 Draw Calls Count 的有效样本，这三项记作 **未测得**。Main Thread、SetPass、三角形计数和场景库存有有效数据。独立运行中近景/中景三角形均值约为 1.719M / 1.304M，集中爆炸池峰值仍为 12、总实时灯峰值为 3、粒子数为 0。没有发现池增长或 `(Instance)` 材质增长。详见 [独立运行完整性能表](verification/VisualUpgrade/player-performance-summary.md)、[Before 原始 JSON](verification/VisualUpgrade/Player/Before-20260908-134804-541/visual-upgrade-validation.json)、[After 原始 JSON](verification/VisualUpgrade/Player/After-20260908-134907-539/visual-upgrade-validation.json)。

逐帧复核进一步显示，修改后集中阶段 1,558 帧中有 33 帧超过 12.5 ms（2.12%）、19 帧超过 20 ms；修改前 2,077 帧中对应为 16 帧（0.77%）和 5 帧。最大尖峰出现在阶段第 1 帧，当时只有 1 个爆炸；排除最初两帧仍有 P99≈20.69 ms、最大≈30.43 ms。19 个超过 20 ms 的帧中，13 个同时有 SetPass≥120，这只是相关证据。该阶段 SetPass 中位数从 68 增至 80、P95 从 76 增至 121，三角形中位数从约 0.551M 增至 1.080M，两边实际探针捕获均为 2 次。当前最明确的可见成本变化是放大后高 LOD 几何与提交量增加；GPU/GC 样本缺失，不能据此锁定单一瓶颈。

新反应堆池 `reactorDropped=0`。诊断中旧 `legacyDropped=37` 来自新场景有意把旧几何池容量设为 0、保留音频路径，不表示新反应堆爆炸漏播。独立版的反应堆池始终为 135 个 Transform，仍受 12 槽预算约束。

最终 **Editor** 对照：

| 视角 | 修改前 FPS | 修改后 FPS | 修改前 P95 ms | 修改后 P95 ms | 前 / 后采样帧数 |
|---|---:|---:|---:|---:|---:|
| 典型舰船近景 | 162.48 | 157.03 | 6.43 | 8.03 | 975 / 943 |
| 舰队中景 | 162.27 | 147.80 | 6.42 | 7.65 | 974 / 887 |
| 集中爆炸 | 149.14 | 159.45 | 12.29 | 9.10 | 895 / 957 |
| 面向太阳 | 162.40 | 162.39 | 6.37 | 6.37 | 975 / 975 |

这些短程 Editor 场景都超过 80 FPS，P95 帧时也低于 12.5 ms；Editor 数据用于单独参考，不代替上面的独立运行检查。Editor 爆炸阶段两边都执行了 37 次目标命中，按每 0.16 秒一次实际 Motor 路径触发；起点重设和相机跟随用于控制实验，并非自然玩家输入节奏。近景、中景、面向太阳三个阶段相机固定，舰队保持完整。

扩大的舰体更早进入较高 LOD，因此近景/中景三角形均值约从 0.826M / 0.673M 增至 1.741M / 1.402M；近景 SetPass 均值从 66.26 增至 74.27，集中爆炸从约 69.14 增至 84.45。集中爆炸实测峰值为 12 个新池效果、3 盏总实时灯（太阳 + 两盏爆炸灯）、0 粒子。没有发现不断生成材质实例或不断扩大的特效池。

`Draw Calls Count` 标记没有取得样本，不能报告为“0 Draw Call”。Main Thread 数据含 Editor 的进程开销。最终 Editor 的前两阶段 FrameTimingManager GPU 原值异常偏低（约 0.01 / 0.04 ms），不能用作可靠 GPU 归因；后两阶段原值也不用于代替独立 GPU 分析。当前证据显示分辨率不变时高 LOD 几何/提交量有所增加，但没有把一个未经隔离测量的单项成本宣称为已确认瓶颈。

详细原始证据：[修改前 JSON](verification/VisualUpgrade/Editor/Before-20260908-132623-784/visual-upgrade-validation.json)、[最终修改后 JSON](verification/VisualUpgrade/Editor/After-20260908-134145-326/visual-upgrade-validation.json)、[自动提取的完整性能表](verification/VisualUpgrade/editor-performance-summary.md)。各运行目录还包含逐帧 `frames.csv` 和实际相机 PNG；截图编码、库存枚举和文件格式化位于计时区间之外。

Windows 构建实际耗时 **37.880 秒**，结果 Succeeded，0 个错误。构建工具原始时间字段保存在 JSON 中；本文使用实际耗时与任务日期，不对可能包含时区偏移的绝对时间做混算。

| 硬盘统计范围 | 实际 bytes | 十进制容量 |
|---|---:|---:|
| 新独立发行内容，不含 `DoNotShip` | 138,666,205 | 0.138666 GB，约 138.67 MB |
| 本轮全部新增源文件 | 9,907,186 | 约 9.907 MB |

旧构建、Blender 源文件、Library 和验证截图继续保留在项目工作目录，不应被打进游戏发行包。当前独立版远低于 25–30 GB 控制预算。证据：[build-final.json](verification/VisualUpgrade/build-final.json)、[交付文件/大小/哈希清单](verification/VisualUpgrade/delivery-inventory.json)。

**9. 已验证、未验证及交付文件**

| 检查 | 实际结果 / 范围 | 证据 |
|---|---|---|
| Unity 实际编译 | 已记录 completed、failed=false、errors=[]，并通过后续真实 Windows 构建 | [compile-results.json](verification/VisualUpgrade/compile-results.json) |
| EditMode 新资产检查 | 3 / 3 通过：场景身份/姿态/参数保护、缩放命中体、反射/VFX 预算与接线、LOD 与材质支持 | [editmode-results.json](verification/VisualUpgrade/editmode-results.json) |
| PlayMode 检查集合 | 55 / 55 通过：原有碰撞/飞行/任务回归，以及新爆炸预算、失稳、暂停、重开、反射来源 | [playmode-results.json](verification/VisualUpgrade/playmode-results.json) |
| 最终反射组件复测 | 2 / 2 通过；这是最终反射修复后的专项复测，不把它另算成 57 个不同测试 | [final-reflection-tests.json](verification/VisualUpgrade/final-reflection-tests.json) |
| 最终 Editor 渲染诊断 | 38 / 38 检查通过、20 张实际截图：完整 120 舰扫掠胜利、结果只转换一次、暂停、三次重开、同帧 120 次命中池上限、关闭 VFX 后仍可摧毁计分 | [最终诊断 JSON](verification/VisualUpgrade/Editor/After-20260908-134145-326/visual-upgrade-validation.json) |
| 实际 FixedUpdate 输入链路 | 合成 Input System 键鼠驱动已有输入与 FixedUpdate：连续 5 舰命中、1,500 分、约 509.72 m，暂停/重开/刹车/四向转向/相机保持直立均通过，头尾方向 dot=1 | [live-flight.json](verification/VisualUpgrade/live-flight.json) |
| 基线保护 | 729 项保护文件中，727 项哈希保持相同；仅 `docs/STATUS.md`、`docs/ENVIRONMENT.md` 追加本轮记录，无缺失文件 | [preservation-after.json](verification/VisualUpgrade/preservation-after.json) |
| Windows 构建 | Succeeded，37.880 秒，0 个错误、1 个可选 Pipeline 运行时桥接配置警告 | [build-final.json](verification/VisualUpgrade/build-final.json) |
| 独立版 Before | 31 / 31 检查、16 张截图，退出码 0，全部计时帧聚焦 | [Before JSON](verification/VisualUpgrade/Player/Before-20260908-134804-541/visual-upgrade-validation.json) |
| 独立版 After | 39 / 39 检查、20 张截图，退出码 0，全部计时帧聚焦；根代理已复核追尾 A/B 与面向太阳画面 | [After JSON](verification/VisualUpgrade/Player/After-20260908-134907-539/visual-upgrade-validation.json) |

Windows 构建曾因诊断脚本引用 Editor 专用灯光统计属性而失败，随后改为 Player 支持的 `Light.bakingOutput.lightmapBakeType` 并修正 Unity 6.5 查询重载，真实重建成功；没有把首次失败当通过，也没有修改玩法。最终唯一构建警告是可选 Pipeline 运行时桥接未配置，该桥接在 Player 中关闭；它不是 URP 渲染管线失效。两个独立版日志均出现 D3D12 调试信息队列接口查询 `0x80004002`，实际 D3D12 图形设备、截图和验证继续正常；未发现 C# 异常。

最终独立版画面证据：[追尾反射关闭](verification/VisualUpgrade/Player/After-20260908-134907-539/07c-frozen-CHASE-explosion-response-OFF.png)、[追尾反射开启](verification/VisualUpgrade/Player/After-20260908-134907-539/07d-frozen-CHASE-explosion-response-ON.png)、[面向太阳](verification/VisualUpgrade/Player/After-20260908-134907-539/performance-FacingSun.png)。已确认追尾局部暖色反光实际出现、目标仍可读，没有粉材质。

最终独立版的尺寸对照、冷银水滴、五引擎、主爆炸、余辉、太阳诊断、结算和重开截图已经复核。仍有明确的画面边界：火团和片状能量喷涌属于风格化程序效果，不是电影级体积流体；引擎蓝色外壳轮廓较明显；装甲向阳区域偏白、背光区域很暗；水滴极亮细反射线上有轻微锯齿，沿用当前抗锯齿设置；正式太阳角尺寸较小。这些限制没有通过全屏过曝或扩大日面来遮盖。

未声称完成的内容：物理键盘/鼠标人工手感评价、长时间温度/功耗稳定性、其他机器/桌面 4060、全场景每个视角的 GPU 隔离剖析，以及天文级太阳/地球细节。地球和岩石完成材质分层实现及场景接线，但本轮最详尽的像素对照集中在水滴、爆炸与太阳；没有把缺少专项近看截图的项目写成全面美术验收。

已交付的新文件集中于以下位置，原稳定资源继续存在：

- [新可玩场景](../Assets/_Project/Scenes/FleetAssault_VisualUpgrade.unity)、[FusionFrigate_VisualUpgrade.prefab](../Assets/_Project/Prefabs/Fleet/FusionFrigate_VisualUpgrade.prefab)、[Droplet_VisualUpgrade.prefab](../Assets/_Project/Prefabs/Player/Droplet_VisualUpgrade.prefab)、[SpaceEnvironment_VisualUpgrade.prefab](../Assets/_Project/Prefabs/Environment/SpaceEnvironment_VisualUpgrade.prefab)。
- [Art/VisualUpgrade](../Assets/_Project/Art/VisualUpgrade)：银色水滴、舰体、岩石、地球、太阳、反应堆、火焰、HDR 环境、灯光和后处理资源副本。
- [Shaders/VisualUpgrade](../Assets/_Project/Art/Shaders/VisualUpgrade)：`PerfectChrome`、`FleetSurface`、`ReactorCore`、`ReactorFire`、`SolarStar`。
- [Runtime/VisualUpgrade](../Assets/_Project/Scripts/Runtime/VisualUpgrade)：`ReactorExplosionPool`、`ReactorDestructionPresenter`、`DropletReflectionResponse`。
- [VisualUpgradePipeline.cs](../Assets/_Project/Scripts/Editor/VisualUpgradePipeline.cs)：使用 Unity Editor API 显式构建/更新自有视觉副本，并保护未保存场景及非自有实例。
- [VisualUpgradeValidationRunner.cs](../Assets/_Project/Scripts/Runtime/VisualUpgradeValidationRunner.cs)、[VisualUpgradeValidationCommands.cs](../Assets/_Project/Scripts/Editor/VisualUpgradeValidationCommands.cs)、[Tools/VisualUpgrade](../Tools/VisualUpgrade)：可复现渲染测量、A/B 像素诊断和结果提取。

复现最终观感可以直接试玩；需要证据自动采集时，在 Play 模式使用 `DropletPrototype > Visual Upgrade > 7 Inspect Current Play Scene`。该命令临时控制检查相机和命中路径，结束后重置任务；`9 Validate Current Play Scene After` 额外执行四段性能采样。调试用临时检查对象不会保存为正常关卡的一部分。完整玩家命令行入口为 `--visual-upgrade-validation <输出目录> --visual-upgrade-scene FleetAssault_VisualUpgrade --visual-upgrade-label After --visual-upgrade-quit`。

本轮范围止于视觉质量与战斗反馈升级，不自动继续新的玩法开发。
