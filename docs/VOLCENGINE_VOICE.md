# Seed-TTS 游戏音频制作

## 2026-09-29 当前可用接入与产物

非语音使用默认 Speech 项目专用 API Key（仅本机用户环境变量 `VOLC_AUDIO_API_KEY`，不写入项目或日志）。`seed-audio-1.0` 的 `/api/v3/tts/create` 已实际生成 131/131 条正式非语音成品，包含激光、金属贯穿、舰爆、飞行持续声、舰桥环境和无歌词配乐。用户已试听认可六类代表；其余独立变体尚待逐条听感检查。原音保存在 `ArtSource/Audio/SeedAudio20260929/raw-catalog/`，处理后成品已由 Unity Editor 导入新场景 `FleetAssault_SeedAudio.unity`。

对白继续由独立 `VOLC_SPEECH_API_KEY` 调用 Seed-TTS 2.0；新版 87 条英语干声已齐，E033 首请求中断且无 WAV，因此保留不明状态，并以独立 E033_R2 完成该句。80 条正式通讯加 78 条强度变体已用这些干声和已完成的 Seed Audio 重混并导入。新音频只在开发机生成；离线 Unity 玩家读取本地导入素材。详见[试听目录](../ArtSource/Audio/SeedAudio20260929/index.html)与[验收记录](SEED_AUDIO_ACCEPTANCE.md)。87 条干声和 158 条通讯未逐条人工试听。

**当前路线：** 用户授权对白用 Seed-TTS 2.0、非语音用国内 Seed Audio（60 元独立上限）；AFP 上限 20,000。按原始任务时长与未知结果预留计算，Audio 当前最多计入约 32.83 元，TTS 累计计入 2235.195 AFP，均非服务商账单。见 [当前方案](SEED_AUDIO_IMPLEMENTATION_PLAN.md)。以下旧版鉴权、失败尝试和“全部用 TTS”段落保留为历史试验记录，不代表当前素材仍在等待生成、导入或接口开通；旧未知请求仍不重发。

## 当前国内 Seed Audio 路线

用户截图旧版鉴权补充：支持 seed_audio.submit(..., app_id=...) 发送 X-Api-App-Id、X-Api-Access-Key，另带 X-Api-Resource-Id=volc.service_type.10074。凭据本地变量 VOLC_AUDIO_APP_ID / VOLC_AUDIO_ACCESS_TOKEN，不使用 Secret Key；当前 CLI 默认仍为新版 API Key，旧版实测采用显式 app_id。2026-09-29 截图双头与补资源头的两次真实请求均为 401/45000010，消息 load grant: requested grant not found in SaaS storage，无 WAV。官方参考：https://docs.volcengine.com/docs/6561/2534847?lang=zh 。不能据此宣称该旧应用已获 Audio 权限。

官方契约和价格见 [API_CONTRACT](verification/SeedAudio-20260929/API_CONTRACT.md)。模型 seed-audio-1.0，POST https://openspeech.bytedance.com/api/v3/tts/create，X-Api-Key，text_prompt + audio_config；同步返回 Base64 音频与 original_duration。没有可传的硬时长参数或异步查询接口。1 元/分钟，最长 120 秒，所以每次先预留 2 元，成功按原始时长向上取整秒核算；失败不明保留预留。

2026-09-29 17:00:20（UTC+8）已用用户配置的 ARK_API_KEY 真实提交 LASER_R1：HTTP 401、45000010、Invalid X-Api-Key。无音频输出，未继续其余代表；现金确认用量未知，账本保留 2 元，不声称实际已花 2 元或请求免费。原请求保留，不自动重发。该结果证明本次已配置的方舟密钥未获此接口接受，不证明模型能力不足或额度耗尽。

官方要求使用[语音服务密钥页](https://console.volcengine.com/speech/new/setting/apikeys?projectName=default)的凭据。新本地助手保存为 VOLC_AUDIO_API_KEY，保留 ARK_API_KEY 及 VOLC_SPEECH_API_KEY：

    powershell -NoProfile -STA -ExecutionPolicy Bypass -File Tools/Audio/Set-SeedAudioCredential.ps1

只在本地密码框输入，不发聊天。助手做读回比较，不调用 API。新客户端执行时优先读 VOLC_AUDIO_API_KEY，缺失时才兼容旧 ARK_API_KEY；预览从不读取密钥。

    python -B Tools/Audio/seed_audio.py catalog --write
    python -B Tools/Audio/seed_audio.py preview --ids LASER METAL EXPLOSION FLIGHT CABIN MUSIC
    python -B Tools/Audio/seed_audio.py generate --ids LASER --execute

最后一条是生成命令示例；LASER_R1 已存在失败记录，重复执行会被拒绝。凭据实际修正后，需以 review-auth 记录明确的 401/45000010 审查、保留原 2 元预留，再写入有修订理由的 LASER_R2，预览后单独提交。未知超时不能用该鉴权审查入口解除；原 ID 永不重放。131 项目录已有独立提示词，但客户端目前只放行六类代表，不假称已有批量生成或发布功能。

本批目录为 ArtSource/Audio/SeedAudio20260929；Audio 账本 audio-ledger.json，修订对白 narrative-revision.json。对白使用独立累计 AFP 账本工具，不直接跑仍引用旧文本及程序声源的 cinematic_audio.py。Unity 导入和混音须等待代表素材通过，不能用历史 172 WAV 计作这次新增交付。

2026-09-29：所有新增声音来源仅允许 Seed-TTS 2.0，传输入口仍为 Tools/Audio/volc_voice.py（Python 3.14.3 标准库）。本批组织工具 Tools/Audio/seed_soundscape.py 使用既有 NumPy 做信号检查。云端调用仅用于开发期制作，Unity 玩家保持离线。全音频计划已记录，但六类非语音代表未通过内容门槛，尚未扩大或导入；详见 [实测报告](verification/SeedTTS-Soundscape-20260929/REPORT.md)。

## 配置与预览

### 火山方舟独立凭据（2026-09-29）

本机状态：用户于 2026-09-29 16:46:29（UTC+8）完成保存，助手写入／读回比较成功，后续独立检查确认用户环境中凭据可读取。仅确认本地配置，尚未验证云端鉴权或音频模型权限。

用户另提供了火山方舟 API Key 控制台地址，要求本地配置。使用新增 `Tools/Audio/Set-ArkAudioCredential.ps1` 打开本地密码输入框，将密钥保存到 Windows 用户环境变量 **ARK_API_KEY**；不写入项目、不记录密钥，不覆盖 **VOLC_SPEECH_API_KEY**。Windows 用户环境变量本身不加密。本地状态记录在 `%LOCALAPPDATA%/DropletPrototype/ark-credential-status.json`，只包含状态、变量名、时间和是否测试，不含凭据。

    powershell -NoProfile -STA -ExecutionPolicy Bypass -File Tools/Audio/Set-ArkAudioCredential.ps1

保存操作会在本机比较写入值与读回值，不输出密钥。已经运行的进程可能仍持有旧环境；新进程可继承更新后的环境，开发工具也可显式读取 Windows 用户环境。不要执行显示变量值的命令。本助手仅配置凭据，**不调用 API、不生成音频、不证明特定音频模型已授权**；现有 Seed-TTS 工具继续使用原专属语音凭据。方舟通用入口参考为 `https://ark.cn-beijing.volces.com/api/v3`，具体音频模型及接口尚未接入。

官方参考：[API Key 管理](https://docs.volcengine.com/docs/ark/api-key?lang=zh)、[ARK_API_KEY 配置示例](https://docs.volcengine.com/docs/ark/online-inference-standard?lang=zh)。

### 已有 Seed-TTS 凭据

Agent Plan 专属接口：https://openspeech.bytedance.com/api/v3/plan/tts/unidirectional

必须使用 Agent Plan 专属 API Key。**现有本机密钥已通过 2026-09-19 与 2026-09-29 真实生成，不需要提供新密钥。** 旧普通接口 403/45000030 和修正接口后的早期 401/45000010 均为历史失败，不能覆盖后续成功状态。官方说明：https://console.volcengine.com/ark/region:cn-beijing/docs/ark/agent-plan-personal-voice-model?lang=zh
资源：seed-tts-2.0。工具先读取进程环境，再读取 Windows 用户环境的 VOLC_SPEECH_API_KEY。现有鉴权及所测音色可用；控制台剩余额度及账单未读取。不得将密钥写入项目或聊天。

本机隐藏输入助手（保存为用户环境变量，注册表明文）：

    powershell -NoProfile -File Tools/Audio/Set-VolcVoiceCredential.ps1

选择已开通的 TTS 2.0 音色后，将真实音色 ID 填入：

    python Tools/Audio/volc_voice.py generate --id C01 --speaker YOUR_TTS2_VOICE_ID

默认仅预览，不读密钥、不联网、不写文件。兼容 --provider seed-tts，拒绝其他 provider。已删除视频模型/时长参数和 collect 命令。

台词源为 Assets/_Project/Data/NarrativeCombat/RadioContent.json，共 C01–C36、N01–N12。支持多个 --id，明确 --all 才选全量。自定义 --text 必须配一个 ID；--direction 指定语音指令，效果取决于音色；--speech-rate 调整语速。
用户要求实际生成后，先预览，再追加 --execute，先单句试听再批量。配置和代码修改不代表付费生成授权。

## 产物与恢复

产物目录：ArtSource/Audio/Generated/Volcengine/seed-tts/<take>/<id>-<请求哈希>/，保留原 TTS 路径及哈希规则。PCM 封装为 24 kHz、16 bit、单声道 WAV。job.json 保存请求、请求 ID、状态、用量、时长及 SHA-256，不保存密钥。

提交前写独占日志；中断保留 submission_unknown，重跑不自动提交。不完整 PCM 不发布 WAV，但服务可能已计费。失败只保存安全诊断（事件数、状态码、是否完成、PCM 字节数与用量），不保存原始响应文本、请求头或密钥。完成缓存经哈希校验后复用。2026-09-29 批次必须走有预算账本的 seed_soundscape.py，不可另换 take 绕过本轮次数／预算约束。

## 2026-09-29 代表素材批次

累计预算 20,000 AFP，无现金付费、无自动重试。完整目录在 ArtSource/Audio/SeedTTS20260929/catalog.json，预览、生成、检查彼此分离。当前脚本实现的是能力试验与证据整理；批量成品后期和 Unity 发布尚未实施。

    python -B Tools/Audio/seed_soundscape.py preview --ids CONTROL
    python -B Tools/Audio/seed_soundscape.py preview --ids LASER METAL EXPLOSION FLIGHT CABIN MUSIC
    python -B Tools/Audio/seed_soundscape.py audit --write

preview 不读取凭据、不联网、不写文件；generate 只有 --execute 才会提交。每类固定 R1–R3，修订要求记录上次观察原因，第四次被拒绝；固定 ID 的请求文本不能改写。完整缓存必须同时通过原请求和音频哈希校验。出现不确定请求会阻止新调用；review-uncertain 只记录显式核对理由并保留全额预留，绝不会重发该请求。已完成的本批六类均用满三次，不继续增加付费尝试。

账本区分服务返回的 text_words × 0.135 AFP 与未知用量预留。预留按文本与指令 UTF-8 字节数的四倍保守估算；官方说明 context_texts 不计费，但未知请求仍保留该额外余量。当前服务返回用量折算 42.255 AFP，未知预留 495.720 AFP，合计占用 537.975 AFP。此值不是控制台账单或账户余额。

两种使用方式均实测：描述文本配 context_texts，以及短拟声提示／场景标签配声学指令。后者标签只是普通合成文本，不声称存在未文档化的音效 API 语法。当前仍有朗读描述／标签的证据，短提示也未取得音色验收。System.Speech 离线自由听写只识别第一句，不注入提示词语法；空转写、有效 WAV、0 削波均不能自动放行。主观试听未执行，所有原始候选在 [试听目录](../ArtSource/Audio/SeedTTS20260929/listening.html) 中。

## Unity 与验证

原有配音、87 条已生成 Seed 干声及 .meta GUID 保留。本批未导入素材或修改当前正式场景。之后仅在六类内容门槛通过时，通过 Unity Editor API 创建增强副本并统一导入，检查当前完整英语剧情、中文字幕、截断、暂停／重开、混音、2000 舰独立版播放，在 docs/ASSET_LICENSES.md 记录来源。

    python -B -m unittest discover -s Tools/Audio -p 'test_*.py' -v

最终 24 项离线回归通过（基础客户端 10、新批次 9、既有增强工具 5），覆盖 PCM/WAV、空完成响应的安全诊断、预算、预览隔离、不确定提交不重发、缓存篡改、修订上限、内容验收不能被 WAV 成功替代等。这些使用 mock 和临时文件，不等于真实 API 验证。真实 API 证据是独立保存的 19 条请求日志。主观试听、Unity 编译／测试／构建和音频替换未执行；没有将旧构建验证算作本批通过。


2026-09-19 live recheck: after configuring the Agent Plan dedicated key, one real English Seed-TTS request succeeded (HTTP 200), producing 5.007083 seconds of 24 kHz mono PCM WAV. See verification/SeedTTS-5s-20260919/newkey-result.json. Human listening remains pending. Earlier 401 is historical and no longer blocks this tested request.
