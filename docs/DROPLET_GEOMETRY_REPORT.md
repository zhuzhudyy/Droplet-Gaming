# 三体水滴几何交付 — 2026-09-08

已在 Blender 5.2.1 LTS 实际完成独立模型、保存重开、游戏 FBX 导出及回导。最终长度 **2.4 m**，最大直径 **0.774194 m**，比例 **3.1:1**；尖端朝 **+Z**、模型上方 **+Y**，原点位于包围盒中心，位置/旋转为零、缩放为 1。游戏版 **5,632 三角形**；源内另保留 **22,528 三角形**参考版，默认隐藏，**未做 LOD**。只有一个无装饰、闭合的轴对称外表面和一个临时中性材质。

## 交付文件

| 文件 | 内容 |
|---|---|
| [完整交付包](../ArtSource/Deliveries/PerfectDroplet-20260908.zip) | 源文件、FBX、15 张检查图、脚本、报告与证据；逐文件 SHA-256 / ZIP CRC 已核对 |
| [PerfectDroplet.blend](../ArtSource/Blender/Droplet/PerfectDroplet/PerfectDroplet.blend) | 游戏版 + 隐藏的高精参考版；可编辑四边面网格及 READ_ME |
| [PerfectDroplet_Game.fbx](../ArtSource/Exports/Droplet/PerfectDroplet/PerfectDroplet_Game.fbx) | 仅一个游戏网格，三角化副本，保留平滑法线、UV 和中性材质槽 |
| [透视图](../ArtSource/Blender/Droplet/PerfectDroplet/Previews/01_Perspective.png) | 游戏版，中性 Workbench 检视 |
| [侧视图](../ArtSource/Blender/Droplet/PerfectDroplet/Previews/02_Side.png) | 从模型 +X 看向原点，尖端在画面右侧 |
| [正视图](../ArtSource/Blender/Droplet/PerfectDroplet/Previews/03_Front.png) | 从 +Z 面向尖端，检查圆形横截面 |
| [顶视图](../ArtSource/Blender/Droplet/PerfectDroplet/Previews/04_Top.png) | 从模型 +Y 看向原点；轴对称使它与侧视轮廓相同 |
| [四边面布线](../ArtSource/Blender/Droplet/PerfectDroplet/Previews/08_QuadTopology.png) | 实际游戏网格边线；另有两端封口近景 |
| [高光检查](../ArtSource/Blender/Droplet/PerfectDroplet/Previews/11_HighlightSweep_1.png) | 三角度内置 MatCap 诊断，另有反射条带与尖端近景；不是正式材质 |

全部检查图为实际 Blender 渲染，1600×1200；没有使用生成式图片替代模型。四张交付主视图使用同一中性材质。诊断图的条带、格纹及高光来自 Blender 内置 MatCap，不是表面刻线或贴图装饰。源文件和 FBX 没有灯光、相机、特效或动画；检视相机和线框辅助只存在于独立渲染进程的内存中。

## 为反射准备的几何

整条纵向轮廓来自**一条五次 Bézier 曲线**，没有球体与锥体的接合段。圆端局部曲率半径约 **414.27 mm**，前端约 **1.624 mm**：末端有有限圆弧，四边面封口中心的顶点是光滑曲面采样点，不是锥尖。最大直径位于 Z = **−0.578474 m**，约从后端向前 25.9% 的位置。最后调整了尾端控制点，使后端更接近饱满椭球弧面。

游戏版具有 **64 边圆环 × 36 段纵向网格**，两端焊接方格映射的四边面封口；合计 **2,818 顶点 / 2,816 四边面**。只有 **8 个三价顶点**，其余均四价，没有几十条边挤向同一个端点。环距同时考虑弧长/局部半径和法线转角，最大直径环精确落在曲线峰值。扩大尖端网格封口并渐进分布半径后，修复了初版鼻部细长面：主模型各四边面最长/最短边比 **P95 6.23、最大 6.80**。参考版以同一曲线提高采样密度，不使用雕刻噪声。

所有平滑法线由原始曲线切线直接求出并写入网格，环绕接缝及封口共用一致法线。UV 使用独立的属性接缝，不拆开几何顶点或制造法线硬边；无凹凸、法线纹理或位移贴图。源保留四边面，只有导出副本三角化。将来手动改形后，应清除旧自定义法线并重新评估/生成法线，不能让原形法线继续覆盖新形体。

## 实际检查与证据

- [构造记录](verification/PerfectDroplet/build.json)：Blender 实际版本、尺寸、曲线参数、端部曲率和面数。
- [重开与几何检查](verification/PerfectDroplet/inspection.json)：两版各 **1 个连通封闭体**；开边、非流形边、反向绕序、游离顶点、退化三角形均为 **0**，有向体积为正，Euler 特征数为 2；每顶点相邻面角法线差为 **0**。
- 20,001 个母线曲率采样均为正；另以 Bernstein 系数细分的正下界验证**整个参数区间**没有曲率变号。该验证针对连续设计曲线，不把有限多边形网格称为数学上无限光滑。
- [FBX 回导检查](verification/PerfectDroplet/roundtrip.json)：在另一个 Blender 进程读取导出文件；**1 网格 / 5,632 三角形**，最大世界顶点误差 **9.13×10⁻⁸ m**，法线角差最大约 **0.042°**，尺寸、尖端方向和源文件不变检查通过。
- [图像清单](verification/PerfectDroplet/renders.json)：四视图、三个反射条带角度、后端/尖端近景、三张布线图、三个高光角度，合计 **15 张**。已实际查看主体中性轮廓及不同角度高光，未见开缝、局部鼓包或条带突然中断。
- [保全检查](verification/PerfectDroplet/preservation.json)：任务开始登记的 **697 个既有文件**最终逐字节一致，覆盖 Assets、Packages、ProjectSettings、既有 ArtSource 和 Tools。

本轮未调用 Unity，没有进行模型替换、编译、EditMode、PlayMode、构建或运行时镜面材质验证；这些不属于本轮几何工作，不能用 Blender 检查宣称 Unity 实测通过。原有场景、飞船、旧水滴及爆炸效果保留。

## 轴向与后续使用

`.blend` 本体严格为用户要求的 **+Z 前 / +Y 上**。FBX 导出副本先使用行列式 +1 的旋转 `(x,y,z) → (−x,z,y)` 转到项目已有 **Blender +Y 前 / +Z 上**校准流程，再使用既有 FBX `axis_forward=-Z`、`axis_up=Y`、单位缩放 1、`bake_space_transform=true`。没有改源网格轴向或引入负缩放。Blender 默认回导按其 Z-up 约定显示为 +Y 前。

后续 Unity 导入沿用项目契约：`globalScale=1`、`useFileScale=true`、`bakeAxisConversion=true`，Normals 选 Import，禁用动画和自动碰撞体。预期 Unity 为 **+Z 前 / +Y 上**；本轮只完成 Blender 回导，尚未重新执行 Unity 的 ART-01 导入门槛。完整参数在 [export_manifest.json](../ArtSource/Exports/Droplet/PerfectDroplet/export_manifest.json)。FBX 暂存于 ArtSource/Exports，没有覆盖 Assets 中的旧水滴或改变游戏根节点、命中体积。

## 查看、复现与停止点

打开 `.blend`，默认只显示 `01_Game_5632tri`。选中模型，按小键盘小数点聚焦，旋转检查轮廓；Tab 可检查端部网格。要查看参考版，先隐藏游戏版，再在 Outliner 启用 `02_Reference_22528tri_HIDDEN` 集合，一次只显示一个版本。正视图这里按模型前进轴定义；Blender 默认的 Front 快捷视图与模型正前方不是同一方向。

在项目根目录依次执行 `python Tools/Blender/PerfectDroplet/run.py build`、`inspect`、`render`、`export`、`roundtrip`（每个阶段都带相同命令前缀）。脚本仅启动独立 Blender 进程，生成源具有哈希保护；如果用户已经手改源，脚本拒绝覆盖，需保留手改文件并在项目副本生成。

已知精度边界：游戏版是有限网格，毫米级尖端被放大到占满画面时仍能看到离散边段及插值痕迹；参考版适合更近的造型检查。没有声称任意放大、任意光照下绝对无离散误差。最终审美和实际 Unity 镜面表现留待用户后续验收。

新增文件集中在 `ArtSource/Blender/Droplet/PerfectDroplet/`、`ArtSource/Exports/Droplet/PerfectDroplet/`、`Tools/Blender/PerfectDroplet/` 与 `docs/verification/PerfectDroplet/`，完整 ZIP 位于 `ArtSource/Deliveries/`；更新本报告、STATUS、ENVIRONMENT 和资产来源记录。**本轮到水滴几何交付停止，不自动进入场景、太阳照明、材质升级或爆炸开发。**
