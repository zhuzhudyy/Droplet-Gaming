# Fleet Assault 试玩说明

打开 Unity 场景 `Assets/_Project/Scenes/FleetAssault.unity`，按 Play，再按 Enter 或点击 **BEGIN SORTIE**。正式场景已经保存，进入 Play 前就能看到和编辑40艘独立舰船。TestRange 保留十船回归用途。

独立版路径：`Builds/Windows-v0.2.2-G09/DropletPrototype.exe`。完整目录必须一起保留；压缩包解压后运行，不能只复制exe。构建与独立运行已完成验证，详见 STATUS.md。

操作：鼠标转向；W / S 调巡航速度；A / D 横向微调；左 Shift 冲刺；Space 刹车；Esc 暂停/恢复并释放鼠标；暂停或结算时 R 重开。鼠标在游戏运行时锁定；切出窗口会暂停。

240秒内贯穿全部40艘舰船即胜利，水滴不会被摧毁。出生正前方4艘是练习连穿线，之后按琥珀色方向标记选择上下/两侧舰群。6秒内连续击毁提高倍率，最高5倍。边界警告后应转回舰队；超出外边界会安全回收，保留得分和已摧毁目标。

准备/暂停/结算界面的 SETTINGS 可调整鼠标灵敏度、反转Y、视野、总音量、减弱镜头运动，以及 OFF/LOW/HIGH 表现预算。默认减弱镜头运动；效果关掉仍可完整计分与结算。界面当前为英语，已用内置字体实际检查。

人工最终验收：试玩一次鼠标转向、冲刺后刹车回转及追踪最后一艘；用真实键盘确认Esc/R；聆听轻/重贯穿音量，按偏好调节。自动路线、输入注入和截图不能替代个人手感与听感。

开发入口：Unity 菜单 `DropletPrototype/Create or Update Fleet Assault`。先保存手工改动；导入工具按稳定ID更新 GeneratedFleet，保留外部手工内容和同ID实例。Blender编辑源与复现命令见 `ArtSource/Blender/DELIVERY.md`。

可重复性能/流程验证（会操控本次运行，结束回到Ready）：

```powershell
& '.\Builds\Windows-v0.2.2-G09\DropletPrototype.exe' -droplet-benchmark -droplet-benchmark-output '.\docs\verification\G05-G09\PlayerBenchmark'
```

该入口仅在显式参数下启用，使用现有任务和运动组件执行测试路线，输出独立时间戳目录。没有这些参数时是普通试玩游戏。`-droplet-benchmark-quit` 可在完成后自行退出此测试玩家。


