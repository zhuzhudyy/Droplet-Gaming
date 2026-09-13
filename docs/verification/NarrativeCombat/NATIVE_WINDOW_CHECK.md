# 最终UI窗口与只读输入检查

工程构建：Windows-NarrativeCombat，Unity6000.5.10f1，实际1920×1080（Windows工具截图按DPI显示为较小逻辑窗口）。2026-09-12 UTC，输出目录 `NativeInput/`，日志 `NativeInput-player.log`。观察器只读，无输入注入/MakeCurrent/InputSettings改动。

已实际观察和点击：

- 普通Ready入口为2000舰，RadioPanel已隐藏，不再与主菜单叠层；有BEGIN APPROACH和DIRECT COMBAT。
- 点击BEGIN APPROACH后进入Narrative，60秒剧情中的地球电视明确“缓存录播”；剩余90:00、击毁0/2000。
- RadioPanel有完整来源、中文字幕、“正在接收 音量70%”和历史/音量提示，Signal不再裁空。
- 点击剧情PAUSE，状态Paused，面板移到右下，字幕与暂停菜单均可见。
- 点击RESUME，返回Narrative，面板回左下；再点击SKIP，进入Playing，初始0/2000、0待爆、0逃脱，没有跳转沿途伤害。
- 其后正常FixedUpdate巡航经过舰群，窗口实际出现3已爆/2待爆等分离状态及事件求救字幕。
- 使用窗口关闭快捷操作退出，日志有正常InputSystem/Physics清理和observer-stop。

只读输入结果：

`NativeInput/native-input-20260912-135916-564.jsonl`证明原生Keyboard存在，interface=RawInput、native=true、enabled=true、currentKeyboardId=1、窗口focused=true，DropletInputEnabled=true。工具注入Return于13:59:40与Escape于14:00:49时，仅观察到TEXT类型事件，没有对应STAT/DLTA按键按下，也没有Update pressedThisFrame；不能把TEXT当成游戏按键状态。

这将本次未响应定位到“没有供现有按键处理器读取的原生按下状态”边界，排除了Keyboard缺失、保存组件禁用和本次窗口失焦；**并不证明物理键盘故障，也不将WGI游戏手柄初始化日志误认为键盘根因**。旧G04/G05-G09已有相同系统注入限制。InputSystem合成Keyboard/Mouse经原Update/FixedUpdate的冲刺/刹车/转向已另有数值硬验证。物理硬件的人类试玩仍未验证。

窗口图像由实际Windows捕获工具逐次展示并目视核对，本文件是观察记录；不伪称已保存Overlay UI PNG。自动Player目录的PNG是实际URP相机请求，不包含Overlay Canvas。
