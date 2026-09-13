# 水滴重建报告 — 2026-09-08

已按 `ArtSource/droplet`（无扩展名 PNG，1448×1086）重新构造旋转曲面。参考只用于轮廓与饱满程度，没有描画银河、亮斑或制作表面装饰。本轮不再采用旧版“尖端 +Z”要求。

## 上次实际错在哪里

- **首尾语义反向，并被错误验收标准放行。** 旧 `Tools/Blender/PerfectDroplet/pipeline.py:28` 把尖端设在 +Z；`PerfectDropletImport.cs:127` 强制检查“+Z tip”。`DropletMotor.cs:55–59` 的正速度却沿局部 +Z 位移。旧实物在本轮 Unity 复查中，+Z 端附近半径仅 **0.011882 m**，−Z 端 **0.122214 m**，所以实际尖尾在前。
- 旧测试只检查运动与 Transform，没有测实际头尾位置相对于位移的点积。此前“朝向正确”的报告不能作为本次依据。
- 本轮打开旧 `.blend` 并检查实际 Unity 层级：**没有发现**负缩放、重复旋转、内外翻面、重叠壳体、重复 Renderer 或 Animator。不能据此虚构“葫芦/破洞”等故障。旧生成器和导入器均为显式执行，没有查到运行时重新换网格的脚本。
- `DropletMotor.cs:23、69、76` 会更新 `VisualRoot` 世界姿态，这是原有插值/暂停/重开行为。因此不能把纠正朝向的旋转留在这个节点上。

证据：`verification/Droplet_Rebuilt/previous-diagnosis.json`、`unity-previous-diagnosis.json`。旧文件、未提交修改和旧构建全部保留，未执行仓库回滚。

## 新模型与坐标

圆头 **局部 +Z**，尖尾 **局部 −Z**，上方 **+Y**；包围盒中心在原点，所有导出对象为单位正缩放。单个连续圆截面外壳，六个控制点定义一条平滑五次母线，侧面规则四边面，端部用三角扇封口。控制参数及采样保存在 `.blend` 的 `PROFILE_PARAMETERS.json` 与生成脚本中；检查曲线和三个 Empty 标记不是表面装饰。

- 长度 **2.4 m**，最大直径 **0.8 m**，比例 **3:1**。长度依据稳定旧模型生成器 `build_fleet_assets.py:224–236` 与 Lighting 场景既有 2.4 m 实测；直径是按本轮比例新定，未继承失败版本的 0.774194 m。
- 最宽处 z≈**+0.654 m**，约在从尾向头的 **77.25%** 位置；长而连续地向尾部收缩。尾端解析收口曲率半径约 **0.557 mm**。
- **3,522 顶点、7,040 三角形**；源网格 3,456 四边面及 128 端部三角面。仅一个游戏模型，无 LOD、雕刻版本、贴图或装饰几何。
- 命中半径保留 **0.7 m**，视觉最大半径为 0.4 m，二者不等大；没有调整已有命中手感。

只在导出副本应用 `(x,y,z) → (−x,z,y)`（行列式 +1），进入既有 Blender +Y 前/+Z 上校准流程；FBX 使用 `axis_forward=-Z`、`axis_up=Y`、单位比例、`bake_space_transform=true`。Unity 使用 `bakeAxisConversion=true`、导入法线、单位比例、禁用动画和自动 Collider；导入后无补偿 Transform。

Blender 保存 HeadMarker/TailMarker/ForwardMarker。FBX 仅导出表面，避开项目已记录的 Empty 转轴问题；Unity 根据**实测导入网格的两端**在视觉子节点下建立同名非渲染标记，并验证圆头宽度确实在正端。

## 实际验证与限制

1. Blender 5.2.1 LTS 实际生成、保存、重开、灰模/镜面渲染、导出与回导均执行。单体闭合；开边、非流形边、重复面/顶点、零面积面、反向面、非相邻面交叉均 **0**；法线向外。回导位置最大误差 **9.13×10⁻⁸ m**、法线最大误差 **0.0485°**。已在可见 Blender 窗口打开主 `.blend`。
2. 灰模侧面、四分之三、头尾视角；镜面五视角、7 帧实际相机移动序列及两侧尾端放大均检查。临时条带环境无高分辨率贴图、Bloom、运动模糊或过曝。尾端黑白小环随视角变化，封口没有孔洞或凹陷；**24 mm 视幅的极端放大可见有限网格的轮廓折线和轻微反射分段**，不宣称任意尺度完美。母线半径单峰且连续，不宣称整个区间严格凸。
3. Unity 实际编译通过，专项 **EditMode 3/3**、专项 **PlayMode 3/3**；完整 **PlayMode 47/47**，含高速/初始重叠/满查询缓冲/复合碰撞去重、计分、暂停、重开。旧会重写 TestRange 的 EditMode 作者测试未运行；不把专项测试称为全 EditMode。
4. 新可玩场景从稳定 Lighting 场景复制。仅替换原 `VisualRoot` 内唯一 MeshFilter，原 Renderer 和正式材质保留，增加三个非渲染标记。PlayerRoot、Motor、HitDetector、输入、相机目标、参数引用、120 舰与 1,080 Collider 保持；任务仍 **540 秒**。保存重开及重导入后的引用和方向测试通过；120 舰真实扫掠胜利与三次重开通过。
5. 合成 Input System 键鼠事件驱动**原真实 FixedUpdate/LateUpdate**，前向直线 **534.678 m / 4.920 s**，贯穿 **5 舰 / 1,500 分**。记录每次世界 `HeadMarker−TailMarker` 和非零实际位移，最小归一化点积 **1.000000**。左右/上下、相机正立、暂停、刹车、重开均通过，效果池 **57→57**。模型中心相对 VisualRoot 误差 **0**；高速时原插值视觉与物理根有至多约 **3.10 m** 的采样差，检查限定在原有两个固定步范围内，未改插值代码。零速、横移、瞬移、转向过渡不参加直线断言。
6. Windows x64/Mono 非 Development 构建成功，0 错误、1 条已有可选 Pipeline 运行桥未配置警告；构建报告确认实际打包新 FBX 网格，约 **123.2 MiB**。可见 **1920×1080 / Direct3D12** 独立 Player 实际运行，既有验证器 **32/32** 检查通过，19 张不同实际截图；包括模型显示、贯穿计分、暂停、120 舰胜利和三次重开。`Player/RebuiltVisible-20260908-130839-513/lighting-validation.json` 为最终结果。此前隐藏窗口运行 2 个反射捕获检查失败、截图停滞，该记录保留且不计通过；可见窗口复测修复了此验证条件，未修改正式灯光或反射代码。不以编译代替 Player 验证。

本轮修复过检视工具自身的问题：近距尾端相机 near plane 裁切、未使用的灰材质重开后释放、Unity 检视帧同步以及异步采样与插值的时间差；均只调整检查工具，不修改运动/相机/碰撞规则。Unity 检视使用独立灰材质和解析条带镜面，避免小 Cubemap 采样锯齿；正式材质和反射系统未改。

未验证：用户物理键鼠主观手感、听感、长期自由飞行及其他硬件；自动测试不代表用户艺术审美验收。旧 EXE 保持旧模型；请用本轮新场景或新构建。

## 打开与回退

- 主源：`ArtSource/Blender/Droplet/Droplet_Rebuilt/Droplet_Rebuilt.blend`。
- FBX：`ArtSource/Exports/Droplet/Droplet_Rebuilt/Droplet_Rebuilt.fbx`；Unity 导入副本在 `Assets/_Project/Art/Models/Droplet_Rebuilt/`。
- 可复用视觉 Prefab：`Assets/_Project/Prefabs/Player/Droplet_Rebuilt.prefab`。
- 检视场景：`Assets/_Project/Scenes/Droplet_Rebuilt_Review.unity`。灰材质默认保存；菜单 `DropletPrototype/Rebuilt Droplet/4 Capture Review` 拍摄五角度灰模/镜面检查图。
- 试玩：`Assets/_Project/Scenes/FleetAssault_Droplet_Rebuilt.unity`，Play 后聚焦 Game、Enter 开始；鼠标转向、W/S 调速、Shift 冲刺、Space 刹车、Esc 暂停、暂停/结算时 R 重开。
- 独立版：`Builds/Windows-Droplet-Rebuilt-20260908/DropletPrototype.exe`。
- Blender 检查图/GIF：主源旁 `Previews/`，主要看 `Gray-Side.png`、`Gray-ThreeQuarter.png`、`Mirror-ThreeQuarter.png`、`Mirror-Sweep.gif` 和尾端 Detail。
- 实际运动证据：`docs/verification/Droplet_Rebuilt/Live-HeadTail-Motion-Evidence.png`、`Live-FiveHits.png`、`live-flight.json`；其余原始结果在同目录。
- 回旧稳定版：直接打开原 `FleetAssault_Lighting.unity` 或原 Windows-Lighting 构建。回看失败版：原 `FleetAssault_PerfectDroplet_Test.unity` 和 `PerfectDroplet.blend` 仍在。额外基线备份为 `verification/Droplet_Rebuilt/BaselineBackup/`，无需回滚仓库或清空场景。

## 修改范围

新增 `Tools/Blender/Droplet_Rebuilt/`、上述新源/导出/Prefab/两个场景、仅检视用材质/Shader、`DropletRebuiltPipeline.cs`、`DropletRebuiltLiveCheck.cs`、两份专项测试和验证证据。本次既有运行时代码、太阳、舰队、爆炸、渲染管线与包配置不改。仅 STATUS/ENVIRONMENT 追加本轮交付记录。最终文件清单和保全结果见 `verification/Droplet_Rebuilt/preservation-final.json`。

本次重建结束后停止，不进入其他美术或玩法里程碑。

最终保全：任务前 **763** 个文件中，**761** 个字节哈希一致，仅 `docs/STATUS.md` 和 `docs/ENVIRONMENT.md` 新增本轮记录。包括失败源/导出、全部旧 Assets、Packages、ProjectSettings 与旧脚本在内的文件保持原样；Git 原有修改不处理。
