# 试玩版资产来源

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
