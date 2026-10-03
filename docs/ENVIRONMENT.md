# Verified environment

## 全 Seed 音频交付环境 — 2026-09-29（最新状态）

工程仍使用 Unity **6000.5.10f1 (3bd4f66ad299)**、URP **17.5.0**、Input System **1.20.0**、Timeline **1.8.13**、Test Framework **1.7.0** 与 Pipeline **0.6.0-exp.1**；没有更换 Editor、管线或包。Blender 本批未运行，历史验证版本为 **5.2.1 LTS**。本批音频生成、后期与检查使用已有 Python **3.14.3**、NumPy **2.4.3** 和 SciPy **1.17.1**；这些开发工具不进入离线玩家。既有 FFmpeg **9.0.1** 属于历史批次环境记录，不将其列为新版素材产生的必经步骤。

本机用户环境中分别保存 `VOLC_AUDIO_API_KEY`（Seed Audio 1.0）与 `VOLC_SPEECH_API_KEY`（Seed-TTS 2.0），密钥不在项目资源或日志中。真实云端产出为 **131** 条独立非语音 Seed Audio 成品与 **87** 条新版 TTS 干声；本机从 Seed 源材混成 **158** 条通讯。六类非语音代表样音得到用户听感认可；其余独立条目尚未逐项人工试听。原始、处理后及发布音频与请求侧车保存在 `ArtSource/Audio/SeedAudio20260929/`。旧 Ark / APP ID 拒绝记录属于早期诊断，不代表现用 Speech 凭据状态。

实际 Unity Editor 导入 **131** 条效果和 **158** 条通讯，并通过 Editor API 保存 `Assets/_Project/Scenes/FleetAssault_SeedAudio.unity`。实际运行时 PlayMode **6/6**、EditMode **3/3**、新场景集成 PlayMode **3/3**、持续世界音源 PlayMode **6/6**，另有验证器迭代器专项 **1/1**；音频工具离线测试 **64/64**。持续音修复后 Windows x64 构建 `Builds/Windows-SeedAudio-20260929/DropletGaming.exe` 成功（**0 错误、1 警告**，最终 BuildReport **353,197,031 bytes / 26.07 s**）。本机 **AMD Ryzen 9 7940HX、RTX 4060 Laptop GPU、Direct3D12、1920×1080** 最新独立玩家在保存的 2000 舰场景通过 **34** 项自动检查，**0** 失败，另 **3** 项需要自然长时路线或人工试听；实际录制六段监听器 WAV 和六张 PNG。1280×720 亦运行验证，但 1920×1080 为本轮主要审查画面。最终证据见 `verification/SeedAudio-20260929/player-validation-release/`。下文旧批次的“未运行”仅描述当时状态。

Unity Editor API 另将 `FleetAssault_SeedAudio.unity` 设为项目 Build Settings 唯一启用场景；前版 CinematicAudio 场景留在列表但禁用。现有 Seed Windows 玩家原本就通过显式新场景列表构建，仍是有效证据。

## Seed Audio production preparation — 2026-09-29

Confirmed unchanged Unity 6000.5.10f1 (3bd4f66ad299), URP 17.5.0, Input System 1.20.0, Timeline 1.8.13, Test Framework 1.7.0. No new packages or third-party tools installed. New domestic Seed Audio client uses Python standard-library HTTP/WAV plus the existing NumPy; model seed-audio-1.0, /api/v3/tts/create, 48 kHz WAV requested with source channels preserved. Official list price 1 CNY/minute, 120-second maximum; 2 CNY reserved per request under user-provided 60 CNY cap.

Actual first LASER_R1 call using locally saved ARK_API_KEY returned HTTP 401 / 45000010 / Invalid X-Api-Key at 2026-09-29T09:00:20Z. Zero audio outputs, unknown usage retains 2 CNY reservation. No balance query. A separate local password dialog for the documented Speech-platform key is available as Set-SeedAudioCredential.ps1, saving VOLC_AUDIO_API_KEY without replacing existing keys. No Unity compilation, tests, build, import or player verification performed for this new batch yet; offline client tests are separate.

## Seed-TTS soundscape capability trial — 2026-09-29

Read the installed project versions again: Unity **6000.5.10f1 (3bd4f66ad299)**, URP **17.5.0**, Input System **1.20.0**, Timeline **1.8.13**, Test Framework **1.7.0**, Pipeline **0.6.0-exp.1**. No Editor/package upgrade or Unity asset change in this trial. Unity compilation, tests, build and player were **not run**, because the six-category content gate did not pass. Blender was not invoked; the retained authored-source environment remains Blender 5.2.1 LTS from the preceding batch.

Actually used Python **3.14.3**, NumPy **2.4.3**, standard-library unittest (**24/24** final); PowerShell **7.6.5** for orchestration and installed Windows PowerShell/System.Speech **4.0.0.0** for offline first-utterance dictation on Windows **10.0.26200.0**. The recognizer reports **Microsoft Speech Recognizer 8.0 for Windows (Chinese Simplified - PRC)**. Only the installed zh-CN recognizer was available. SciPy **1.17.1** remains installed; no new dependencies were installed. The soundscape WAV inspection itself uses NumPy and wave, with no audio source synthesis or postprocessing.

Seed-TTS 2.0 Agent Plan endpoint executed **19** actual requests: English Tim control plus six non-speech categories with up to two revisions each; **17** complete native 24 kHz mono PCM WAVs and **2** invalid/empty outputs. Chinese revisions use the official example voice `zh_female_vv_uranus_bigtts`; English control/early laser use `en_male_tim_uranus_bigtts`. Service usage totals **313 text_words / 42.255 AFP conversion**, with **495.720 AFP** separately reserved for missing usage. Existing local key works. No console invoice/remaining account balance is claimed.

This session's tools do not provide audio perception input. Subjective listening was **not performed**; local ASR is supporting evidence for prompt leakage only. The initial offline test run failed because one test read a Chinese UTF-8 ledger with Windows' GBK default; explicit UTF-8 fixed it, and the failed log is retained. See [trial report](verification/SeedTTS-Soundscape-20260929/REPORT.md) for technical/content results separately.

## Cinematic audio / cubic fleet batch — 2026-09-19

Actual current batch: Unity **6000.5.10f1 (3bd4f66ad299)**, URP **17.5.0**, Timeline **1.8.13**, Input System **1.20.0**, Test Framework **1.7.0**, Pipeline **0.6.0-exp.1**; no upgrades. No Cinemachine package is installed, so the existing ChaseCamera is extended. Unity CLI beta8 controls the one local Editor. Actual targeted checks: 6 EditMode and 27 PlayMode cases passed (18 shots, 1 native rendered LOD lifecycle, 3 audio, 5 saved full-fleet scene); standalone results are recorded separately in CINEMATIC_AUDIO_ACCEPTANCE.md.

Blender **5.2.1 LTS**, build **9e2066aef7ef**, embedded Python **3.13.13** actually generated, saved, reopened and repeated the 2000-instance layout. Audio authoring actually used Python **3.14.3**, NumPy **2.4.3**, SciPy **1.17.1**, FFmpeg **9.0.1-full_build-www.gyan.dev**. Seed-TTS 2.0 Agent Plan endpoint completed 87 real English voice requests; the previously recorded voice-authorization failure below is historical. One separate non-speech probe returned 45002001 without audio. No unverified Seed SFX endpoint was added. Original programmatic sound effects supply the non-speech layers.

Audacity is installed but its automation was unavailable and it was not operated. No Audacity project or subjective listening pass is claimed. Existing tools only; no software or package installation. Runtime plays imported local clips and contains no cloud, FFmpeg or Audacity calls. Actual rendered player: AMD Ryzen9 7940HX / NVIDIA GeForce RTX4060 Laptop / Direct3D12 / 1920×1080. This batch did not optimize or certify frame rate.

The earlier entries below describe their respective historical checks, not the latest readiness state.

Voice tooling update 2026-09-19: Python 3.14.3, standard-library unittest 9/9; Seed-TTS 2.0 only. User speech key is readable; live authentication/synthesis unverified. FFmpeg 9.0.1-full_build-www.gyan.dev is installed and passed local synthetic-video extraction (1 second, 48 kHz/16-bit/mono); the voice tool no longer uses it. PowerShell helper syntax passed; persistence helper not executed.

## 火山引擎配音工具 — 2026-09-19

本轮实查 `ProjectSettings/ProjectVersion.txt`：Unity **6000.5.10f1（3bd4f66ad299）**；manifest：URP **17.5.0**、Input System **1.20.0**、Timeline **1.8.13**、Unity Test Framework **1.7.0**、ugui **2.5.0**、Pipeline **0.6.0-exp.1**。全部保留。本轮未运行 Unity 或 Blender；Blender **5.2.1 LTS** 是历史记录，不算本轮实查。



## Narrative Combat 六项集成 — 2026-09-12；路径复核2026-09-13

实际实施沿用Unity **6000.5.10f1（3bd4f66ad299）**、URP **17.5.0**、Timeline **1.8.13**、Input System **1.20.0**、Test Framework **1.7.0**、ugui/TextMeshPro **2.5.0**、Pipeline **0.6.0-exp.1**；2026-09-13当前ProjectVersion与manifest复核一致，没有升级包或迁移管线。本轮复用既有FusionFrigate/Droplet模型及挂点，**未执行Blender**；历史源资产由Blender5.2.1 LTS创作，不将历史建模校验当成本轮执行。48条人声实际由本机Microsoft Huihui离线导出，另2提示音，无在线语音服务或新付费依赖。

2026-09-12实际Unity EditMode **15/15＋矩阵6/6**、完整相关PlayMode **85/85**；Windows x64/Mono/非Development构建成功，最终17.271秒、0错误、1条可选Pipeline运行桥未配置警告。可见原生1920×1080/D3D12/High独立运行 **43/43**，PID61876退出0；完整2000身份、60秒Timeline、实际输入固定步、四阶段渲染、3次重开证据见[NARRATIVE_COMBAT_REPORT.md](NARRATIVE_COMBAT_REPORT.md)。

硬件为Ryzen9 7940HX、RTX4060 Laptop GPU（适配器7956MB）、Windows11 10.0.26200、系统RAM15575MB。renderScale=1，动态分辨率与帧生成关闭；最终静态全景113.38FPS、全舰撤退94.50FPS，后者P95 14.053ms，不承诺锁80。WDDM精确Player进程独立显存峰值427.32MiB，不混同共享GPU系统内存、适配器容量或204.49MiB发行体积。

2026-09-13目录整理后，唯一工程是`C:/学习/玩/Unity/Trysolar Drip`。本次继续收尾重新读取已保存测试结果并复核421/421保护文件哈希；未重跑原性能。当前CLI查询没有发现该路径的Pipeline实例，当前会话无Unity连接工具，所以即时Editor状态未验证；原构建及历史证据保留。物理人类键鼠手感、90分钟热机和全2000舰自然撤出仍未验证。以下为保留的历史环境记录。

## 视觉升级、反应堆爆炸与水滴火光反射 — 2026-09-08

实际使用项目既有 **Unity 6000.5.10f1（3bd4f66ad299）**、**URP 17.5.0 / PC_RPAsset / Forward+**、**Input System 1.20.0**、**Test Framework 1.7.0**、**Custom NUnit 2.1.0**、**Pipeline 0.6.0-exp.1**。没有升级包、迁移管线、安装工具或改写 Packages / ProjectSettings。复用前轮 **Blender 5.2.1 LTS（9e2066aef7ef）** 创作的 FusionFrigate 与 Droplet_Rebuilt FBX；本轮没有执行 Blender，也不将历史建模检查算成本轮执行。

Unity Editor 实际编译、新资产 EditMode **3/3**、完整 PlayMode **55/55**、最终反射专项复测 **2/2**；针对性复测是原 55 个测试中的两项，不另报新增测试总数。新显式 Editor 菜单用原生 API 保存场景/Prefab。Player 首次编译暴露诊断脚本误读 Editor-only `Light.lightmapBakeType`，修复为运行时 `Light.bakingOutput.lightmapBakeType` 并采用 6000.5 支持的 FindObjectsByType 重载；之后独立构建成功。早期未通过的视觉反光/太阳表现保留调试证据，以最终 After 运行目录为验收依据。

Windows **StandaloneWindows64 / Mono / 非 Development** 构建真实执行，**37.880 秒、138,666,205 bytes、0 错误、1 条可选 Pipeline 运行工具桥未配置警告**。后者不指 URP 失效。原始 BuildReport 时间戳原样留存，工具绝对时间与渲染诊断时钟有偏移，不用二者差值推算时长。Player 命令行显式包含新/旧两个比较场景，没有更改全局 Build Settings。新正常启动默认场景为 `FleetAssault_VisualUpgrade`。

本机 **AMD Ryzen 9 7940HX / NVIDIA GeForce RTX 4060 Laptop GPU（Unity 报告显存 7,956 MB）/ Windows 11 10.0.26200 / Direct3D12 / RAM 15,575 MB**，Editor 与可见独立 Player 均实际输出 **1920×1080**。每个四视点阶段暖机 60 帧，再测至少 480 帧且至少 6 秒；VSync=0，不限帧，全部采样帧聚焦。最终独立场景 **39/39**、旧场景 **31/31**，退出码均为 0。独立平均 FPS 修改后 **219.46/189.75/259.66/241.16**，GPU/GC/DrawCall 标记没有有效样本；Main Thread/SetPass/Triangles 有实际样本。FrameTimingManager 的 Editor 前两阶段 GPU 读数异常偏低，未用于 GPU 成本归因。集中爆炸 P99 **22.27 ms**、最大 **57.93 ms**，保留控制实验的同步扫掠开销与短程/非自然输入限制。两个 Player 日志共有 D3D12 info queue 调试接口查询失败行（0x80004002），正常建立 D3D12 设备、渲染和所有检查完成，无 C# 运行异常。

外部 **Python 3.14.3** 和现有 Pillow 用于清单、哈希、原始 PNG 像素比较及可见 Player 启动；没有外部图形素材或新运行依赖。构建、源资源、缓存、证据和旧版本磁盘大小分别记录在 `verification/VisualUpgrade/delivery-inventory.json`，不把整个开发工作区当作游戏发行体积。证据、具体方案、操作入口和未执行项目见 [VISUAL_UPGRADE_EXPLOSION_REFLECTION_REPORT.md](VISUAL_UPGRADE_EXPLOSION_REFLECTION_REPORT.md)。

以下为保留的历史环境记录。

## Droplet_Rebuilt 重建与验证 — 2026-09-08

本轮实际使用 **Blender 5.2.1 LTS（9e2066aef7ef）**、内嵌 Python **3.13.13**、官方 bundled **io_scene_fbx 5.15.0**，在独立后台进程生成/重开/渲染/导出/回导，并在可见 Blender 窗口打开 `Droplet_Rebuilt.blend`。外部 Python **3.14.3** 与已有 Pillow **12.1.1** 用于文件保全、读取用户无扩展名 PNG 及由真实渲染帧组成 GIF；没有安装工具。Cycles 128 samples、不降噪、不使用 Bloom/运动模糊；一份 7,040 三角形主游戏网格。

实际使用 **Unity 6000.5.10f1（3bd4f66ad299）**、URP **17.5.0**、Input System **1.20.0**、Test Framework **1.7.0**、Custom NUnit **2.1.0**、Pipeline **0.6.0-exp.1**；不升级现有包、Editor 或管线。专用 EditMode 3/3、PlayMode 3/3、完整 PlayMode 47/47。Windows x64/Mono 非 Development 构建与可见 **1920×1080 / Direct3D12 / RTX 4060 Laptop GPU** Player 32/32 检查分别完成。保留隐藏窗口运行未完成反射捕获的失败记录；不将其截图或计时作为最终证据，不宣称跨硬件性能。

Blender 源圆头 +Z/上方 +Y；只在导出副本转换到既有 Blender +Y 前/+Z 上，FBX -Z forward/Y up/bake_space_transform；Unity bakeAxisConversion、导入法线、单位缩放。Unity 检视截图按安装 URP 源码与随包 Render Requests 文档使用 `RenderPipeline.SubmitRenderRequest`/`SingleCameraRequest`，临时解析条带镜面仅用于独立检视，不修改正式反射系统。详细结果见 [DROPLET_REBUILD_REPORT.md](DROPLET_REBUILD_REPORT.md)。

以下保留历史环境记录。

## 天体尺度、太阳照明与材质反射批次 — 2026-09-08

本轮实际使用 **Unity 6000.5.10f1（3bd4f66ad299）**、**URP 17.5.0**、**Input System 1.20.0**、**Test Framework 1.7.0**、**Custom NUnit 2.1.0**、既有 **Pipeline 0.6.0-exp.1**。保留 PC_RPAsset/PC_Renderer 的 Forward+ 管线、默认 50 m 阴影距离及现有包版本，没有迁移 HDRP、升级 Editor 或安装工具。最终实际专项 EditMode 5/5、完整 PlayMode 42/42；实际非 Development Windows x64/Mono 构建及可见 Player 运行分别验证。证据见 [本轮报告](LIGHTING_MATERIALS_SCALE_REPORT.md)。

运行设备为 Windows 11 10.0.26200、**Ryzen 9 7940HX / RTX 4060 Laptop GPU / Direct3D12**，Unity 报告 RAM 15575 MB、显存 7956 MB。硬件报告支持光追，但 URP 17.5.0 没有原生硬件光追反射/SSR；本轮使用 HDR cubemap 与一个局部 Reflection Probe。High 时仅本场景运行期启用全局实时探针许可并在退出时恢复，不改 QualitySettings 资产。

独立版本 `Builds/Windows-Lighting-20260908/DropletPrototype.exe`：26.877 秒构建成功、129179970 字节、0 错误/1 可选 RuntimePipelineConfig 缺失警告；未发布。原始 BuildReport 时间戳偏移保留，报告使用构建时长，不伪改时间。实际 Player 1920×1080 Windowed、PC/High、VSync 0、不限帧、非 batch，5 段采样全部前台；近景、全景、贯穿各 300 帧，反射刷新/关闭各 2400 帧。32 条记录通过，19 张真实画面截图，自动退出。Editor 与 Player、均值与 P95、tracked memory 与未测 GPU/显存归因分别记录。

本轮没有执行 Blender 或重建舰船网格，复用先前 **Blender 5.2.1 LTS（9e2066aef7ef）/ io_scene_fbx 5.15.0** 产出的已导入 FBX。外部 **Python 3.14.3** 标准库仅用于项目文件保全和证据整理，没有新的运行时依赖。Unity 域重载附近的偶发工具连接失败通过重连和已编译 Editor 菜单处理；失败调用不计为通过。以下为历史环境记录。

## FusionFrigate 导入与扩大舰队批次 — 2026-09-08

实际使用既有 **Unity 6000.5.10f1（3bd4f66ad299）** Editor导入、保存场景/Prefab、编译、EditMode和PlayMode；**URP 17.5.0**、**Input System 1.20.0**、**Test Framework 1.7.0**、**Custom NUnit 2.1.0**、**Pipeline 0.6.0-exp.1**保持原安装版本。最终安全EditMode **13/13**、完整PlayMode **38/38**；未执行会重写TestRange的旧作者测试。没有安装工具、升级包或修改ProjectSettings/Packages，实际结果见 [扩编报告](FLEET_IMPORT_EXPANSION_REPORT.md)。

Blender独立后台进程实际复查既有舰模并生成布局副本，沿用 **Blender 5.2.1 LTS（9e2066aef7ef）**、内嵌 **Python 3.13.13**、bundled **io_scene_fbx 5.15.0**；外部 **Python 3.14.3**运行布局数学、重复性和文件保全检查。没有重新建模或覆盖FusionFrigate原始源/FBX。

性能为本机 **Ryzen 9 7940HX / RTX 4060 Laptop / Direct3D12** 的 **Editor Play 1920×1080**，每视点300帧，既有PC质量、VSync=0、AA=0、lodBias=2。ProfilerRecorder/UnityStats/原生LOD查询与Frame Debugger实际执行；内存与GC计数含整个Editor，Mesh/Material原生大小是全部LOD资源驻留口径，非显存归因。未构建/测量扩大版Standalone或GPU耗时，不复用旧SolarLayout独立版结果冒充本轮结果。

中文路径下动态eval偶发ROSLYN001、域重载时连接短暂失效；改用实际编译的Editor入口完成操作，失败尝试未计为通过。Input System诊断的首轮合成设备焦点问题修复后重测5舰贯穿通过，既有玩家输入代码未改。以下为历史环境记录。

## FusionFrigate 模型批次 — 2026-09-08

实际使用现有 **Blender 5.2.1 LTS（9e2066aef7ef）**，执行路径 `C:/Program Files/Blender Foundation/Blender 5.2/blender.exe`，独立 `--background --factory-startup` 进程；内嵌 **Python 3.13.13**、bundled **io_scene_fbx 5.15.0**、FBX 7400。`bpy`、`bmesh`、`mathutils` 和 Workbench 实际用于建模、重开、导出回导、网格与喷口检查和中性渲染。检查器版本 **1.0**。外部 **Python 3.14.3** 用于参数数组启动进程；现有 **Pillow 12.1.1** 用于真实帧排版和像素比较，没有安装工具/包。

读取并保留项目 **Unity 6000.5.10f1（3bd4f66ad299）**、**URP 17.5.0**、**Input System 1.20.0**、**Test Framework 1.7.0**、既有 **Pipeline 0.6.0-exp.1**。本轮没有调用 Unity Editor、导入 Assets、编译、运行 EditMode/PlayMode 或构建；这些版本是项目已安装环境，不是本轮 Unity 验证结果。

沿用项目米制和 FBX 轴向选项。当前两级 Empty 层次的临时导出矩阵处理依据安装版 `io_scene_fbx/fbx_utils.py` 的父级烘焙逻辑实现，并由实际回导世界几何和五个挂点矩阵核验；不更改源模型和既有导出器。最终检查/资源数量/未测项目详见 [FUSION_FRIGATE_MODEL_REPORT.md](FUSION_FRIGATE_MODEL_REPORT.md)。没有测量模型运行内存/显存。以下旧批次环境记录保留。

## G05–G09 batch — 2026-09-07

Unity 6000.5.10f1 (3bd4f66ad299), URP 17.5.0 with the existing PC_RPAsset, Input System 1.20.0 and Test Framework 1.7.0 remain in place. Blender 5.2.1 LTS (9e2066aef7ef) was actually executed in independent background processes; existing open unsaved Blender/Unity work was not force-closed. Its bundled FBX exporter and Python APIs generated the source models, closed pre-cut wrecks, marker layout and previews. No tools or packages were installed. Python standard library generated the original 48 kHz PCM16 audio.

New build scene order is FleetAssault, TestRange, then existing SampleScene. New builds explicitly select FleetAssault; old Builds/Windows is retained. Performance and standalone results are recorded separately in BATCH_G05_G09.md / verification/G05-G09. An Editor compilation success is not treated as a player/runtime test. Actual Unity 6000.5 requires GetEntityId instead of the obsolete GetInstanceID in new tests.

最终Windows v0.2.2：Mono、StandaloneWindows64、非Development，1920×1080 Windowed，PC质量/High表现、Direct3D12，沿用PC_RPAsset。实际原生构建成功，0错误/1可选开发桥警告，16.237秒。最终实际Unity测试15/15 EditMode、27/27 PlayMode；Test Framework 1.7.0、Custom NUnit 2.1.0。Python 3.14标准库用于原创WAV/打包，不引入运行时依赖。硬件实测与计数器可用性见 PERFORMANCE_G09.md；以下均为历史环境记录，不覆盖本批结论。

## G01–G04 batch update — 2026-09-07

The existing Unity 6000.5.10f1 / URP 17.5.0 / Input System 1.20.0 environment is unchanged. Installed `windowsstandalonesupport` was found under the same Editor's Data/PlaybackEngines directory. No package or tool was installed. Runtime and Editor assemblies now explicitly reference the already-installed Unity.InputSystem assembly.

The G00 empty-assembly warnings no longer apply: those assemblies now contain the batch's actual scripts and tests. Final Unity Test Framework 1.7.0 results: EditMode 3/3, PlayMode 18/18; Custom NUnit remains 2.1.0. JSON and NUnit XML evidence are in docs/verification. Additional live Input System smoke uses an Editor-only helper and temporary synthetic devices, removed on completion; it is excluded from the player.

Editor integration still intermittently reports ROSLYN001 illegal-byte-sequence or a network error around domain reloads. Work was completed through the actual compiler/test runner and a compiled Editor helper; none of these integration failures are represented as passing checks. Build and standalone launch results are recorded in STATUS.md.

Project configuration changes for this batch: reserve the previously empty user layer 8 as ShipTarget; add TestRange as the first enabled build scene, retaining the existing SampleScene entry. The Windows build explicitly selects TestRange. Editor version, packages, input-handler mode, GraphicsSettings and QualitySettings were preserved. The records below describe the historical G00 baseline.

The actual first Windows build also caused Unity to save its existing PC_RPAsset, DefaultVolumeProfile, UniversalRenderPipelineGlobalSettings and PlayerSettings serialization. These files are therefore included in the final changed-file list; preserving the pipeline means preserving its installed version and active asset/GUID, not claiming that every pipeline asset remained byte-identical after native build processing. Existing .meta files and SampleScene are unchanged.

Windows build succeeded (StandaloneWindows64, Mono, 0 errors, 1 development-bridge-disabled warning). Actual standalone rendering, scoring, victory and reset were observed. The native automation keyboard-injection limitation and manual physical-keyboard checks are documented in STATUS.md. The build tool's absolute timestamps appear offset from the local session; its reported duration is 76.193 seconds and raw timestamps are retained without silently rewriting them.

## G00 actual baseline — 2026-09-07

Verified against the existing project at `C:\学习\玩\Unity\新建文件夹\DropletPrototype` and its already-open Editor. No Editor/pipeline change, package installation, scene save, Play-mode entry, or player build was performed. Raw Unity results are in [verification/G00-unity-baseline.json](verification/G00-unity-baseline.json).

### Machine and installed tools

| Item | Observed value |
|---|---|
| OS | Windows 11 Home Chinese, 64-bit; 10.0.26200 / build 26200 |
| CPU | AMD Ryzen 9 7940HX with Radeon Graphics |
| RAM reported by Windows | 16,331,579,392 bytes (approximately 15.21 GiB usable) |
| GPUs | NVIDIA GeForce RTX 4060 Laptop GPU (driver 32.0.16.1062); AMD Radeon 610M (32.0.11036.4) |
| Unity project and live Editor | **6000.5.10f1**, revision **3bd4f66ad299** |
| Unity executable | `C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe` |
| Unity Hub | 3.21.1; `C:\Program Files\Unity Hub\Unity Hub.exe` |
| Unity CLI | 1.0.0-beta.8; `C:\Users\蒋雨辰\AppData\Local\Unity\bin\unity.exe` |
| Blender | **5.2.1 LTS**, build hash **9e2066aef7ef**, build date 2026-08-25 |
| Blender executable | `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` |
| Git | 2.53.0.windows.2; `C:\Program Files\Git\cmd\git.exe` |

Versions were checked using ProjectVersion.txt, the connected Editor, executable metadata, `unity --version`, `blender.exe --version`, Windows CIM, and uninstall-registry entries. The standard Unity Hub and Blender Foundation installation directories contain the versions above; no other installation was found in those locations or registry entries. Portable installations elsewhere were not exhaustively searched. Blender is running, but only its command-line version was queried; no scene was changed. Blender availability is not a G00 prerequisite.

### Unity configuration

- Active render pipeline: `UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset`, Universal RP **17.5.0**.
- Graphics default and current PC quality override: `Assets/Settings/PC_RPAsset.asset` (GUID `4b83569d67af61e458304325a23e5dfd`). Confirmed by live Graphics/Quality queries and a read-only Editor API evaluation.
- Current quality: index 1, **PC**. Mobile quality uses `Assets/Settings/Mobile_RPAsset.asset` (GUID `5e6cbd92db86f4b18aec3ed561671858`). Both existing assets and their renderer settings were retained.
- Active build target: `StandaloneWindows64`; build-module functionality and player startup have not been tested.
- Input System package **1.20.0**; serialized active input handler is 1 (Input System). Existing `Assets/InputSystem_Actions.inputactions` retained.
- Existing serialization mode: Force Text (`m_SerializationMode: 2`); version control mode: Visible Meta Files.
- Open scene: `Assets/Scenes/SampleScene.unity`, three roots, active/loaded, `isDirty: false` before and after setup. Editor was ready and out of Play mode. No new scene or prefab was created in G00.

### Resolved package versions

Read from the unchanged `Packages/manifest.json` and `Packages/packages-lock.json`. Core pipeline, Input System, NUnit and Test Framework versions were also checked in the resolved PackageCache package.json files. The lock file records Unity-provided packages as `builtin`; this does not mean the project uses the Built-in Render Pipeline.

| Package | Version | Lock source |
|---|---|---|
| `com.unity.ai.navigation` | 2.0.14 | registry |
| `com.unity.burst` | 1.8.30 | registry |
| `com.unity.collab-proxy` | 2.13.6 | registry |
| `com.unity.collections` | 6.5.0 | builtin |
| `com.unity.ext.nunit` | 2.1.0 | builtin |
| `com.unity.ide.rider` | 3.0.38 | registry |
| `com.unity.ide.visualstudio` | 2.0.26 | registry |
| `com.unity.inputsystem` | 1.20.0 | registry |
| `com.unity.mathematics` | 1.4.0 | builtin |
| `com.unity.multiplayer.center` | 1.0.1 | builtin |
| `com.unity.nuget.mono-cecil` | 1.11.6 | registry |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | registry |
| `com.unity.pipeline` | 0.6.0-exp.1 | registry |
| `com.unity.render-pipelines.core` | 17.5.0 | builtin |
| `com.unity.render-pipelines.universal` | 17.5.0 | builtin |
| `com.unity.render-pipelines.universal-config` | 17.5.0 | builtin |
| `com.unity.searcher` | 4.9.5 | registry |
| `com.unity.shadergraph` | 17.5.0 | builtin |
| `com.unity.test-framework` | 1.7.0 | builtin |
| `com.unity.test-framework.performance` | 3.5.0 | registry |
| `com.unity.timeline` | 1.8.13 | registry |
| `com.unity.ugui` | 2.5.0 | builtin |
| `com.unity.visualscripting` | 1.9.12 | registry |

All resolved `com.unity.modules.*` entries are version 1.0.0; the lock file is the complete module/dependency inventory. Custom NUnit 2.1.0 is Unity's fork based on NUnit 3.5, per its installed package description. Test Framework Performance is already a transitive dependency; no performance test ran. Existing packages unrelated to the initial game scope were preserved, not adopted as gameplay requirements.

### Folder and assembly baseline

Created `ArtSource/Blender/` and `Tools/Blender/` outside Assets, plus the full `Assets/_Project/` folder layout in IMPLEMENTATION_PLAN.md. Unity's folder API authored the asset folder .meta files; Unity imported the four .asmdefs and generated their .meta files. Empty leaf folders have .gitkeep files so Git retains the structure.

| Assembly | Location under `Assets/_Project/` | Boundary |
|---|---|---|
| `DropletPrototype.Runtime` | `Scripts/Runtime/` | Runtime, no Editor assembly references |
| `DropletPrototype.Editor` | `Scripts/Editor/` | Editor only; references Runtime |
| `DropletPrototype.Tests.EditMode` | `Tests/EditMode/` | Editor only; references Runtime and Editor; TestAssemblies enabled |
| `DropletPrototype.Tests.PlayMode` | `Tests/PlayMode/` | Runtime reference; TestAssemblies enabled; available for PlayMode/player tests |

Test definitions use the `optionalUnityReferences: ["TestAssemblies"]` form present in the installed Test Framework 1.7.0 samples. Both test assemblies have autoReferenced disabled. All four assemblies are intentionally empty at G00; Unity skips compiling them and emits four no-scripts warnings. They are layout scaffolding, not implemented systems or passing tests. Add real milestone-specific scripts/tests when their milestone is requested. `TestRange.unity` belongs to G01; `FleetAssault.unity` belongs to a later milestone.

### Git baseline

- Git currently resolves to the existing parent repository **`C:/学习/玩`**, branch `master`, HEAD `d82ae1b`; there is no project-local .git and no configured remote.
- DropletPrototype was entirely untracked at inspection. The parent also has unrelated existing changes; none were staged, committed, reverted, or modified.
- No .gitignore existed in the project or its ancestor chain. No core.excludesFile was configured; the parent info/exclude contained comments only.
- Added a project-local `.gitignore` for Unity caches, build/test outputs, generated IDE files, OS files and Blender backup copies. Shared .vscode files, source .blend files, .meta files, Packages, ProjectSettings and docs remain eligible for version control.
- `git check-ignore -v` confirmed representative generated paths are ignored and source paths are not. No Git initialization, index change, remote configuration or commit was performed. Decide whether to use the parent repository or a dedicated project repository before the first project commit; this does not block G00.

### Verification and limitations

**BASE-01 passed in the actual, already-open Unity 6000.5.10f1 Editor.** After setup, `recompile({focus:false})` and `recompile_status` returned `completed`, `failed:false`, `errors:[]`. The Editor returned ready, compiling=false, domainReloadInProgress=false. A subsequent Editor API check returned scriptCompilationFailed=false. Captured Console output contained four empty-assembly warnings and no errors; it was not cleared. Evidence timestamp: 2026-09-07 11:10 UTC / 19:10 Asia/Shanghai.

One optional `eval` call requesting AssetDatabase.Refresh failed inside the integration's Roslyn evaluator with `ROSLYN001: Illegal byte sequence`. The native recompile command then succeeded, all .asmdefs/.meta files were imported, and a later read-only eval succeeded. This was a tool-call failure, not a reported project compilation failure.

Test discovery through `list_tests(mode:"all")` succeeded and found **0 tests**. EditMode and PlayMode tests were **not run**. Player build, scene/gameplay smoke tests, fresh Editor launch/license activation, Blender export/import and performance checks were **not run**; they are outside G00 or require later milestone content. No dotnet build was used as Unity evidence.

SHA-256 comparison of all **62 pre-existing files** under Assets, Packages and ProjectSettings found **zero changed or missing files** after setup. This covers existing scenes, assets, .meta GUIDs, Editor version, render pipeline and package configuration.

### Manual confirmation / reproducing BASE-01

1. Focus the existing DropletPrototype Unity window. Confirm the project path and Help > About Unity version **6000.5.10f1**. If it is closed later, use Unity Hub > Projects > Add project from disk and select this project root, then open with the recorded Editor. No install or upgrade is required on this machine.
2. Preserve any personal unsaved work. In the current session `Assets/Scenes/SampleScene.unity` is already open; opening another scene or closing Unity is unnecessary. If reopening on another day, save your work before opening SampleScene.
3. Choose Assets > Refresh and wait until import/compilation activity stops. Open Window > General > Console, enable the Error filter and confirm no red errors. The four empty-assembly warnings are expected until future milestone scripts exist. Do not treat those warnings as passed tests.
4. Open Edit > Project Settings > Graphics and confirm PC_RPAsset as the default Render Pipeline Asset. Under Quality, PC should use PC_RPAsset and Mobile should use Mobile_RPAsset. Inspect without changing these values.
5. In the Project window, inspect Assets/_Project and its folders. Window > General > Test Runner should have no project tests yet; do not create sample tests or claim an empty run passed.
6. If any red errors appear, retain the first error and its stack trace and record them in STATUS.md before G01. Otherwise no corrective setup action is required. Git repository choice is only needed before committing the project.

## Earlier integration setup record (preserved)

The following notes predate G00 verification. The restart instruction referred to that integration session; the current session successfully used the MCP tools.

Verified 2026-09-07 for official Unity MCP setup.

- Unity Editor: 6000.5.10f1; existing version preserved.
- Render pipeline: Universal RP 17.5.0.
- Unity CLI: 1.0.0-beta.8 (official beta channel latest manifest at setup time; installer SHA-256 verified).
- Official MCP: Unity CLI `unity mcp`, Codex global server name `unity`, pinned to this project.
- Unity Pipeline: com.unity.pipeline 0.6.0-exp.1, installed through `unity pipeline install`.
- Unity Test Framework: 1.7.0. No EditMode/PlayMode tests or player build run for this integration-only task.
- Blender: 5.2.1 LTS (running window version).
- Checks: Editor reports ready, compiling=false; CLI scene read succeeded; MCP initialize and tools/list succeeded (149 tools).
- Previous Codex `unityMCP` HTTP server entry and its per-tool settings removed. No old third-party MCP package was found in this project's Packages, Assets, or ProjectSettings; no listener was found on port 8080.
- Restart Codex to load the new MCP entry in a fresh session.

## SpaceEnvironment 环境几何批次 — 2026-09-07 至 2026-09-08

本轮在同一 Windows 项目中实际运行 Blender 生成、保存、重开、导出，以及已有 Unity Editor 的编译、导入、场景/Prefab 保存、重复导入和资源审计。未升级或安装 Editor、Blender、渲染管线、包或外部工具。下表是本轮使用的环境，详细通过项、未测项和 TestRange 保全偏差以 [SPACE_ENVIRONMENT_GEOMETRY_REPORT.md](SPACE_ENVIRONMENT_GEOMETRY_REPORT.md) 为准；环境可用不等于所有保全约束均已满足。

| 工具 / 包 | 本轮实际版本与用途 |
|---|---|
| Unity Editor | **6000.5.10f1**，修订 `3bd4f66ad299`；沿用既有安装与项目 |
| Universal RP | **17.5.0**；沿用 `PC_RPAsset` 及原默认 Renderer |
| Input System | **1.20.0**；原玩法输入配置保留 |
| Unity Test Framework | **1.7.0**；既有 EditMode / PlayMode 回归检查 |
| Custom NUnit | **2.1.0**（`com.unity.ext.nunit`，Unity 的 NUnit 分支）；未替换测试依赖 |
| Unity CLI | **1.0.0-beta.8**；既有 `C:\Users\蒋雨辰\AppData\Local\Unity\bin\unity.exe`，通过命令/Editor 集成执行本机检查 |
| Unity Pipeline | **0.6.0-exp.1**；既有 `com.unity.pipeline`，未重新安装 |
| Blender | **5.2.1 LTS**，构建 `9e2066aef7ef`；独立 `--background --factory-startup` 进程，使用随 Blender 提供的 `bpy`、`bmesh`、`mathutils` 和 FBX 导出器 |
| 外部 Python | **3.14.3**；本机 `python --version` 再确认，用于 `run.py`、精确参数 CLI 调用和文件/证据处理；不是对 Blender 内嵌 Python 版本的声明 |

Blender 执行程序仍为 `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`；Unity Editor 仍为 `C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe`。未强制关闭已有未保存工作。星图生成使用当前 Blender 环境已有 NumPy，没有执行 pip 安装。

导出继续使用已校准的 bundled FBX 流程：Blender +Y 前/+Z 上，`axis_forward=-Z`、`axis_up=Y`、`global_scale=1`、`bake_space_transform=true`；Unity 使用 `bakeAxisConversion=true` 和单位比例 1。布局 Empty 以微型非对称姿态代理导出，再由 Editor 读取完整变换；没有修改 Unity 序列化 YAML 或猜测 GUID。`Tools/Blender/SpaceEnvironment/unity_command.py` 使用 Python `subprocess` 的参数数组调用已安装 CLI，避免 PowerShell 5 字符串转义影响代码。

Unity 写入由当前版本 Editor API 完成：`AssetDatabase` / `ModelImporter` / `TextureImporter` 管理资源，场景、Prefab 和 Undo API 管理持久化对象；本轮 `SpaceEnvironmentPipeline.cs` 与 `SpaceEnvironmentFrameProbe.cs` 位于 Editor 程序集。原生 LOD 检查调用本机 `LODUtility.CalculateVisualizationData`；逐资源内存采用 `Profiler.GetRuntimeMemorySizeLong`，绘制量采用实际 Frame Debugger 事件。相关 Editor 内部诊断按本机版本执行，不声称可直接兼容其他 Unity 版本。

检视配置有明确局部调整：在 `PC_RPAsset` 的 Renderer 列表追加任务自有 `PreviewOnly/NeutralRenderer.asset`，原默认索引 0 和原 PC Renderer 保持不变。独立检视相机选用该无 Renderer Features 的 Forward Renderer，以排除原 Renderer 的 SSAO。检视场景中的白色检查灯独立于环境 Prefab；未新增正式光效、反射或后处理。新增 `SpaceEnvironment` 层 9，原命中掩码仍为 256。

本轮实际回归结果为 EditMode **15/15**、PlayMode **27/27**，零失败或跳过；但旧 EditMode 场景重建测试意外重写 TestRange，不能用测试通过否认该偏差，恢复状态见上述报告。没有为本轮生成新的独立 Player 或发布包。Editor / Editor Play Mode 资源内存、整体 Editor 内存、缓冲与 mip 链估算分别记录；未测独立 Player 内存、可归因显存、GPU 时间或跨硬件帧率，不将文件大小当作运行内存。

## SolarSystemLayout 重布局批次 — 2026-09-08

继续使用Unity **6000.5.10f1 (3bd4f66ad299)**、URP **17.5.0**、Input System **1.20.0**、Unity Test Framework **1.7.0**、Custom NUnit **2.1.0**、Unity CLI **1.0.0-beta.8**、Pipeline **0.6.0-exp.1**、Blender **5.2.1 LTS (9e2066aef7ef)** 和外部Python **3.14.3**。没有升级、安装包或迁移管线；ProjectVersion、manifest、packages-lock的任务前后哈希一致。执行程序路径沿用上表。

实际运行 `Tools/Blender/SolarSystemLayout/run.py` 的build/inspect/export，在独立Blender进程保存和重开新源，沿用既有FBX校准契约，只导出32个姿态标记和配置；模型/材质/贴图复用原库。Unity写入由Editor编译程序集的SolarLayout菜单/API执行。PC_RPAsset在Unity原生处理后字节改变，默认Renderer仍为0；本轮代码只查询既有NeutralRenderer，不新增Renderer或后期。未保存此asset任务前字节副本，原生写回不能作可靠字段级比较，见保全报告。

实际Unity编译通过；安全的新增数学EditMode **7/7**、全部PlayMode **30/30**。旧会重写TestRange的EditMode场景生成测试未运行。临时Input System合成键鼠驱动真实FixedUpdate完成约40.66秒扩展地图飞行、提示与回收；它不是用户物理键鼠的主观验收。原生LODUtility用于LOD0/1/2/剔除查询；本轮没有新的Frame Debugger GPU逐事件记录。

实际Windows64/Mono本地验证构建成功：103.814秒、0错误/1个可选Pipeline运行桥未配置警告。路径 `Builds/Windows-SolarLayout-Validation-20260908/`，未发布，旧构建保留。可见独立Player实测1920×1080、Direct3D12、PC质量/High表现、NVIDIA GeForce RTX 4060 Laptop GPU、AMD Ryzen 9 7940HX；20条继承记录通过，含7条截图写入。此前隐藏Player的黑帧及异常快帧时结果已明确否决，不能引用为渲染或性能成功。

Editor环境唯一Mesh+Texture原生大小6.688 MiB；可见Player全局Unity分配器统计约192.0–193.7百万字节/保留290.5百万字节（Profiler.GetTotalAllocatedMemoryLong/GetTotalReservedMemoryLong），不是操作系统进程工作集，也不是环境独占值。GC逐帧计数器不可用；可归因显存、Player环境逐资源内存、GPU耗时、长期与跨硬件性能未测。完整证据与限制见 [SOLAR_SYSTEM_LAYOUT_REPORT.md](SOLAR_SYSTEM_LAYOUT_REPORT.md)。


## PerfectDroplet ???? ? 2026-09-08

??????? **Blender 5.2.1 LTS**??? **9e2066aef7ef**?2026-08-25???????? `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`?????/??/??/??/????? `--background --factory-startup` ???????????? Blender ????? Python ?? **3.13.13 / MSC v.1944 64 bit AMD64**??? Python **3.14.3** ????????????????? Blender ??? bpy?bmesh?mathutils?Workbench ? FBX 7.4 ?????????????

????????? Unity **6000.5.10f1 (3bd4f66ad299)**?URP **17.5.0**?Input System **1.20.0**?Unity Test Framework **1.7.0**??? Custom NUnit **2.1.0**?Pipeline **0.6.0-exp.1** ???ProjectVersion?manifest ??? Packages/ProjectSettings ??????????**????? Unity????????EditMode?PlayMode???????????**????????????????

??????? +Z ? / +Y ?????????? `(x,y,z) ? (?x,z,y)` ???? Blender +Y ? / +Z ???????? `axis_forward=-Z`?`axis_up=Y`????? 1 ? bake_space_transform??? Blender FBX ?????????????? Unity ???? bakeAxisConversion ? Import Normals ??????? ART-01 ??????????????????/???? Blender ?? MatCap ?????????????????????????? [????](DROPLET_GEOMETRY_REPORT.md) ? `verification/PerfectDroplet/` ?????/JSON?


## PerfectDroplet 几何批次 — 2026-09-08

实际使用已安装 **Blender 5.2.1 LTS**，构建 **9e2066aef7ef**（2026-08-25），执行程序仍为 `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`。构造、重开、渲染、导出和回导在独立 `--background --factory-startup` 进程运行，没有覆盖交互式 Blender 会话。内嵌 Python 实测 **3.13.13 / MSC v.1944 64 bit AMD64**；外部 Python **3.14.3** 用于参数数组启动和文件证据。使用随 Blender 提供的 bpy、bmesh、mathutils、Workbench 和 FBX 7.4 导入导出器；没有安装或下载。

本轮读取的项目仍为 Unity **6000.5.10f1 (3bd4f66ad299)**、URP **17.5.0**、Input System **1.20.0**、Unity Test Framework **1.7.0**；既有 Custom NUnit **2.1.0**、Pipeline **0.6.0-exp.1** 保留。ProjectVersion、manifest 和整个 Packages/ProjectSettings 的任务前后哈希一致。**本轮未调用 Unity，未运行其编译、EditMode、PlayMode、构建或新模型导入校准**；不把历史结果当成本轮通过记录。

源为用户要求的 +Z 前 / +Y 上；只在导出副本通过 `(x,y,z) → (−x,z,y)` 转到既有 Blender +Y 前 / +Z 上校准流程，沿用 `axis_forward=-Z`、`axis_up=Y`、单位缩放 1 和 bake_space_transform。独立 Blender FBX 回导对比位置与法线通过；后续 Unity 使用既有 bakeAxisConversion 和 Import Normals 契约，仍需实际 ART-01 检查。四视图用临时中性材质，额外高光/条带使用 Blender 内置 MatCap 作法线诊断；没有新场景灯光、反射探针或正式材质。详见 `docs/DROPLET_GEOMETRY_REPORT.md` 和 `docs/verification/PerfectDroplet/` 的实际日志/JSON。


## PerfectDroplet Unity 导入测试 — 2026-09-08

实际复用 Unity **6000.5.10f1 (3bd4f66ad299)**、URP **17.5.0**、Input System **1.20.0**、Unity Test Framework **1.7.0**、Custom NUnit **2.1.0**、Unity CLI **1.0.0-beta.8** 和 Pipeline **0.6.0-exp.1**。未升级、安装或迁移。Blender 源及游戏 FBX 来自上一轮已完成的 Blender **5.2.1 LTS (9e2066aef7ef)** 几何交付；本轮直接导入其显式导出，不重新执行 Blender 建模。

实际 Editor API 通过已编译的 `PerfectDropletImport` 菜单运行：ModelImporter 设置 Bake Axis Conversion、Import Normals、MikkTSpace 切线、关闭网格压缩和 Read/Write；只读检查现有 1 m/+Z/+Y/+X 校准物，再验证新模型 2.4 m 长、0.774194 m 直径、单位变换、+Z 尖端及解析法线。场景由 AssetDatabase.CopyAsset 和 EditorSceneManager 保存为新路径，仅替换水滴共享 Mesh，保留原 Renderer 和玩法引用。原始 758 个 Assets/Packages/ProjectSettings/ArtSource/Tools 文件最终全部哈希一致。

实际 Unity 编译通过，专项 EditMode **3/3**、PlayMode **2/2**。临时 Input System 合成设备驱动真实 FixedUpdate 完成 528.438 m / 4.860 s 连续五舰飞行、1,500 分和暂停/刹车/转向/重开检查；不是物理键鼠的主观验收。实际 Unity 相机/屏幕截图共 9 张，均 1920×1080，复用现有 URP 灯光及水滴材质；中性近景用临时内存材质，拍摄后恢复全部临时配置。未生成新构建或执行本轮独立 Player/GPU 性能测试。

本轮动态 Roslyn eval 出现 Illegal byte sequence，后续通过编译菜单完成操作；首次 EditMode 工具请求超时，随后查询获得实际 3/3 完成结果。结束时 Editor ready、Play stopped、测试场景无未保存修改，近期捕获错误为 0。未运行会重建 TestRange 的旧作者测试。完整证据与边界见 [DROPLET_UNITY_IMPORT_REPORT.md](DROPLET_UNITY_IMPORT_REPORT.md)。


## Enhancement integration — 2026-09-19

Current project `C:/学习/玩/Unity/Trysolar Drip`; preserved Unity **6000.5.10f1 (3bd4f66ad299)**, URP **17.5.0**, Timeline **1.8.13**, Input System **1.20.0**, Unity Test Framework **1.7.0**, Pipeline **0.6.0-exp.1**. No Blender operation or geometry re-export in this batch (existing Blender 5.2.1 sources retained). Python **3.14.3** runs the existing Seed-TTS 2.0 authoring tool and procedural PCM synthesis; FFmpeg **9.0.1-full_build-www.gyan.dev** located. Audacity not used. No new software installed.

Actual Editor compile and full PlayMode **91/91**, new fleet EditMode **6/6**, audio offline Python **14/14**. Seed-TTS real call is **blocked HTTP403 / resource permission not granted**, not a passed live synthesis. New voice/listening and 4–6 minute opening acceptance remain blocked. The real AudioMixer was authored through installed Editor APIs; the isolated solar feature uses installed URP17.5 RenderGraph/Blitter/depth APIs.

Hardware: AMD Ryzen9 7940HX, NVIDIA GeForce RTX4060 Laptop GPU; standalone Direct3D12, native 1920x1080, renderScale1, no frame generation. Final player timing and PID-scoped WDDM memory evidence are reported separately in `ENHANCEMENT_ACCEPTANCE.md`. Scene and build paths are `Assets/_Project/Scenes/FleetAssault_Enhanced.unity` and `Builds/Windows-Enhanced-20260919/DropletGaming.exe`.

Follow-up visual repair: the contact-flash lifetime regression class was rerun in the actual Editor, **4/4 passed** after the initial full91 pass. The verification build enables Unity FrameTiming statistics; the project's original `PlayerSettings.enableFrameTimingStats=false` was restored after building. FrameTiming's invalid GPU timestamp values are counted and rejected rather than reported as physical GPU time. Existing PC quality preset and High effects are used; no dynamic resolution or frame generation was added. See the final accepted run and retained intermediate failed observation-window run in the acceptance report.
