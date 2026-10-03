<span id="371522ec"></span>
# 一、安装和登录 ArkCLI

**环境要求：**  Node.js \>= 16


<Tabs>
<Tab zoneid="rsoH63apHi" title="手动安装并登录">
<TabTitle>手动安装并登录</TabTitle>

<span id="d2ff3bb9"></span>
## 安装 Ark CLI

在终端执行以下命令：

```Bash
# 1. 通过 NPM 安装（下载最新版本）
npm install -g @volcengine/ark-cli@latest

# 2. 验证安装
arkcli --version
```


<span id="4f23966a"></span>
## 登录 Ark CLI

```Bash
# 登录方式
arkcli auth login

# 查看登录状态
arkcli auth status
```



</Tab>
<Tab zoneid="JbXfos3fHj" title="通过 AI Agent 安装并登录">
<TabTitle>通过 AI Agent 安装并登录</TabTitle>

将下方提示词复制给 AI Agent（Claude Code、Codex、Cursor、Trae 等），它会自动完成安装。

```Plain
根据下面命令帮我安装 Ark CLI：https://lf3-static.bytednsdoc.com/obj/eden-cn/psjryh/ljhwZthlaukjlkulzlp/intro/volc.md
```



</Tab>
</Tabs>


<span id="61453f50"></span>
# 二、配置 Agent Plan

在终端中输入命令 `arkcli helper`，根据 TUI 引导选择需要配置的套餐、模型、harness 能力。

现以Agent plan个人版配置Claude Code为例：

<span>![图片](https://arkdoc.tos-cn-beijing.volces.com/images/Arkcli/ap-personal.gif) </span>

<span id="ab2a86b7"></span>
# 三、使用 ArkCLI

<span id="3eb7d109"></span>
## 能力概览

ArkCLI 支持 Agent Plan 相关命令，覆盖 **交易、配置、用量、安全、场景化使用** 五大范围，更多能力见 [ArkCLI 指南](https://console.volcengine.com/ark/region:cn-beijing/docs/82379/2536875?lang=zh)。


* 交易：新购、续费、查询账号下套餐状态

* 配置：配置模型/Harness 能力、查询套餐支持模型、查询配置状态

* 用量：余额查询、用量查询、具体模型/工具用量查询

* 安全：轮换 API Key

* 场景化能力：生图/生视频、Harness 能力使用


<span id="63b87ec5"></span>
## 交易


<Tabs>
<Tab zoneid="pbwuhR3HGk" title="通过 Agent 调用">
<TabTitle>通过 Agent 调用</TabTitle>


<span aceTableMode="list" aceTableWidth="1,2"></span>
|功能 |示例提示词 |
|---|---|
|新购套餐 |"我想买一个 Agent Plan 个人版 Max，帮我下单" |
|续费套餐 |"帮我续费一个月 Agent Plan" |
|查询账号下套餐状态 |"我都订阅了哪些 Plan？是什么规格？" |



</Tab>
<Tab zoneid="mEpAxuspnb" title="通过终端调用">
<TabTitle>通过终端调用</TabTitle>

* 新购套餐：

   ```Bash
   # arkcli plans buy --plan <套餐名> --type <规格> [--duration <月数>] [--yes]
   
   # 参数说明：
   #     套餐名：agent-plan
   #     规格：small/medium/large/max
   #     月数：1-12 月，例如：1，若不传该参数，默认 1 个月
   #     --yes：真实下单确认，若没有 --yes 仅返回订单预览与协议，建议先不加 --yes，预览订单
   
   # 示例命令：
   # 预览：只返回协议链接
   arkcli plans buy --plan agent-plan --type max --duration 1
   # 真实下单
   arkcli plans buy --plan agent-plan --type max --duration 1 --yes
   ```
   

* 续费：

   ```Bash
   # arkcli plans renew --plan <套餐名> [--duration <月数>] [--yes]
   
   # 参数说明：
   # 套餐名：agent-plan
   # 月数：1-12 月，例如：1，若不传该参数，默认 1 个月
   # --yes：真实下单确认，若没有 --yes 仅返回订单预览与协议，建议先不加 --yes，预览订单
   
   # 示例命令：
   # 预览：只返回协议链接
   arkcli plans renew --plan agent-plan --duration 1
   # 真实下单
   arkcli plans renew --plan agent-plan --duration 1 --yes
   ```
   

* 查询账号下套餐状态：

   ```Bash
   arkcli plans get
   ```
   


</Tab>
</Tabs>


<span id="585ad425"></span>
## 配置


<span aceTableMode="list" aceTableWidth="1,2"></span>
|功能 |示例提示词 |
|---|---|
|配置全部 Harness 能力 |"把 Agent Plan 的 harness 能力都配到我本机的 Claude Code 上" |
|仅配置单项 Harness 能力 |"帮我的 Codex 装上豆包搜索的 MCP" |
|移除配置 |"把 claude code 上的豆包搜索 mcp 配置全撤掉" |
|查询配置状态 |"看看我本机哪些 AI 工具已经接了 Agent Plan，MCP 装齐了没" |
|查询套餐支持的模型 |"Agent Plan Max 支持哪些模型？" |


<span id="4ed262df"></span>
## 用量


<span aceTableMode="list" aceTableWidth="1,2"></span>
|功能 |示例提示词 |
|---|---|
|余额查询 |"我这个月 Agent Plan 还剩多少 ？" |
|用量查询 |"我本周使用了多少 ？" |
|Model/Harness 用量查询 |"我本周 doubao\-seed\-2.0\-lite 用了多少 ？" |


<span id="d77d89cc"></span>
## 安全


<Tabs>
<Tab zoneid="UXp3wla86i" title="通过 Agent 调用">
<TabTitle>通过 Agent 调用</TabTitle>


<span aceTableMode="list" aceTableWidth="1,2"></span>
|功能 |示例提示词 |
|---|---|
|轮换 API Key |"帮我轮换 Agent Plan 的 API Key" |



</Tab>
<Tab zoneid="elKaN7o1i9" title="通过终端调用">
<TabTitle>通过终端调用</TabTitle>

<div data-tips="true" data-tips-type="warning" data-tips-is-title="true">注意</div>


<div data-tips="true" data-tips-type="warning">该命令需二次确认，执行后旧 API key 失效。</div>


```Bash
arkcli plans personal rotate-apikey
```



</Tab>
</Tabs>


<span id="963401f9"></span>
## 场景化能力

<div data-tips="true" data-tips-type="tip" data-tips-is-title="true">说明</div>



* <div data-tips="true" data-tips-type="tip">ArkCLI 支持配置 Agent Plan 套餐内的 Harness 能力。对于需要额外安装的 Harness，ArkCLI 可完成对应 Skill、MCP 的安装与配置。</div>


* <div data-tips="true" data-tips-type="tip">相关 Harness：</div>


   * <div data-tips="true" data-tips-type="tip"><a href="https://console.volcengine.com/ark/region:cn-beijing/docs/82379/2479086?lang=zh">专业数据集</a></div>


   * <div data-tips="true" data-tips-type="tip"><a href="https://console.volcengine.com/ark/region:cn-beijing/docs/82379/2301412?lang=zh">豆包搜索</a></div>


   * <div data-tips="true" data-tips-type="tip"><a href="https://console.volcengine.com/ark/region:cn-beijing/docs/82379/2545595?lang=zh">Agent 记忆</a></div>


   * <div data-tips="true" data-tips-type="tip"><a href="https://console.volcengine.com/ark/region:cn-beijing/docs/82379/2545596?lang=zh">AI Native 应用开发底座</a></div>


* <div data-tips="true" data-tips-type="tip">除上述 Harness 能力外，ArkCLI 还内置生图、生视频等命令，可通过 Agent 或终端直接调用。</div>




<Tabs>
<Tab zoneid="ZpmQCqMtKp" title="通过 Agent 调用">
<TabTitle>通过 Agent 调用</TabTitle>

可直接通过提示词说明生图、生视频要求，具体能力可见 [Agent Plan 支持视觉模型能力](https://console.volcengine.com/ark/region:cn-beijing/docs/82379/2375486?lang=zh#7a1c4e9b)。

**生图：** 

```Bash
"帮我使用 AgentPlan 生成一张 4k 大图，一只赛博朋克风格的猫咪坐在东京街头，霓虹灯反射"
```


**生视频：** 

```Bash
视频生成："生成一段 5 秒 16:9 视频：无人机航拍雪山日出，缓慢右移，同步等结果"
查询列表："目前我都有哪些视频正在生成中？"
查询进度："刚刚的视频生成好了吗？"
取消任务："取消刚刚的视频生成任务"
```



</Tab>
<Tab zoneid="TsLJY3qhzc" title="通过终端调用">
<TabTitle>通过终端调用</TabTitle>

**1. 生图**

```Bash
arkcli +gen "赛博朋克风格的猫咪坐在东京街头，霓虹灯反射"
```


```Bash
arkcli +gen "赛博朋克猫咪，霓虹灯反射，超写实" \
  --model doubao-seedream-5.0 \
  --size 1024x1024 \
  --image-count 4 \
  --save-to ./out
```


**图生图：** 

```Bash
arkcli +gen "把这只猫改成水彩画风格" \
  --input @./cat.png \
  --model doubao-seedream-5.0 \
  --save-to ./out
```


**2. 生视频**

```Bash
# 生成视频
# 方式一：异步
arkcli +gen "无人机航拍雪山日出，缓慢平移" --modality video --duration 5 --ratio 16:9
# 方式二：同步
arkcli +gen "无人机航拍雪山日出，缓慢平移" --modality video --duration 5 --ratio 16:9 --wait --save-to ./out

# 查单个视频任务（成功后自动下载）
arkcli gen get cgt-20260708-xxxxxxxx --save-to ./out

# 列出最近任务（分页）
arkcli gen list --page-size 10

# 删除任务
arkcli gen delete cgt-20260708-xxxxxxxx --yes
```



</Tab>
</Tabs>




