# 画面证据说明

最终版优先看 `normal-flight-final-sampled.mp4`：接触亮点修复后，实际重新进入保存的2000舰增强场景，用隔离的空白键鼠设备保持正常前进，完全由实时FixedUpdate/激光AI驱动，没有 FireRay、伤害/逃跑命令、相机或Motor位姿修改。约每0.15秒一帧，112帧，最后模拟18.260秒/30km/s、4艘击毁和1艘待爆。FFmpeg使用 `NormalFlightFinal/frames.csv` 记录的壁钟差构成采样回放。重复入口为 `Tools/Enhancement/CaptureNormalFlight.cs`，只在显式Editor验证调用。独立版最终验收/性能数据使用 `PlayerAccepted`（22/22、退出0）；`PlayerFinal`是此前通过版，`PlayerRelease`为保留的观察窗不足21/22中间轮。

- `normal-flight-sampled.mp4`：Unity 保存的正式 2000 舰增强场景，正常实时 FixedUpdate 前进贯穿与舰船射击。约每 0.3 秒保存一帧，再用 FFmpeg 编为采样回放，非全帧性能录像。普通第三人称，FOV 65，HUD 30 km/s。没有调用 FireRay/ForceFlee/ApplyDamage；转向通过 InputSystem 注入。该次刹车输入未生效，实际仍在高速飞行，不把它当作固定机位逃跑证明。`EditorNormalTurn/frame-040.png` 清楚呈现蓝白入射、边缘接触与橙色反射；早先帧006暴露的接触残留另行修复。
- `escape-observation-sampled.mp4`：为了把受威胁邻舰转入画面，以 0.02 秒 `Mission.Step` 输入普通 forward / brake / look 命令，贯穿后刹车并转向，然后保持相机；未调用任何舰船伤害或逃跑调试指令。前段待爆舰遮挡镜头，爆炸后相同邻舰目标提示从中央向右移动。目标 `SPAWN_Small_FS2C25R01`，权威位移在 `EditorEscapeTurn/positions.csv`，保留原始PNG。此补充取景使用脚本化飞行命令，不冒充人工操控；正常 FixedUpdate 与近远景同步以独立版 `PlayerFinal/report.json`、`normal-escape.json` 为正式证据。视频仅采用前49帧，后续退出Play附近的重复帧不纳入。
- 独立版 `PlayerFinal/reflection-third-person.png` 为一次8束受控光学压力图，水滴停止，舰队模拟继续。不得将其描述为自然战斗画面。
- `PlayerFinal/sun-volume-off.png` / `sun-volume-on.png` / `sun-offscreen.png` / `sun-occluded.png` 为固定曝光、同FOV下的太阳检查。遮挡图使用原有舰体的临时视觉副本，不是新目标，不改变2000舰身份；取景后销毁。第一轮 `Player/sun-occluded.png` 的默认材质不适合作为最终证据。

视频无音轨，不能用于听感验收；不用于宣称游戏只有3–4FPS。游戏性能以独立版逐帧CPU时间差、Unity FrameTiming及PID显存CSV为准。截图、取景与FFmpeg编码不在性能测量窗口同时执行。
