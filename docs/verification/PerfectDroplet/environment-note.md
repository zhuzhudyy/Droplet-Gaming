## PerfectDroplet 几何批次 — 2026-09-08

实际使用已安装 **Blender 5.2.1 LTS**，构建 **9e2066aef7ef**（2026-08-25），执行程序仍为 `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`。构造、重开、渲染、导出和回导在独立 `--background --factory-startup` 进程运行，没有覆盖交互式 Blender 会话。内嵌 Python 实测 **3.13.13 / MSC v.1944 64 bit AMD64**；外部 Python **3.14.3** 用于参数数组启动和文件证据。使用随 Blender 提供的 bpy、bmesh、mathutils、Workbench 和 FBX 7.4 导入导出器；没有安装或下载。

本轮读取的项目仍为 Unity **6000.5.10f1 (3bd4f66ad299)**、URP **17.5.0**、Input System **1.20.0**、Unity Test Framework **1.7.0**；既有 Custom NUnit **2.1.0**、Pipeline **0.6.0-exp.1** 保留。ProjectVersion、manifest 和整个 Packages/ProjectSettings 的任务前后哈希一致。**本轮未调用 Unity，未运行其编译、EditMode、PlayMode、构建或新模型导入校准**；不把历史结果当成本轮通过记录。

源为用户要求的 +Z 前 / +Y 上；只在导出副本通过 `(x,y,z) → (−x,z,y)` 转到既有 Blender +Y 前 / +Z 上校准流程，沿用 `axis_forward=-Z`、`axis_up=Y`、单位缩放 1 和 bake_space_transform。独立 Blender FBX 回导对比位置与法线通过；后续 Unity 使用既有 bakeAxisConversion 和 Import Normals 契约，仍需实际 ART-01 检查。四视图用临时中性材质，额外高光/条带使用 Blender 内置 MatCap 作法线诊断；没有新场景灯光、反射探针或正式材质。详见 `docs/DROPLET_GEOMETRY_REPORT.md` 和 `docs/verification/PerfectDroplet/` 的实际日志/JSON。
