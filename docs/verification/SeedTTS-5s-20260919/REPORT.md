# Seed-TTS 约 5 秒实请求诊断 — 2026-09-19

本次使用现有 Tools/Audio/volc_voice.py，先 preview，再执行一次真实请求。未修改服务、凭据、Unity 资产或旧生成任务；未自动重试。

- 文本：This is fleet command. All ships, hold position and await further orders.
- 73 字符，13 个英语单词；目标约 5 秒，非精确时长参数。未获得音频，因此实际时长未知。
- speaker: en_male_tim_uranus_bigtts；speech_rate: 0；资源头 seed-tts-2.0。
- endpoint: https://openspeech.bytedance.com/api/v3/tts/unidirectional
- HTTP 403；provider header.code 45000030。
- provider message: `[resource_id=volc.seedtts.default] requested resource not granted`。
- WAV 数量 0。当前凭据对请求资源未获授权，合成没有成功。无证据将原因归为余额耗尽、文本长度、英语音色或 FFmpeg。

使用临时进程内错误观察器读取一次失败响应，仅保存 code/message（密钥先脱敏），未改变原工具请求构造、鉴权、缓存或无重试行为。原 job.json 仍保守记录 submission_unknown，不能将它解读为成功或确定未计费。

用户给定 https://console.volcengine.com/ark/region:cn-beijing/subscription/agent-plan 可通过普通 HTTP 获取公开前端壳（已保存 agent-plan-public.html），但不是登录后的订阅数据。Web 工具失败，浏览器连接两次均因 request-header policy 加载失败而不可用，无法核对实际订阅权益。不能据此声称已查看账户套餐或确认密钥来源。

排查方向：Ark 套餐页面与当前 openspeech 语音接口属于不同入口；当前实际请求已证明语音资源未授权。需要在语音服务控制台核对同一账号/项目的 Seed-TTS 2.0 开通状态及 API Key 所属服务和资源权限。若现有密钥来自 Ark 套餐，需核实其是否适用于语音服务，不能默认套餐额度可用于该接口。以上是待核对方向，不冒充已验证套餐条款。

证据：result.json 及 ArtSource/Audio/Generated/Volcengine/seed-tts/diagnosis-5s-20260919/ 下的原始任务日志。未生成可供试听的音频，不进行 Unity 导入。
