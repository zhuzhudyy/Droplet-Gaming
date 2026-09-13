# DropletPrototype — 灰盒试玩说明

## 启动

- Unity 版本：6000.5.10f1；保留现有 PC_RPAsset / URP 17.5.0。
- 打开 `Assets/_Project/Scenes/TestRange.unity`，按 Unity 的 Play，再按 **Enter** 或点击 **BEGIN FLIGHT**。
- 独立版本输出：`Builds/Windows/DropletPrototype.exe`。运行时请保留同目录下的 Data 文件夹、UnityPlayer.dll 等全部构建文件。
- 准备界面不计时。目标是在 **120 秒**内贯穿全部 **10 艘**静止舰船。水滴不会损毁，也不会被舰船挡停。

## 操作

| 操作 | 按键 |
|---|---|
| 开始 | Enter / BEGIN FLIGHT 按钮 |
| 左右、上下转向 | 鼠标 |
| 提高 / 降低巡航速度 | W / S |
| 左右横向微调 | A / D |
| 冲刺 | 按住左 Shift |
| 刹车至停止 | 按住 Space |
| 暂停 / 恢复，释放 / 锁定鼠标 | Esc |
| 从暂停或结算重新开始 | R / RESTART 或 TRY AGAIN 按钮 |
| 关闭独立版本 | 窗口关闭按钮 / Alt+F4 |

重开回到 Ready，需再次按 Enter。若暂停时仍按住飞行键，恢复后先松开再按，防止遗留冲刺或转向输入。失去窗口焦点会暂停。

先沿中央航道练习，四艘舰船依次排列；再尝试左右两组。转弯前用 Space 降速，金色提示指向最近存活舰船，BEHIND 表示目标在身后。青色中心标记是视线参考，击中判定使用水滴的实际运动路径。

## 规则与调参

- 每艘舰船基础分 100。4 秒内连续击毁递增倍率，最高 x5。胜利额外奖励剩余整秒数 × 10。
- 同一模拟步中，先限制运动时长不超过剩余时间，处理整步全部命中，然后判定全部摧毁，最后判定超时。因此最后一击和倒计时同时结束时算胜利，结算只发生一次。
- 活动范围以 `(0, 8, 100)` 为中心，230 米处提醒，超过 280 米回收到起点。回收保留舰队、分数和剩余时间；瞬移不扫掠伤害。
- 调参资产：`Assets/_Project/Data/DefaultSettings.asset`。速度、冲刺、刹车、鼠标灵敏度、转向、镜头、计时和连击均在此。
- 舰船：`Assets/_Project/Prefabs/Ships/GrayboxShip.prefab`；水滴：`Assets/_Project/Prefabs/Player/Droplet.prefab`。游戏根节点单位缩放；模型位于 VisualRoot，舰船碰撞体位于 HitVolumes。

## 编辑场景与重复生成

菜单 **DropletPrototype > Create or Update Test Range** 可重新生成 `GeneratedTestRange` 下的内容。范围外的手工根对象保留。工具会拒绝存在未保存场景修改的情况，请先自行保存。SampleScene 不会被覆盖。

`GeneratedTestRange` 是工具管理区，手工场景内容应放在其外。已存在的模型/材质资产会复用；工具维护 Prefab 的必要组件及引用。修改 Prefab 的 VisualRoot 可以替换外形。生成器不在运行时生成关卡。

菜单 **DropletPrototype > Use Test Range as Build Startup Scene** 将 TestRange 放在构建场景第一位并保留其他条目。本次 Windows 构建显式只打包 TestRange。

## 检查记录

实际 Unity 编译、EditMode 3/3、PlayMode 18/18 以及现场输入冒烟结果见 [STATUS.md](STATUS.md) 和 [verification/](verification/)。图像仅证明画面状态；碰撞、暂停、胜负和重开另有实际测试。

已验证的现场输入包括 Enter、W/Shift 加速、穿船计分、Space 刹停、D 横移、鼠标无滚转转向、Esc 暂停及 R 重开。需要你试玩确认的内容：鼠标舒适度、镜头距离、刹车/转向手感，以及是否希望 HUD 中文化。当前灰盒 HUD 使用英文。

上面的完整输入链路在 Unity Editor 内验证。独立程序已实际显示贯穿计分、全灭胜利和重开；Windows 自动化注入的 Esc/Enter 未稳定确认，请你聚焦独立程序后用实体键盘检查 Enter 开始、Esc 暂停/恢复、R 重开。这项人工检查仍待完成，不能用截图替代。

本批不含声音、精细美术、复杂破坏效果或 Blender 导入；这些不影响灰盒任务流程。G05 未开始。
