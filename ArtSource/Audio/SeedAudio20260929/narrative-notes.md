# 英语对白修订准备 — 2026-09-29

本批已写好 87 条英语对白及对应中文字幕，尚未提交合成、试听、混音或导入。全部角色和稳定 ID 沿用前版；开场扩写，战斗对白缩短。未改 2000 舰、5400 秒任务、任何玩法参数或事件逻辑。

## 内容与长度

四幕各保留 10 条，E001–E040 的顺序、stage 0–3 和 cameraShot 0–3 不变。第一幕增加公众期待与工作中的船员，第二幕展开接触协议和观测的不确定性，第三幕围绕阵型风险与独立撤离进行实际争论，第四幕收紧到警告、反射风险、延迟反应堆故障和首次接触。新增内容通过正常对白延长开场，不采用拖慢音频或填充静音。

| 项目 | 旧版 | 本次文本准备 |
|---|---:|---:|
| 开场对白 | 40 条 / 899 英语词 | 40 条 / 1,356 英语词 |
| 事件对白 | 36 条 | 36 条，缩短到关键观察和行动 |
| 舱内代表及背景短回应 | 11 条 | 11 条，全部重写 |
| 新增短语音 ID | — | 0，保留最多 24 条的后续空间 |
| 开场干声 | 实测 347.9465 秒 | 按旧角色语速估计 524.960 秒 |
| 开场完整混音 | 实测 387.921583 秒 | 沿用旧混音开销估计 564.935 秒，约 9 分 25 秒 |
| 全部英语计费字符代理 | — | 9,772 字符 |
| 字符数 × 0.135 AFP 代理估算 | — | 1,319.220 AFP |

估算不是生成结果或实际账单。开场预测使用 `generation-manifest.json` 内旧 E001–E040 的实际时长与各角色词数，得到角色各自秒/词，再加入旧混音与干声相差的 39.975083 秒。新背景回应更短，实际混音开销可能下降；新表演节奏也会变化。完成新干声与混音后，必须重新测量是否落在 480–600 秒目标范围内，并检查字幕同步。`context_texts` 指令与中文字幕不计入本表英语字符代理，提交前的保守预留、返回用量、不确定提交仍由批次账本管理。

B001–B036 保留全部事件绑定。贯穿、临界与求救条目保留 `pendingOnly=true`；舰爆后文本由存活观察者报告，不让已爆舰继续说话。撤离确认不宣称已经越过边界，逃脱文本只在既有 ShipEscaped 事件触发。移除了旧 B023 容易让人误以为又发生一次爆炸的“Secondary flash”表述。求救与穿防护服仍是既有舰内叙事，不新增玩家救援、呼吸、受伤或弹药机制。

## 可供批次管线消费的结构

`narrative-revision.json` 是本次唯一对白源，权威正文位于 `lines`。`groups` 只存 ID 顺序，不重复正文。每条含：

- `id` 和 `originalId`：两者与旧 ID 完全相同。
- `english`：送往 Seed-TTS 2.0 的英语对白；`text`：中文字幕，保持项目旧命名语义。
- `role`、`channel`、`speaker`、`direction`：保持旧角色、音色和情绪枚举。`performanceNote` 给出该条具体表演重点，完整生成指令已经组成 `context_texts` 数组。
- `stage`、`cameraShot` 或 `eventKind`、`pendingOnly`、`weight`、`important`：从旧内容原样继承，不增加第二套事件。
- `group`、`use`：开场、战斗或辅助干声的用途；`speech_rate=0`。
- `original`：旧英语、中文、生成 job、原干声路径、文件 SHA256、旧时长和完成状态，供版本追溯，不能误当新请求正文。
- `estimate`：词数、字符代理、AFP 估算、按旧角色语速得到的时长。全部为 `isMeasured=false`。
- `generationStatus=not_submitted`、`subjectiveReview=not_performed`：明确内容准备状态。

顶层 `take=seed-all-audio-voices-en-20260929-r1` 与旧 `cinematic-radio-en-20260919` 区分；新请求采用此 take，保留旧缓存，不原位改写成功 job。顶层 `timingPolicy` 要求按新音频重建全部时间；不包含旧生成结果的 `duration`、`time` 或旧 `captions`。

## 与旧工具的准确差异

旧 `Tools/Audio/EnhancementEnglish.json` 提供 40 条 narrative + 36 条 combat；`Tools/Audio/cinematic_audio.py::EXTRA` 再加入 SC01、SC02、SA01、SA02、SE01、SE02、SE03、BG01、BG02、BG03、HELP，共 11 条。`ArtSource/Audio/CinematicAudio/generation-manifest.json` 记录这 87 条已完成干声，全部旧 WAV 在本次只读核验中存在，SHA256 已绑定。

旧 `cinematic_audio.all_lines()` 将三组拼接为 87 条；本次 `lines` 顺序严格对应同一序列。重构旧内容对象时，按 `groups.narrative/combat/extra` 的 ID 顺序从 `lines` 选择即可。当前旧 `content()` 仍读取旧文件和硬编码 EXTRA，旧 `direction()` 仍动态拼提示词，旧 `plan()` 固定旧 take。因此不能仅调用原来的 generate 命令就认为生成了本次版本；新批次入口必须显式读取本 JSON 的英语和 `context_texts`，并使用新 take。87 条、9,772 字符仍低于旧工具 88 请求/16,000 字符上限，但旧流程没有本批所需的跨重启 AFP 账本，不能直接沿用它作为预算保障。

旧 `mix()` 实际选 40 开场 + 36 战斗 + SC01/SA01/SE01/HELP，共 80 条主通讯。SC02、SA02、SE02、SE03 和 BG01–BG03 是这些段落内的辅助人声，不是另加七个独立触发事件。相同关系应保留。旧 make_scene 还从 `CinematicAudio/Effects` 读取程序音源；未换成通过验收的 Seed Audio 音效之前，不应重新制作并标记为“全 Seed”。

旧 `mix()` 基于每个新 WAV 的实际时长排主对白、背景回应和字幕，再按段落时长加 0.16 秒更新开场游标；本次仍可采用这种时间驱动关系，但不能复用旧 `CinematicRadioContent.json` 里的时间。较长的开场台词应按句或短语切分中文字幕，字幕与最终英语表演核对，不能把整段翻译始终挤在屏幕上。

## 已检查与未执行

已读取 STATUS、VOLCENGINE_VOICE、旧对白源、生成清单和实际混音代码；已核对当前 Unity 6000.5.10f1、URP 17.5.0、Input System 1.20.0、Timeline 1.8.13、Test Framework 1.7.0，未更改版本。

文件级检查确认 87 个唯一 ID 与旧清单逐一对应，所有英语和中文均已改写，角色、speaker、stage、cameraShot、事件筛选字段没有变化，87 条旧干声文件均存在且绑定哈希，新增语音数量为 0。JSON 可以按 UTF-8 解析，目标时长与 AFP 只作估算。

未执行任何云生成、听感验收、音频后期、Unity 写入、编译、EditMode/PlayMode 测试或玩家构建；本准备工作的付费请求数为 0。实际生成后仍需核查英语自然度、短警示清晰度、字幕对应及全开场时长。本子任务仅交付两个暂存文档，没有改旧源、Assets、场景、STATUS 或实施计划。
