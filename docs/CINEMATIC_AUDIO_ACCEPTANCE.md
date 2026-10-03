# 本批试玩与验收 — 2026-09-19

打开 **`Assets/_Project/Scenes/FleetAssault_CinematicAudio_Cubic.unity`**，或运行 **`Builds/Windows-CinematicAudio-20260919/DropletGaming.exe`**（保留同目录全部文件）。Enter 开始约6分28秒的英语剧情，Tab 跳过，R 直接战斗/重开，Esc 暂停；鼠标转向、W/S 调速、A/D 横移、Shift 冲刺、Space 刹车。Q 立即返回主视角，F8 循环关闭/低频/标准特写，H 看广播历史，-/+ 调广播音量。

试听打开 [listening.html](../ArtSource/Audio/CinematicAudio/listening.html)，先播放 SC01 平静舰桥、SA01 舰桥受袭、SE01 紧急撤离。页面链接成品、分轨、母版与配方。游戏与试听均使用真实本地音频，不需要云账户。

## 实际交付

| 部分 | 已完成内容 |
|---|---|
| 场景与布局 | 新保存场景、独立 CubicFlight/Scale 数据、20×25×4=2000 个原有稳定身份；20×25 正面朝向固定开战水滴。用户给出的20×25×2实际上只有1000，故保持2000并补足4层。中心跨度14250×14400×14400 UU，比例1.010526；不缩船、不放大根节点 |
| 镜头 | 复用 ChaseCamera，四类真实事件 ShotDirector；.65/.85/1.1秒，标准12–20秒冷却，距离/可见性/重要性/近期重复加权，无积压队列。主光侧攻击构图、附近待爆时降低普通事件机会；不改变时间、速度、输入、伤害和延爆。唯一监听器跟随正常听点，仅临时提升被拍船精度 |
| 人声生成 | **87条真实 Seed-TTS 2.0 英语语音**，固定4个角色，7835英文字符；另一次非语音探测失败。缓存/请求日志保留，无无限重试、充值、视频或密钥打包 |
| 混音 | **80完整场景＋78额外强度变体＋14世界/链路音效＝172导入WAV**。SA01 9.288秒、SE01 9.784秒，均包含主角、背景人物、动作、设备、事件与通讯层。剧情40段累计387.922秒；没有强行加速 |
| 动态音频 | 8秒附近真实事件窗口平滑强度；低/中/高混音、间隔、动作/警报密度与5–12路预算变化。主通讯最多1条，队列4条/过期3.5秒，真实舰船状态/身份绑定，爆炸立即停止整段舰内混音，只留短接收端断线 |
| 作者资源 | Blender可编辑共享低模实例文件、权威JSON、重复导出脚本；Seed提示词/模型参数/英文/中文字幕分离的manifest，逐场景mix_recipe，干声、真实使用分轨、scene_mix/radio_mix、FFmpeg批处理、试听页 |

非语音专用 Seed 接口在当前接入中不可用；真实空对白探测返回45002001，没有把描述朗读当音效。25个非语音源素材是明确标注的原创程序效果，包括分离脚跟/脚尖激励钢甲板模态的脚步、舱门、控制台及世界战斗瞬态；关键效果各3个变化。它们不是实录或云端复杂 Foley，衣物/人群奔跑的自然度没有主观验收。详见 [音频报告](CINEMATIC_AUDIO_REPORT.md)、[镜头报告](CINEMATIC_SHOTS_REPORT.md)、[布局报告](CINEMATIC_FLEET_REPORT.md)。

## 验证证据

证据根目录 `docs/verification/CinematicAudio-20260919/`；原始音频质量证据在 `docs/verification/CinematicAudio/`。

- 实际Unity编译通过；EditMode **6/6**，PlayMode **27/27**：最终相机18、实际渲染LOD生命周期1、音频3、完整已保存2000舰场景5。完整Cinematic回归25/25后，仅最后普通机会调整又重跑相机18/18，去重合计33个用例。证据 `compile-final.json`、`editmode-results.json`、`playmode-final-results.json/.xml`、`shots-final-results.json`。没有用dotnet或小规模fixture冒充完整场景。
- 实际 Blender 生成/保存/重开/第二次生成成功，导出哈希重复一致；Unity两次布局审计字节一致。2000唯一ID、18000碰撞体、1,999,000对旋转船壳AABB检查，交叉0，最小净空541.729 UU，出生净空971.218 UU，最小舰首朝向点积0.99999982。见 `layout-first.json` / `layout-repeat.json`。
- 保存场景测试覆盖初始朝向、真实Motor贯穿与邻舰逃跑、渲染/查询同源、延爆一次计分、原点移动、正常/跳过剧情共享入口、暂停、结算与连续三次重开。相机测试另外覆盖代际/身份复用、遮挡、强转向、重定位和监听器。
- **172音频实际解码通过、0削波，126字幕条目文本/时间边界有效**，设备循环首尾归零；代表场景六轨各有实际信号，完整混音无异常长静音。自动检查不等同逐词听写或表演、混音验收。见 `audio-qa.json`、`decode-qa.json`、`READY.json`。
- 首轮窗口化完整2000舰 Player：24/25检查通过，正常路线真实出现触点/贯穿/攻击者特写，4次爆炸均合法落在冷却内。非零音频1199次采样、主通讯最大1、世界音效最大12、强度0→.999998→.000257；SA01实际长通讯随来源舰真实爆炸整舱停止。唯一失败为QA在重定位后又让AI正常走一步却拿旧位置比较，已修复验证器为原子坐标检查，保留原失败报告。没有扩大容差或修改舰船运动掩盖错误。
- 最终 Windows 构建成功，**0错误、1条可选Pipeline运行时桥未配置警告**（桥在玩家中关闭），266,014,136字节。完整2000舰最终玩家 **26/26通过，实际进程退出0**；普通S键调至99 UU/s后，真实镜头依次为爆炸9.980s、触点38.801s、攻击者54.141s/87.117s、爆炸107.154s、贯穿152.476s，四类全部实际触发，最短相邻间隔15.34s。实际贯穿、邻舰逃跑、独立Q返回、暂停、长SA01随爆炸整舱停止、200舰集中延爆、原点重定位、三次连续重开与结算均通过。证据 `build.json`、`build-final-warnings.json`、`PlayerFinal/report.json` / `process-exit.json`。
- 最终音频输出非零1972次采样，最大幅度0.2752，主通讯并发最大1、世界音效最大12；强度 **0→0.9999986→0.0002555**，无永久高强度。最终玩家日志没有Exception/Error或已修复的LOD警告。200舰集中压力保留完整2000舰，其直接伤害输入在日志明确标记。
- 另在实际完整Editor场景以默认300 UU/s自然飞行100秒，按特写中段分帧捕捉12张图；已实际查看贯穿比例与爆炸火球/冲击环/碎片，见 `EditorVisual/`。只在这次画面复核暂时静音Editor，避免与独立开场玩家叠播；结束已恢复监听音量1。没有事件注入、相机强制或改变速度。
- 最终保存状态 `saved-scene-final.json`：新场景未脏、Play已停止、2000唯一ID、唯一监听器、实际AudioMixer/Timeline引用、默认构建仅新场景；旧场景和源资产保护证据为 `preservation-final.json`。
- 完整英语开场 **5/5通过，实际进程退出0**：真实墙钟388.695秒播放40条自然Timeline提示，正常完成仅初始化战斗一次；3612次非零音频采样，主声并发最大1，峰值0.1898。没有快进、倍速或模拟时钟替代实际播放。见 `Opening/report.json`、`Opening/process-exit.json`；[截图和结构化证据入口](verification/CinematicAudio-20260919/index.html)汇总33个Unity用例、26个战斗玩家检查和5个开场检查。

运行入口：在项目根目录执行 `python Tools/Verification/run_cinematic_players.py <新的证据目录> --speed 100`，顺序启动1920×1080实际窗口玩家的正常S键路线/完整2000舰压力检查，然后播放完整真实时长英语开场。不会并行播放两份游戏。验收器只在显式 `-cinematic-validation` / `-cinematic-opening` 参数下创建；正常玩家无自动测试或自动输入。直接伤害压力阶段与普通FixedUpdate/键盘路线在报告中分别标记，不把压力输入冒充自然命中。

修复说明：首次远舰特写日志暴露了未激活LODGroup的ForceLOD调用顺序，现先激活单舰再提升LOD，销毁时保持隐藏并在实际恢复时解除强制；Camera/RenderTexture/Renderer.isVisible检查真实LOD2→LOD0→销毁→两次重开恢复LOD2，并检测该警告为0。期间一次LODGroup类型API编译错误已修正并保留 `compile-lod-first.json`，最终编译通过。普通Attack/Contact共用1.5秒单次机会、概率权重降为原1/4，避免高频事件不断占满冷却；新爆炸背向正常视点的最低权重改为0.9，仍须通过真实构图遮挡检查。没有改变伤害、预排爆炸或补播过期事件。

## 修改归属与保护

根：`CombatEvents.cs`、`FleetCombatSimulation.cs`、`FleetLaserDirector.cs`增加既有事件快照/稳定ID；`MissionEffects.cs`/`LaserBeamPool.cs`新增默认为开的旧世界声开关，在新场景关闭以避免双播；`CinematicAudioIntegration.cs`通过Editor API统一保存场景/引用/构建；`CinematicValidationRunner.cs`仅在显式QA命令行启用。默认玩家不加载测试路径。

A：`ShotDirector.cs`、`CombatListenerAnchor.cs`、`ChaseCamera.cs`、`FleetRenderManager.cs`及镜头测试。B：`BattleAudioDirector.cs`、既有Radio控制/字幕/库、`CinematicAudioAssets.cs`、音频导入/Timeline/Mixer、`Tools/Audio/cinematic_audio*.py`及音频测试。C：`CinematicFleetLayout.cs`、`Tools/Blender/CinematicFleet/`、Blender源/导出/配置及布局测试。根统一所有Unity操作，子代理没有并行保存场景或Mixer。

基线内16旧场景及16对应.meta、ProjectVersion与manifest哈希未变；新场景保持所有2000旧ID。旧模型源/FBX和旧权威布局哈希不变，原有成功音频、旧meta、未提交工作保留。默认Build Settings经Editor API更新为新场景；旧玩家本批未删除。当前回退为Enhanced完整玩家，compact v0.2.2 ZIP保留。不对基线之外全部资产作未经记录的全量哈希承诺。没有提交或推送Git。

## 已试听、尚未完成与复核方式

**已生成、已混音、技术信号检查和上述实际Unity/Player验证已执行；已主观试听：无，全部待人工试听。** 当前工具无法真正听取声音，不能把波形、音频存在或运行输出当作情绪与混音合格。Audacity安装存在但未操作、未保存.aup；全部剪辑混音已由FFmpeg实际完成，无需用户自行制作。

人工复核：试听三代表段，检查英语节奏、指挥官与远处回应、脚步/舱门因果关系、背景不盖词、链路统一远近和逐词中英字幕。游戏中R开战，尝试W/S改变接近速度、Q返回和F8档位，再观察贯穿→短求救→真实延爆→整舱断线；Esc暂停/继续和R连续三次。物理键鼠主观手感、跨机器声音设备、复杂Foley逼真度未验收；本批按要求未做帧率优化或帧率保证。

没有当前云端语音额度/权限阻塞。非语音专用云端不可用的部分已用标明来源的程序效果实际交付；主观验收是剩余限制。完成本批后停止，不进入额外功能阶段。
