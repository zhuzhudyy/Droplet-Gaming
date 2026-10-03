# Seed 通讯离线重混

`Tools/Audio/seed_remix.py` 只处理已返回的 Seed 音频，不访问云接口，不写 Unity `Assets`。旧 `ArtSource/Audio/CinematicAudio/Scenes/*/mix_recipe.json` 仅提供场景顺序、声部、相对事件时机和透视方向；其中 25 个程序声效 WAV 从不进入新音频。旧 80 条成品的覆盖为 E001–E040、B001–B036、SC01、SA01、SE01、HELP；B001–B036 及三条 S* 通讯另有 39×2 条强度变体。新英语、中文字幕和相对字幕时序来自修订的 `narrative-revision.json` 与真实 Seed-TTS 干声长度，旧语音仅用于计算时间变换。

E033 首次调用仍处于 `submission_unknown` 且无 WAV；重混工具只使用已完成的 `E033_R2`，并逐字比对它与 E033 修订的英语、音色和上下文指令。发布旁侧来源列表记录 R2 的原始 job/SHA，不引用首次未知请求。

效果映射为旧 DeckSteps/Console/Hatch/Explosion/Laser/Reflect/Alarm/Connect/Disconnect 同名 Seed Audio 家族；旧 Impact 对应 Penetration；旧 Equipment 对应 CabinBed。所有重复的旧四秒 Equipment 底噪改为一条 Seed CabinBed，重复处做交叉淡化。每个输入须有完成的原始 Seed job 和一致的 SHA-256；非语音还必须有 `published/<cueID>.wav` 及旁侧 `accepted:true` 文件。来源校验按原 `job.json` 同目录的 `audio.wav` 进行，不信任历史 job 内可能损坏的绝对路径字段。

常用命令（均从项目根目录执行）：

```powershell
python -B Tools/Audio/seed_remix.py preview --all
python -B Tools/Audio/seed_remix.py render --ids SC01
python -B Tools/Audio/seed_remix.py render --all
python -B Tools/Audio/seed_remix.py pages --all
```

预览只列缺少或尚未验收的来源，不写文件。渲染要求来源齐备；每条草稿位于 `remixed/Scenes/<ID>/<输入指纹>/`，包含 `radio_mix.wav`，适用时还有 `radio_low.wav`、`radio_high.wav`，以及 `mix_recipe.json`。指纹将旧时间模板、修订文本、音频哈希和处理版本一起固定；完整缓存只在输出哈希全部匹配时复用，部分或损坏的输出不静默重做。`remixed/listening.html` 是只指向实际草稿的试听页。长环境循环、声部均衡、对白压低背景、通讯带限和片尾淡出均只处理 Seed 输入；没有合成声源。

完整听过并接受的场景可以标为单条听感验收：

```powershell
python -B Tools/Audio/seed_remix.py publish --ids SC01 --accept-listening --review "Human listened to the complete scene and accepted its speech and effects."
```

为了先形成可玩的 Unity 版本，经过来源、时序、波形和字幕技术检查的场景也可暂发；这不代表逐条人工试听：

```powershell
python -B Tools/Audio/seed_remix.py publish --all --accept-technical --review "Six representative categories accepted by user; source and timing QA passed; individual radio scene listening remains pending."
```

旁侧 JSON 的 `acceptanceBasis: technical_provisional`、`subjectiveListening: not_individually_performed` 如实保留这一限制。后续实际试听若发现问题，应在新版本中修订，不能把技术暂发改写成人耳验收。

发布位置是 `published-radio/<ID>.wav` 和适用的 `<ID>_low.wav` / `<ID>_high.wav`。各 WAV 有同名 JSON：`accepted`、成品哈希、Seed 来源 job 与原始 SHA、全部来源列表、真实成品时长、修订英语/中文字幕及分句时序。已有发布文件若内容不同则拒绝覆盖。Unity 编辑器只能从此发布目录导入至新的 Seed 音频目录，旧版资源保留。

用户已试听并接受激光、贯穿、爆炸、飞行、舱室、配乐六类代表。131 条非语音成品、87 条新版干声和 158 条通讯（80 主版、78 强度变体）现已完成，全部由 Unity Editor 正式导入到安全复制的新场景。`remixed/technical-preview/` 保留早期不完整的 SC01 原型，仅供历史对照，不供 Unity 使用。158 条正式通讯位于 `published-radio/`，技术暂发不代表完整通讯内容和表演已通过逐条试听。

2026-09-29：`python -B -m unittest discover -s Tools/Audio -p test_seed_remix.py -v`，9/9 通过（含 E033_R2、Laser_03_R2 和 158 输出契约）。测试 WAV 是临时目录的算法样本，只验证哈希阻断、时间重排、循环、缓存和发布契约，不算真实云生成或主观试听。

独立只读审查逐一核实了 87 条修订 Seed-TTS 干声的完成状态、文本/音色/指令和 SHA；80 条旧通讯时间模板用真实新干声重排后，无字幕超出成品或台词重叠。40 条开场主片连 0.16 秒句间隙和末尾余量合计约 561.44 秒。25 个旧程序源均映射到 Seed cue；重混中的接通、断线、警报与干扰变体按场景轮替，22 条舰内家族变体均可由真实通讯配方触及。`Laser_03_R2` 以稳定发布 ID `Laser_03` 引用已完成 R2 job，主要供世界战斗音。上述静态/格式验证与 158 条实际渲染、发布均已完成，但不等于主观试听。

## 单条音效的暂存与发布

`Tools/Audio/seed_publish.py` 可处理六类实测代表（`--representatives` 或 `--ids LASER METAL ...`）和 131 条完整目录（`--all` 或指定 cue ID）。`preview` 只核查原始 Seed job、提示词、WAV 与哈希，不写资源；`stage` 从同目录 `audio.wav` 制作一条保留实际声道/采样率的候选。短音只在必要时定位起音、裁切和淡入尾；持续音把真实 Seed 原声首尾交叉混合为循环片段；可用增益和削峰控制电平。不制造新声源、不信任 job 内可能乱码的绝对音频路径。暂存版本按输入哈希隔离，损坏或部分输出不静默重建。

```powershell
python -B Tools/Audio/seed_publish.py preview --representatives
python -B Tools/Audio/seed_publish.py stage --representatives
python -B Tools/Audio/seed_publish.py preview --all
python -B Tools/Audio/seed_publish.py stage --all
```

六类真实代表已完成本地暂存并获用户试听认可；原始音仍在 `raw/`。131 条目录的单条候选尚未逐一人工试听，均以 `acceptanceBasis: user_accepted_six_categories_and_technical_qa` 技术暂发。所有原始请求已完成或以明确的独立来源替换，`preview --all` 显示 131/131 可用。

试听确认后的单条发布示例：

```powershell
python -B Tools/Audio/seed_publish.py publish --ids Laser_01 --accept --review "Human listened to the whole cue, including its attack and tail; accepted."
```

代表音还需 `capability-review.json` 的该类状态确认为 `accepted_after_listening`，否则即使带 `--accept` 也拒绝发布。发布前预检所有选中条目的暂存哈希与同名目标冲突；每个 `published/<ID>.wav` 的 `<ID>.json` 含 `accepted:true`、处理后 SHA、原始 `sourceJob`、原始 `sourceSha256`、模型、提示词及处理配方。目录单条 JSON 另记 `subjectiveListening: not_individually_performed`。2026-09-29：整个 Tools/Audio 离线测试最近一次 64/64 通过，均为算法样本，不能代替云服务调用或听感验证。
