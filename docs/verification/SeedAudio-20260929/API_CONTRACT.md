# Seed Audio 1.0：官方接口契约证据

核对日期：2026-09-29。仅查看官方公开页面，无账号登录、密钥读取、生成调用或计费请求。部分页面由 JavaScript 渲染，使用后台浏览器读取公开正文；没有保存含账号或凭据的截图。此文件证明文档契约，不证明本地密钥的实际权限。

## 接口及请求

来源：[音频生成 HTTP](https://docs.volcengine.com/docs/DoubaoVoice/audio-generation-http?lang=zh)，页面更新时间 **2026-09-28 19:12:54**。原数字链接为 <https://docs.volcengine.com/docs/6561/2550782>。

- `POST https://openspeech.bytedance.com/api/v3/tts/create`，同步、非流式 JSON；不是 Agent Plan TTS 接口，也不是 Ark `/api/v3/chat/completions`。
- Header：`Content-Type: application/json`、`X-Api-Key`；建议添加 UUID `X-Api-Request-Id` 供追踪。旧控制台的双 Header 鉴权不用于本批实现。
- 必填 `model: "seed-audio-1.0"`、`text_prompt`（最多 3000 字符）。纯文本生成无需 `references` 或 `speaker`。
- 可选 `references` 最多 3 条音频，每条不超过 30 秒／10 MB；支持 `speaker`、`audio_data`、`audio_url`，同一参考项三者互斥。图片参考最多 1 张／10 MB，不能与音频参考混用。本批非语音代表只使用文本。
- `audio_config.format` 支持 `wav/mp3/pcm/ogg_opus`。WAV/PCM 默认采样率 40000，可选 8000、16000、24000、32000、40000、44100、48000；MP3 默认 44100；OGG Opus 仅 48000。本批建议 WAV 48000，保留实际声道数。
- 不设置变速、变调或音量参数时默认不调整。时长仅通过 `text_prompt` 自然语言控制；文档没有硬性 `duration` 请求字段。模型原始输出上限 **120 秒**。

请求体示例（只展示契约，不表示已执行）：

```json
{
  "model": "seed-audio-1.0",
  "text_prompt": "Create a five-second isolated science-fiction laser shot: a sharp electronic attack, a short energy sweep and a metallic decay ending in silence. Sound effect only. No speech, vocals, music, narration or spoken description.",
  "audio_config": {"format": "wav", "sample_rate": 48000}
}
```

## 响应、保存与不确定状态

- HTTP 成功示例直接返回 JSON：`audio`（完整音频的 Base64）、`duration`（处理后秒数）、`original_duration`（模型原始秒数）、`url`（有效期 2 小时）；Header `X-Tt-Logid` 用于排障。
- `code`、`message` 可用于业务错误；成功示例没有 `code` 字段，因此客户端不能要求成功响应必须含 `code == 0`。必须同时检查 HTTP、业务错误、实际音频和完整解码。
- `audio_config.enable_subtitle` 默认 false，开启后返回字幕信息。无字幕或空字幕不构成无语音内容的听感证明。
- 此同步接口文档未提供任务 ID、查询任务或恢复下载接口。优先解码 Base64 保存原始文件，避免依赖临时 URL；提交后断线／超时不能靠自动重发恢复。
- `X-Api-Request-Id` 文档仅承诺追踪用途，没有承诺幂等去重。重发同一 ID 不能被当成免费或不重复生成。

## 计费与上限

来源：[豆包语音计费说明](https://docs.volcengine.com/docs/DoubaoVoice/Billinginstructions-21?lang=zh)，页面更新时间 **2026-09-28 17:27:40**。

- 豆包音频生成模型 1.0，后付费推理调用刊例价 **1 元／分钟**。
- 按每次模型原始输出时长累计，精确至秒，折算分钟；倍速等后处理不改变计费时长。以响应 `original_duration` 留账，不用裁切成品时长倒算。
- 每请求最多 120 秒，故按刊例价预留 **2 元**。不因提示词写了 1 秒就减少最坏情况预留。完成后可按原始时长保守向上取整到秒核算；这仍是本地预算核算，不冒充控制台账单。
- 本批用户授权 Audio 总上限 **60 元**，不查询余额、不充值、不购买资源包或并发包。每次请求先持久化预留；状态不明保留 2 元预留，不自动重发。
- 正式版默认 5 并发；本批可串行工作，无需增购。
- 页面列有预付费资源包，但本批不购买。Agent Plan 的 20000 AFP 是对白独立账本，不可拿来抵扣此现金预算。

## 凭据适用范围

来源：[API Key 使用](https://docs.volcengine.com/docs/DoubaoVoice/APIKeyUsage?lang=zh)，页面更新时间 **2025-09-02 19:34:20**。

官方音频接口及该说明均指向 [豆包语音控制台 API Key 管理](https://console.volcengine.com/speech/new/setting/apikeys?projectName=default)，通过 `X-Api-Key` 使用。用户此前提供的是火山方舟 `ark/region:cn-beijing/apiKey` 入口，已在本地存为 `ARK_API_KEY`；公开文档没有确认这两类密钥互通，也没有明确证明其不互通。

因此：仅本地保存成功不能标为音频鉴权通过；用户已授权的代表生成可以给出真实结果。若实际返回 401／403，记录脱敏错误和 Logid 后停止依赖该权限的调用，再按证据处理正确凭据。不要打印密钥、自动替换 Agent Plan 密钥、将密钥写入请求清单或 Unity。

## 能力证据的边界

官方接口明确列出游戏音效用途；[Seed 团队模型介绍](https://seed.bytedance.com/en/blog/from-speech-to-audio-creation-introducing-the-seed-audio-1-0-audio-creation-model) 描述语音、音效和环境统一生成、最长两分钟及延长能力。输出适合本项目的程度仍须通过六类代表内容验收；HTTP 成功、文件解码或字幕为空均不等于主观听感通过。
