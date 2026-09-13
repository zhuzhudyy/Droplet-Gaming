# FusionFrigate 星舰模型交付

2026-09-08；本轮 Blender 建模、基础材质、三档 LOD、实际检查与预导出已完成，停在模型交付。没有调用 Unity 工具、写入 Unity Assets、替换舰船、修改太阳系布局或玩法，也没有新建游戏项目。

## 打开与产物

- **可编辑模型**：[FusionFrigate.blend](../ArtSource/Blender/Ships/FusionFrigate/FusionFrigate.blend)。默认仅 LOD0 可见；不是待执行脚本或图片平面。
- **四视图**：[FusionFrigate_Inspection.png](../ArtSource/Blender/Ships/FusionFrigate/Previews/FusionFrigate_Inspection.png)，包含三分之四、侧、顶、尾视。
- **LOD 对照**：[FusionFrigate_LOD_Comparison.png](../ArtSource/Blender/Ships/FusionFrigate/Previews/FusionFrigate_LOD_Comparison.png)。源单帧、舰底、舰首与推进组件近看均在同一 `Previews` 目录。
- **FBX**：[FusionFrigate.fbx](../ArtSource/Exports/Ships/FusionFrigate/FusionFrigate.fbx)。包含三档模型、LOD 层级及五个 Empty 尾焰挂点；没有参考图、预览相机、灯光、动画或尾焰。
- **导出参数/挂点**：同目录的 `FusionFrigate_export_manifest.json`、`FusionFrigate_sockets.json`。
- **实际回导检查文件**：[FusionFrigate_FBX_Roundtrip.blend](verification/FusionFrigate/FusionFrigate_FBX_Roundtrip.blend)，默认仅显示 LOD0；这是检查副本，编辑应优先使用上述源文件。
- **检查证据**：[verification/FusionFrigate](verification/FusionFrigate)：原始日志、数值报告、回导模型实际渲染、重复生成记录。

## 参考图与比例决策

用户提供的 `ArtSource/Spaceship` 实际是无扩展名的 **1448×1086 RGB PNG**。已实际打开查看，保留原文件，并按原字节复制为 `ArtSource/References/FusionFrigate/FusionFrigate_reference.png`。两者 SHA-256 都是 `d094cc81ec891c626577518272a297af51cbe1009b5cfdc5da087fac56988561`。没有把概念图作为游戏贴图或模型检视图。

大幅三分之四图决定长楔形舰身、低舰桥、前部炮塔、大装甲区和外露尾部动力系统。尾视决定 **1 个中央主喷口 + 4 个辅助喷口**：左右同高一对、左右下方一对，保持左右镜像；没有将各视图冲突的圆筒额外叠加成更多引擎。参考中的密集微细节没有逐一复制。

以实读既有 `fleet_assets.blend / Frigate_Source` 为尺寸基准：现有护卫舰长 22.14、宽 9、高约 4.456 米。本舰采用压缩游戏尺寸，**长 22.14 × 宽 7.38 × 高 6.211535 米**，高度包含天线和下方辅助引擎。不是原著精确尺寸，也没有按数百米放大。Blender 1 单位 = 1 米，`scale_length=1`，**+Y 向前、+Z 向上**；沿用项目 Unity **+Z 向前、+Y 向上**的资产契约。

源根与三个 LOD 根均在 `(0,0,0)`，旋转为零、比例为一；模块具有有意义的局部位置。三档外包围尺寸完全一致，源 bounds 为 `(-3.69,-11.07,-2.731535)` 至 `(3.69,11.07,3.48)`。

## 实际几何与资源

以下是**整艘舰**在最终生效修改器后按三角形统计，包含四个辅助引擎、三座炮塔和重复装甲的每次摆放。不是唯一原型面数，也不是 Blender 基础多边形数。

| LOD | 实际三角形 | 预算 | 源网格对象 / 唯一 Mesh | FBX 网格对象 / 唯一 Mesh |
|---|---:|---:|---:|---:|
| LOD0 | **17,224** | 12,000–18,000 | 20 / 14 | 13 / 7 |
| LOD1 | **5,400** | ≤6,000 | 20 / 14 | 13 / 7 |
| LOD2 | **1,396** | ≤1,500 | 12 / 7 | 10 / 5 |
| 三档资源合计 | 24,020 | 不同时绘制三档 | 52 / **35** | 36 / **19** |

源文件保留按功能组织的可编辑分件；引擎等模块内部由可单独选取的封闭几何岛组成。导出副本应用修改器、三角化，并把静态部分整理为 Hull、Superstructure、FusionDrive、Details；重复模块继续独立链接。每个 LOD 的四个辅助引擎共享一个 Mesh、三座炮塔共享一个 Mesh；LOD0/1 的两段重复装甲也共享 Mesh。源文件保留完整分组，导出整理没有反写源文件。

共 **4 个共享材质、0 张贴图**。没有 1024/4K/8K 图集、UDIM、法线图、外部材质资源或高模雕刻副本；初版无需依赖纹理 UV。材质仅用 Principled BSDF 的基础色、金属度、粗糙度，发光强度全部为零。

| 材质 | 基础色 RGB（线性参数） | 金属度 | 粗糙度 |
|---|---|---:|---:|
| FF_Armor 主装甲 | 0.390 / 0.430 / 0.455 | 0.50 | 0.42 |
| FF_Structure 深色结构 | 0.063 / 0.083 / 0.103 | 0.45 | 0.52 |
| FF_EngineMetal 引擎金属 | 0.235 / 0.285 / 0.325 | 0.78 | 0.34 |
| FF_BlueGray 窗带/内芯 | 0.075 / 0.250 / 0.345 | 0.35 | 0.32 |

这些基础参数和四个材质引用均经 FBX 实际回导核对。没有测量模型实际运行内存或显存；上述网格、三角形及资源数量不能当作内存占用，也不以文件体积推算内存。

## 保留的结构与简化

舰体是多截面的封闭八边楔形结构，前窄后宽，舰首具有斜面和收尖，腹部有独立装甲龙骨。中段以大块装甲和深色结构间隔分区；每侧两个浅设备槽有后壁、边框及简化设备，不制作舱内小艇或机库内部。低舰桥分层、使用窄窗带，配三根主要天线、少量传感器和三座外观炮塔。

主推进器具有连接舰体的反应舱外壳、**三层有间隔的粗磁环**、四根主要支架、四根粗管线、隔热板、喷口外壳和连续内壁。四个辅助引擎使用同一原型，每台有两道约束环并沿用支架/护板语言；两组简化散热板接到舰尾结构。主喷口从唇口至可见内芯约 **2.42 米**，辅助喷口约 **1.694 米**，关闭光效仍能辨认结构。

LOD0 主圆形采用 32 段、辅助 16 段，磁环用简单倒角截面；LOD1 降至主 20、辅助 12 段，并去掉细管线、局部倒角和服务件。LOD2 使用专门重建的主 12、辅助 8 段轮廓，合并装甲分区、缩减舰桥层级/炮管/磁环细节，保留五口排列和真实内壁；没有对整船盲目减面造成堵口或塌鼻。

省略螺丝阵列、密集管线、独立小窗灯点、逐条几何装甲缝、舰桥/机库/反应舱完整内部。没有细分曲面、多分辨率雕刻、高密度置换、粒子/流体/体积火焰、Bloom、复杂动画或正式灯光。

## 已执行检查与修复

全部 Blender 操作通过已安装的 **5.2.1 LTS，build 9e2066aef7ef** 在独立背景进程真实执行。没有连接或强制关闭已有用户编辑会话。使用 bundled `io_scene_fbx 5.15.0`，FBX 文件版本 7400；完整环境见 [ENVIRONMENT.md](ENVIRONMENT.md)。

1. 实际生成并保存源文件；独立进程重新打开后检查默认 LOD0 可见性、全部模型、材质与外部资源。`inspect-source.json` 通过。
2. 初版预算超限后减少辅助引擎圆周分段、支架倒角并重新构建 LOD2；修复 ForeDeckArmor 和 LayeredBridge 二次整体倒角带来的 24 个零面积三角形；将部分埋入喷口的隔热板移至可见位置。最终源和 FBX 回导均为 **0 边界边、0 非流形边、0 绕序异常、0 无效面法线、0 零面积三角形**。按每个封闭连通组件独立计算，负体积组件均为 0。
3. 每次只显示一个 LOD，实际渲染并查看舰首、侧面、顶部、底部、尾部和推进细节；三档三分之四及尾部对照检查通过。四视图由真实 960×640 Workbench 帧排版，没有生成概念图替代模型。最终四视图 1980×1549 PNG；LOD 对照 2240×1229 PNG。
4. FBX 实际导出并在独立 Blender 进程回导。`inspect-fbx.json`、`roundtrip_comparison.json` 通过，整船三角形、原型共享、基本材质、三档尺寸、舰首正 +Y / 天线正 +Z 和五个完整挂点矩阵均核对。最大 bounds 误差约 **1.43×10⁻⁶ 米**，最大挂点矩阵元素误差约 **2.64×10⁻⁶**；没有依靠手动旋转玩家或游戏根修复。
5. 沿用项目 `axis_forward=-Z`、`axis_up=Y`、`global_scale=1`、`FBX_SCALE_UNITS`、`bake_space_transform=true`。当前 Blender 导出器对 Root→LOD→Mesh / Root→Sockets→Socket 的两级 Empty 父级有烘焙变换问题；依据安装版 `fbx_utils.fbx_object_matrix` 的实际代码推导，仅在**临时导出副本**作矩阵处理：记轴向矩阵为 G，容器=G，二级 Mesh=G·W，二级 Socket=G·S·G。脚本强制检查这一层级前提，实际回导世界几何/挂点验证消除了轴向偏差；不更改原源或项目既有导出器。
6. 每档五喷口各取中心和八个开口采样，源与回导合计 **270 条内向 + 270 条外向射线**。中心都命中后缩的内芯，内壁深度大于阈值，向外通道无阻挡。结果在 `nozzle_and_sharing_checks.json`。有限采样不是穷尽自交证明。
7. 回导三分之四、尾部和推进近看均实际渲染并查看；同相机源/回导 RGB 平均绝对误差每通道小于 **0.079 / 255**，结果在 `render_comparison.json`，不替代目视检查。
8. 实际重复运行生成脚本，三档最终统计、材质及挂点完全一致，没有叠加对象；`repeatability.json` 通过。生成器要求全新背景进程，若现有 `.blend` 哈希不再等于上次生成记录，会中止以保留手工修改。导出前后源文件哈希一致。

## 手动查看与复现

用 Blender 打开 `ArtSource/Blender/Ships/FusionFrigate/FusionFrigate.blend`，展开 `FusionFrigate`。默认仅 `FusionFrigate_LOD0` 集合启用；查看其他档时先关闭当前 LOD，再启用对应集合，渲染时也切换集合的相机图标。`FusionFrigate_Sockets` 内五个挂点默认隐藏显示辅助线，可在 Outliner 打开眼睛查看；挂点局部 +Z 指向舰尾外侧。

从项目根目录运行以下命令。它们仅写本任务源、导出及检查目录，不触发 Unity 导入；源生成会检查手工编辑保护条件。

```powershell
python Tools/Blender/run_fusion_frigate.py build
python Tools/Blender/run_fusion_frigate.py inspect
python Tools/Blender/run_fusion_frigate.py export
python Tools/Blender/run_fusion_frigate.py verify-export
python Tools/Blender/run_fusion_frigate.py roundtrip
python Tools/Blender/run_fusion_frigate.py render
python Tools/Blender/run_fusion_frigate.py details
python Tools/Blender/compose_fusion_frigate_review.py
```

源几何脚本为 `build_fusion_frigate.py`，检查器 `inspect_fusion_frigate.py`，导出与回导 `export_fusion_frigate.py`，喷口与实例检查 `verify_fusion_details.py`；均位于 `Tools/Blender`。

## 未验证、已知限制与停止点

本轮按用户范围**不导入 Unity**，因此 Unity ModelImporter 实际导入、URP 最终材质、LODGroup 切换距离、运行时阴影、碰撞体适配、编译、EditMode/PlayMode、独立构建、游戏性能、模型实际内存及显存都未运行或测量。不能将 Blender 检查称为这些检查通过。当前没有纹理 UV/法线贴图、动画、尾焰系统或内部舱室；这是本轮刻意简化。

目视和数值检查未发现明显翻面、破面、严重闪烁或悬空动力部件，但有限视图/射线不能证明所有视角下绝无互穿。模块接合处有用于结构连接的适量实体交叠。低档在近距离会显著减少细节，切换距离需未来在实际游戏相机中决定。

本轮修改仅包括本任务新源/参考副本/导出/检视证据与六个脚本，以及本报告、STATUS、ENVIRONMENT、ASSET_LICENSES 的任务记录。旧舰船源、用户原参考文件、Unity 场景/玩法/太阳系和旧构建未被本轮操作写入。停止于 **FusionFrigate 模型和导出交付**，不自动进入舰队扩充、关卡替换或光效制作。
