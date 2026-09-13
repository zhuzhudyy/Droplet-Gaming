视觉升级性能比较（自动提取实际 JSON）

修改前：独立运行测量；NVIDIA GeForce RTX 4060 Laptop GPU；1920×1080；Assets/_Project/Scenes/FleetAssault_Droplet_Rebuilt.unity。
修改后：独立运行测量；NVIDIA GeForce RTX 4060 Laptop GPU；1920×1080；Assets/_Project/Scenes/FleetAssault_VisualUpgrade.unity。

| 场景 | 前 FPS | 后 FPS | 前 P95 ms | 后 P95 ms | 前/后 GPU 均值 ms | 前/后实际帧数 | 前/后焦点帧比例 | 前/后摧毁数 |
|---|---:|---:|---:|---:|---|---|---|---|
| NearCombat | 329.70 | 219.46 | 3.89 | 6.27 | 未测得 / 未测得 | 1979 / 1317 | 1979/1979 / 1317/1317 | 0 / 0 |
| FleetMedium | 250.49 | 189.75 | 5.42 | 6.59 | 未测得 / 未测得 | 1503 / 1139 | 1503/1503 / 1139/1139 | 0 / 0 |
| ConcentratedExplosions | 346.12 | 259.66 | 3.96 | 5.15 | 未测得 / 未测得 | 2077 / 1558 | 2077/2077 / 1558/1558 | 38 / 37 |
| FacingSun | 300.97 | 241.16 | 4.41 | 5.28 | 未测得 / 未测得 | 1806 / 1447 | 1806/1806 / 1447/1447 | 0 / 0 |

每阶段先暖机 60 帧，再采样至少 480 帧且至少 6 秒。双方 VSync=0、帧率不封顶。集中爆炸按每 0.16 秒一次真实 Motor 扫掠；目标起点重设用于控制实验，不代表自然操作节奏。若帧率低于 80，达到 480 帧所需时间可能超过 6 秒，攻击总数也可能不同，摧毁数已经单独列出。

| 阶段 | 前/后 SetPass 均值 | 前/后三角形均值 | 前/后 GC 字节/帧 | 后爆炸峰值/容量 | 前/后实时灯峰值 | 前/后粒子峰值 | 前/后探针捕获数 |
|---|---|---|---|---|---|---|---|
| NearCombat | 66.12 / 74.19 | 775416.16 / 1718676.60 | 未测得 / 未测得 | 0 / 12 | 1 / 1 | 0 / 0 | 1 / 1 |
| FleetMedium | 71.00 / 72.99 | 672960.63 / 1304131.26 | 未测得 / 未测得 | 0 / 12 | 1 / 1 | 0 / 0 | 0 / 0 |
| ConcentratedExplosions | 68.54 / 84.11 | 574336.87 / 1140909.66 | 未测得 / 未测得 | 12 / 12 | 1 / 3 | 0 / 0 | 2 / 2 |
| FacingSun | 75.15 / 82.18 | 829758.47 / 1717213.76 | 未测得 / 未测得 | 0 / 12 | 1 / 1 | 0 / 0 | 1 / 1 |

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

前验证：completed=True，allChecksPassed=True；证据：docs/verification/VisualUpgrade/Player/Before-20260908-134804-541/visual-upgrade-validation.json。
后验证：completed=True，allChecksPassed=True；证据：docs/verification/VisualUpgrade/Player/After-20260908-134907-539/visual-upgrade-validation.json。
