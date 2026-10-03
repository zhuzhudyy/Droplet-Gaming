# Implementation status

## 版本整理与远端交付 — 2026-10-03

按当前保留策略整理，并提交此前未推送的 Seed / Cinematic / Enhanced 源码、资源及验证记录。已删除仓库中的旧 NarrativeCombat 发行 ZIP（56,067,483 bytes）；Git 历史仍可恢复。最新版 Seed 玩家归档为 176,407,481 bytes，197 个条目的 SHA-256 全部与本机现有已验证构建一致。发行包通过 GitHub Release `seed-audio-20260929` 分发，README 已指向最新版。详见 [整理报告](verification/VersionCleanup-20261003/REPORT.md)。

**11 个旧构建目录仍未清理：** 已校验路径的递归删除再次被工具自动审核以 `blocked by policy` 拒绝，未执行、未释放其空间。本机当前 Seed 玩家、前版 CinematicAudio 完整回退、v0.2.2 ZIP、源材、历史场景与 GUID 均保留；无关根目录截图不纳入提交。本次仅做整理、归档与版本控制交付，没有重新编译/试玩，不改变下方 34 自动通过、3 人工待验的结论。

## 当前交付：全 Seed 音频与 2000 舰音频场景 — 2026-09-29

最新可玩场景是 `Assets/_Project/Scenes/FleetAssault_SeedAudio.unity`，独立玩家是 `Builds/Windows-SeedAudio-20260929/DropletGaming.exe`。Unity Editor API 从上一版正式场景创建安全副本，并将新版设为默认启用的 Build Settings 场景；旧场景在设置中保留但禁用，原 GUID 和无关未提交工作保留。播放器只使用已导入的本地音频，不联网。2000 舰、90 分钟任务与原有玩法参数保留。

六类 Seed Audio 代表样音（激光、贯穿、爆炸、飞行、舱室、配乐）已由用户试听并确认“六类都可用”。随后完成 **131/131** 条独立非语音成品、**87/87** 条新版 Seed-TTS 2.0 英语干声及 **80** 段通讯加 **78** 条强度变体，共 **158** 条新通讯混音。旧程序音效的 25 个源槽位已由 Seed 录音替换，通讯随之重混；旧素材仍保留。`Laser_03` 原请求返回 502 且状态不明，目录显式映射到成功的 `Laser_03_R2`；E033 原中断请求也保留，以 `E033_R2` 替代，均未对不明请求自动重发。提示词、原声、处理与哈希保存在 `ArtSource/Audio/SeedAudio20260929/`，试听入口见其中 `listening.html`、`catalog-qa/delivery-listening.html`、`voice-qa/listening.html` 和 `remixed/listening.html`。

客观素材检查显示 **131/131** 可发布，**0** 技术错误、**0** 疑似重复；25 项非阻断提醒包括 22 条文件级通讯频段余量、两条起音偏迟及一条偏短，需在实际混音中试听判断。音频离线工具测试 **64/64** 通过。Unity 6000.5.10f1 实际导入 131 条效果与 158 条通讯，运行时 PlayMode **6/6**、Editor 导入 EditMode **3/3**、新场景集成 PlayMode **3/3**，验证器迭代器专项 **1/1** 通过；先前飞行测试首轮失败及修复后的通过记录均保留。Windows 构建成功、**0 错误、1 警告**。原生 **1920×1080 / Direct3D12** 独立玩家在完整 2000 舰场景通过 **34** 项自动检查、**0** 失败，录得六段真实监听器 WAV 与六张画面；覆盖四幕音轨/舱室映射、巡航与冲刺、压低背景、暂停恢复、跳过、真实贯穿至延爆、12 路世界声源上限和连续三次重开。证据见 `docs/verification/SeedAudio-20260929/`。

终审修复了反应堆与撤离持续声的生命周期：同舰两层可并行循环，同类重复事件去重，舰爆/逃脱即停对应持续声，复用槽位清除循环。最终 Unity 编译、针对世界音源 PlayMode **6/6**、场景集成 **3/3**、运行时 **6/6** 复测通过；随后重新构建并在最新二进制上复跑 1920×1080 玩家，仍为 **34 自动通过、0 失败、3 人工**。最终玩家报告及六段录音位于 `docs/verification/SeedAudio-20260929/player-validation-release/`，专项测试位于同级 `seed-sustained-world-release-tests.json`。

**仍待人工验收 3 项：** 自然走完 90 分钟及成功结算；逐条成品/英文表演、背景循环接缝与玩家内混音试听；完整四幕自然播放后以真实键鼠攻击。四幕映射已经由加速定位技术检查覆盖，但这不等于自然完整观看。除六类代表样音外，131 条正式变体、87 条新版干声与 158 条通讯均未逐项由人试听，因此最终 `allAcceptancePassed=false`。目前没有自动检查失败或已知功能阻断；下一步是上述主观与长时路线验收，不把技术通过写成听感通过。完整交付与手动步骤见 [本批验收报告](SEED_AUDIO_ACCEPTANCE.md)，下方 2026-09-29 较早条目和 2026-09-19 条目保留为历史记录。

版本保留清理已检查目标路径，拟移除的 11 个过时构建目录的递归删除被自动审核拒绝，因此**旧目录仍在，未报告为已清理**。当前 Seed 玩家、前版 CinematicAudio 完整回退及 v0.2.2 精简 ZIP 均在；素材源文件与验证证据不属于清理目标。

## Audio 免费服务开通后复测 — 2026-09-29

用户告知已开通免费服务并明确要求再试。对同一 seed-audio-1.0 激光请求依次复测已配置的 UUID Key、Ark Key、旧版 APP ID + Access Token（含 Audio 资源头）：分别仍返回 403/45000030 requested resource not granted、401/45000010 Invalid X-Api-Key、401/45000010 load grant: requested grant not found in SaaS storage。无音频产物。独立原始记录在 ArtSource/Audio/SeedAudio20260929/raw/LASER_ACTIVATED_e66b5798、LASER_ACTIVATED_491b2fb6、LASER_ACTIVATED_LEGACY_8b9740e6。此次三条均为用户报告服务状态变化后的显式复测，无自动重试。下一步需核对实际开通服务名称及所属项目与调用凭据对应关系，不能仅凭错误推断开通未成功。未修改 Unity 或新增账本工作。

## Audio 权限修改后复测 — 2026-09-29

用户明确告知刚修改权限并要求再试。以文档模型 seed-audio-1.0 对两个已保存 Key 各提交一次激光请求：截图方舟 Key 仍返回 401/45000010 Invalid X-Api-Key；先前 UUID Key 仍返回 403/45000030、[resource_id=volc.service_type.10074] requested resource not granted。均无音频输出。证据为 ArtSource/Audio/SeedAudio20260929/raw/LASER_PERMISSION_25f1c1ba/job.json 与 LASER_PERMISSION_4d0f7c8a/job.json。不推断权限传播延迟或修改无效，仅确认当前接口仍拒绝这两次请求。未修改 Unity 或开展额外账本工作。

## Audio 截图模型名与两把 Key 对照实测 — 2026-09-29

用户提供模型勾选“Doubao-音频生成-1.0”和方舟 API Key 截图，明确要求都试。针对同一国内官方 /api/v3/tts/create 接口，执行有限四组激光请求：seed-audio-1.0 + 截图 ARK_API_KEY 返回 401/45000010 Invalid X-Api-Key；seed-audio-1.0 + 先前 VOLC_AUDIO_API_KEY 返回 403/45000030、volc.service_type.10074 requested resource not granted；将截图显示名称 Doubao-音频生成-1.0 字面作为 model 分别配这两把 Key，两次均返回 403/45000030、extract request resource id: fail to convert model to resource_id。四组均无 WAV；真实日志依次为 raw/LASER_MATRIX_021a745c、530538ee、939c140b、f2901ead 下 job.json（后三项同 LASER_MATRIX_ 前缀）。确认截图显示名称不能作为此接口 model 参数；尚未验证截图方舟页面是否存在另一种官方接入路径，不将 Ark Key 在语音接口拒绝等同于全局失效。未自动重试、改计费、开发账本或修改 Unity。

## Audio 旧版双头鉴权实测 — 2026-09-29

按用户两张截图，将 APP ID / Access Token 保存到本地用户变量 VOLC_AUDIO_APP_ID / VOLC_AUDIO_ACCESS_TOKEN（不使用 Secret Key，不覆盖 TTS），扩展 seed_audio.submit 的旧版鉴权。先按截图双头提交激光请求，返回 HTTP 401 / 45000010 / load grant: requested grant not found in SaaS storage。随后打开官方音频文档链接的旧版鉴权示例，发现其通用旧版合成要求 X-Api-Resource-Id；用前次 Audio 服务实际返回的资源标识 volc.service_type.10074 补齐后再提交一次，结果相同，无音频输出。两次请求分别留在 raw/LASER_LEGACY_a48c01ce 和 raw/LASER_LEGACY_RESOURCE_2c66b807。不是超时自动重发；尚无法区分凭据/应用匹配或该应用资源授权缺失，不把失败声称为模型能力问题。没有额外账本开发或 Unity 改动。

## Audio 替换密钥实测 — 2026-09-29

用户提供替换凭据并要求直接试用。已保存到 Windows 用户 VOLC_AUDIO_API_KEY，保留原 TTS；随后仅提交一条约 4 秒激光请求。国内 Seed Audio 1.0 接口这次返回 HTTP 403 / 45000030，明确消息为 [resource_id=volc.service_type.10074] requested resource not granted。没有音频输出、没有自动重试，也未自行开通/购买资源。此结果区别于前两次的 401 Invalid X-Api-Key：当前请求被资源授权拒绝，尚不能生成。证据：ArtSource/Audio/SeedAudio20260929/raw/LASER_NEWKEY_b5062a4f/job.json。未修改 Unity 或运行构建；本次直接请求仅保存来源/结果，不开展额外账本工作。

## Audio 密钥直接配置与生成 — 2026-09-29

用户要求停止账本准备、直接配置其明确提供的凭据并先生成音频。已保存为 Windows 用户变量 VOLC_AUDIO_API_KEY，读回比较成功，原 TTS 凭据保留，关闭本批已不需要的本地输入窗口。随即向国内 Seed Audio 1.0 官方 /api/v3/tts/create 实际提交一条约 4 秒激光请求：仍返回 HTTP 401 / 45000010 / Invalid X-Api-Key，没有生成音频。原始请求与脱敏结果留在 ArtSource/Audio/SeedAudio20260929/raw/LASER_DIRECT_*；没有自动重试。此直接请求未写入前面的 audio-ledger，后续若恢复账本须从原始 job 纳入未明用量，不应将旧账本当最新总额。当前缺口是这把密钥被 Audio 接口拒绝，不是本地保存失败或额度判断；需使用官方语音服务密钥入口的可用凭据。未修改 Unity 场景或生成新构建。

## 全 Seed 音频补全 — 2026-09-29，新 Audio 路线执行中

用户最新授权非语音改用已配置 Audio，独立上限 60 元，无需查询余额；对白保持 Seed-TTS 2.0，累计上限 20,000 AFP，继承旧试验占用 537.975 AFP。[修订方案](SEED_AUDIO_IMPLEMENTATION_PLAN.md) 已保存，旧 TTS-only 方案和证据保留。目标 131 个非语音、重写 87 条英语对白/中文字幕、按缺口最多 24 条新增短语音；适度延长开场，保持 2000 舰与 5400 秒任务参数。目前进行官方 API 契约核对、代表提示词和对白/工具准备，未声称新素材已生成或 Unity 已接入。

## 火山方舟 API Key 本地配置 — 2026-09-29，已保存并核对

用户提供方舟 API Key 管理页面并要求本地配置。已新增 `Tools/Audio/Set-ArkAudioCredential.ps1`，语法检查通过。最初隐藏启动未显示窗口，改为可见交互窗口后，用户已完成输入。2026-09-29 16:46:29（UTC+8）本机助手保存到 Windows 用户变量 `ARK_API_KEY` 并比较读回值成功；随后独立检查确认状态 `saved_and_readback_verified`、方舟凭据可读取、原 `VOLC_SPEECH_API_KEY` 仍存在。本轮未打印、记录或写入项目任何密钥值。状态记录在 `%LOCALAPPDATA%/DropletPrototype/ark-credential-status.json`，不含密钥。**本地配置完成，云端鉴权和具体音频模型权限尚未测试。** 未调用 API、消耗生成额度或修改 Unity；说明见 [音频配置文档](VOLCENGINE_VOICE.md)。

## Seed-TTS 全音频补全 — 2026-09-29，停在代表素材门槛

已先记录 [实施计划](SEED_AUDIO_IMPLEMENTATION_PLAN.md) 与 [131 项声音目录](../ArtSource/Audio/SeedTTS20260929/catalog.json)，再执行真实测试。唯一模型 Seed-TTS 2.0，沿用既有 Agent Plan 和本机密钥。**现有鉴权可用，不需要提供新密钥。** 英语控制生成 5.396 秒完整 WAV；六类非语音各完成初次 + 两轮修订，合计 19 次请求、17 个完整 WAV、2 次无有效音频。

**六类内容验收未全部通过，按用户计划停止扩量。** 爆炸、飞行、舰内环境、配乐等样例的离线自由转写出现输入描述／标签语句；拟声音节候选也尚未证实具有目标物体或器乐音色。17 个 WAV 格式／哈希检查通过、0 削波，不等于听感验收。本会话不具备音频感知输入，未声称主观试听；转写仅为辅助证据，空转写不算通过。

服务返回累计 313 个 text_words，按官方系数折算 **42.255 AFP**；两次未返回用量的请求保守预留 **495.720 AFP**，预算占用合计 **537.975 / 20,000 AFP**。预留不是确认消费，未读取控制台结算账单。没有重发不确定请求、充值、启用额外付费或更换模型。

交付 [原始试听目录](../ArtSource/Audio/SeedTTS20260929/listening.html)、[AFP 账本](../ArtSource/Audio/SeedTTS20260929/ledger.json)、[来源与提示词清单](../ArtSource/Audio/SeedTTS20260929/source-manifest.json)、[验收报告](verification/SeedTTS-Soundscape-20260929/REPORT.md)。离线工具回归 **24/24** 通过；初次测试编码错误及修复保留在报告中，mock 不算 API 实测。

131 项仍为计划目录，未生成游戏成品、补充正式语音、替换旧 25 个程序声源或导入 Unity。音频增强场景、速度归一化与质量开关解耦、混音接入、Unity 编译／测试、Windows 新构建及 2000 舰验收均未执行。未修改本批范围以外的现有工作；下方 2026-09-19 仍是当前可玩版本。下一阶段是对保留候选实际试听并解决六类代表验收，之后才能进入批量制作；本次每类三次额度已用完，不自动增加第四次调用。

## 当前交付：事件特写、完整英文通讯与立体2000舰阵 — 2026-09-19

打开 **`Assets/_Project/Scenes/FleetAssault_CinematicAudio_Cubic.unity`**，或运行 **`Builds/Windows-CinematicAudio-20260919/DropletGaming.exe`**。Enter播放约6分28秒英语剧情，Tab跳过，R直接战斗/重开，Esc暂停，Q返回正常镜头，F8切换关闭/低频/标准特写。试听入口 [完整通讯试听页](../ArtSource/Audio/CinematicAudio/listening.html)，三个代表场景置顶并提供六条分轨链接。完整说明、改变文件、执行证据、未验项目与手动步骤见 [本批验收报告](CINEMATIC_AUDIO_ACCEPTANCE.md)。

- 已保存新安全副本，复用最新Enhanced场景和已有游戏系统；Cinemachine未安装，扩展ChaseCamera。四类镜头消费既有带稳定ID/代际/身份/方向/位置的真实事件，独立Q返回，不修改水滴操控、时间、碰撞、伤害和延爆；唯一监听器维持正常听点。只提升被拍舰细节，修复远舰激活及销毁后恢复LOD顺序。
- 实际完成 **87条Seed-TTS 2.0英语语音、80段完整通讯、78个额外强度变体、14个世界/链路效果，172个导入WAV**；源干声、分轨、配方、提示词、中文字幕、生成日志和母版保存在Assets外。FFmpeg实际混音，172解码通过/0削波，126字幕条目技术校验通过。声音密度来自近8秒附近交战，战后回落；主通讯1条、世界池最多12，舰爆即停整个舱内声音。
- 20×25×2只有1000；本批保留2000身份，实际采用 **20×25×4**，20×25面朝水滴，范围14250×14400×14400 UU。Blender共享低模实例源已真实保存/重开/重复导出；Unity重复导入2000ID/18000碰撞体一致，1,999,000对实际船壳0穿插，不缩船、不缩放舰队根。
- 实际Unity编译通过；去重 **33/33** 当前范围用例（EditMode6，PlayMode27）；Windows构建0错误。最终完整2000舰玩家 **26/26**、完整英语开场 **5/5** 均通过，两个实际进程退出0。普通S键路线真实触发攻击者/水滴/贯穿/爆炸四类镜头，真实伤害/延爆/整舱通信中断/计分、强度衰退、原点重定位与三次连续重开成立。开场按真实墙钟388.695秒自然播放40条提示并转入战斗。另补实际Editor中段画面复核，未代替玩家音频检查。详见 [运行证据页](verification/CinematicAudio-20260919/index.html) 与 `delivery-audit.json`。首次验证器原点时序失败、LOD警告及其修复记录均保留，不改写成初次通过。
- 已主观试听：**无，全部待人工试听**；英语自然度/情绪、对白遮蔽、逐词字幕一致性和程序脚步/人群真实感尚未验收。Audacity未操作，没有声称.aup工程。当前未接入可用Seed非语音端点，实探返回45002001；非语音由明确来源的原创程序效果完成，不冒充录音或云生成Foley。当前英语TTS没有权限阻塞。

旧16场景及其meta、Editor版本与manifest基线哈希未变，旧成功音频、模型、现有未提交工作和历史构建保留。本批不做FPS优化、旧版本删除、Git提交或推送，不扩展其他玩法。当前默认构建入口通过Editor API改为新场景；Enhanced作为前版完整回退，compact v0.2.2 ZIP保留。本批之后停止；下面都是历史记录，不覆盖本节状态。

## Version retention cleanup - 2026-09-19

Changed default Unity build entry through Editor API to FleetAssault_Enhanced only; current scene untouched. Added current-version/affected-feature test policy and opt-in historical scene checks to AGENTS/TEST_PLAN/START_HERE. Retain current player, previous NarrativeCombat rollback and compact v0.2.2 ZIP. Identified 9 obsolete generated build folders (1.118 GiB), but automatic policy rejected recursive deletion before execution: all remain, 0 bytes reclaimed. No source assets deleted or gameplay checks rerun. See [cleanup report](verification/VersionCleanup-20260919/REPORT.md).


## Natural English Seed-TTS audition - 2026-09-19

User found the first voice stiff. Authored new context_texts direction for conversational connected phrasing, calm urgency, light emphasis, natural pauses, and avoidance of newsreader delivery. Kept Tim speaker and rate 0; rewrote the line into three short spoken phrases. Previewed then executed one request; real PCM WAV completed at 4.778292 seconds, 24 kHz mono, peak -13.22 dBFS, zero clipped samples. Saved under ArtSource/Audio/Generated/Volcengine/seed-tts/agent-plan-natural-20260919/Natural5s-3ccdf5a67bc979e7/ with job.json and qa.json. Prior audition preserved. AI-generated dry voice, human listening pending; no claim that subjective naturalness has passed, no artificial silence/stretching, no Unity import or batch generation.


## Agent Plan Seed-TTS live success - 2026-09-19

After the user configured the dedicated key, refreshed the Windows user credential into this test process without printing it. One real request to the corrected /api/v3/plan/tts/unidirectional endpoint succeeded (HTTP 200, complete stream). Generated English AI voice: ArtSource/Audio/Generated/Volcengine/seed-tts/agent-plan-newkey-20260919/Probe5s-22e973ea3eafb8d8/voice.wav. Actual duration 5.007083 seconds, 24 kHz mono 16-bit PCM, peak -11.17 dBFS, zero clipped samples. Human listening pending. Evidence: verification/SeedTTS-5s-20260919/newkey-result.json and generation job.json. Confirms endpoint plus dedicated-key configuration now works; no video calls, batch voice generation, or Unity imports performed. Earlier authorization blocker is resolved for this tested voice/request, not an assertion about all voices or remaining quota.


## Agent Plan TTS endpoint correction ? 2026-09-19

Read the official agent-plan-personal-voice-model document: Agent Plan DOES support Seed-TTS 2.0 through /api/v3/plan/tts/unidirectional with its dedicated API key. Corrected the existing tool endpoint and journal endpoint field; updated its endpoint assertion and guide. Previewed and executed exactly one 73-character English TTS request (about 5 seconds intended): HTTP 401 / 45000010 / Invalid X-Api-Key. No audio returned. Earlier ordinary-endpoint HTTP 403 is not evidence that the plan excludes TTS. Existing local credential is not accepted at the correct plan endpoint; account-side key origin/revocation remains unverified. Offline tool tests 9/9 passed; this does not count as synthesis success. No video calls, key changes, billing changes, or Unity changes. See [corrected diagnosis](verification/SeedTTS-5s-20260919/PLAN_DIAGNOSIS.md).


## Seed-TTS 5-second diagnostic ? 2026-09-19

User requested a new approximately five-second audio request and failure analysis against the Ark subscription page. Previewed then submitted one 73-character English line through the existing volc_voice.py. Actual response: HTTP 403, provider code 45000030, requested resource not granted (volc.seedtts.default). No WAV returned; no retries, credential changes, purchases, or Unity changes. The subscription URL returned only a public application shell; browser connection was unavailable, so authenticated plan entitlements and key origin remain unverified. Evidence and next checks: [diagnostic report](verification/SeedTTS-5s-20260919/REPORT.md). This is a live failed API check, not successful synthesis.


## 当前增强副本：逃跑、反射、太阳与混音 — 2026-09-19

打开 **`Assets/_Project/Scenes/FleetAssault_Enhanced.unity`**（2000舰），独立版 **`Builds/Windows-Enhanced-20260919/DropletGaming.exe`**；12舰检查副本为 `FleetAssault_Enhanced_Small.unity`。没有重建工程、升级Unity/URP、减少正式舰数或改水滴/舰船/太阳系布局。旧场景、旧构建和开始时未提交的工作保留，尚未提交或推送本批。完整改动、证据和手动复核步骤见 [简短验收报告](ENHANCEMENT_ACCEPTANCE.md)。

**部分交付，英语声库存在真实外部阻塞。** 现有 Seed-TTS 2.0 接口首条英语试音实际返回 HTTP403；空文本鉴权确认 `45000030 / requested resource not granted`。不是已证实的额度耗尽，没有更换服务、充值、暴露密钥或继续盲重试。完成40条/四幕原创新英语开场、36条事件广播和中文字幕，但未生成英语人声，不能验收实际4–6分钟剧情、英语表演和新字幕同步。增强场景明确显示 `AI VOICE: ZH (legacy)`，保留与旧中文音频匹配的60秒Timeline。新增4个真实独立PCM音效与实际5组AudioMixer；试听清单、固定角色和恢复条件见 [音频报告](ENHANCEMENT_AUDIO_REPORT.md)，声音待人工试听。

- D优先：正常战斗基线已能逃跑，实际修复1–3秒反应延迟与同帧位置发布顺序。正常贯穿后完好邻舰离阵加速；权威位置、近景Transform、远景矩阵和查询同步；待爆漂移、相对运动扫掠、独立逃脱计数保留。新增可关闭0/3/10秒诊断和正常触发测试，不用ForceFlee代替验收。
- B：真实表面命中/平滑法线/反射方向保留。入射、接触、反射与局部网格高光可辨；瞬时光路整体冻结0.18秒，追加修复将世界接触亮点/粒子限制到最多2个非暂停渲染帧且25ms以内，避免高速离开后大亮点残留。不改计伤，不逐束加灯。
- C：物理太阳角直径约0.213162°，美术倍率2，显示约0.426324°，光晕单独配置；曝光0、FOV65，主光绑定太阳。保存独立URP17.5 Renderer与真正半分辨率16采样、深度裁切的局部体积日冕，质量Off可关闭；旧Renderer仍为默认索引。
- 验证：实际Unity编译、逃跑EditMode **6/6**、完整PlayMode **91/91**；接触闪光修复后专项PlayMode **4/4**；Python音频离线 **14/14**。Windows构建成功，独立版正常战斗/暂停/延爆/结算/连续三次重开等 **22/22** 实测通过，保留完整2000身份。没有把离线测试算作云合成成功，也没有将旧60秒剧情检查算作新4–6分钟英语剧情通过。
- 证据：`docs/verification/Enhancement-20260919/` 含原始测试、构建报告、实机PNG、采样MP4、0/3/10秒ID位置、逐帧耗时和PID显存CSV。旧场景与原meta **136/136** 哈希不变。未运行会覆盖旧TestRange的作者测试。性能与本机RTX4060 Laptop/1080p原生结果详见验收报告；平均FPS不能证明P95满足12.5ms，未承诺稳定80FPS。

最终二进制对应 `build-accepted.json`，实机最终结果在 `PlayerAccepted/report.json`：**22/22、退出0**。最终5阶段平均81.56–112.41FPS，P95为12.745–17.002ms，未达到P95≤12.5ms目标；体积开/关整帧GPU均值2.848/2.746ms。PID专用显存峰值323.63MiB（共享312.64MiB）。中间一次21/22由验证器没有等待实际威胁后10秒样本导致，已保留失败记录并修复观察窗口、隔离测试输入，未改变舰船行为或降低误差阈值；最终15舰的0/3/10秒位置误差均0。

未验收：新英语试音/全库/真实4–6分钟剧情和其音画同步；新增音效与混音主观试听；人的物理键鼠手感、长期热机、跨机器、全部2000舰自然飞达撤离圈。云端只阻塞音频部分；其余已持续集成与验证。下一步仅在现有账户取得Seed-TTS资源授权后补齐该音频批次，不扩展无关玩法。

## Seed-TTS only - 2026-09-19

User requested Seed-TTS 2.0 exclusively. Removed video endpoints, submission/query/download/collection, video model/duration options, FFmpeg extraction and video credential selection.

- Changed: Tools/Audio/volc_voice.py, Set-VolcVoiceCredential.ps1, test_volc_voice.py, AGENTS.md, VOLCENGINE_VOICE.md, ENVIRONMENT.md, STATUS.md and verification/SeedTTSOnly-20260919.json. Original verification remains historical evidence.
- Executed: Python 3.14.3 unittest 9/9, default-provider CLI preview/help, PowerShell helper syntax parsing. Tests verify the speech endpoint/resource and rejection of removed video options. Mocks are not live API verification.
- Environment: user speech key is readable; no value logged. FFmpeg 9.0.1 passed local synthetic-video extraction during the preceding check; this tool no longer needs it.
- Unrun: cloud authentication/synthesis, listening QA, credential persistence helper, Unity compilation/tests/build. No paid calls or changes to game assets, voices, GUIDs or builds.
- Manual next step: select an enabled TTS 2.0 speaker, preview one line using the guide, then execute/listen when generation is requested. Known limitation: cloud readiness remains unverified.
- Requested cleanup complete. No gameplay milestone advanced; audio import is a separate task.


## 本次仓库提交内容 — 2026-09-13

为`origin/main`整理完整源工程与最新已验证可玩构建：`Releases/Droplet-Gaming-Windows-NarrativeCombat.zip`，56,067,483字节，包含原构建全部199文件，逐文件SHA-256一致。根README提供下载与Unity打开方式。仅打包现有2026-09-12构建，没有改玩法或重新声称运行测试。Unity缓存、旧构建、迁移Git恢复包、重复基线快照、原始Profiler和运行日志保留本机，不上传；可玩归档、源码/源美术、原场景、测试、报告及结构化证据纳入本次提交。远端提交结果以实际Git推送核对为准。

## 工程目录整理 — 2026-09-13

唯一工作工程现在位于 `C:/学习/玩/Unity/Trysolar Drip`。已从 `新建文件夹/DropletPrototype` 原样迁移完整工程，保留原场景、脚本、Blender 源文件、验证证据和历史构建；合并早期模板的 `Assets/美术` 参考图及其原始 `.meta`，保留原 `Trysolar Drip` 的 Git 历史与远程配置，未提交或推送。

旧模板源文件与 Git 快照位于 `docs/ProjectCleanup-20260913/InitialTemplate-and-Git.zip`，不包含可再生成的旧 `Library` 和 `.vs` 缓存。迁移验证结果与原文件 SHA-256 清单位于同一目录。以下历史记录中的旧绝对路径仅表示当时位置；当前启动入口见根目录 `START_HERE.md`。本次未开发新玩法，也未重跑会改写场景的作者测试。

整理验证：5,230 个完整工程原文件在迁移后哈希一致；231 个旧模板文件（含 Git）通过 ZIP 解压流哈希校验；3,216 个核心资源、配置、脚本、源美术和构建文件复核一致。Git 完整性检查通过。Unity 6000.5.10f1 在新路径启动后返回 ready，正式 NarrativeCombat 场景已加载且无未保存改动，捕获 Console 错误为 0。独立 stdio MCP 握手、149 个工具发现、editor_status/get_console_logs 调用通过。Codex 已移除闲置 8080 HTTP 连接，保留指向此目录的 unity 连接；重启 Codex 重建当前会话已关闭的旧传输。

## 当前交付：Narrative Combat 六项集成、正式2000舰 — 2026-09-12

已实施、运行、修复并完成本批验证，停止于本次六项功能交付。2026-09-12实施时完整工程位于 `C:/学习/玩/Unity/新建文件夹/DropletPrototype`，当时未改初始模板；当前唯一工程已迁入 `C:/学习/玩/Unity/Trysolar Drip`，见顶部整理记录和`START_HERE.md`。正式场景：`Assets/_Project/Scenes/FleetAssault_NarrativeCombat.unity`，独立版：`Builds/Windows-NarrativeCombat/DropletPrototype.exe`。按Play/聚焦Game后 Enter 开始约60秒剧情，Tab跳过，Esc暂停/继续，N重播，R直接战斗/重开；也有BEGIN APPROACH、PAUSE/SKIP与DIRECT COMBAT按钮。鼠标转向、W/S调速、A/D横移、Shift冲刺、Space刹车；H广播历史、-/+广播音量。

- 六项接入既有MissionController/Motor/ShipTarget/FleetRenderManager：唯一100m/UU物理换算、30/150km/s巡航/冲刺、50/75/100km分轴舰距；独立行为/损伤状态与相对运动扫掠；2–5模拟秒种子延爆；2–8km/s自主撤离及独立逃脱计数；池化即时激光与真实水滴7040三角平滑法线一次反射；中文事件广播及Timeline剧情。保留现有水滴/圆头方向/材质/太阳/舰船资产与管线版本。
- 明确游戏尺度：现有舰长57.564UU对应5.7564km，水滴2.4UU对应240m，不冒充原著数米尺度。舰体原4盒+5胶囊未更换，胶囊采用保守局部包围盒检测，相关误报边界在报告说明。撤离半径2600km小于玩家3200km边界，逃脱降评级且不计击毁，待爆清零后才结算。
- 资源完整：36条原创战斗＋12条剧情，48个人声WAV由本机Huihui离线导出，另2提示音；ScriptableObject台词库、限流/去重/历史/爆炸打断，保存Timeline与中文字体。没有原AudioMixer资产，本轮实际2个AudioSource加现有主音量，不声称Mixer总线。实际窗口检查修复了字幕裁切和菜单遮挡。
- 小规模在先：12舰集成、真实渲染及修正后的输入9项检查通过；正式场景始终2000个稳定ID，不以Small替代交付。最终EditMode **15/15＋仿射6/6**，完整相关PlayMode **85/85**；含保存舰体150km/s移动/转向及LOD关闭2项、保存网格反射命中延爆1项。最终Windows真实四阶段 **43/43**、完整60秒剧情、实际运动、2000集中延爆一次结算、2000同时撤退、连续3次重开并观察旧回调均通过，进程退出0。
- 本机Ryzen9 7940HX＋RTX4060 Laptop、1920×1080原生/D3D12/High/无帧生成：最终全景平均113.38FPS（模拟暂停），近景激光270.78FPS，集中延爆302.28FPS，全2000撤退94.50FPS；撤退P95 14.053ms，不能称锁80。修复前撤退约66FPS，仿射矩阵与批处理优化未删舰、减面或更换材质。PID精确WDDM独立显存峰值427.32MiB；完整构建204.49MiB，不凑25–30GB硬盘体积。
- 保护与限制：原资源/场景/包/ProjectSettings **421/421哈希一致**，原核心脚本及旧STATUS有任务前副本。未运行会覆盖旧TestRange的历史作者测试，未动用户其他项目/改动。原生菜单点击与InputSystem合成键鼠已验证；工具OS按键注入只提供TEXT、无按下状态，物理人类键鼠手感未验收；未跑完整90分钟热机、全2000自然飞抵撤离圈或跨硬件验证。无已知功能阻断，不把这些未测项写成通过。

完整实现、配置、原始测试/性能/显存证据及限制见 [NARRATIVE_COMBAT_REPORT.md](NARRATIVE_COMBAT_REPORT.md) 和 `verification/NarrativeCombat/delivery-audit.json`。2026-09-12交付时编辑器停在正式新场景，自动验证进程已退出。2026-09-13继续收尾仅在当前路径复核已有测试与421/421保护哈希、更新路径/环境文档；本次CLI未发现Pipeline实例，未将即时Editor状态或测试重跑算作通过。本批完成后不扩展新玩法。

以下保留历史记录，其中旧“当前任务”不覆盖本节交付状态。

## 当前任务：视觉质量、反应堆爆炸与水滴火光反射升级 — 2026-09-08

**已完成实际实现、画面修复、Unity 测试和 Windows 独立运行。** 打开 `Assets/_Project/Scenes/FleetAssault_VisualUpgrade.unity`，Play 后聚焦 Game 并按 Enter；鼠标转向，W/S 调速，A/D 横移，Shift 加速，Space 刹车，Esc 暂停，R 重开。独立版为 `Builds/Windows-VisualUpgrade-20260908/DropletPrototype.exe`。从最新 `FleetAssault_Droplet_Rebuilt` 复制，保留旧稳定场景/Prefab/素材/构建；沿用当前 120 舰、540 秒与原操控、贯穿、计分和任务逻辑。

- **舰船尺度**：FusionFrigate 视觉/9 个命中体/挂点分别同步放大 2.6 倍，根与布局位置不变。舰长 22.140 → 57.564 m，舰长/2.4 m 水滴长为 9.225 → 23.985；新舰宽/高为 19.188/16.150 m，最小舰体包围盒间隙约 53.136 m。保留并复核三档 LOD、主引擎和四个辅助引擎。
- **材质与光效**：新增独立 `VisualUpgrade` Shader/材质/环境配置。水滴为银灰高反射金属，采用 URP BRDF + HDR 环境 + 单个 128 局部探针，太阳方向统一主光；蓝白反应堆核心、太阳颗粒/边缘亮度层次、节制 Bloom、舰体金属分区、粗糙岩石及海陆云层材质接线完成。保持 URP 17.5.0 和现有太阳系映射，未做管线迁移。
- **爆炸与反射**：真实命中点贯穿闪光 → 0.16 s 反应堆失稳 → 固定尾部爆心的分层火球/冲击环/碎片/强光 → 2.7 s 内回收。摧毁和分数立即由原玩法提交。池上限 High 12 / Low 6 / Off 0、最多 2 盏共享无阴影点光、0 ParticleSystem，预热 135 个 Transform。最多 4 个爆心驱动唯一水滴的世界方向反射与局部掠射暖光带；这是为第三人称可读性设计的可信近似，非 SSR/光追。实际冻结画面 OFF/ON 对照确认暖色反光；暂停、过期、关闭效果及重开均清空或冻结正确。
- **实际检查**：Editor 编译通过；新资产 EditMode **3/3**、完整 PlayMode **55/55**；最终反射专项复测 **2/2**。最终 Editor **38/38** 检查、20 张实际截图；合成 Input System 键鼠走原 FixedUpdate 连续飞行约 **509.72 m**、贯穿 **5 舰/1,500 分**，转向、刹车、暂停、重开、相机与头尾方向通过。没有运行会重写 TestRange 的旧作者测试。
- **独立版**：Windows x64/Mono 非 Development 构建成功，37.880 s、0 错误、1 条可选 Pipeline 工具运行桥未配置警告；可见 1080p/D3D12 Player 修改前 **31/31**、修改后 **39/39**，均退出码 0。完整 120 舰胜利、结算一次、三次重开、关闭特效计分与同帧 120 次命中池预算通过。
- **性能与容量**：本机 RTX 4060 Laptop，四视点全部聚焦，VSync=0/不限帧、每阶段至少 480 帧且 6 秒。近景/中景/集中爆炸/面向太阳平均 FPS：**329.70/250.49/346.12/300.97 → 219.46/189.75/259.66/241.16**；修改后 P95 为 **6.27/6.59/5.15/5.28 ms**，短程平均达到约 80 FPS 目标。集中诊断有 **22.27 ms P99、57.93 ms 最大帧时**，不能称持续锁定 80；该阶段含同一渲染帧多次 Motor.Step 的控制实验。GPU/GC/DrawCall 标记无样本，未伪报 0；详细表与限制见报告。独立发行约 **138.67 MB**，新增源资源约 **9.91 MB**，旧版本和缓存未计入发行容量。
- **保全与交付**：任务前哈希清单 729 项中仅 STATUS/ENVIRONMENT 追加本轮文档，其余 727 项保持，既有 `.meta`、源模型、Packages、ProjectSettings 及 TestRange 不变。新场景、3 个 Prefab、共享材质/配置、Shader、池与反射组件、验证脚本和逐帧证据已保存，详见 [VISUAL_UPGRADE_EXPLOSION_REFLECTION_REPORT.md](VISUAL_UPGRADE_EXPLOSION_REFLECTION_REPORT.md)。未完成人工物理键鼠主观手感、长时间温度/功耗稳定性和 GPU 分项隔离；太阳保留现有小角尺寸，近看诊断大日面不是游戏比例。无阻塞交付的问题，本轮结束，不自动进入新玩法里程碑。

以下保留历史状态，其“当前任务”不覆盖本轮交付。

## 当前任务：Droplet_Rebuilt 重建与朝向修复 — 2026-09-08

**新模型已实际生成、打开、导出、接入并完成游戏验证。** 圆头 +Z、尖尾 −Z，覆盖旧“尖端 +Z”要求；下方 PerfectDroplet 历史记录中的“方向正确”不再作为验收依据。

- 主源 `ArtSource/Blender/Droplet/Droplet_Rebuilt/Droplet_Rebuilt.blend` 已在可见 Blender 窗口打开；2.4 × 0.8 m、3:1、7,040 三角形、单壳闭合。灰模、镜面多角度/动画、尾端放大、FBX 回导和 Unity 校准已完成。
- 试玩 `Assets/_Project/Scenes/FleetAssault_Droplet_Rebuilt.unity`；检视 `Droplet_Rebuilt_Review.unity`；视觉 Prefab `Prefabs/Player/Droplet_Rebuilt.prefab`。从稳定 Lighting 场景复制，只换视觉 MeshFilter 并加非渲染标记；PlayerRoot、运动/碰撞/相机、120 舰、540 秒、正式材质/光照保留。
- 实际编译通过；专项 EditMode **3/3**、专项 PlayMode **3/3**、完整 PlayMode **47/47**。真实合成键鼠驱动原 FixedUpdate 飞行 **534.678 m / 4.920 s**，5 舰/1,500 分，世界首尾与位移点积最小 **1.000000**；四向输入、相机、暂停/刹车/重开通过。
- 新 Windows x64/Mono 非 Development 构建成功，0 错误、1 条可选 Pipeline 运行桥未配置警告；可见 1080p/D3D12 Player **32/32** 检查通过，19 张真实截图。隐藏窗口运行的反射/截图失败记录保留，最终以可见复测为准。
- 旧源、旧场景、旧 Prefab、已有 `.meta`、包/管线和旧构建保留。完整路径、证据、回退方式和限制见 [DROPLET_REBUILD_REPORT.md](DROPLET_REBUILD_REPORT.md)。未验证物理键鼠主观手感、长期自由飞行和其他硬件；毫米级极端放大可见网格离散。本轮完成后停止。

以下为历史记录，不覆盖本轮首尾约定与修复状态。

## 当前任务：三体水滴 Unity 导入与测试 — 2026-09-08

**已导入、保存新测试场景并通过实际验证。** 打开 `Assets/_Project/Scenes/FleetAssault_PerfectDroplet_Test.unity`，Play 后聚焦 Game 并按 Enter。编辑器交付时已停在此干净场景。完整变更、证据、检查图与复查步骤见 [水滴 Unity 导入报告](DROPLET_UNITY_IMPORT_REPORT.md)。

- 导入 `Art/Models/PerfectDroplet/PerfectDroplet_Game.fbx`，保存 `Prefabs/Player/PerfectDropletVisual.prefab`；测试场景从既有 Lighting 场景复制，仅替换水滴 MeshFilter 网格，保留原 Renderer 及全部玩法引用。120 舰、540 秒、1,080 个 Collider、原材质/灯光/相机配置保留。
- Unity 实测 2.4 × 0.774194 m、3.1:1、5,632 三角形、2,983 个含 UV 拆分的顶点；+Z 前、+Y 上、中心原点及单位变换正确。导入解析法线，坏法线/反向或零面积三角形为 0，同位置拆分顶点法线角差最大 0°；重导入 GUID/网格 ID/Prefab 引用保持。
- 实际编译通过，专项 EditMode **3/3**、PlayMode **2/2**；全 120 舰扫掠胜利与 3 次重开通过。临时合成键鼠驱动真实 FixedUpdate 连续飞行 **528.438 m / 4.860 s**，贯穿 5 舰、1,500 分，暂停/刹车/转向/重开通过，效果池 **57 → 57**。9 张 Unity 检查图实际保存并查看。
- 保全：**758/758** 个任务前 Assets/Packages/ProjectSettings/ArtSource/Tools 文件哈希一致，旧场景、Prefab、材质、设置、Blender 源和导出保留。未运行旧 SceneAuthoringTests；动态 eval 和首个测试请求的工具故障已通过编译菜单及实际结果查询绕过。
- 限制：保留原 **0.7 m 命中半径**，大于新视觉半径约 0.3871 m；未调整命中手感。未生成本轮独立构建，旧 EXE 不包含新模型；没有 Player 性能、长期飞行或物理键鼠主观验收。仅复用已有材质/灯光，没有开展新特效或材质升级。
- 下一步：用户在新场景试玩确认；本轮到此停止。

以下保留历史记录，其中“当前任务”与“下一步”不覆盖本轮导入测试状态。

## 当前任务：三体水滴 Blender 几何 — 2026-09-08

**已完成模型构造、实际保存重开、FBX 导出/回导和检查图，停在几何交付。** 源文件为 `ArtSource/Blender/Droplet/PerfectDroplet/PerfectDroplet.blend`，游戏导出为 `ArtSource/Exports/Droplet/PerfectDroplet/PerfectDroplet_Game.fbx`；四视图和额外法线/布线检查位于源目录 `Previews/`。详见 [水滴几何报告](DROPLET_GEOMETRY_REPORT.md)。

完整交付 ZIP：`ArtSource/Deliveries/PerfectDroplet-20260908.zip`，包含 36 个交付文件及 SHA-256 清单，ZIP CRC 与逐文件内容校验通过。

- **形体与预算**：2.4 m 长、0.774194 m 最大直径，3.1:1；源 +Z 尖端 / +Y 上，包围盒中心原点，单位缩放。游戏版 5,632 三角形，源网格全部 2,816 四边面；隐藏参考版 22,528 三角形，无 LOD。
- **几何处理**：一条连续凸五次曲线旋转成形，圆端局部曲率半径 414.27 mm、尖端 1.624 mm；两端焊接四边面封口，仅 8 个三价顶点，其余四价。解析平滑法线跨环线/UV 接缝连续。修复初版鼻部细长面，最终最长/最短边比最大 6.80。
- **实际检查**：Blender 5.2.1 LTS 保存重开、两版闭合/拓扑/外向法线、20,001 曲率采样及连续区间凸性验证通过；15 张实际 1600×1200 检查图。FBX 回导 1 网格/5,632 三角形，最大顶点误差 9.13×10⁻⁸ m，最大法线角差约 0.042°。
- **保全与边界**：697 个既有 Assets/Packages/ProjectSettings/ArtSource/Tools 文件哈希一致；未调用 Unity、未替换旧水滴、未改场景/飞船/爆炸。Unity 导入/材质实测、编译、EditMode/PlayMode 和构建本轮未运行。毫米级端部在极端放大时存在有限网格离散精度边界；无几何工具阻塞。
- **下一步**：仅由用户查看本模型；不自动进入太阳照明、材质升级或特效开发。

以下保留历史记录；此前“当前任务”不覆盖本轮几何交付状态。

## 当前任务：天体尺度、太阳照明、聚变引擎与材质反射 — 2026-09-08

**已完成实际实施、修复、场景保存、测试及 Windows 独立运行。打开 `Assets/_Project/Scenes/FleetAssault_Lighting.unity`，Play 后按 Enter 开始。** 独立版为 `Builds/Windows-Lighting-20260908/DropletPrototype.exe`；保留 120 舰、540 秒和原飞行/碰撞/计分规则。完整配置、前后截图、性能及限制见 [LIGHTING_MATERIALS_SCALE_REPORT.md](LIGHTING_MATERIALS_SCALE_REPORT.md)。

- **尺度与太阳光**：基于最新 `FleetAssault_Expanded` 创建新场景，未覆盖稳定场景。确认基线已采用 2.5 AU 战区与物理角大小；新太阳显示角约 0.426324°（物理 0.213162° × 艺术倍率 2），地球仍为 0.00141189° × 1。舰船、水滴、战区及相机尺度保留。一个与太阳方向一致的 Directional Light 取代新场景中三盏冲突强光，辅以弱环境光、自发光太阳及克制 Bloom。
- **引擎与材质**：新增 `FusionFrigate_Lighting` 与 `SpaceEnvironment_Lighting` Prefab、共享质量配置、14 个共享材质、5 个 Shader 和 HDR 环境立方体。1 主 + 4 辅引擎使用几何内核、能量壳与发光内衬，共享 3 个材质，无逐舰实时灯/粒子图；全部进入既有 LOD 与 VisualRoot 摧毁/重开逻辑。水滴镜面金属、分区舰船金属、粗糙岩石、海陆云区分和自发光太阳已接入。最终修复了程序纹理周期条纹及岩石法线坐标混用。
- **反射方案**：保留 URP 17.5.0，采用定向 HDR 环境 + 单个 128 分辨率局部实时探针 + PBR 高光；没有硬件光追或 SSR。探针分面捕获、至少 3 秒间隔、220 m 捕获范围；毁船/重开立即撤回旧捕获影响。修复了项目全局实时探针开关关闭和原生旧纹理后备问题，场景退出后恢复质量开关。现有 High/Low/Off 控制效果成本。
- **实际验证**：最终 Unity 编译、专项 EditMode **5/5**、完整 PlayMode **42/42**；三视点比例/方向、五引擎各 LOD/质量、真实扫掠、120 舰胜利和三次重开通过。实际 Input System 合成键鼠驱动连续贯穿前 5 舰、1500 分、约 532.04 m/4.90 s，暂停/刹车/转向/重开通过。未运行会重写 TestRange 的旧作者测试。
- **性能与独立版**：Windows 非 Development 构建成功，0 错误、1 个可选 Pipeline 桥配置警告；可见 1080p/D3D12/PC High 独立运行 **32 条检查通过、19 张截图**。Editor 近景均值 6.180 → 6.178 ms，全景 6.190 → 7.826 ms；单次样本不代表长期表现，部分其它阶段有前后台差异，详见报告。Player 近景/全景均值 **3.302/3.159 ms**；2400 帧探针刷新/关闭对照为 **2.888/2.871 ms**，前者包含 3 次捕获，两段全部前台。Player tracked memory 约 213–217 MiB，非可归因显存；没有同 120 舰旧版 Player 对照或 GPU 独立耗时结论。
- **保全与停止**：609 个任务前文件最终仅 2 个 asmdef 增加既有 URP/Core 引用，以及 STATUS/ENVIRONMENT 追加记录；其余 605 个一致。旧场景、Prefab、材质、模型源、已有 .meta、Packages、ProjectSettings 和旧构建保留。尚未做物理键鼠主观验收、长时间自由飞行及跨硬件性能；无阻碍交付的工具问题。本轮到此停止，不进入新玩法开发。

以下保留历史记录；其中的“当前任务”和“下一步”不覆盖本轮交付状态。

## 当前任务：FusionFrigate 导入与舰队扩编 — 2026-09-08

**已实际接入、扩编、保存并完成本轮自动验证。打开 `Assets/_Project/Scenes/FleetAssault_Expanded.unity`，按 Play，再按 Enter 开始试玩。** 独立540秒任务，120艘可击毁舰船；鼠标转向，W/S调速、Shift冲刺、Space刹车、Esc暂停、R重开。完整变更、证据、截图、复现和人工验收见 [FLEET_IMPORT_EXPANSION_REPORT.md](FLEET_IMPORT_EXPANSION_REPORT.md)。

- **实际交付**：复用已完成FBX和四共享URP材质，保存 `Prefabs/Fleet/FusionFrigate.prefab`、独立 `Data/FleetMission_Expanded.asset`、扩大版场景及Blender布局副本。Prefab根单位缩放，三档LOD、9个简单命中体积、5个校正方向挂点接入既有ShipTarget/摧毁/计分。旧舰型残骸在新场景停用，保留通用反馈池与并发上限。
- **扩编与空间**：实际40→120舰，6群×20；最近邻中位数51.778→110.888米（2.142倍，按22.14米统一舰长为2.339→5.008 L）。中心范围335×135×420→1424.351×437.692×1455.825米，舰群最小净空216.988米；首舰距出生点100米。活动/警告半径保持5840/4880米，不改太阳系宏观布局或52/156米每秒速度。540秒由代表航线与转向瞄准余量估算，非人工完整航线实测。
- **模型与资源实测**：长22.14、宽7.38、高6.212米，LOD三角形17,224/5,400/1,396。120实例共享19 Mesh、4 Material、0舰船贴图；每舰36 Renderer/60 Transform，只有一个LODGroup，无逐舰资源复制。关闭舰模Read/Write；所有LOD和主要引擎具备独立于显示的命中覆盖。
- **已执行**：实际Unity编译；安全EditMode 13/13、完整PlayMode 38/38；单舰贯穿/计分/全部LOD隐藏恢复、4096/4097查询容量边界、全120舰真实摧毁事件胜利与3次重开、暂停/超时；相同配置2次生成/保存重开一致。真实Input System合成键鼠连续贯穿开局5舰，暂停、刹车、转向和重开通过。未运行会重建TestRange的旧作者测试。
- **Editor性能**：1920×1080同视点各300帧；扩编后近舰/全景/贯穿P95为6.370/6.358/12.266 ms。全景原生选择120个LOD2，Frame Debugger实际确认SRP Batcher路径。Draw Call显著增多，全Editor计时/GC/内存含编辑器成本，不能据此声称Player或GPU达标。
- **保全**：809个基线文件中仅STATUS/ENVIRONMENT追加记录，另807个哈希一致；旧稳定场景、既有.meta、原模型/布局/太阳系源和本轮开始的TestRange未改。既有玩法代码、管线与输入方案未改；历史TestRange更早版本问题未恢复，记录继续保留。
- **未测与停止范围**：本轮未新增构建/运行独立版；Player性能、GPU耗时/显存归因、跨硬件长期运行和用户物理键鼠/美术手感尚未验证。没有阻碍场景试玩的工具问题。停在本轮导入与扩编交付，不进入尾焰、光效、AI或移动舰船开发。

以下保留此前任务原文；历史“当前任务”与“下一步”不覆盖本轮交付状态。

## 当前任务：FusionFrigate 星舰模型 — 2026-09-08

**Blender 建模、基础材质、LOD、保存重开和 FBX 回导检查已完成；停在模型交付。** 打开 `ArtSource/Blender/Ships/FusionFrigate/FusionFrigate.blend`，默认只显示 LOD0；预导出 `ArtSource/Exports/Ships/FusionFrigate/FusionFrigate.fbx`。真实四视图位于源目录的 `Previews/FusionFrigate_Inspection.png`。完整变更、证据、手动查看与复现步骤见 [FUSION_FRIGATE_MODEL_REPORT.md](FUSION_FRIGATE_MODEL_REPORT.md)。

- 实际查看用户的 `ArtSource/Spaceship`（无扩展名 PNG），保留原件并整理参考副本。22.14 米长楔形完整舰体、低舰桥、三座炮塔、1 主 + 4 辅助深喷口、磁环、支架、管线、隔热板和两组散热板均已实际建模。
- 最终整舰三角形 **LOD0 17,224 / LOD1 5,400 / LOD2 1,396**，含修改器和重复摆放。源三档 35 唯一 Mesh，整理 FBX 19 唯一 Mesh；4 共享材质、0 贴图。三档原点/轴向/整体尺寸相同。
- 实际源重开、预算/拓扑/材质检查、单档多视图、FBX 导出/独立回导/挂点轴向、270 内向+270 外向喷口采样、实例共享与重复生成检查通过。已修复初版面数超限、24 个退化三角形、埋入外壳的隔热板及两层 Empty 的 FBX 轴向问题；最终源/回导退化与拓扑异常为零。
- 本轮未调用或写入 Unity，旧舰船源、太阳系布局、玩法和旧构建不由本轮改动。Unity 实际材质、LODGroup 切换、碰撞适配、测试/构建、性能、内存及显存未验证，不能用 Blender 检查替代。无建模工具阻塞；下一步仅由用户查看本模型，不自动推进关卡替换或光效。

以下保留此前任务记录；其中“当前任务”与“下一步”均为当时状态。

## 当前任务：太阳系尺度重布局 — 2026-09-08

本轮已实际实施并完成可执行检查，停在太阳系布局交付。**试玩打开 `Assets/_Project/Scenes/FleetAssault_SolarLayout.unity`，Play 后按 Enter**；可编辑源为 `ArtSource/Blender/SpaceEnvironment/SpaceEnvironment_SolarLayout.blend`，独立检视为 `Assets/_Project/Scenes/SolarSystemLayout_Review.unity`。完整交付、A–E说明、证据及复现步骤见 [SOLAR_SYSTEM_LAYOUT_REPORT.md](SOLAR_SYSTEM_LAYOUT_REPORT.md)；旧构图要求的替代范围见 [TASK_SOLAR_SYSTEM_LAYOUT.md](TASK_SOLAR_SYSTEM_LAYOUT.md)。

- **实际实现**：单一权威AU配置、double相对位置、物理角大小远景代理、以太阳为中心的2.1–3.3 AU主带示意，以及固定相位的2.5 AU战区。Blender宏观与局部场景、32个稳定ID的FBX姿态导出、共享原型引用、Unity Prefab/检视/可玩副本均已保存。没有天文距离直接写入Transform；不是无缝太阳系旅行。
- **范围与玩法**：半径730 → 5840米、警告610 → 4880米。40舰位置/碰撞体/模型、240秒、52/156 m/s巡航/冲刺及战斗镜头参数保留。真实Input System/FixedUpdate连续飞行越过旧边界，最远采样5837.799米（7.996985倍），约40.66秒安全回收；暂停、刹车、转向、重开、计分及441池对象稳定。
- **密度与资源**：对比旧独立几何环境，岩石172 → 32，移除8个远景组合分区；29唯一网格、5材质、2张原贴图。Editor唯一Mesh+Texture原生大小API合计8.842 → 6.688 MiB，包含所有LOD；不等于可归因显存。太阳/地球物理角直径约0.213162°/0.00141189°，1080p、65°居中解析直径约3.15/0.021像素，无可读性放大。
- **执行证据**：Blender生成/保存/重开/数值与多视角实查；Unity实际编译、两轮重复导入/外部内容保留/场景重开/全部舰队一致性；EditMode数学7/7、完整PlayMode30/30；原生LOD0/1/2/剔除、200米平移视差及实际Game画面检查。没有运行会重建TestRange的旧EditMode测试。继承故障隔离测试故意记录的异常不是未修复运行故障。
- **独立版**：`Builds/Windows-SolarLayout-Validation-20260908/DropletPrototype.exe` 构建成功，0错误/1个可选Pipeline开发桥配置警告；可见1080p运行的20条记录通过（含7条截图写入），40舰胜利、3次重开。首次隐藏窗口黑帧的渲染/性能结果已否决，改用可见窗口重测证据。未发布、未覆盖旧构建。
- **保全与变更**：先备份Scenes/Data并哈希490个既有文件。旧场景（含本轮开始状态的TestRange）、旧.blend、旧模型/材质/贴图及已有.meta不变。既有代码仅HudPresenter增加可配置位置文字、Editor asmdef引用已安装URP；PC_RPAsset被Unity原生处理写回，默认Renderer和版本不变，但无任务前字节副本，不能声称该文件字节保全或给出可靠字段差异。历史TestRange更早版本的问题未恢复，不能用本轮检查掩盖。
- **未验证/已知限制**：用户物理键鼠的主观操控/审美/听感、长期自由飞行、跨硬件、GPU逐事件与耗时、环境可归因Player内存/显存未测；Blender手改回传接口未做用户手改文件端到端验收。装饰岩石继续无碰撞。没有必要工具阻塞。下一步仅由用户集中查看和试玩，不自动进入光效制作。

以下保留此前环境几何与G00–G09原记录；其中“当前任务”或“下一步”是当时状态。

## 当前任务：空间环境几何 — 2026-09-08

已实际完成参考图驱动的 Blender 模型、完整布局、保存重开检查、FBX 原型/布局分离导出及 Unity 导入。打开 `ArtSource/Blender/SpaceEnvironment/SpaceEnvironment.blend` 和 `Assets/_Project/Scenes/SpaceEnvironment_Review.unity`；复用资产为 `Assets/_Project/Prefabs/Environment/SpaceEnvironment.prefab`。详见 [本轮交付与检查报告](SPACE_ENVIRONMENT_GEOMETRY_REPORT.md)。

- 八型岩石三档 LOD、太阳、地球、八个远景分区及天空球：37 个唯一网格，包含全部 LOD 共 38,656 三角形；183 个布局实例；5 个共享材质；两张 2048×1024 DXT1、12 级 mip 贴图。Editor 实际唯一 Mesh/Texture 原生大小 API 合计 8.842 MiB；显存可归因驻留和独立 Player 内存未测。
- Blender 重开、拓扑/法线/预算/引用、多视角及线框实查通过；Unity 实际引用共享、原生 LOD0/1/2/剔除、两轮重复导入及重开通过。独立检视使用无 Renderer Features 的中性 Renderer，默认正式 Renderer 未变；没有新增正式光效或玩法。
- 实际 Frame Debugger 参考视点 53,488 个环境三角形/105 次绘制；侧面 66,368、俯视 45,248、可玩区转向 32,376、岩带近看 34,512 个环境三角形。五个视点有效事件和预算检查均通过。详见报告及 `draw-summary.json`，没有把唯一网格数或解析估算当成实际提交量。
- **未解决的保全失败：**运行旧 EditMode 回归测试时，`SceneAuthoringTests` 调用 `TestRangeBuilder` 并保存了 `TestRange.unity`，意外重写该场景。原始哈希及查找恢复副本的结果已记录；尚无原文件备份，未恢复。FleetAssault、玩法代码、旧构建及原 `.meta` 哈希保持一致，不能据此声称 TestRange 也保全通过。禁止再次运行这项重建测试来验证环境。
- 停止在本轮几何环境交付；不启动光效、游戏开发或新发布包。主观美术验收、跨硬件性能与可归因显存仍未验证。

以下为原 G05–G09 及更早批次记录，保留原文。历史“TestRange 保留”结论不代表本轮保全状态。

Last updated: G05–G09 delivery, 7 September 2026.

打开 `Assets/_Project/Scenes/FleetAssault.unity`，按Play后按Enter或点击BEGIN SORTIE；独立版运行 `Builds/Windows-v0.2.2-G09/DropletPrototype.exe`，完整目录/压缩包一起交付。240秒贯穿40艘舰船，支持计分、暂停、结算、设置和重开。操作见 [PLAYTEST_FLEET.md](PLAYTEST_FLEET.md)，资产及全部改动见 [DELIVERY_G05_G09.md](DELIVERY_G05_G09.md)。G00–G04和TestRange保留；历史记录在下方，不代表当前阶段仍停止于G04。

本批G05–G09实现及自动化门槛通过；审美、操作舒适度和听感仍待用户最终试玩验收。没有未解决的模型生成、编译或Windows构建环境阻塞。详见 [批次记录](BATCH_G05_G09.md)、[性能记录](PERFORMANCE_G09.md) 和 [独立审查](verification/G05-G09/review.md)。停止于G09，不自动推进后续阶段。

## Milestones

| Stage | Name | State | Evidence |
|---|---|---|---|
| G00 | Actual project baseline | Complete — BASE-01 passed | Unity recompile completed, failed=false, errors=[]; [raw evidence](verification/G00-unity-baseline.json); user Console confirmation steps below |
| G01 | Saved graybox scene | Passed (batch) | Actual Unity compile; SceneAuthoringTests 2/2 passed; saved TestRange and prefabs |
| G02 | Flight and camera | Passed (batch, subjective feel pending) | Actual Unity compile; FlightTests 3/3 PlayMode passed |
| G03 | Collision correctness | Passed (batch) | Actual Unity compile; CollisionTests 7/7 PlayMode passed |
| G04 | Complete graybox loop and first build | Implemented; automated gates passed; manual acceptance pending | EditMode 3/3; PlayMode 18/18; live input smoke; Windows build succeeded; standalone victory/reset observed |
| G05 | Blender asset pipeline | Implemented and Unity asset checks passed | Actual source/FBX; calibration-unity.txt; imported dimensions/normals and collision tests |
| G06 | Destruction effects | Implemented and automated/visual gates passed; listening acceptance pending | Real six-piece wrecks/WAV, bounded pools, Off/exhaustion/reset tests and actual player images |
| G07 | Authored fleet import | Implemented and verified | 40 targets; source-to-Unity poses; repeated identity/manual preservation/save-reopen tests |
| G08 | Presentation and accessibility | Implemented and visually checked; subjective acceptance pending | Actual Game/start/flight/results/settings observed; metallic reflection, Earth/starfield, player options |
| G09 | Profiling, regression, and release | Build and automated gates passed; manual scope below | Final EditMode15/15, PlayMode27/27; real rendered standalone benchmark; versioned Windows delivery |

## G05–G09 最终验收范围

- **已实现且已验证**：Blender5.2.1实际生成、校准后FBX导入；两种不同舰型/指挥变体/水滴/六块预制残骸；40稳定ID真实Prefab，保存重开、重复导入、保留手工内容；既有扫掠/去重/暂停/边界/结算规则；表现关掉、耗尽或回调报错不影响玩法；三次重开及441对象池稳定；原创WAV接入、声音并发和清理；实际Game画面与设置界面。
- **Unity验证**：本批开始先重跑旧EditMode3/3、PlayMode18/18。最终新增覆盖后全套 [EditMode15/15](verification/G05-G09/release-editor.json)、[PlayMode27/27](verification/G05-G09/release-playmode.json)，0失败/跳过。实际Editor编译、测试及原生Windows构建分别执行；未使用dotnet结果代替。
- **Windows构建**：v0.2.2成功，0错误、1可选Pipeline开发桥禁用警告，16.237秒；[实际构建报告](verification/G05-G09/windows-v0.2.2-build.json)。1920×1080窗口、PC质量、High表现，保留旧Windows和中间候选版本。
- **独立玩家实际运行**：最终v0.2.2 [20条记录全部通过](verification/G05-G09/PlayerBenchmark/20260907-123115-512/benchmark.json)（含7条截图写入），40舰胜利、暂停、单段3舰、3重开和41次无伤重定位；正常/密集前台1080p采样P95为2.126/2.192ms，441池对象稳定。完整[Windows ZIP](../Builds/DropletPrototype-v0.2.2-Windows.zip)已产出；198个文件SHA256及CRC通过，详见[校验报告](verification/G05-G09/package-verification.json)。
- **已实现但未完成主观验收**：物理键盘/鼠标最终体验、音量听感、240秒路线难度。原生注入Enter/Esc未稳定转换状态，工具限制与输入故障尚不能据此区分；需用真实硬件按说明集中试玩。菜单鼠标点击、FOV改变及恢复默认实际成功。
- **未测量/未执行**：独立GC逐帧分配（计数器不可用）、GPU时间、长时间浸泡、其他机器和分辨率/刷新率矩阵、整局自然等待240秒超时。超时及最后一击同一步优先胜利已由PlayMode验证。
- **已知画面局限**：近镜头尾迹存在折线外观，残骸短暂经过视野；不会挡住运动或重复计分。当前UI英语，程序化地球为风格化示意。目标文字低对比问题已在v0.2.2加深色底修复。

手工验收：启动最终exe → Enter/点击开始 → 鼠标、W/S、A/D、Shift与Space练习 → Esc暂停并调设置 → R重开 → 完成或等待超时，再重开。不要把自动路线/截图当作主观操控与听感通过的证据。

最终还实际点击BEGIN，观察正常运动4/40及1000分；切换窗口触发暂停，点击RESTART恢复Ready/240秒/40舰/0分。见 [原生交互记录](verification/G05-G09/v0.2.2-native-interaction.md)。玩家留在Ready；Editor停止Play、FleetAssault已保存且无脏场景。

## Historical G01–G04 batch — 2026-09-07

User authorized automatic progression through G01–G04, with internal validation/repair and no per-stage approval. G00 is retained; stop after G04. Mission duration for this batch is 120 seconds.

G01 completed: TestRangeBuilder authors only GeneratedTestRange, creates/reuses five URP materials and two prefabs, refuses dirty scenes, preserves SampleScene and hand-authored roots. SceneAuthoringTests passed 2/2 in Unity 6000.5.10f1. Save/reopen, repeated generation, unit roots, separate VisualRoot/HitVolumes and preservation verified. No gameplay tests apply yet; subjective scene review remains for final delivery. Next internal stage: G02.

G02 completed: DropletInput uses installed Input System only, DropletMotor owns fixed-step poses, ChaseCamera follows interpolated presentation without roll, and DefaultSettings stores flight/camera tuning. FlightTests passed 3/3 in actual PlayMode (boost/braking bounds, render-batched fixed-input consistency, disabled movement). Input System assembly reference added; no packages changed. Subjective mouse feel and 30/60/144 FPS hardware runs are not claimed. Next internal stage: G03.

G03 completed: ShipTarget owns identity/idempotent destruction independently of visuals. DropletHitDetector checks initial overlap then every traveled segment, grows full buffers (complete allocating fallback at large capacities), sorts and deduplicates by ShipTarget. Added ShipTarget user layer through Editor API; no existing layer changed. CollisionTests passed 7/7 in actual PlayMode: 4x-boost thin target, 8/25 target sweeps, compound/repeated hits, saturated overlaps/zero motion, corner versus chord, and teleport/non-target exclusion. Next internal stage: G04.

G04 completed: Ready/Playing/Paused/Results, 120-second mission, score/combo/time bonus, HUD and remaining-target guide, boundary warning/recovery, cursor control and full restart. MissionController supplies a clamped final simulation step to the sole movement authority; all step hits resolve before victory, then timeout. Fixed a presentation interpolation issue when simulation stops. Final Unity regression: EditMode **3/3**, PlayMode **18/18**, zero failed/skipped. PlayMode includes the actual saved ten-ship scene completing and restarting three times, plus ten isolated reset cycles. No stage approval was requested.

### Batch deliverables and changed files

- Saved scene: `Assets/_Project/Scenes/TestRange.unity` and Unity-generated .meta.
- Saved prefabs: `Prefabs/Player/Droplet.prefab`, `Prefabs/Ships/GrayboxShip.prefab` under Assets/_Project, with metadata. Unit gameplay roots, replaceable VisualRoot, separate HitVolumes.
- Five authored URP materials under `Assets/_Project/Art/Materials/`: Hull, Signal, Droplet, Reference and Lane. Configuration: `Assets/_Project/Data/DefaultSettings.asset`.
- Runtime scripts: GeneratedRangeRoot, DropletSettings, DropletInput, DropletMotor, ChaseCamera, ShipTarget, DropletHitDetector, ScoreSystem, MissionController and HudPresenter.
- Editor scripts: TestRangeBuilder (explicit Undo-aware generation, dirty-scene refusal and build-scene configuration), LiveInputSmoke (temporary synthetic-device verification; Editor only).
- Tests: SceneAuthoringTests, ScoreTests, FlightTests, CollisionTests, MissionTests and SceneLoopTests. Updated Runtime/Editor .asmdefs to reference the installed Input System; preserved their .meta GUIDs.
- Configuration: `ProjectSettings/TagManager.asset` (free user layer 8 named ShipTarget), `ProjectSettings/EditorBuildSettings.asset` (TestRange first, SampleScene retained).
- Native Unity writes during first build: `Assets/Settings/DefaultVolumeProfile.asset`, `Assets/Settings/PC_RPAsset.asset`, `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`, and `ProjectSettings/ProjectSettings.asset`. The Editor saved URP serialization/build-prefilter/runtime-resource data and PlayerSettings. These were not manually edited as YAML. No Editor/package upgrade or render-pipeline switch occurred. Their existing .meta GUIDs remain unchanged. Only DefaultVolumeProfile's original bytes matched the installed template exactly; template comparisons for the other files are not represented as exact pre-task diffs.
- Documentation: AGENTS.md and IMPLEMENTATION_PLAN.md contain the authorized batch exception; ENVIRONMENT.md, STATUS.md, PLAYTEST.md and verification artifacts record actual results.
- Output: `Builds/Windows/DropletPrototype.exe` and its complete adjacent player data/runtime files. No pre-existing output existed at this path; no prior release was overwritten. Git index/parent repository unchanged.

### Batch verification evidence

| Check | Actual result | Evidence |
|---|---|---|
| Unity compilation | Passed in actual 6000.5.10f1 Editor; final captured Console errors empty | Unity recompile and Console tools; tests/build below also compiled actual Unity assemblies |
| G01 gate | 2/2 scene tests | [G01-tests.json](verification/G01-tests.json) |
| G02 gate | 3/3 flight tests | [G02-tests.json](verification/G02-tests.json) |
| G03 gate | 7/7 collision tests | [G03-tests.json](verification/G03-tests.json) |
| Final EditMode | 3 passed, 0 failed, 0 skipped | [JSON](verification/G04-editmode-results.json), [NUnit XML](verification/G04-editmode-results.xml) |
| Final PlayMode | 18 passed, 0 failed, 0 skipped | [JSON](verification/G04-playmode-results.json), [NUnit XML](verification/G04-playmode-results.xml) |
| Live Editor input | Enter, W/Shift acceleration, real fixed-step ship hit and score, Space braking, D strafe, mouse turn without roll, Esc frozen pause/cursor, R reset | [G04-live-input.txt](verification/G04-live-input.txt) |
| Windows standalone build | Succeeded, 0 errors, 1 warning; 76.193 seconds; reported output size 115,490,360 bytes | [G04-windows-build.json](verification/G04-windows-build.json) |
| Standalone UI observation | Launched built executable; clicked Begin; observed actual 4/10 then 6/10 scoring, later victory 10/10, score 3580, used time 61.18 seconds; subsequently observed reset to Ready / 120 seconds / 0 score | [standalone report](verification/G04-standalone.md), screenshots below |
| Source preservation | Compared 62 G00 files: 56 unchanged, 6 changed as listed above. SampleScene, all pre-existing .meta files, packages, ProjectVersion, GraphicsSettings and QualitySettings unchanged | [preservation report](verification/G04-preservation.json) |

Screenshots: [scene view](verification/G01-scene.png), [Ready](verification/G04-ready.png), [live Playing](verification/G04-playing.png), [live Paused](verification/G04-paused.png), [standalone victory](verification/G04-standalone-victory.png), [standalone reset](verification/G04-standalone-restarted.png). Screenshots supplement actual tests and input observations; they do not prove collision correctness or subjective feel by themselves.

### Remaining manual checks and known limitations

- **Implemented and verified:** saved authored scene/prefabs; assisted flight and swept collision; mission, score, timer ordering, pause, boundary recovery, reset; actual Unity tests; Windows build; standalone rendering, scoring and victory/reset observations.
- **Implemented but not fully verified:** independent physical keyboard handling in the standalone. Windows automation's injected Esc/Enter did not consistently produce an observable expected transition; a reset was subsequently observed after injected R. This does not identify whether the cause was input injection/focus or game handling, so standalone Esc/Enter are not marked passed. The corresponding Editor Input System smoke passed. Focus the standalone, physically press Enter/Esc/R and confirm the expected Ready/Playing/Paused states before accepting input feel.
- **Not run:** subjective flight/camera comfort, full 30/60/144 FPS hardware sweeps, measured performance budgets, long-session soak, every functional regression repeated inside the standalone, and natural 120-second standalone timeout. Automated actual Unity tests cover timeout/tie ordering; the observed standalone run ended in victory.
- Build warning: no RuntimePipelineConfig asset, so the development Pipeline bridge is disabled in the player. This is compatible with the offline game and was not enabled to silence the warning. Player log also reports an unavailable D3D12 info-queue interface, while actual rendering succeeded; no managed gameplay exception was observed.
- Tool limitations: intermittent Pipeline eval Roslyn encoding/network errors were bypassed using native compilation/test tools and a compiled Editor helper. A screenshot tool initially wrote into Assets; its generated copy/metadata subtree was removed through AssetDatabase after saving the delivery image outside Assets. No user asset was removed.
- HUD is English; destruction hides the intact visual. No audio/complex VFX/art import was implemented in this batch.

### Run and next step

Open TestRange and press Play, then Enter or Begin Flight; or run the Windows executable with all adjacent files present. Controls and exact manual checks are in [PLAYTEST.md](PLAYTEST.md). Unity is left out of Play mode with the saved TestRange open and clean. The standalone was last observed at Ready.

**Stop after G04. G05 is not started.** The user's next useful action is a short physical-input/feel playtest; there is no package installation or build-module blocker.

## G00 report (historical)

### Stage and result

**Complete.** Existing Unity 6000.5.10f1 (3bd4f66ad299), URP 17.5.0 and all packages preserved. Actual post-setup Unity compilation succeeded in the already-open Editor. No G00 blocker remains. User visual Console confirmation remains a manual action, not an automated observation.

### Changes

- Added the planned `ArtSource/Blender/`, `Tools/Blender/` and `Assets/_Project/` folder trees, with .gitkeep files in empty leaf folders and Unity-generated asset .meta files.
- Added Runtime, Editor, EditMode-test and PlayMode-test .asmdefs in their corresponding folders. No C# gameplay or test implementation was added.
- Added project-local `.gitignore` without changing parent repository rules or configuration.
- Expanded existing [ENVIRONMENT.md](ENVIRONMENT.md) with actual tool/package versions, pipeline, machine, Git findings and reproducible manual checks; retained its earlier integration record.
- Added [verification/G00-unity-baseline.json](verification/G00-unity-baseline.json), containing actual Unity responses, warnings, test discovery and file-preservation results.
- Updated this status file. No existing scene, prefab, material, input asset, package manifest or ProjectSettings file changed.

### Checks actually run

| Check | Result / evidence |
|---|---|
| Repository, AGENTS, implementation/test plan and prior status inspection | Existing Unity template project; required G00 layout identified |
| ProjectVersion, manifest/lock and render pipeline inspection | Unity 6000.5.10f1; URP 17.5.0; live PC_RPAsset; full inventory in ENVIRONMENT.md |
| Install folders, executable versions, registry and running Editor identity | Unity/Hub/CLI and Blender 5.2.1 LTS found; live connection verified against this exact project |
| Initial Editor/scene/Console state | Ready, stopped, no script compilation failure; clean SampleScene with three roots; captured warnings/errors initially empty |
| Actual Unity recompile after setup | `recompile` + `recompile_status`: completed, failed=false, errors=[]; later scriptCompilationFailed=false |
| Post-compile Console and scene check | Four expected empty-assembly warnings; zero captured errors; SampleScene remains clean and unchanged |
| Unity Test Framework 1.7.0 discovery | `list_tests(mode:"all")` succeeded, 0 tests; discovery only, no test pass claim |
| Folder, .asmdef JSON and .meta inspection | All planned folders exist; four definitions parse, references and Editor/test boundaries inspected; Unity created their metadata |
| SHA-256 source preservation | All 62 pre-existing Assets/Packages/ProjectSettings files unchanged, none missing |
| Git inspection and ignore probes | Parent repo C:/学习/玩, master, no remote, project untracked; generated paths ignored and source paths retained |
| Git whitespace check | `git diff --check -- .` returned no errors; limited because project files are untracked |
| Direct artifact verification | Evidence JSON parsed; asset folder/assembly metadata present; whitespace checked directly for untracked deliverables; 0 new C# or scene files |

### Checks not run

- EditMode/PlayMode test execution: no tests exist at G00; tests will be added with the relevant milestone. No zero-test run is reported as a pass.
- Standalone build and player smoke test: first playable build belongs to G04; no G00 gameplay scene exists.
- Play-mode gameplay, rendering/feel, collision and regression checks: future milestone work, not G00.
- Fresh Editor launch / independent license activation: reused the already-open working Editor to preserve the user's session.
- Blender scene/export calibration and performance tests: deferred to G05/G09. Blender version was checked only.

### Manual verification

1. Focus DropletPrototype in Unity; Help > About Unity must show **6000.5.10f1**. Keep any unsaved work; no Editor restart is necessary.
2. With the existing `Assets/Scenes/SampleScene.unity` open, choose Assets > Refresh, wait for compilation/import to finish, then open Window > General > Console and confirm **no red errors**. Four no-scripts warnings for the empty G00 assemblies are expected.
3. Inspect Edit > Project Settings > Graphics/Quality: default/current PC asset is PC_RPAsset, Mobile override is Mobile_RPAsset. Inspect the new Assets/_Project folders in the Project window.
4. If an error appears, record the first full error before starting G01. Full reopen and Test Runner instructions are in [ENVIRONMENT.md](ENVIRONMENT.md#manual-confirmation--reproducing-base-01).
5. Before the first commit, decide whether to keep this project in the existing parent Git repository or use a dedicated repository. No commit or repository migration was attempted.

Actual automated observations are recorded above; user UI confirmation has not been claimed.

### Decisions and known issues

- Preserved the valid existing Editor/pipeline even though the planning document discusses an LTS option for new projects. No scope change was required.
- Empty .asmdefs establish G00's requested assembly layout and deliberately emit four non-blocking no-scripts warnings. Unity has not compiled these empty assemblies into DLLs; compilation evidence applies to the existing project baseline.
- A single optional AssetDatabase.Refresh eval failed with a Roslyn illegal-byte-sequence tool error. Native Unity recompile and later read-only eval succeeded; the required setup/import/compile checks completed. See raw evidence.
- The parent Git repository includes unrelated existing changes. All writes were confined to DropletPrototype; no Git index or parent configuration was changed.
- No new assets needing license entries were added. Asset authoring, scene creation, movement, collision, ships and effects remain deferred.

### Next stage

**G01 — Saved graybox scene**, only when explicitly requested after reviewing G00. Stop here; G01 was not started.

## Milestone report template

### Stage and result

Stage:
State: Not started / In progress / Blocked / Failed / Accepted

### Changes

Files changed and purpose:

### Checks actually run

Command/tool, software version, result, and evidence path:

### Checks not run

Check, limitation, and required next action:

### Manual verification

Exact scene, menu, controls, expected observations, and actual observations:

### Decisions and deviations

Proposed requirement changes, reasons, and approval state:

### Known issues

Reproduction and severity:

### Next stage

Only the next unstarted stage after current acceptance:


## Integration setup - 2026-09-07

Official Unity CLI MCP configured for this project; legacy Codex unityMCP removed. Unity CLI 1.0.0-beta.8 and Pipeline 0.6.0-exp.1 installed. Editor compilation completed, editor_status reported ready, and scene hierarchy read succeeded. MCP handshake and discovery returned 149 tools. See ENVIRONMENT.md. This setup does not mark any gameplay milestone complete; no gameplay tests or player build were run.
