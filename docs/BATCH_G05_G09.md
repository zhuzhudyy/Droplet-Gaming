# G05–G09 执行记录

开始：2026-09-07。授权连续完成，最终集中交付，停止于 G09。

## 当前状态
- 基线：Unity 6000.5.10f1 / URP 17.5.0 / Input System 1.20.0；TestRange 已保存、无未保存修改。
- 实际重新运行旧测试：EditMode 3/3、PlayMode 18/18，XML 在 `verification/G05-G09/baseline-*.xml`。
- G05–G08 已实现并通过自动化及画面检查；可编辑Blender源、实际FBX/WAV、40舰正式场景均存在。主观手感与听感待用户集中试玩。
- G09 最终v0.2.2构建及独立运行通过（0错误/1可选开发桥警告）；Unity回归15/15、27/27。完整Windows目录和ZIP已产出并逐文件hash/CRC检查通过；本批停止于G09。
- 最终入口、资产范围和人工局限见 [DELIVERY_G05_G09.md](DELIVERY_G05_G09.md)，详细数据不复制到主记录。

## 分工和接口
- 主代理：现有运行时兼容扩展、Editor 导入/生成工具、所有 Unity 场景/Prefab/材质/配置/构建设置最终写入，界面、环境、验证与打包。独占共享 Editor 状态。
- A `blender_assets`：仅 `Tools/Blender`、`ArtSource/Blender`。FBX 暂存 `ArtSource/Blender/Exports`，主代理导入。可编辑 `.blend` 不在 Assets 内。
- B `presentation`：仅新 `Scripts/Runtime/Presentation`、`Tools/Audio`、`ArtSource/Audio`。实际 WAV 交由主代理导入。
- C `regression_review`：仅新 `FleetAssetTests.cs` / `PresentationRegressionTests.cs` 和本批 review.md；主代理统一运行测试。
- 游戏根单位缩放，Unity +Z 舰首/+Y 向上；VisualRoot 可替换，HitVolumes 独立。Frigate 约 9×5×22，Cruiser 19×9×34，Command 模块变体，Droplet 长 2.4；实际尺寸以校准报告为准。
- FBX 布局 Empty `SPAWN_Small_001` / `SPAWN_Large_...` / `SPAWN_Command_...`。40 船、非随机分组。Importer 使用导入后的完整层级变换，不猜 Euler 交换，先验证再变更；保留同 ID 实例。
- `ShipHitContext(point,direction,speed)`、`ShipTarget.LastHit`，保留参数为空的 TryDestroy 和原 Destroyed 事件。Destroyed 各订阅者异常隔离。新增 Restored、Mission.Restarted/StateChanged、Motor.Teleported，供表现清理；不改已有扫掠和结算顺序。
- B：EffectSettings、MissionEffects（Quality/ActiveEffectCount/ActiveAudioCount/Capacity/PoolInstanceCount/ResetEffects/InitializePool）、DestructionPresenter、DropletTrail。残骸无碰撞和目标组件，预算满只省略表现。

## 关键决定
- TestRange 的灰盒 Prefab、默认任务与回归用途保留。正式资产/材质独立目录，不破坏旧材质数量检查。
- 新正式任务使用独立配置；UI 目标数取实际 membership，不硬编码十船。
- 旧 `Builds/Windows` 保留，新构建使用明确版本子目录并交付完整 ZIP。
- 独立玩家、Unity测试、画面观察和主观体验分别记录；不可互相替代。

## 后续集成点
（下列为开始时约定的顺序，执行结论见后续记录。）
1. 校准 FBX 导入/尺度/方向/法线/标记变换。
2. 首艘实模 + 完整命中、计分、表现、重开。
3. 40 舰 FleetAssault、重复导入和场景契约测试。
4. 真实画面检查、设置、全套回归、性能、独立构建运行与压缩包。

## 集成点 1–2（已实际执行）
- Blender 第一次校准发现 Empty 重复转换；第二次发现未刷新依赖图导致零位姿；修复为源 Empty + 临时轻量 mesh pose proxy，第三次 Unity 校准通过。原始模型和可编辑布局已实际输出，不是仅脚本。
- 实际导入水滴、护卫舰、巡洋舰、指挥变体及各6块残骸；FleetAssault 已保存40真实Prefab实例。每型完整模型单Renderer/共享材质，单位根与HitVolumes分离。
- 自定义冷色反射Cubemap、星场静态Mesh、原创程序化星球shader及URP材质已接入。Game摄像机截图与实际Editor Game界面均已查看，水滴有银色反射、舰船无丢材质；待继续侧面与命中近景检查。
- 实际点击开始成功进入Playing；完整游戏PlayMode回归 **27/27**，包括生产40舰3轮贯穿胜利/重开，关闭/耗尽效果预算、坏表现回调、暂停冻结、尾迹瞬移清理。
- 修复首次运行发现的MaterialPropertyBlock构造器初始化异常。证据 `playmode-integrated-27.xml`。
- 新EditMode测试首轮12例因Unity TestRunner的临时untitled scene与additive fixture冲突而失败（旧3例通过）；正在修fixture，保留失败XML，没有跳过或删除断言。
- 视觉/声音资源已接入；主观飞行手感和听感仍需最终用户试玩，不以测试替代。

## 集成点 3（正式舰队与导入）
- EditMode全套 **15/15** 通过：4实模精确bounds/normals、40条FBX pose与独立Blender期望逐项一致、重复导入保留实例ID、duplicate/unknown/非单位scale预检、手工内容保留、保存重开；旧3用例保留通过。证据 `editmode-integrated.xml`。
- fixture修复为识别Unity TestRunner自己的默认Camera/Light shell并在结束恢复干净shell，未覆盖原用户场景、未修改旧断言。
- 实际Unity比较模型局部尺寸后细化护卫侧引擎、巡洋舰侧长矛、指挥舰舰首/冠部/侧翼HitVolumes。只调整简单盒子，不改扫掠逻辑；还需针对最终参数重跑PlayMode。
- 正式任务240秒，40舰(29护卫/10巡洋/1指挥)，combo6秒，警告半径610m/回收730m；灰盒120秒配置保留。
- Editor 1080p初次采样真实完成40舰胜利、暂停、3次重开，池441对象稳定；采样路线的“单段多舰”检查选错近垂直目标只击中1舰，已改用明确水平共线的001–003，保留失败报告并重跑。数值仅Editor数据，不代替独立版。

## 集成点 4（画面、回归、性能准备）
- 已实际观察真实Game界面：开始、飞行、结算与重开；另使用标注的侧后方检查镜头观察实际事件创建的残骸。发现冲击网格无UV而材质按UV裁切导致不可见，已修为显式网格/材质契约；继续把硬多面闪光改为交叉柔边面片，限制局部暖色范围。
- 最终Prefab碰撞轮廓/渲染代码之后再跑：EditMode **15/15**、PlayMode **27/27**，证据 `final-editor.xml` / `final-playmode.xml`。
- 已完成的Editor 1080p High流程采样全部检查通过：单段3舰、40舰胜利、结算一次、暂停、3重开、41次无伤瞬移；池441对象稳定，效果峰16/音源6。最新完整Editor证据 `EditorBenchmark/20260907-121520-484`（柔边闪光最终版将在独立玩家再观察）。
- Windows新构建已排队：`Builds/Windows-v0.2.0-G09`，version0.2.0，1920×1080窗口，非Development，显式FleetAssault。旧Builds/Windows保留；当前还不能称构建成功或独立运行通过。

## 集成点 5（Windows实跑及修复）
- v0.2.0和v0.2.1均已实际构建并独立运行，20项路线/截图检查全部通过，包括40舰胜利、单段3舰、暂停、3重开、41次无伤回收；各自时间戳报告保留于PlayerBenchmark。
- v0.2.1实测1080p PC/High正常/密集贯穿p95约2.174/2.185ms，全部前台采样，GC recorder不可用不冒充零分配；441池对象稳定。这是短路线的帧间隔数据，不是GPU时间或显示器刷新率。
- 实际点击Settings、改变FOV并恢复默认65；ON/OFF、High选中状态和界面文字已查看。目标提示在明亮星球前偏暗，修复为独立亮琥珀色style与深色底，v0.2.2再验。
- 修复后全套Unity测试再次15/15、27/27，结果为release-editor.json/release-playmode.json；v0.2.2构建0错误1可选桥警告、16.237秒，windows-v0.2.2-build.json。旧版本保留。
- 一次v0.2.1构建遭工具/运行时illegal byte sequence，重新编译和完整测试后原生构建成功，保留windows-final-build-failed-first.json。没有关闭Editor或禁用约束。

## 最终交付（v0.2.2）
- `PlayerBenchmark/20260907-123115-512`：全部20条记录通过（含7条截图落盘），40舰胜利、单段3舰、暂停、3重开、41次无伤瞬移；7132/6587帧全部前台，正常/密集p95 2.126/2.192ms；池441不增，GC未测。详见PERFORMANCE_G09.md。
- 主代理及独立审查实际查看最终飞行、局部冲击/残骸截图，亮琥珀提示与深色底已解决星球背景对比问题；尾迹近镜头折线作为小型美术局限记录。没有以截图代替碰撞测试。
- 完整运行目录 `Builds/Windows-v0.2.2-G09`；压缩包 `Builds/DropletPrototype-v0.2.2-Windows.zip`，45,752,309 bytes，198个运行/说明文件逐个SHA256与源目录一致、ZIP CRC通过。只排除DoNotShip符号且原目录保留。报告package-verification.json。
- 未解决的必需资产/编译/构建环境阻塞：无。待人工：真实键盘/鼠标、听感、路线难度与舒适度；未做GPU时间、独立GC、长期浸泡或跨硬件矩阵。保留Editor干净FleetAssault，停止G09。
- 最终另做普通交互：点击BEGIN后自动巡航4舰/1000分；切窗口触发Paused；点击RESTART回到Ready/240秒/0分/40舰。最终玩家留在Ready，原生交互记录与3图为v0.2.2-native-*。键盘注入Esc仍未确证，不冒充物理键盘已过。
