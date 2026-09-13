# G09 实测与独立版本验证

2026-09-07，最终程序 `Builds/Windows-v0.2.2-G09/DropletPrototype.exe`。本机短路线的1080p约60 FPS目标达到：全部已采集帧间隔低于16.67ms。这不是GPU耗时、显示器实际刷新FPS或长期性能保证。

## 条件与方法

Windows11 build26200，Ryzen9 7940HX，约15.21GiB系统内存，RTX4060 Laptop（7956MB显存，已记录驱动32.0.16.1062）。Unity6000.5.10f1 / URP17.5.0，Direct3D12，1920×1080窗口，PC质量、High表现，40互动舰船。非Development WindowsPlayer，vSync=0、targetFrameRate=-1，全部测量样本窗口在前台。机器同时有Editor及其他既有应用运行，未为测量强制关闭程序。

实际运行显式CLI benchmark：预热3秒；正常飞行8秒；密集贯穿8秒；暂停0.3秒验证冻结；40舰胜利；连续3次重开和瞬移无伤检查。飞行通过既有MissionController.Step和唯一DropletMotor，不另写碰撞器。密集路线明确重新定位到实际舰船前并扫过，属于可复现压力路线，不代表玩家自然一局的路线或用时。

Time.unscaledDeltaTime记录渲染循环帧间隔；ProfilerRecorder记录可获得的Main Thread计数器。截图捕获、检查镜头等待与暂停不进入飞行采样。每次重开只采一个随后帧，共3帧，因此没有把它称为大量样本或长期稳定性测试。

## 最终Windows v0.2.2

| 阶段 | 前台/全部样本 | 平均帧间隔 | P95 | 最大 | Main Thread P95 | Unity已分配 / 预留MiB | 效果/音源峰值 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 正常飞行，约8秒 | 7132/7132 | 1.122ms | 2.126ms | 5.990ms | 2.126ms | 179.147 / 269.020 | 4 / 2 |
| 密集贯穿，约8秒 | 6587/6587 | 1.215ms | 2.192ms | 6.691ms | 2.191ms | 179.694 / 271.020 | 16 / 6 |
| 三次重开的随后帧 | 3/3 | 1.327ms | 1.715ms | 1.715ms | 1.714ms | 180.040 / 271.020 | 0 / 0 |

效果池始终441个Transform对象，High最多16个同时效果、8个冲击音槽；实际音源峰6。Mono已用快照分别4.113/4.473/4.648MiB。上述内存是Unity分配器/Mono快照，不是整个进程工作集；采样与截图等也在本进程内。短时间内存增长不能据此判定泄漏或无泄漏。

**独立GC逐帧分配未测量**：gcRecorderAvailable=false、samples=0，JSON里的零数值是空分布，不是零分配。未取得GPU timestamp；没有长时间浸泡、其他硬件或刷新率矩阵。未据不存在的数据进行ECS/Jobs/自定义渲染等架构改造。

原始报告：[benchmark.json](verification/G05-G09/PlayerBenchmark/20260907-123115-512/benchmark.json)、[summary.txt](verification/G05-G09/PlayerBenchmark/20260907-123115-512/summary.txt)、[玩家日志](verification/G05-G09/player-v0.2.2-benchmark.log)。20项记录全部通过，其中包括7项截图落盘检查；其余验证40目标/1080p/池配置、单段3舰、真实事件残骸、暂停、40舰胜利且结算一次、3重开、41次ResetPose不伤沿途目标。不是“20种独立玩法测试”。

真实画面已由主代理和独立审查代理查看：[普通飞行](verification/G05-G09/PlayerBenchmark/20260907-123115-512/02-flight.png)、[连续贯穿](verification/G05-G09/PlayerBenchmark/20260907-123115-512/03-chain.png)、[局部冲击](verification/G05-G09/PlayerBenchmark/20260907-123115-512/03-impact-inspection-flash.png)、[残骸](verification/G05-G09/PlayerBenchmark/20260907-123115-512/03-impact-inspection-wreck.png)、[结算](verification/G05-G09/PlayerBenchmark/20260907-123115-512/04-results.png)。冲击/残骸为明确标注的临时侧后检查镜头，不冒充正常追尾视角。最终目标文字深色底修复已在实际星球背景前观察确认。

## Editor与构建是独立的检查

- 实际Editor完整回归：EditMode15/15、PlayMode27/27，最新release-*.json。仍保留旧场景与测试。
- Editor初步1080p High实测：`EditorBenchmark/20260907-121520-484`，正常1264帧平均6.33/P956.45ms，密集1188帧平均6.74/P9512.30ms。该采样发生在最后柔边闪光/文字修复前，不代替最终玩家数字。早期失败路线与截图也保留。
- v0.2.0、v0.2.1玩家候选分别实际跑通，证据保留；最终引用v0.2.2，不混用版本数字。
- v0.2.2实际原生Windows构建成功，16.237秒，报告195文件/118856072 bytes（不含随后添加的说明文档），0错误/1可选Pipeline桥警告。见 [build report](verification/G05-G09/windows-v0.2.2-build.json)。未安装模块，未替换管线。构建绝对时间由工具原样记录，已知存在时区偏差，以原报告时长为准。
- 完整包校验和文件清单见 `verification/G05-G09/package-verification.json`。运行需要exe、Data、MonoBleedingEdge、UnityPlayer/D3D12等完整相邻文件；ZIP仅排除Burst DoNotShip符号目录，符号原目录保留。

普通启动和鼠标菜单设置确实操作过；自动路线没有验证真实键盘、主观鼠标手感或听感。人工步骤见 [试玩说明](PLAYTEST_FLEET.md)。
