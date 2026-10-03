# 本批验收 — 2026-09-19

打开 `Assets/_Project/Scenes/FleetAssault_Enhanced.unity`；小规模检查副本为 `FleetAssault_Enhanced_Small.unity`。旧场景、旧构建和用户开始时的未提交修改均保留。此次没有重新创建工程、升级 Unity/URP、缩减正式舰数或更换水滴/舰船/太阳系布局。

**部分完成，有一项真实外部阻塞：Seed-TTS 2.0 返回 HTTP 403 / 45000030 / requested resource not granted。** 新英语实音频和实际 4–6 分钟剧情未完成。场景明确显示 `AI VOICE: ZH (legacy)`，保留与旧中文音频匹配的字幕及 60 秒 Timeline。没有把静音、词数估计或旧中文包当作新英语剧情通过。详见 [音频报告](ENHANCEMENT_AUDIO_REPORT.md)。

## 改动与基线

- A：原创四角色、四幕开场 40 条／899 英语词和对应中文字幕；36 条事件广播。新增四个独立真实 PCM 通信音、警报和底噪；通过 Editor API 创建 Voice/Signal/Alarm/Background/Combat AudioMixer，广播时 Combat 下降 3 dB，警告可抢占普通话音。保留死亡舰实时发言过滤和爆炸求救中断。实际音频长度驱动 Timeline 的集成入口已实现，但新英语资源未获许可，未启用空库。
- B：保留真实 7040 三角表面命中、平滑法线、Vector3.Reflect、遮挡和二次命中。修复短脉冲只跟随接触点导致旧光路被拉弯；整个瞬时光路固定，局部余辉以网格局部坐标跟随水滴。新深度测试发光 Shader、像素宽度下限、0.18 秒渐隐、少量池化粒子；无逐束实时灯，表现不重复计伤。
- C：原固定 34° 圆盘改为物理约 0.213162° × 美术倍率 2 = 约 0.426324°；光晕范围单独配置，曝光固定为 0，FOV 保留 65。主方向光继续绑定太阳方向。新增独立 Renderer 与 URP17.5 RenderGraph/HLSL 局部日冕：半分辨率、16 次采样、视线深度裁切和全分辨率边缘遮挡，质量 Off 关闭；并非 Bloom/径向模糊。旧 Renderer 与默认索引保留，仅向管线列表添加新 Renderer 并在新场景相机选择。
- D：基线正常前进 12 秒已造成 2 舰爆炸、1 舰待爆、27 艘脱阵，因此不能声称旧 AI 完全不工作。实际修复旧 0.7–5.5 秒反应和额外最高 2.4 秒延迟，统一约 1–3 秒不同反应；先提交所有舰位置和实例矩阵，再发送爆炸/威胁事件，避免同一事件混用前后帧位置。保留待爆失能漂移、相对运动扫掠和独立逃脱计数。新增可关闭 ID／原因／速度／0、3、10 秒绝对米坐标诊断，包含渲染误差和原点重定位补偿。

共享接口及文件归属见 [实施约定](ENHANCEMENT_CONTRACT.md)。主代理负责 C、共享接口、所有 Editor 场景与资产写入；A、B、D 三子代理并行，只有 A 执行云语音请求。

## 实际检查

- Unity 6000.5.10f1 实际编译通过；URP17.5.0、Timeline1.8.13、InputSystem1.20.0、TestFramework1.7.0 未升级。
- 新逃跑 EditMode **6/6**：[结果](verification/Enhancement-20260919/fleet-editmode.json)。
- 完整 PlayMode **91/91**：[结果](verification/Enhancement-20260919/playmode.json)。覆盖保存的 2000 舰正常攻击、LOD/碰撞同步、150km/s 相对扫掠、反射及计伤、警告抢占、求救中断、暂停、结算和重开。未运行会重写旧 TestRange 的作者 EditMode 测试。
- Python 音频离线检查 **14/14**，不替代云端成功或听音验收。
- 已保存并实际进入 12 舰副本 Play，随后统一保存正式 2000 舰副本。此前脚本写入/域重载期间的临时编译错误已修复；一轮控制工具超时的 PlayMode 请求已取消，最终以上完整 91/91 重新运行结果为准。
- 旧场景与原有 meta 基线 **136/136** 不变：[保护检查](verification/Enhancement-20260919/protection.json)。

### 独立版与画面

已实际构建并运行 `Builds/Windows-Enhanced-20260919/DropletGaming.exe`。初次和补充帧计时版均为 **22/22 功能检查通过、退出码 0**；2000 个独立身份、旧 60 秒剧情自然结束/四分位暂停跳过、正常贯穿触发逃跑、暂停冻结、200 舰延爆一次性结算、连续三次重开和超时结果均有实际日志。旧剧情检查不代表新四幕英语剧情通过。

正常 FixedUpdate 攻击后有 15 艘完好舰实际移动，没有调用 ForceFlee/RequestRetreat。例 `SPAWN_Small_FS2C25R01`：NearbyPenetration，反应 2.4819 秒，10 秒时为 Intact/Fleeing、2.9584 km/s，绝对坐标从威胁点移出约 17.1 km；权威位置、Transform、远景实例误差均为 0。远景模型关闭近景 Collider 时仍由移动形状/空间分区扫掠检测，不能把 `colliderEnabled=false` 解读为目标不可交互。证据：[独立版](verification/Enhancement-20260919/PlayerFinal/report.json)、[0/3/10 秒诊断](verification/Enhancement-20260919/PlayerFinal/normal-escape.json)。

补充 [正常高速采样回放](verification/Enhancement-20260919/normal-flight-sampled.mp4) 与 [刹车转向观察采样回放](verification/Enhancement-20260919/escape-observation-sampled.mp4)。帧040可见 30 km/s 下蓝白入射、接触与橙色反射；后者使用普通飞行命令让邻舰进入画面，视角稳定后目标提示向右离阵，保留 ID/世界坐标 CSV。拍摄方法和限制见 [画面说明](verification/Enhancement-20260919/VISUAL_EVIDENCE.md)，视频是3–4FPS采样回放，不用于测游戏FPS。正式独立版另保留太阳开关、出屏、实际舰体遮挡和近景延爆截图。

逐帧复核发现旧接触闪光在高速前进后残留，已追加修复：世界接触亮点与少量粒子最多 2 个非暂停渲染帧，且不超过 25 ms 模拟时间；完整光路仍固定并渐隐 0.18 秒，网格局部高光保持跟随表面。实际重新编译及该类 PlayMode **4/4** 通过，包含高速移动、跨暂停帧、粒子清除、池槽复用和不改变反射路径：[追加回归](verification/Enhancement-20260919/laser-final-playmode.json)。此前完整91项与此追加4项分别报告，没有宣称重新运行完整92项。

修复后中间轮 `PlayerRelease` 为 **21/22、退出码2**，记录保留：首个真实威胁晚到模拟15.64秒，65张固定壁钟截图结束前尚未到威胁后的10秒检查点；65份已采样记录的位置误差全部为0。随后只修验证器：专用输入设备隔离外部鼠标事件，检查显式等待实际10秒样本（有限20秒看门狗），不通过修改舰船行为或放宽误差阈值消除失败。GPU FrameTiming 曾出现不可能的数十亿毫秒时间戳，验证器另记录并剔除非有限/超过1000ms的GPU样本，原始中间报告保留，不将异常值宣称为实测GPU成本。

### 最终独立版性能与结果

接触亮点修复后另实际录制18秒正常FixedUpdate飞行：[最终正常飞行采样回放](verification/Enhancement-20260919/normal-flight-final-sampled.mp4)，112帧与壁钟CSV保留；HUD30km/s，正常贯穿4舰并有1舰待爆，未调用伤害/逃跑或手动FireRay。它与此前暴露残留问题的历史视频分开保存。

**最终修复包实测22/22、退出码0**：[最终运行](verification/Enhancement-20260919/PlayerAccepted/report.json)、[进程退出](verification/Enhancement-20260919/PlayerAccepted/vram.csv.exit.json)。最终构建30.1秒、0错误；唯一警告为未配置可选 Pipeline Runtime 调试桥，游戏不依赖该桥：[构建报告](verification/Enhancement-20260919/build-accepted.json)。正常威胁诊断15舰各有0/3/10秒样本，三套位置误差0；最终所有功能与性能阶段均保留2000身份。

本机 Ryzen9 7940HX／RTX4060 **Laptop**，Windows独立版、D3D12、PC画质/High特效、1920×1080原生、renderScale=1、无帧生成，vSync关闭，帧率不设上限。每阶段至少480帧且不少于6秒；所有采样帧均为前台聚焦。Editor在后台打开；无本地TTS、FFmpeg编码或其他游戏测试并行。该包开启FrameTiming统计，原项目设置在构建后恢复。短期单机数据不能代替长期热机。

| 场景 | 平均 FPS | 平均 ms | P95 ms | CPU帧均值 ms | GPU帧均值 ms |
|---|---:|---:|---:|---:|---:|
| 集中反射 | 81.56 | 12.262 | 17.002 | 12.281 | 3.332 |
| 局部体积关 | 107.74 | 9.281 | 12.975 | 9.301 | 2.746 |
| 局部体积开 | 101.33 | 9.869 | 13.752 | 9.811 | 2.848 |
| 近景200舰延爆＋自然撤退 | 86.96 | 11.500 | 15.959 | 11.514 | 3.132 |
| 其余1800舰撤退 | 112.41 | 8.896 | 12.745 | 8.855 | 2.639 |

**没有达到P95≤12.5ms的稳定80FPS目标。** 体积开相对关观察到整帧GPU均值约+0.102ms、整帧平均约+0.587ms；这不是独立GPU pass的精确计时，多轮有CPU调度噪声，不解释为开启体积提升性能。最终有效GPU样本分别458/605/574/476/628，异常计时拒绝数均0。GPU帧时间与CPU帧时间并行，不应相加。

PID6208 WDDM进程采样121次：专用显存峰值 **323.63MiB**、共享内存峰值 **312.64MiB**：[原始CSV](verification/Enhancement-20260919/PlayerAccepted/vram.csv)。这是进程计数器用量，不是显卡容量。构建约204MiB，没有为25–30GB硬盘目标填充资源。性能用例中的200舰伤害为显式压力设置；正常逃跑验收独立使用真实飞行攻击，不混淆两者。

### 文件范围

- 新场景：`FleetAssault_Enhanced.unity`、`FleetAssault_Enhanced_Small.unity`；新资源集中在 `Assets/_Project/{Data,Art}/Enhancement/`、`Audio/EnhancementEnglish/` 与 `Art/Shaders/NarrativeCombat/`。保存实际 Mixer、Renderer、材质、设置和4个PCM音效；场景保留与旧语音匹配的60秒 Timeline 引用，新英语 Timeline 未发布。
- A：`RadioController/Library/Presenter.cs`、`NarrativeApproachController.cs`、`Editor/EnhancementVoiceAssets.cs`、`Tools/Audio/EnhancementEnglish.json`、`enhancement_content.py`、`enhancement_voice.py`、`test_enhancement_voice.py`。
- B：`LaserBeamPool.cs`、`FleetLaserDirector.cs`、`LaserWeaponSettings.cs`、`DropletReflectiveSurface.cs`、新增 `LaserContactResponse.cs` 与 LaserPulse/LaserChrome Shader。
- C/集成：`SunDisplayRig.cs`、`Runtime/Enhancement/SolarScatteringFeature.cs`、`SolarScatteringVolume.cs`、`SolarScattering.shader`、`Editor/EnhancementIntegration.cs`、`MissionController.cs`、`MissionEffects.cs`、管线 Renderer 列表 `Assets/Settings/PC_RPAsset.asset`。
- D：`FleetCombatSimulation.cs`、`CombatScaleSettings.cs`、`FleetRenderManager.cs`、新增 `CombatEvents.cs`、`FleetEscapeDiagnostics.cs`。新增四个测试文件见 `Tests/{EditMode,PlayMode}/` 的 FleetNormalThreat/FleetNormalEscapeIntegration/LaserPresentationRegression/EnhancementRadioPriority。
- 验证/文档：`EnhancementValidationRunner.cs`（仅显式验证命令启用）、`Tools/Enhancement/SampleGpuMemory.ps1`、本报告、音频报告、接口约定、`STATUS/ENVIRONMENT/ASSET_LICENSES.md` 和根目录 `START_HERE.md`。未提交或推送；原有未提交工作保留。

## 尚未验收与边界

新四角色英语声音、4–6 分钟实际剧情、基于新英语音频的字幕同步、英语表演自然度受上述权限阻塞。四个新本地音效和新 Mixer 下的听感均标记**待人工试听**。不声称真人录音。运行时没有在线语音调用。

功能自动检查不能替代人的键鼠手感和听觉评审。长期热机、跨机器测试、全部 2000 艘自然飞到撤离圈不在已通过项中。性能记录以独立版实际帧时间和 PID 显存采样为准，不把显卡容量或硬盘目标当作显存预算。

## 手动复核入口

1. 用现有 Unity 6000.5.10f1 打开增强场景并 Play，或运行上述 exe（保留整个构建目录）。Enter/BEGIN APPROACH 开始当前60秒旧剧情；Esc 暂停，Tab 跳过，N 重播，R 直接战斗/重开。
2. 正常前进贯穿第一艘后，Space 刹车，用鼠标转向附近完整邻舰，观察约1–3秒不同反应、离阵加速及目标提示移动；不要用 ForceFlee 作为验收入口。需要诊断时在临时 Play 对象启用 `FleetEscapeDiagnostics.captureEnabled`，普通场景默认无诊断开销。
3. 战斗中观察蓝白入射、表面接触、橙色反射，H 查看广播记录；本批声音均为合成素材。按音频报告试听四个新音效和警报/语音压低效果，确认求救在对应舰爆炸后中断。
4. 相机 `SolarScatteringVolume.scatteringEnabled` 或质量 Off 对比体积开关；使用同曝光/FOV，转向太阳、让舰体遮挡、快速转离太阳，再连续重开三次。操作 Play 对象退出后不保存临时调参。

下一步只限解除当前账户的 Seed-TTS 资源授权后补齐本批音频及实际时长验收；本批不扩展其他玩法。
