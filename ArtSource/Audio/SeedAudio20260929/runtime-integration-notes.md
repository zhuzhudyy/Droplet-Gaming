# Seed Audio 20260929 — runtime integration audit

2026-09-29。仅审查代码/配置/历史证据；未运行 Unity、测试、构建或生成 API。本说明是唯一写入文件。最新授权按根代理传递：非语音使用已配置 ARK 的 Seed Audio，预算 60 元，不查账户余额；不使用本地程序合成替代。现有 131 项清单位于 `ArtSource/Audio/SeedTTS20260929/catalog.json`，新批次须更新 provider/模型/资金单位，不能继承旧 TTS-only AFP 标签。

## 已核实事实与必须处理的问题

- 当前正式场景 `FleetAssault_CinematicAudio_Cubic.unity`，2000 舰；Unity 6000.5.10f1、URP 17.5.0、Timeline 1.8.13、Input System 1.20.0、Test Framework 1.7.0、Pipeline 0.6.0-exp.1。遵守 TEST_PLAN 当前版本策略，不运行会重写旧场景的历史 authoring 测试。
- `CinematicFleet/CubicFlight.asset` 为 initialSpeed/maxCruiseSpeed=300、boostMultiplier=5、missionSeconds=5400。`DropletSettings.ApplyWorldScale()` 从 30,000/150,000 m/s 与 100 m/UU 写回这些值。
- `MissionEffects.cs:299–300` 仍以 `Speed/100`、`Speed/120` 计算飞行增益/音高，因此初始巡航已全部 Clamp 到 1。`AudioCapacity` 在 Quality Off 为 0（30 行）；质量 setter 调 `ResetEffects()` 停全部声音（39 行）；Update 在 Quality Off 提前返回（239 行）。注释说飞行独立，实际不独立。
- 保存场景确有 `MissionEffects.worldAudioEnabled=0`，但 `flightClip` 仍引用原程序生成飞行 WAV；`LaserBeamPool.worldAudioEnabled=0`。只替换 BattleAudioDirector 四组 clips 不足以达成全部 Seed。
- `BattleAudioDirector` 现有 12 槽配置，但代码允许 4–16，应收紧最大 12。现有 4 类声音为 WeaponFired/DropletContact/HullPenetrated/ShipExploded，RetreatOrdered/ReactorUnstable 只改变 intensity。稳定 eventId 去重、generation 验证、0.35 秒过期、8 秒滑窗以及浮动原点修正已经存在。
- `RadioController` 一条主通讯、四条队列、whole-scene premix；暂停 Pause/UnPause 四个 source。ShipExploded/CommunicationInterrupted 在预算检查前打断全舱，必须保留。普通接通/断线使用 PlayOneShot，扩展时不能让同 source 无限叠声绕过预算。
- 当前 Mixer 只有 Voice/Signal/Alarm/Background/Combat。Normal/Broadcast 快照给 Background 与 Combat 降音；BattleAudioDirector 又在起播时乘一次 .72，因此不能再增加第三套独立 ducking。
- 当前 `AudioManager.asset` 为 32 个真实声部、512 虚拟声部；保留此设置。唯一 AudioListener 在 `CinematicPresentation/CombatListener`，CombatListenerAnchor 跟正常 chase pose，不跟特写镜头。

## 最小运行时方案（在素材代表门槛通过后实施）

新增 `Runtime/SeedAudio/SeedAudioCatalog.cs` ScriptableObject：以 cue family/variant 持有所有 131 条真实导入 AudioClip、loop/类别/默认增益；空 clip 是配置错误，不隐式回退程序声。分工为 StageAudioDirector、FlightAudioController、UiMissionAudioController 三个小组件；战斗继续扩展 BattleAudioDirector，通讯继续 RadioController，不能新增第二条游戏事件总线。

### 世界战斗的 35 条

| family | 现有权威触发与生命周期 |
|---|---|
| Laser(6) | WeaponFired；近场优先、远密集开火限流 |
| Reflect(6) | DropletContact；不再在 LaserReflected 重播声音 |
| Penetration(6) | HullPenetrated 且 source=DamageSource.Penetration |
| LaserBreach(3) | HullPenetrated 且 source=DirectLaser/ReflectedLaser |
| Explosion(6) | ShipExploded；同 eventId 仅一次 |
| Reactor(3) | ReactorUnstable 开始；subject 爆炸/逃脱/失效/代际变化即停 |
| RetreatEngine(3) | RetreatOrdered 开始；跟随 subject，逃脱/爆炸/退出撤离即停 |
| Escape(2) | ShipEscaped；2D 反馈由统一短音预算或世界 12 槽播放，不另建无界 source |

BattleAudioDirector 槽增加 source ship、stable targetId、generation、loop kind。持续声和单次声共同占用最多 12 槽，持续舰声最多 3 槽；优先级以近距离命中/爆炸高于撤离引擎和远激光，满池拒绝/替换较低优先级，不调用 Instantiate。停止主体持续声先于提交爆炸音，不受起音限流。未获槽的持续声可舍弃，不创建每舰监听器或2000项待播队列。

单次声保留 `eventPosition + eventOrigin - fleet.AccumulatedOriginOffset`。跟随声用 `fleet.TryGetAuthoritativePose(subject, out position, out rotation)`，不要读取关闭 LOD 子对象，也不要再对已重定位权威位置重复减原点。暂停不减剩余时长、不换样本、不改 source timeSamples；重开清 slots、去重、activity、variant、loop身份和计数。特写仅强调已播放 eventId，不触发新音。

### 飞行的 18 条

`DropletMotor.Simulate()` 在实际运动完成后发布只读 flight presentation sample（Speed、CruiseSpeed、该次消费的 boost/brake/strafe、实际夹紧后的转角速率、step序号）。音频不得再调用 input.ReadStep 或直接读取 Keyboard。ResetPose/HoldSimulationPose/禁用模拟清理已消费状态；音频监听 Teleported 与 Mission.Restarted，不把重开/瞬移误判急转或 boost release。

归一化明确用 `Clamp01(Speed / Max(epsilon, settings.maxCruiseSpeed * settings.boostMultiplier))`，巡航权重可另用 `Clamp01(Speed / Max(epsilon, settings.maxCruiseSpeed))`；当前 300→1500 UU/s 必须产生不同输出。2个持续 source 负责巡航与冲刺交叉淡变，1个 source 负责 BoostEnter/Release、Brake、Turn、Recover 瞬态。按已消费状态边沿触发；转弯阈值结合实际 degrees/dt 并带0.6秒冷却，不能每帧播；制动恢复仅 braking true→false 且仍在 Playing。新的安全场景清除旧 effects.flightClip；旧组件/旧场景资产不删除。这样 VFX Off 不影响新飞行声音。

### 阶段背景19条、环境6条

StageAudioDirector 订阅 Mission.StateChanged/Restarted，并观察 `narrative.CurrentStage` 与 `BattleAudioDirector.Intensity`。剧情阶段目前无事件，可每帧比较整数stage；只在变化时切换。Ready 1、Narrative 4、战斗Low/Medium/High各2、Aftermath1、PauseBed1、ResultBed2、Transition4。

战斗音乐沿现有强度低/中/高三档，阈值 .25/.65，加 .07 滞回与最少8秒驻留；近8秒活动消失且强度低于 .08 时进入 Aftermath，再次真实活动回战斗。每档两条顺序轮替，不按累计击杀数永远增压。两路音乐以2.5秒交叉淡变；叙事配乐随stage变化但不改 Timeline。SpaceTexture3 仅抽象非写实背景，2路交叉淡变；CabinBed3 只作为通讯母版环境层，不在玩家耳边另播全频舰舱。

进入暂停时暂停原音乐/环境/飞行/世界/通讯 source，并冻结其淡变时钟。额外1路2D PauseBed 使用 unscaled 时钟，恢复先停 PauseBed，再 UnPause 原声音，不能重新选曲或重置 sample。跳过剧情、进入Results、重开必须停止旧循环和淡变后选目标阶段；Results 保留分类 BGM 与一次性结算落点。音频只播放本地已导入文件。

### UI16条、任务15条

UiMissionAudioController 用最多3个2D source（2 UI + 1任务反馈），clip/Play 而非无界 PlayOneShot，UI source.ignoreListenerPause=true，冷却以 unscaled time。HudPresenter 现为 IMGUI：只在 GUI.Button 返回true、toggle值真正改变、slider量化步真正改变时播；hover只在进入一个控件时播一次，不能 Layout/Repaint 每次都播。不同UI动作与Mission.StateChanged的Pause/Resume避免重复Confirm。

RadioPresenter.ToggleHistory 和 SetVolume 统一发历史/滑条反馈；快捷键与按钮走同一通知路径。NarrativeApproachController.Skip() 需一个轻量 `Skipped` 只读事件（仅 IsActive 时一次），不能把自然 Completed 当 Skip；重播/重开反馈按用户动作播，Mission 初始化重置不播。其余无须改玩法：观察 NearBoundary false→true、RecoveryCount增加、Remaining跨60/30/10..1、Score.Multiplier上升/达到settings.maxMultiplier、ResultsShown时 Won/EscapedCount。跨多个门槛时只播最高紧迫的一条，不能同帧串十次倒计时；每任务清已播门槛，0秒pending等待期不继续倒计时。边界用状态边沿，安全返回用RecoveryCount，不把所有Motor.Teleported当返回。

## 总线、预算与作者工具

- 活跃声源上限建议：world12 + radio最多4 + music2 + ambience2 + flight3 + UI/mission3 + pause1 = 27，低于32。统计真实播放声部，不能仅统计组件数；禁止在单一AudioSource上叠无界one-shots。当前premix一般radio只有主声/断线2路，仍按4路保守核验。
- 新Mixer复制到新路径，保留Voice/Signal/Alarm/Background/Combat兼容路由，新增Music/Ambience/Flight/UI。通讯用既有Normal/Broadcast单一ducking入口；扩展快照覆盖Music/Ambience/Flight，并移除/禁用新目录模式下BattleAudio起播 .72 的重复duck。UI保持不duck。用户分组音量应与快照音量分开（父组暴露用户增益、子组快照duck），避免SetFloat覆盖同一快照参数。Master沿用PlayerOptions.Volume，避免双乘。Radio.Volume仍作为通讯用户旋钮；明确只作用一次。
- 音乐/环境长片不能复用 CinematicAudioAssets.Import() 的强制单声道PCM全解压策略。保留Seed原始采样率/声道；长音乐立体声Streaming+Vorbis，高质量；短3D世界音单声道PCM DecompressOnLoad；声场是否单声道由用途决定。重新导入不能声称提升源质量。
- 新Editor helper应从当前CinematicAudio_Cubic复制安全场景，不调用会重复应用Blender布局的旧整批Integrator。Guard保护dirty scenes、Play/compile；所有新对象限定拥有的SeedAudioPresentation子树，Undo-aware，可重复执行。同一存档中2000ID/ship引用、唯一listener、场景可见性不变。
- 保存新Catalog/Mixer/RadioLibrary与Timeline副本，不修改旧CinematicAudio assets。复用87条Seed干声，替换旧25个程序源之后重混80场景及78强度变体；每条配方只允许Seed源，保留原对白及字幕时间/Timeline长度，SFX需裁切/循环适配既有时长而非延长剧情。旧程序Flight也必须停用；构建对新scene依赖树做音源来源审计，不能仅检查新增文件。

## 验证入口与验收

现有CLI包装器 `Tools/Blender/SpaceEnvironment/unity_command.py` 调用 `%LOCALAPPDATA%/Unity/bin/unity.exe`（磁盘存在）。以下命令根据已保存command schema及包README整理，本审查未执行。始终在本工程cwd或明确 `--project-path`，不要启动第二个Editor覆盖已开场景。

```powershell
python -B Tools/Blender/SpaceEnvironment/unity_command.py command --project-path "C:/学习/玩/Unity/Trysolar Drip" editor_status --json
python -B Tools/Blender/SpaceEnvironment/unity_command.py command --project-path "C:/学习/玩/Unity/Trysolar Drip" recompile --focus false --json
python -B Tools/Blender/SpaceEnvironment/unity_command.py command --project-path "C:/学习/玩/Unity/Trysolar Drip" recompile_status --json
python -B Tools/Blender/SpaceEnvironment/unity_command.py command --project-path "C:/学习/玩/Unity/Trysolar Drip" run_tests --mode editor --filter Cinematic --async_tests true --json
python -B Tools/Blender/SpaceEnvironment/unity_command.py command --project-path "C:/学习/玩/Unity/Trysolar Drip" test_status --json
python -B Tools/Blender/SpaceEnvironment/unity_command.py command --project-path "C:/学习/玩/Unity/Trysolar Drip" run_tests --mode playmode --filter SeedAudio --async_tests true --json
```

另跑受影响 CinematicAudioTests/CinematicShotTests/CinematicLodPinTests 与新的 SeedAudioFullSceneTests。旧 CinematicFullSceneTests 硬编码旧scene：旧测试通过不能证明新安全场景接入。对Motor状态发布变更跑运动/暂停/重开相关测试，不因只读sample重复全历史作者套件。需要C#代码精确参数时沿用wrapper `eval-file <已保存C#路径>`，由根统一执行。

旧 `CinematicAudioIntegration.Build()` / 菜单 `DropletPrototype/Cinematic Audio/2 Build Windows` 硬编码旧scene、Builds/Windows-CinematicAudio-20260919与旧证据路径，不能用于本批。新构建helper应指定新的SeedAudio scene/player/evidence，通过BuildPipeline.BuildPlayer执行并保存summary；实际成功才可称构建通过。已装Editor绝对路径为 `C:/Program Files/Unity/Hub/Editor/6000.5.10f1/Editor/Unity.exe`，仅在没有占用会话时才考虑batchmode，不与当前Editor竞争。

现有 `Tools/Verification/run_cinematic_players.py` 同样硬编码旧player。新玩家可直接用保留的CinematicValidationRunner引导参数：

```powershell
& 'Builds/Windows-SeedAudio-20260929/DropletGaming.exe' -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -cinematic-validation 'docs/verification/SeedAudio-20260929/Player' -cinematic-slow-route -cinematic-route-speed 100 -logFile 'docs/verification/SeedAudio-20260929/player.log'
& 'Builds/Windows-SeedAudio-20260929/DropletGaming.exe' -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -cinematic-opening 'docs/verification/SeedAudio-20260929/Opening' -logFile 'docs/verification/SeedAudio-20260929/opening.log'
```

这两条需先真正生成对应新player，输出目录必须全新；现runner只覆盖旧音频检查，须扩展以下真实断言：131条非空/Seed来源闭包；所有阶段能进入；巡航/boost/brake/turn边沿；VFX Off仍有飞行声音；pause前后世界/通讯/music timeSamples不漂移（允许一个DSP buffer容差）、UI与PauseBed仍发声；resume无重启；舰爆全舱停止且持续reactor/retreat停；强度能回落；浮动原点前后世界和跟随声相对listener连续；2000舰集中爆炸world≤12、总声部≤27；三次连续重开generation、队列、计数、fade、loop与去重重置；只一个listener且镜头切换不重播。

最终独立版证据必须区分常规操控路径与直接伤害压力路径；全英语剧情使用真实墙钟完成40条。记录输出峰值及非零样本，只代表音频技术输出，不冒充试听。所有新生成Seed音频仍需内容验收：无被朗读的提示词、无不必要人声/音乐混入孤立SFX、长音循环接缝、对白清楚与主次层级。没有音频感知工具时应保留该限制，不用格式、空ASR或波形通过代替内容通过。
