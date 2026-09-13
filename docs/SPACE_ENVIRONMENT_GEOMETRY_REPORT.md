# 空间环境几何交付记录

打开 `ArtSource/Blender/SpaceEnvironment/SpaceEnvironment.blend` 检查完整可编辑场景；默认场景为 `SpaceEnvironment`，`ReferenceCamera` 为 16:9 参考视点。另有 `AsteroidLibraryReview` 场景查看八种原型和全部 LOD。

Unity 打开 `Assets/_Project/Scenes/SpaceEnvironment_Review.unity`；环境 Prefab 为 `Assets/_Project/Prefabs/Environment/SpaceEnvironment.prefab`。不需要按 Play，天体、岩石和布局已经保存。检视相机、白色检查灯位于独立的 `PreviewOnly_SpaceEnvironment_v1`，不在环境 Prefab 内。

本轮已实际生成、保存、重开 Blender，完成检查后导出共享模型和独立布局，再实际导入 Unity、保存 Prefab/场景并重复导入验证。尚有一项保全偏差未解决：旧回归测试重写了 TestRange，详见下文；不能将本轮描述为所有保全约束均已满足。

## 资产与几何实查

| 资源 | 唯一 Mesh 数 | 三角形实数 |
|---|---:|---:|
| 太阳 | 1 | 1,472 |
| 地球 LOD0 / LOD1 / LOD2 | 3 | 6,016 / 1,840 / 528 |
| 八种岩石，每型 LOD0 / LOD1 / LOD2 | 24 | 每型 1,280 / 320 / 80；全部 13,440 |
| 远景八个空间分区 | 8 | 每区 1,800；全部 14,400 |
| 内向天空球 | 1 | 960 |
| **全部运行环境资源，包括所有 LOD** | **37** | **38,656 / 60,000 目标** |

布局包含 12 个近景、160 个中景、8 个远景分区、太阳、地球和天空球，共 **183 个实例**。远景 720 块完整低面岩石合并为八个独立分区网格，没有为每个远景点建立 GameObject。172 个近中景实例继续共享八种原型。Unity Prefab 包含 173 个 LODGroup、529 个 MeshRenderer（其中许多属于非当前 LOD），并不等于同时绘制 529 个物体。

实际引用断言采用 `ReferenceEquals` 比较每个实例的 `sharedMesh` 与对应 FBX 子资源；材质同样逐个比较持久化资产引用。最终是 **5 个共享基础材质、2 张共享贴图**，没有每实例 Mesh/Material/Texture 副本。关闭所有背景模型及贴图 Read/Write、动画、骨骼动画、BlendShape、光照贴图 UV、碰撞体导入；没有 Static Batching。GPU Instancing 选项开启不等于已实现 GPU 实例批次，实际批处理结果单独记录。

校准/布局的微型姿态代理只用于 Editor 导入检查，不进入环境 Prefab；其网格没有计作运行环境的 37 个 Mesh。Editor 整体内存包含这些编辑辅助资产，不能与可归因的 Prefab 资源混为一谈。

## 构图、空间和 Blender 验证

已实际打开用户参考图。太阳中心约在左上 `(0.158,0.219)`，地球约在右侧 `(0.833,0.389)`；实测球体屏幕高度分别约 14.1%、17.7%。岩带从左上向右下横穿画面，三块主要近景岩石沿左下、右下边缘建立尺度，保留中央留白。使用游戏化有限尺寸与距离，不声称天文学距离或原著测绘复原。

固定随机种子 `907260`；布局参数集中在 `Tools/Blender/SpaceEnvironment/config.json`。现有活动球中心 Unity `(0,20,260)`、边界半径 730；留空半径 760，考虑现有相机距离 8、高度 2.2 和命中半径 0.7。独立检查按各封闭组件的保守包围球扣除自身半径后，距 760 留空球的最小剩余间距约 **636.98**。没有通过关闭 Collider 来掩盖岩石占用航道。

五个实际视角分别检查参考构图、侧面、俯视、可玩区转向及靠近岩带。岩带沿有厚度的环形空间弧延续到侧方/后方，太阳、地球和岩石均为完整三维网格。没有参考图平面、单面岩石、地球透明云球、大气壳或粒子石海。

保存并重新打开源文件后，37 个网格的拓扑、法线、三角形和链接引用通过自动检查；所有实体组件封闭、无非流形边或退化面、方向一致，天空球特意朝内。两张纹理均有外部文件并已打包到 `.blend`。原型库与场景实例独立，库集合被排除，未将多个 LOD 重叠显示在场景中。

实际生成并查看了七张 Blender 图片：五个场景视角、八型三档原型总览和线框。Workbench 中仅隐藏天空壳以避免棚拍光照作用于壳体造成明暗网格；天空球及原创星点贴图仍是独立导出资源，Unity 用 Unlit 显示。第一次自检发现 Earth 没有打包，修复并重新生成/保存/重开后才放行导出。早期草稿 `.blend` 以 `SpaceEnvironment.before-*.blend` 保留。

证据：`ArtSource/Blender/SpaceEnvironment/Previews/`、`blender_selfcheck.json`、`build-process.log`、`inspect-process.log`、`export-process.log`；`docs/verification/SpaceEnvironment/blender-visual-review.json` 记录已实际查看的图和源文件 SHA256。最终生成、重开和导出进程退出码均为 0。

## 导入与中性预览

沿用已校准的 Blender +Y 前/+Z 上 → Unity +Z 前/+Y 上契约：`axis_forward=-Z`、`axis_up=Y`、`bake_space_transform=true`；Unity `bakeAxisConversion=true`、单位比例 1。原型独立 FBX 各导出一次。权威布局是 `SpaceEnvironmentLayout.fbx`，源 Empty 转为微型非对称姿态代理；Unity 读取完整导入变换并丢弃代理几何。`export-manifest.json` 的坐标是对照证据，不是另一套用于摆放的 JSON 布局。

本机重新导入单位立方体、不对称前/上/右标记及三个旋转/平移标记，0.002 容差通过。全部环境姿态、缩放、导入尺寸、三角形及完整材质引用通过检查。生成器只管理 `GeneratedEnvironment_v1`；复用稳定 ID，分组变化时迁移原实例，保留外部手工内容。导入入口检查 Blender 源文件哈希与视觉放行记录，防止导入未经检查的新源。

原项目 PC Renderer 含既有 SSAO，首次完整帧调试实际发现 SSAO 与 DepthNormals 通道。为满足本轮的中性预览，创建任务自有 `Assets/_Project/Art/Environment/SpaceEnvironment/PreviewOnly/NeutralRenderer.asset`，移除其 Renderer Features，使用 Forward 路径。仅在原 `PC_RPAsset` 的 Renderer 列表追加这一项；原默认索引 0、原 PC Renderer、正式场景的相机及光照配置保持原样。独立检视相机明确使用新增 Renderer，关闭后处理、HDR、MSAA 和相机深度/不透明贴图请求。这是有记录的检视配置调整，未升级或更换 URP。

环境 Prefab 没有灯、反射探针、后处理组件、MonoBehaviour、Collider、Rigidbody 或 ShipTarget；没有新玩法。环境位于新增 `SpaceEnvironment` 层 9，原命中层掩码仍为 256，与它无交集。基础材质仅 Sun/Sky Unlit 和无镜面高光的 Earth/Rock Simple Lit；白色方向灯只在检视场景中。未制作太阳辉光、日冕、Bloom、镜头光斑、大气散射、体积雾、反射或烘焙光照。

## LOD、绘制量和内存

LOD 不仅检查了配置：调用本机 `LODUtility.CalculateVisualizationData` 查询五台实际相机的原生选择，再用移动的诊断相机验证 Rock05 和 Earth 各自 LOD0/1/2/剔除，八项均通过，没有用 ForceLOD 替代自动距离选择。诊断使用当前 `lodBias=2`；最远诊断距离超过正式检视裁剪范围的情况明确标记，诊断相机已删除，不改变场景尺寸或相机。

首次继承 SSAO 的参考帧为 106,976 个环境几何提交三角形（含两次几何遍历）。最终中性 Renderer 的参考帧成功取得全部 3 个事件：天空 Unlit、岩石/地球 ForwardLit、太阳 Unlit，合计 160,464 个索引，即 **53,488 个实际提交环境三角形 / 180,000 目标**；103+1+1=105 次绘制，和该 Game View 总计数一致。没有 SSAO、DepthNormals 或后处理通道。原始 SRPBatch 索引数已经是批内汇总，不能再次乘实例数。没有实际 GPU 实例批次；SRP Batcher 减少状态切换，但仍有上述绘制调用。各视角完整结果见 `docs/verification/SpaceEnvironment/draw-summary.json`；只有实际成功捕获的事件才列为实测。

| 检视相机 | 实际提交的环境三角形 | 实际环境绘制调用 |
|---|---:|---:|
| ReferenceCamera | 53,488 | 105 |
| SideCamera | 66,368 | 183 |
| TopCamera | 45,248 | 183 |
| GameplayTurnCamera（可玩区内） | 32,376 | 54 |
| BeltCloseCamera（岩带近看） | 34,512 | 48 |

五个视点全部取得有效事件，最高 66,368，低于抽样视点 250,000 目标。这些值来自有效 Frame Debugger SRPBatch 事件及其环境网格路径，不是独立 Player 性能测试。原生 LOD 查询及五个视点的解析估算另保留在 `native-lod-audit.json`、`unity-audit.json`；即使估算碰巧与实测相同，也不互相替代证据。

| 内存项目 | 实际值 / 范围说明 |
|---|---|
| 全部唯一 Mesh 原生大小 | 3,590,288 字节，约 3.424 MiB |
| 两张 Texture 原生大小 | 5,681,680 字节，约 5.418 MiB |
| **Mesh + Texture API 合计** | **9,271,968 字节，约 8.842 MiB，低于 64 MiB 目标** |
| 顶点/索引缓冲布局估算 | 1,765,840 字节，约 1.684 MiB |
| DXT1 全 mip 链估算 | 每张 1,398,120 字节；两张 2,796,240 字节，约 2.667 MiB |

两张导入贴图均实际为 **2048×1024、DXT1 / RGBA_DXT1_SRGB、12 级 mipmaps、Read/Write=false**。地球为 NASA 历史全球含云层合成图，不是随机噪声大陆，也不是实时天气。星空为原创简单星点图；太阳和岩石无专用贴图。岩石共用两种纯基础色材质。

上述原生大小由实际 Editor（包括进入 Play Mode 的隔离检视场景）调用 `Profiler.GetRuntimeMemorySizeLong` 逐个唯一资源取得，所有返回值大于 0，包含未选中 LOD。它不是 PNG/FBX 文件大小，也不是可独立拆分的系统 RAM / 显存驻留实测。缓冲估算按导入后顶点数×各流步长，加各子网格索引数×索引宽度；DXT1 估算逐级累加 `ceil(w/4) × ceil(h/4) × 8`，直到 1×1。两类估算不能再与原生 API 大小相加，否则会重复计算。

最终中性场景再次进入 Editor Play Mode：整体分配 809,251,376 字节（约 771.76 MiB）、保留 1,381,113,856 字节（约 1317.13 MiB），图形驱动整体估计 351,000,332 字节（约 334.74 MiB）；唯一 Mesh/Texture API 结果仍与表格一致。这是整个 Editor 的上下文，不是本环境独占资源或可归因显存。原始值见 `unity-playmode-audit.json` / `unity-audit.json`。**独立 Player 的运行内存、可归因显存、GPU 时间、长期帧率与跨硬件性能未测，也没有承诺 60 FPS。** 本轮不发布或制作新独立包。

## 已执行、限制及保全偏差

- 实际 Unity 6000.5.10f1 编译通过；安装环境保持 URP 17.5.0、Input System 1.20.0、Test Framework 1.7.0、Custom NUnit 2.1.0。Blender 5.2.1 LTS，构建 `9e2066aef7ef`。没有安装工具或包。
- 两次完整模型/布局重新导入并更新 Prefab/检视场景，然后关闭重开：数量、稳定 ID、全部共享引用及已有环境 GUID 正确。外部测试手工对象及其位置保留，检查后只删除测试对象。中性 Renderer 调整后再次执行相同检查。
- 多视角 Unity 图片和线框已实际生成、打开检查，和 Blender 的天体位置、大小、岩带方向相符。最终图片在 `docs/verification/SpaceEnvironment/Unity-*.png`。
- 既有测试实际执行：EditMode 15/15、PlayMode 27/27，0 失败/跳过；原始记录保留。这些是回归检查，不是新环境的独立性能测试。
- **保全偏差：**上述旧 EditMode `SceneAuthoringTests` 调用了会重建并保存生成区的 `TestRangeBuilder.Build()`，造成 `Assets/_Project/Scenes/TestRange.unity` 与本轮开始哈希不一致。这是本轮执行检查造成的意外写入，未获得覆盖该场景的授权，不能用测试通过来掩盖。原始 SHA256 为 `431EFCECD2C69DAC8226AFA5CE1DF3607E4683EE1052C54DE96A54C478BE22EC`。已检查项目候选文件、空 Undo 缓存、Git 和仅属本项目的 Codex 记录，未找到精确恢复副本；已请求用户提供原始备份路径。**尚未恢复，不声称 TestRange 字节或所有手工内容完整保留。** 不再运行该重建测试；后续必须先有可恢复的场景副本或使用隔离测试副本。
- 保全比对覆盖开始时的 1,069 个文件。FleetAssault、现有玩法脚本、原模型/Prefab、旧构建、包/版本和全部原 `.meta` 保持哈希一致。预期调整是层注册及 `PC_RPAsset` 的中性 Renderer 追加引用；TestRange 是上面的非预期差异。最终实际清单见 `preservation-after.json`。
- 工具问题曾包括 Roslyn 编码错误、域重载短暂连接中断、临时对象创建/原生 LOD 诊断偏差及 Frame Debugger 窗口初始化/重绘问题。帧检查按本机内部方法同步事件选择、等待真实初始化、派发真实 GameView Repaint，并明确绑定当前本机 Profiler；保留逐事件索引与有效性断言。失败调用没有算作通过证据。
- 重生成防护真实检查通过：当前源匹配生成收据；手改后拒绝；即使另行更新并通过视觉检查，也无法绕过生成收据保护。真实 `.blend` 前后 SHA256 不变，临时测试对象已清理。详见 `source-guard-check.json`。

## 文件和复现

- `Tools/Blender/SpaceEnvironment/`：`config.json`、天体/岩石模块、完整场景生成、重开检查、导出及 `run.py`。
- `ArtSource/Blender/SpaceEnvironment/`：完整 `.blend`、原型库、纹理原始文件及来源、模型 FBX、独立布局、manifest、日志与预览。
- `Assets/_Project/Art/Environment/SpaceEnvironment/`：实际 Models、Textures、Layout、Materials、PreviewOnly；Unity 自行生成 `.meta`。
- `Assets/_Project/Scripts/Editor/SpaceEnvironmentPipeline.cs` 与 `SpaceEnvironmentFrameProbe.cs`：Editor 生成/验证及原生 LOD/实际帧事件检查。全部在 Editor 程序集中，不进入 Player。
- `Assets/_Project/Prefabs/Environment/SpaceEnvironment.prefab`、`Assets/_Project/Scenes/SpaceEnvironment_Review.unity`。
- 本报告、STATUS、ENVIRONMENT、ASSET_LICENSES 及 `docs/verification/SpaceEnvironment/` 证据。

复现先保存自己的未保存场景。Blender 入口只允许独立 background/factory 进程，不能在正在编辑的文件中执行。它重生成整个任务源文件，不是对手工集合做增量合并：源哈希必须与生成收据一致，否则拒绝覆盖，并要求在独立项目副本中生成；视觉验证收据不作为重生成许可。允许重生成时仍保留带时间戳的旧源。该限制保护手改文件，不能将备份描述成“已合并保留手工集合”。Unity 更新自有节点且保留外部手工对象的行为另由重复导入实际检查。

```powershell
python Tools/Blender/SpaceEnvironment/run.py build
python Tools/Blender/SpaceEnvironment/run.py inspect
# 实际打开并检查 Previews 下七张图，更新与新 .blend SHA256 对应的视觉记录后：
python Tools/Blender/SpaceEnvironment/run.py export
```

Unity 菜单 `DropletPrototype > Space Environment` 按 1（导入）、2（保存）、3（资源审计）、4（两次导入重开）、5（截图）、6（原生 LOD）执行。帧捕获先选择某个 Frame Capture 视角，等 Game View 渲染后执行 Collect Frame Events，结果完成后自动关闭本工具开启的调试器。不要运行旧 TestRange 重建测试来复现这项环境任务。

本轮停止在空间环境几何、布局、基础外观和导入验证；不继续正式光效或新玩法。美术最终光影和用户主观构图验收不以本轮中性模型预览代替。
