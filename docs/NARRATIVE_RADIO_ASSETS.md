# 剧情与无线电资源说明

本文件记录本批新增资源的制作方法；整体验证结论以 `NARRATIVE_COMBAT_REPORT.md` 为准。

- 原创文本：`Assets/_Project/Data/NarrativeCombat/RadioContent.json`，36 条战斗广播、12 条剧情广播。剧情 0–60 秒，电视内容明确标注“缓存录播”。
- 离线人声：本机已安装 `Microsoft Huihui Desktop`（zh-CN），使用 Windows `System.Speech`，语速 4、音量 88，实际导出 C01–C36、N01–N12 共 48 个 WAV。没有网络请求或付费服务。
- 通信提示：同一脚本额外导出短双音 `ConnectTone.wav` 和确定性噪声 `InterruptTone.wav`。总计 50 个 WAV；文本与提示音独立于人声保留。
- 重生成：在项目根目录执行 `Tools/NarrativeCombat/ExportRadioVoices.ps1`。随后由显式新场景 Builder 调用 `NarrativeRadioAssets.Configure`，保存台词 ScriptableObject、60 秒 Timeline、中文 TMP 字形图集和场景 UI；不会改写旧场景。
- TMP 依赖：项目原先只有包代码、没有 Essential Resources。生成器从已安装 ugui 包导入官方资源。实际首次异步导入后保存于新增 `Assets/TextMesh Pro`，没有移动旧用户资产。中文字体使用本机微软雅黑生成静态 SDF 字形图集，包含本批所有字幕、标签与 ASCII，未复制完整系统字体文件。字体的材质和各张图集先作为子资产保存，再写入字体引用，并进行保存后重载校验；中断生成可保留原 GUID 修复。
- 广播：全舰队一个语音源和一个通信音源；队列上限 6、事件有效期 7 秒、全局间隔 1.1 秒、相同舰/事件冷却 12 秒、最近 8 条不重复。事件受每渲染帧 24 条表现预算约束；正在说话舰船爆炸的中断不受该预算约束。
- 实际音频路由：两个 `AudioSource` 均为二维播放，正式场景的 `OutputAudioMixerGroup` 为空，项目没有新增或接入 `.mixer` 资源。广播音量直接控制两个源（提示音另乘 0.55），已有全局主音量仍由 `PlayerOptions` 控制 `AudioListener.volume`；不能将本实现描述为已经接入独立 AudioMixer 总线或混音快照。
- 台词状态：普通作战与目击台词要求完好说话者；贯穿、求救、失稳台词要求待爆说话者。爆炸及逃脱舰不产生新实时发言，旧队列在状态变化后丢弃。队列带本局代号，舰队重置时清空。
- 位置优先级：读取统一舰队根位置与事件位置，结合距离、画面内可见性和紧急程度。距离加权使用统一尺度配置换算 100 km。
- 剧情：Timeline 自定义轨道调度广播、镜头与自动驾驶启用指令；位置只由既有 DropletMotor 按配置巡航速度推进。镜头只平滑水滴相对偏移，不对高速飞行的绝对世界坐标做滞后插值。完成和跳过共用一次完成事件，Mission 负责统一战斗就位与扫掠历史清理。
- 操作：Enter 从 Ready 开始剧情；Tab 跳过（剧情暂停时也可跳过）；Esc 暂停/继续；N 从任意状态重开并重播剧情；R 从本批场景任意状态重开并直接战斗；H 显示最近记录。主键盘 `-` 降低广播音量，`=` / `+` 所在键提高音量，每次 10%；未绑定数字小键盘加减键。内部保留最近 40 条，历史面板显示最近 10 条。

专项测试位于 `NarrativeRadioAssetTests`（文本/真实音频资产）和 `NarrativeRadioTests`（状态过滤、去重、队列/过期、爆炸中断、暂停、旧局事件、Timeline 自然结束及连续重开）。必须由主集成代理在实际 Unity Test Runner 运行后才能标为通过；源文件的存在不作为验证证据。
