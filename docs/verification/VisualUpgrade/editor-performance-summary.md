视觉升级性能比较（自动提取实际 JSON）

修改前：Editor 测量；NVIDIA GeForce RTX 4060 Laptop GPU；1920×1080；Assets/_Project/Scenes/FleetAssault_Droplet_Rebuilt.unity。
修改后：Editor 测量；NVIDIA GeForce RTX 4060 Laptop GPU；1920×1080；Assets/_Project/Scenes/FleetAssault_VisualUpgrade.unity。

| 场景 | 前 FPS | 后 FPS | 前 P95 ms | 后 P95 ms | 前/后 GPU 均值 ms | 前/后实际帧数 | 前/后焦点帧比例 | 前/后摧毁数 |
|---|---:|---:|---:|---:|---|---|---|---|
| NearCombat | 162.48 | 157.03 | 6.43 | 8.03 | 1.30 / 0.01 | 975 / 943 | 975/975 / 943/943 | 0 / 0 |
| FleetMedium | 162.27 | 147.80 | 6.42 | 7.65 | 1.31 / 0.04 | 974 / 887 | 974/974 / 887/887 | 0 / 0 |
| ConcentratedExplosions | 149.14 | 159.45 | 12.29 | 9.10 | 1.01 / 1.39 | 895 / 957 | 895/895 / 957/957 | 37 / 37 |
| FacingSun | 162.40 | 162.39 | 6.37 | 6.37 | 1.21 / 1.58 | 975 / 975 | 975/975 / 975/975 | 0 / 0 |

每阶段先暖机 60 帧，再采样至少 480 帧且至少 6 秒。双方 VSync=0、帧率不封顶。集中爆炸按每 0.16 秒一次真实 Motor 扫掠；目标起点重设用于控制实验，不代表自然操作节奏。若帧率低于 80，达到 480 帧所需时间可能超过 6 秒，攻击总数也可能不同，摧毁数已经单独列出。

| 阶段 | 前/后 SetPass 均值 | 前/后三角形均值 | 前/后 GC 字节/帧 | 后爆炸峰值/容量 | 前/后实时灯峰值 | 前/后粒子峰值 | 前/后探针捕获数 |
|---|---|---|---|---|---|---|---|
| NearCombat | 66.26 / 74.27 | 826028.08 / 1740940.62 | 12721.51 / 19287.44 | 0 / 12 | 1 / 1 | 0 / 0 | 1 / 1 |
| FleetMedium | 69.00 / 73.26 | 672887.00 / 1401639.80 | 12715.55 / 19182.44 | 0 / 12 | 1 / 1 | 0 / 0 | 0 / 0 |
| ConcentratedExplosions | 69.14 / 84.45 | 618730.94 / 1207948.13 | 13575.31 / 13641.88 | 12 / 12 | 1 / 3 | 0 / 0 | 2 / 2 |
| FacingSun | 75.25 / 81.26 | 849999.57 / 1748605.37 | 12928.36 / 12646.99 | 0 / 12 | 1 / 1 | 0 / 0 | 1 / 1 |

| 库存（第一阶段开始） | 修改前 | 修改后 |
|---|---:|---:|
| 场景全部 Renderer（含关闭的池和 LOD） | 7456 | 7544 |
| 场景共享材质种类 | 17 | 18 |
| 名称含 Instance 的场景材质 | 0 | 0 |
| 旧特效池 Transform 数 | 57 | 9 |
| 反应堆池 Transform 数 | 0 | 135 |
| 同时反应堆爆炸预算 | 0 | 12 |
| 反射探针数 | 1 | 1 |
| 启用的实时反射探针 | 1 | 1 |
| ParticleSystem 数 | 0 | 0 |

未取得样本的 Profiler 标记显示‘未测得’，不能解释为零成本。GPU 值来自 FrameTimingManager 的可用样本；总帧时包含当前进程与 Editor（如有）的开销，不等于纯 GPU 时间。共享材质库存不等于完整 GPU 显存占用。

前验证：completed=True，allChecksPassed=True；证据：docs/verification/VisualUpgrade/Editor/Before-20260908-132623-784/visual-upgrade-validation.json。
后验证：completed=True，allChecksPassed=True；证据：docs/verification/VisualUpgrade/Editor/After-20260908-134145-326/visual-upgrade-validation.json。
