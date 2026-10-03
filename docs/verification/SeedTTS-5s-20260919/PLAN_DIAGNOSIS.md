# Agent Plan 语音接口对照诊断

已读取官方文档“接入语音模型”，DocumentCode=agent-plan-personal-voice-model，正文与完整返回分别保存在 voice-plan-official.md/json。

官方明确：Agent Plan 支持 Seed-TTS 2.0；X-Api-Key 必须使用套餐专属密钥；资源头 seed-tts-2.0；HTTP 地址 https://openspeech.bytedance.com/api/v3/plan/tts/unidirectional。套餐内 AFP 抵扣，超额行为取决于用户是否已开启后付费，本次未更改任何计费设置。

此前普通接口缺少 /plan 路径，返回403/45000030。按文档修正现有工具后，先预览再执行一次相同73字符英语短句（目标约5秒）：HTTP401，45000010，Invalid X-Api-Key，无音频。唯一主动请求为TTS，没有请求视频、ASR或其他生成服务。plan-result.json与新take任务日志保留。

结论：原请求入口与套餐不匹配；正确入口仍拒绝当前密钥。不能据此断言套餐不含语音、额度耗尽或必须购买独立语音服务。可能为密钥来源错误、已失效/撤销，需在Agent Plan专属API Key页面核对。未看到账号侧状态，无法进一步区分。只检查环境变量名称：发现VOLC_SPEECH_API_KEY，未发现另一个Ark密钥变量；arkcli不在PATH。没有输出密钥值。

已修正volc_voice.py接口并增加任务endpoint记录，同步现有接口回归断言及使用文档。旧任务和Unity音频未覆盖。待用户在本机配置有效的Agent Plan专属密钥后才能继续；不要在聊天中发送密钥。
