# 试玩版资产来源

## Seed 全音频新增来源 — 2026-09-29

由本项目自写提示词通过火山引擎语音服务 `seed-audio-1.0` 实际生成 **131 条独立非语音音效/音乐**；由 Seed-TTS 2.0 生成 **87 条新版英语角色干声**。本地仅对 Seed 产物做裁切、均衡、循环和混音，形成 **80 条完整通讯及 78 条强度变体**；新版没有下载音效库或用程序合成新声音源。六类代表样音获用户试听认可；131 条正式变体、87 条干声与 158 条通讯各项只有技术层的暂定通过，尚未逐项人工试听。

原始请求、模型、提示词、原声及成品 SHA-256 保存在 `ArtSource/Audio/SeedAudio20260929/` 与 `ArtSource/Audio/Generated/Volcengine/`。经 Unity Editor 导入的新版资源位于 `Assets/_Project/Audio/SeedAudio/`，并由默认启用的安全副本 `Assets/_Project/Scenes/FleetAssault_SeedAudio.unity` 使用；旧来源、旧场景和 GUID 仍保留。`Laser_03` 与 E033 原请求状态不明，各自以明确新 ID 的成功产物替代，原始记录未删除。本节只记录技术来源，不额外推定服务条款授予的分发权；发行前按当前服务协议复核。

## Seed-TTS 全音频能力试验 — 2026-09-29（未导入）

本轮 17 个原始 WAV 均由现有 Agent Plan 的 **Seed-TTS 2.0** 真实返回：1 条英语控制与 16 个非语音意图的测试候选；另有 2 次请求无有效音频。使用官方服务音色 `en_male_tim_uranus_bigtts` 与 `zh_female_vv_uranus_bigtts`，没有上传第三方人物录音、克隆人物声音或使用外部音效库。原始请求、用途、变体、用量和文件 SHA-256 见 [source-manifest.json](../ArtSource/Audio/SeedTTS20260929/source-manifest.json)。提示词由本项目编写。

本地仅将返回 PCM 无损封装成 WAV，没有另行合成游戏声源，也没有裁切、调音、循环或混合候选。非语音意图不代表生成结果已成为可用非语音素材；本轮内容门槛未通过，全部留在 Assets 外，未替换既有音频。使用继续依照现有账户适用的火山引擎服务条款，本记录不推断独占权或额外权利授予。下方旧程序声效来源仍然有效；没有将其重标为 Seed 生成。

## Enhancement 批次 — 2026-09-19

四角色英语对白、对应中文字幕（40 条开场与 36 条战斗广播）为本批原创文本，源文件 `Tools/Audio/EnhancementEnglish.json`。`Assets/_Project/Audio/EnhancementEnglish/` 中的 ConnectTone、InterruptTone、Alarm、EquipmentBed 为本项目数学合成 PCM 音效，没有第三方录音素材；生成方法保留在 `Tools/Audio/enhancement_voice.py`。新激光与局部日冕 Shader 为本项目源码，没有下载第三方效果包。

Seed-TTS 2.0 实际请求因资源授权返回 403，未产出新英语人声音频，不能将文本或预定音色声明为已取得的语音素材。现有中文合成语音及原 GUID 保留；游戏明确标识 AI 合成。详见 `ENHANCEMENT_AUDIO_REPORT.md` 和试听清单，新增音效待人工试听。此次未购买新资产或安装第三方工具。

## FusionFrigate 模型批次 — 2026-09-08

| 资产 | 制作与源文件 | 来源 / 使用说明 |
|---|---|---|
| FusionFrigate 舰体、推进系统、三档 LOD、四个基础材质 | 本轮在 Blender 实际新建；`Tools/Blender/build_fusion_frigate.py`、`ArtSource/Blender/Ships/FusionFrigate/FusionFrigate.blend` | 按用户概念参考重新建模，没有下载模型、纹理或插件；不声明参考设计本身的独占权 |
| 星舰概念参考 | 用户提供 `ArtSource/Spaceship`，保留原件；原字节副本 `ArtSource/References/FusionFrigate/FusionFrigate_reference.png` | 1448×1086 PNG，仅内部造型参照；未嵌入模型/FBX或作为游戏贴图；未确认参考图公开分发权 |
| 模型检视图 | 本轮 Blender Workbench 实际模型帧，Pillow 排版 | 不是新概念图；不含参考图像素，零生成式图像；排版文字使用本机系统字体，字体文件未打包或导出 |

本轮没有外部资源下载、购买、发布或第三方工具安装。来源副本哈希及交付范围见 [FUSION_FRIGATE_MODEL_REPORT.md](FUSION_FRIGATE_MODEL_REPORT.md)。以下保留此前批次记录。

本批未下载模型、纹理、字体或音效；未购买、上传或发布资源。

| 资产 | 制作与源文件 | 来源 / 使用说明 |
|---|---|---|
| 水滴、护卫舰、巡洋舰、指挥舰及各6块残骸 | 本项目原创程序化制作；Tools/Blender/build_fleet_assets.py、ArtSource/Blender/*.blend | 无第三方美术内容或额外署名要求，保留可编辑源与导出 |
| 40舰编队 | 本项目原创构图，fleet_layout.blend / FleetLayout_expected.json | 40个独立稳定ID，无外部布局来源 |
| 轻/重贯穿与飞行共鸣声 | 本项目原创数学合成，Tools/Audio/generate_audio.py | 实际PCM WAV；说明和hash在ArtSource/Audio/ORIGINAL_AUDIO.txt及manifest，无外部录音 |
| 星空、反射环境、风格化地球 | 本项目Editor生成网格/Cubemap及原创shader | 星球为程序化示意，不是地理测绘纹理；无外部卫星图像 |
| UI | 原创布局，Unity内置GUI字体/控件 | 当前显示英语，使用内置字形；无外部字体下载 |
| URP材质与Unity运行库 | 已安装Unity/URP | 依照项目原有Unity及包许可；未新增第三方软件包 |

游戏名称为 DropletPrototype / Fleet Assault，不包含外部影视音乐、美术、角色图像或商标素材。

## SpaceEnvironment 环境几何批次 — 2026-09-07 至 2026-09-08

上方“未下载纹理”“无外部卫星图像”等说明仅指此前 G05–G09 批次。本轮新增一张 NASA 地球底图；原正式关卡的程序化地球和既有资产继续保留。完整来源记录见 [Textures/SOURCES.md](../ArtSource/Blender/SpaceEnvironment/Textures/SOURCES.md)，实际导入和资源检查见 [环境几何报告](SPACE_ENVIRONMENT_GEOMETRY_REPORT.md)。

| 资产 | 制作与源文件 | 来源 / 使用说明 |
|---|---|---|
| 地球共享基础色，含大陆、海洋、海冰与云层 | `ArtSource/Blender/SpaceEnvironment/Textures/Earth_BaseColor_2048x1024.jpg`；原始 RGB 2048×1024 下载文件 | NASA Goddard Space Flight Center，Blue Marble 2002；Reto Stöckli 制作陆地、浅海和云图，Robert Simmon 增强海洋颜色并合成。历史卫星观测合成图，不是实时天气。全部 Earth LOD 共享一张图 |
| 太阳、地球球体及 UV；八种岩石及全部 LOD | `Tools/Blender/SpaceEnvironment/celestial_library.py`、`asteroid_library.py`；完整源为 `ArtSource/Blender/SpaceEnvironment/SpaceEnvironment.blend` | 本项目原创程序化几何，无第三方模型；没有从参考图提取模型或把参考图作为几何替代 |
| 空间布局、八个远景分区、内向天空球 | `Tools/Blender/SpaceEnvironment/build_environment.py`、`config.json` 及完整 `.blend` | 本项目原创三维布局和组合网格，固定随机种子；太阳、地球和近中景岩石独立可编辑 |
| 共享星空基础图 | `ArtSource/Blender/SpaceEnvironment/Textures/SpaceStars_BaseColor.png`，2048×1024 | `build_environment.py` 原创生成简单星点，不含外部摄影、参考图像素或银河雾；无额外第三方署名要求 |
| 构图参考图 | 用户提供的 `ArtSource/References/space_environment_reference.png` | 只用于内部构图观察；未作为游戏贴图、平面布景或全景天空球导入。未据此推定其发行许可 |

NASA 原始图来自 [官方图像归档](https://eoimages.gsfc.nasa.gov/images/imagerecords/57000/57735/land_ocean_ice_cloud_2048.jpg)，制作背景和署名见 [NASA Blue Marble 2002](https://science.nasa.gov/earth/earth-observatory/the-blue-marble-true-color-global-imagery-at-1km-resolution/)。本轮下载保留原始字节，没有新增日夜明暗、太阳辉光、大气或方向性场景照明。

使用依据为 [NASA Images and Media Usage Guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/) 和 [NASA 图像使用说明](https://heasarc.gsfc.nasa.gov/docs/www_info/credit/nasa.html)。NASA 通常不主张其图像版权；应注明 NASA 来源，不得暗示 NASA 为本项目背书。该底图没有 NASA 标志或可识别人物，引用的来源页没有标注第三方版权。这里不主张 NASA 源图的独占权，也不把 NASA 标志的使用权限扩大为本项目权限；未来宣传或发行仍应遵守这些来源条件。

已重新计算的源文件 SHA-256：

- `Earth_BaseColor_2048x1024.jpg`：`fb67ac030214c1891994c8f976e7f6c9cd5b0f21586aba8567250781a4fe708e`
- `SpaceStars_BaseColor.png`：`c01da67d8408f2dcefd80d6fa81d03da14738caff82e4219c84605c85caabeea`

哈希用于来源完整性，不代表运行内存。本轮没有购买、上传、发布资产或安装第三方工具；新增基础材质由 Unity 显式配置，不把 Blender 材质节点的可移植性或最终光效视作已完成。


## PerfectDroplet ???? ? 2026-09-08

??? UV ?????????????? `Tools/Blender/PerfectDroplet/pipeline.py`????? `ArtSource/Blender/Droplet/PerfectDroplet/PerfectDroplet.blend`???????????????????????????????????? Blender Workbench ?????????????? Blender ?? `check_reflection_horizontal.exr` / `fullmetal.exr` MatCap????????????????? FBX????????UV???????????????????????????????????????????


## PerfectDroplet 水滴几何 — 2026-09-08

模型和 UV 为本项目原创数学构造，源脚本 `Tools/Blender/PerfectDroplet/pipeline.py`，可编辑源 `ArtSource/Blender/Droplet/PerfectDroplet/PerfectDroplet.blend`。没有第三方模型、图片参考、贴图、字体、音乐或生成式美术内容。检查图是实际 Blender Workbench 渲染；额外法线诊断使用已安装 Blender 内置 `check_reflection_horizontal.exr` / `fullmetal.exr` MatCap，其资源文件未复制、嵌入源或打包到 FBX。导出只有几何、UV、平滑法线和一个中性材质槽，没有装饰资产。没有外部下载、购买、上传、发布或新增工具安装。

## CinematicAudio 通讯与战斗音频批次 — 2026-09-19

本批通过用户已接入的 Agent Plan 专属接口，实际生成 **87 条 Seed-TTS 2.0 英语人声**：40 条剧情、36 条战斗广播及 11 条代表场景/背景回应/短求救。台词、中文字幕和表演指令由本项目编写；四个角色固定使用服务官方音色 `en_female_dacey_uranus_bigtts`（主播）、`en_male_tim_uranus_bigtts`（指挥官）、`en_female_jane_uranus_bigtts`（工程师）、`en_female_stokie_uranus_bigtts`（通信军官），未上传第三方人物录音或进行声音克隆。实际请求、选用文件及来源哈希见 [generation-manifest.json](../ArtSource/Audio/CinematicAudio/generation-manifest.json) 和其中关联的生成日志。其使用依照现有账户适用的火山引擎服务条款；本记录不推断生成音频、服务音色或角色声线的独占权，也不把成功调用等同于额外权利授予。

非语音素材为本项目原创程序合成：脚跟/脚尖脉冲激励钢甲板模态形成脚步，另有门机构、控制台、设备振动、警报、通讯接通/断线、激光、反射、撞击及爆炸效果。脚步、舱门和爆炸各保留 3 个实际不同的变体；源参数、种子、文件和 SHA-256 记录在 [effects-manifest.json](../ArtSource/Audio/CinematicAudio/effects-manifest.json)，生成工具为 `Tools/Audio/cinematic_audio.py`。未采用外部录音、音效库或音乐，也未把程序声效描述为真实人物/船舱录音。

上述人声与程序声效经 FFmpeg 形成 80 个完整通讯场景及其必要强度变体，原始人声、分轨、母版和混音配方保存在 `ArtSource/Audio/CinematicAudio/` 及原生成目录，实际使用的成品导入 `Assets/_Project/Audio/CinematicAudio/`。混音制作不改变源素材的适用权利条件。原音频、美术及其 GUID 保留；本节不覆盖此前批次的来源记录。主观声音与表演验收仍标记“待人工试听”，不将信号检查当作听感验收；详情见 [CINEMATIC_AUDIO_REPORT.md](CINEMATIC_AUDIO_REPORT.md)。
