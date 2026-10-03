# 2026-09-19 剧情与音频子任务验收记录

本组已完成新剧情文本、独立音效、混音/抢占/中断代码和安全集成入口；**新英语实音频、4–6 分钟实际播放未完成，受到云端资源权限阻塞**。主代理保存的新场景应使用旧版中文语音及其原中文字幕、60 秒 Timeline，HUD 明示 `AI VOICE: ZH (legacy)`。不会用英语字幕搭配旧中文配音，也不会用静音或重复台词伪造新剧情时长。

## 原创内容与角色

`Tools/Audio/EnhancementEnglish.json` 是完整可审阅台本，由 `enhancement_content.py` 生成。

| 角色 | 固定 Seed-TTS 2.0 音色（文档已核对，云端发声未验证） | 内容 |
|---|---|---|
| 主播 | `en_female_dacey_uranus_bigtts` | 社会评论与公共转播 |
| 指挥官 | `en_male_tim_uranus_bigtts` | 接触规则、招降、交战与撤离命令 |
| 工程师 | `en_female_jane_uranus_bigtts` | 科学判断、反射与反应堆警告 |
| 通信军官 | `en_female_stokie_uranus_bigtts` | 接触尝试、求救、信号切换 |

开场《The Open Corridor》共 **40 条独立英语台词、899 词、四幕各 10 条**：社会评论 → 接近与停止/检查条件 → 军事与科学分歧 → 最后警告与交战。每条有对应中文翻译。36 条战斗广播覆盖 9 个现有事件，各 4 条，包含反射、贯穿、失稳、求救、爆炸、撤离、逃脱与通信中断。全库 76 条、7,462 英文字符，无重复开场台词。当前源中时间均为 0，明确代表尚未测得音频时长，不能拿词数估算冒充实际播放时长。

## 实际云端调用与阻塞

已读取原接入工具及环境变量方案，未更换服务、未展示密钥、未充值。2026-09-19 从官方文档公开数据接口核对了英语音色、`seed-tts-2.0`、`X-Api-Key` 和单向 HTTP 接口。只使用真实支持的 `audio_params.speech_rate`（范围 -50～100）以及 `additions.context_texts` 的首个自然语言指令；不使用其他服务的标签、SSML 或 1.0 的情感枚举。

- [官方音色列表](https://www.volcengine.com/docs/6561/1257544)
- [官方 HTTP API](https://www.volcengine.com/docs/6561/1598757)
- [官方语音指令](https://www.volcengine.com/docs/6561/1871062)

先预览四角色 511 字符，随后执行试音。**首条 E001，117 字符，实际返回 HTTP 403，无音频返回**；其余三角色与全库未提交。为避免重复发送计费文本，另作两次空文本鉴权诊断，第二次确认嵌套错误字段：`header.code=45000030`，`[resource_id=volc.seedtts.default] requested resource not granted`。这是明确的资源未授权；没有证据称额度已经耗尽。现有工具头部和接口与当前官方文档一致，没有可据以重发的接口修复。已停止云端调用。

证据：`verification/Enhancement-20260919/audio/auth-diagnostic.json`、`provider-verification.json`，以及 `ArtSource/Audio/Generated/Volcengine/seed-tts/enhancement-en-20260919/E001-c0d192d04ef08c96/job.json`。原工具保守保留 `submission_unknown`，重跑不会自动重发。

## 实际本地产物与代码

新增真实 24 kHz、16 bit、单声道程序合成音频，无第三方录音、音乐或噪声掩盖：

| 文件（`Assets/_Project/Audio/EnhancementEnglish/`） | 实测时长 | 峰值 |
|---|---:|---:|
| ConnectTone.wav | 0.14 s | -16.48 dBFS |
| InterruptTone.wav | 0.19 s | -18.42 dBFS |
| Alarm.wav | 1.20 s | -16.48 dBFS |
| EquipmentBed.wav | 4.00 s | -37.32 dBFS |

它们使用独立 AudioSource 与 Mixer 组，不烘焙进人声。`EnhancementVoiceAssets.Configure(MissionController)` 由主代理在安全副本执行；只写新 Enhancement 资产，返回 `false` 表示英语包尚不完整，但仍接线旧语音库的安全副本和新的本地混音。AudioMixer 通过实际 Editor API 创建，未手写 YAML。

Mixer 分为 Voice / Signal / Alarm / Background / Combat。Normal/Broadcast 快照使战斗组从 -1 降至 -4 dB，背景从 -12 降至 -18 dB；播音起落分别用 0.08/0.22 秒过渡。重要反射、失稳和撤离警告可抢占普通广播，相同等级警告不会不断互相打断。舰船爆炸会立即取消该舰当前语音及队列，死亡/逃脱舰不能产生新的实时发言。暂停控制人声、信号、警报和底噪；重开清空优先级、事件预算和混音状态；结算/待机停止音频。

新完整英语资源可用时，builder 按每个导入 AudioClip 的真实 length 重建 Timeline 与字幕槽位，只有 0.16 秒自然交接，不补长静音；超出 240–360 秒即拒绝接入。开场仍由 DropletMotor 持续推进，正常完成和跳过沿既有一次性战斗入口。镜头用位置偏移切换，保留保存的 FOV。干声保存在 ArtSource，FFmpeg radio master 使用轻量 190 Hz 高通、6.2 kHz 低通、2.4:1 压缩和 -18 LUFS / -2 dBTP 响度处理。**因没有返回的干声，FFmpeg 人声处理与成片音质尚未执行。** 本机确实找到 FFmpeg 9.0.1-full_build-www.gyan.dev；未安装软件。

## 检查

实际运行 `python -B -m unittest discover -s Tools/Audio -p test_*voice.py -v`，**14/14 通过**，Python 3.14.3。其中包括原 9 项与新增 5 项：四幕双语唯一性和事件覆盖、预览不读凭据/不调用云、有限字符/请求上限与固定配角、真实独立 PCM 音效、缺少实音频不会发布带时间的假英语包。它们是离线检查，不能代替活 API 成功或试听。

新增 Unity PlayMode `EnhancementRadioPriorityTests` 两项供主代理执行：普通广播被警告抢占且同等级警告不饥饿、暂停与三次重开复位。A 组未运行 Unity、未写场景/Prefab/Renderer；实际编译、Mixer 资产、游戏画面/播放验证由主代理统一报告。旧 48 条语音与其 `.meta` 未由 A 修改。

## 试听清单与未验收项

**待人工试听**：以上四个本地音效；旧中文包在新 Mixer 中的音量与警报可读性。没有英语试音文件，故不存在已通过试听的英语素材。

**阻塞**：四角色实英语试音、全库生成、干声/无线电人声、4–6 分钟实际剧情、基于新实音频的字幕同步和四幕跳过/重播验收。未谎报为通过。无需用户重接工具；须由现有火山账户为当前 Key 所属项目授予所需 Seed-TTS 2.0 资源权限后，再对该部分继续。

生成队列固定单线程，每个 take 最多 76 条合成条目/14,000 字符；缓存命中不重发，完成结果校验哈希，不确定提交不会自动重发。两次空文本鉴权诊断单独记录。权限恢复后选择有意的新 take，再依次 preview → samples --execute → signal QA/待人工试听 → all preview → all --execute → master；不要更换 provider 或无界重试。所有 runtime 只引用本地 AudioClip，没有云请求。
