方舟 Agent Plan 提供图片生成、视频生成模型，支持通过 Skill、ArkCLI、API 接口调用方式接入并使用生图、生视频能力。

<span id="dd9e9bb8"></span>
# 通过 Skill 接入

在 AI 工具中使用图片生成、视频生成能力，建议使用 Skill 方式接入。

<span id="72de147a"></span>
## 安装视频生成 Skill

通过 Skill 方式，可以快速为 AI 工具安装并接入视频生成能力，目前支持的 AI 工具：OpenClaw、Hermes Agent、Claude Code。视频生成 Skill 信息参见 [byted-ark-seedance-skill](https://findskill.com/volcengine/agentplan/byted-ark-seedance-skill)。


<Tabs>
<Tab zoneid="oOeumjBNfj" title="OpenClaw">
<TabTitle>OpenClaw</TabTitle>

1. 已完成 OpenClaw 的安装及语言模型的配置，具体步骤见 [OpenClaw](https://ark.volcengine.com/region:cn-beijing/docs/82379/2373742?lang=zh)。

2. 在终端执行以下命令为 OpenClaw 安装并接入视频生成 Skill `byted-ark-seedance-skill`。

   ```Bash
   npx skills add https://skills.volces.com/skills/volcengine/agentplan -s byted-ark-seedance-skill --agent openclaw
   ```
   

3. 安装完成后，可以在 OpenClaw 发送视频生成的提示词，触发并使用生成视频能力。


</Tab>
<Tab zoneid="ltwUqMxiS7" title="Hermes Agent">
<TabTitle>Hermes Agent</TabTitle>

1. 已完成 Hermes Agent 的安装及语言模型的配置，具体步骤见 [Hermes Agent](https://ark.volcengine.com/region:cn-beijing/docs/82379/2318283?lang=zh)。

2. 进入 [byted-ark-seedance-skill](https://findskill.com/volcengine/agentplan/byted-ark-seedance-skill) 页面下载 ZIP 压缩包，将解压后的文件粘贴至`~/.hermes/skills/`目录下。

3. 配置完成后，可以在 Hermes Agent 发送视频生成的提示词，触发并使用生成视频能力。


</Tab>
<Tab zoneid="BlrBHG60lN" title="Claude Code">
<TabTitle>Claude Code</TabTitle>

1. 已完成 Claude Code 的安装及语言模型的配置，具体步骤见 [Claude Code](https://ark.volcengine.com/region:cn-beijing/docs/82379/2373740?lang=zh)。

2. 在终端执行以下命令为 Claude Code 安装并接入视频生成 Skill `byted-ark-seedance-skill`。

   ```Bash
   npx skills add https://skills.volces.com/skills/volcengine/agentplan -s byted-ark-seedance-skill --agent claude-code
   ```
   

3. 配置完成后，可以在 Claude Code 发送视频生成的提示词，触发并使用生成视频能力。


</Tab>
<Tab zoneid="vsUmdXjPYp" title="TraeCode">
<TabTitle>TraeCode</TabTitle>

1. 已完成 TraeCode 的安装及语言模型的配置，具体步骤见 [TraeCode](https://ark.volcengine.com/region:cn-beijing/docs/82379/2389869?lang=zh)。

2. 在终端执行以下命令下载 `byted-ark-seedance-skill`。

   ```Bash
   npx skills add https://skills.volces.com/skills/volcengine/agentplan -s byted-ark-seedance-skill --agent trae
   ```
   

3. 安装完成后，可以在 TraeCode 发送视频生成的提示词，触发并使用生成视频能力。


</Tab>
</Tabs>


<span id="aef48a5c"></span>
## 安装图片生成 Skill

通过 Skill 方式，可以快速为 AI 工具安装并接入图片生成能力，目前支持的 AI 工具：OpenClaw、Hermes Agent、Claude Code。图片生成 Skill 信息参见 [byted-ark-seedream-skill](https://findskill.com/volcengine/agentplan/byted-ark-seedream-skill)。


<Tabs>
<Tab zoneid="tMFWR9AaDe" title="OpenClaw">
<TabTitle>OpenClaw</TabTitle>

1. 已完成 OpenClaw 的安装及语言模型的配置，具体步骤见 [OpenClaw](https://ark.volcengine.com/region:cn-beijing/docs/82379/2373742?lang=zh)。

2. 在终端执行以下命令为 OpenClaw 安装并接入图片生成 Skill `byted-ark-seedream-skill`。

   ```Bash
   npx skills add https://skills.volces.com/skills/volcengine/agentplan -s byted-ark-seedream-skill --agent openclaw
   ```
   

3. 安装完成后，可以在 OpenClaw 发送图片生成的提示词，触发并使用生成图片能力。


</Tab>
<Tab zoneid="HnMjq5ekCt" title="Hermes Agent">
<TabTitle>Hermes Agent</TabTitle>

1. 已完成 Hermes Agent 的安装及语言模型的配置，具体步骤见 [Hermes Agent](https://ark.volcengine.com/region:cn-beijing/docs/82379/2318283?lang=zh)。

2. 进入 [byted-ark-seedream-skill](https://findskill.com/volcengine/agentplan/byted-ark-seedream-skill) 页面下载 ZIP 压缩包，将解压后的文件粘贴至`~/.hermes/skills/`目录下。

3. 安装完成后，可以在 Hermes Agent 发送图片生成的提示词，触发并使用生成图片能力。


</Tab>
<Tab zoneid="eHyZn49f5i" title="Claude Code">
<TabTitle>Claude Code</TabTitle>

1. 已完成 Claude Code 的安装及语言模型的配置，具体步骤见 [Claude Code](https://ark.volcengine.com/region:cn-beijing/docs/82379/2373740?lang=zh)。

2. 在终端执行以下命令为 Claude Code 安装并接入图片生成 Skill `byted-ark-seedream-skill`。

   ```Bash
   npx skills add https://skills.volces.com/skills/volcengine/agentplan -s byted-ark-seedream-skill --agent claude-code
   ```
   

3. 安装完成后，可以在 Claude Code 发送图片生成的提示词，触发并使用生成图片能力。


</Tab>
<Tab zoneid="ucKWKKX2FJ" title="TraeCode">
<TabTitle>TraeCode</TabTitle>

1. 已完成 TraeCode 的安装及语言模型的配置，具体步骤见 [TraeCode](https://ark.volcengine.com/region:cn-beijing/docs/82379/2389869?lang=zh)。

2. 在终端执行以下命令下载 `byted-ark-seedream-skill`。

   ```Bash
   npx skills add https://skills.volces.com/skills/volcengine/agentplan -s byted-ark-seedream-skill --agent trae
   ```
   

3. 安装完成后，可以在 TraeCode 发送图片生成的提示词，触发并使用生成图片能力。


</Tab>
</Tabs>


<span id="cli-access"></span>
# 通过 ArkCLI 接入

通过 ArkCLI（命令行工具）同样可以接入图片生成、视频生成能力，支持通过 **Agent 调用**或**终端调用**两种方式使用。

<span id="cli-setup"></span>
## 安装与配置

请确保已购买 [Agent Plan 套餐](https://ark.volcengine.com/region:cn-beijing/openManagement?LLM=%7B%7D&advancedActiveKey=agentPlan)。安装、登录及配置步骤，参见 [ArkCLI：Agent Plan 个人版使用指南](https://ark.volcengine.com/region:cn-beijing/docs/82379/2656113?lang=zh)。

> **注意**：登录 ArkCLI 时需要选择消费模式为 `agent-plan`。


配好 Agent 工具后即可开始使用。

<span id="cli-usage"></span>
## 使用示例


<Tabs>
<Tab zoneid="hHbtS6WIJb" title="通过 Agent 调用">
<TabTitle>通过 Agent 调用</TabTitle>

在 Agent 对话中直接说明生图、生视频要求即可调用，建议**在提示词中注明“不要使用后付费”** 。

**1. 生图**

```text
请使用 Agent Plan 的 doubao-seedream-5.0-lite 模型生成一张 4K 大图：一只赛博朋克风格的猫咪坐在东京街头，霓虹灯反射，不要使用后付费
```


**2. 生视频**

```text
请使用 Agent Plan 的 doubao-seedance-2.5 模型生成一段 5 秒 16:9 视频：无人机航拍雪山日出，缓慢右移，同步等结果，不要使用后付费
```



</Tab>
<Tab zoneid="fYN1WgAX1H" title="通过终端调用">
<TabTitle>通过终端调用</TabTitle>

**1. 生图**

```Bash
arkcli +gen "赛博朋克风格的猫咪坐在东京街头，霓虹灯反射"
```


**2. 生视频**

```Bash
arkcli +gen "无人机航拍雪山日出，缓慢平移" --modality video --duration 5 --ratio 16:9
```



</Tab>
</Tabs>


<span id="b9ddd4f1"></span>
# 通过 API 接入

如果使用的 AI 工具不支持接入 Skill，或者无需在 AI 工具中使用，可通过 API 接入的方式使用图片生成、视频生成能力。

<span id="8d00fec2"></span>
## 核心配置信息

在使用 Agent Plan 时，需要使用专属 API Key、专属 Base URL、支持的模型来调用视频生成、图片生成 API，否则可能会调用失败或产生额外费用。


* **专属 API Key**：[获取专属 API Key](https://ark.volcengine.com/region:cn-beijing/openManagement?LLM=%7B%7D&OpenModelVisible=false&advancedActiveKey=agentPlan)，其他方舟 API Key 如 Coding Plan API Key 无法在 Agent Plan 中使用。

   <span>![图片](https://arkdocs.tos-cn-beijing.volces.com/images/CodingPlan/image-20260908-203322-616.png) </span>

* **支持的模型**：见[支持模型及 Harness](https://ark.volcengine.com/region:cn-beijing/docs/82379/2366394?lang=zh#3d801f5f)。

* **专属 Base URL**：Agent Plan 对应的 API 接口信息中包含`/plan`，请勿混用其他 API 接口。

   * Curl 方式：支持的 API 参数可查看对应的 API 文档。

      
      |视频生成 API |图片生成 API |
      |---|---|
      |* [创建视频生成任务](https://ark.volcengine.com/region:cn-beijing/docs/82379/1520757?lang=zh)：`https://ark.cn-beijing.volces.com/api/plan/v3/contents/generations/tasks`<br><br>* [查询视频生成任务](https://ark.volcengine.com/region:cn-beijing/docs/82379/1521309?lang=zh)：`https://ark.cn-beijing.volces.com/api/plan/v3/contents/generations/tasks/{id}`<br><br>* [查询视频生成任务列表](https://ark.volcengine.com/region:cn-beijing/docs/82379/1521675?lang=zh)：`https://ark.cn-beijing.volces.com/api/plan/v3/contents/generations/tasks?page_num={page_num}&page_size={page_size}&filter.status={filter.status}&filter.task_ids={filter.task_ids}&filter.model={filter.model}`<br><br>* [取消或删除视频生成任务](https://ark.volcengine.com/region:cn-beijing/docs/82379/1521720?lang=zh)：`https://ark.cn-beijing.volces.com/api/plan/v3/contents/generations/tasks/{id}` |[图片生成](https://ark.volcengine.com/region:cn-beijing/docs/82379/1541523?lang=zh)：`https://ark.cn-beijing.volces.com/api/plan/v3/images/generations` |
      

   * SDK 方式：

      专属 Base URL：`https://ark.cn-beijing.volces.com/api/plan/v3`

       &nbsp;


<span id=".6LCD55So6KeG6aKR55Sf5oiQLWFwaQ=="></span>
## 调用视频生成 API

参见[视频生成教程](https://ark.volcengine.com/region:cn-beijing/docs/82379/2298881?lang=zh)、[Doubao Seedance 2.5 教程](https://ark.volcengine.com/region:cn-beijing/docs/82379/2607688?lang=zh)、[Doubao Seedance 2.0 系列教程](https://ark.volcengine.com/region:cn-beijing/docs/82379/2291680?lang=zh)完成视频生成任务，需注意必须使用 Agent Plan 专属 API Key、专属 Base URL 及支持的模型，否则可能会调用失败或产生额外费用。具体参见[核心配置信息](https://ark.volcengine.com/region:cn-beijing/docs/82379/2375486?lang=zh#8d00fec2)。

通过 Agent Plan 创建视频生成任务的示例如下：


<Tabs>
<Tab zoneid="XwoqnuvP82" title="Curl">
<TabTitle>Curl</TabTitle>

```Bash
curl https://ark.cn-beijing.volces.com/api/plan/v3/contents/generations/tasks \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $AGENT_API_KEY" \
  -d '{
    "model": "doubao-seedance-2.0",
    "content": [
        {
            "type": "text",
            "text": "女孩抱着狐狸，女孩睁开眼，温柔地看向镜头，狐狸友善地抱着，镜头缓缓拉出，女孩的头发被风吹动，可以听到风声"
        },
        {
            "type": "image_url",
            "image_url": {
                "url": "https://ark-project.tos-cn-beijing.volces.com/doc_image/i2v_foxrgirl.png"
            }
        }
    ],
    "generate_audio": true,
    "ratio": "adaptive",
    "duration": 5,
    "watermark": false
}'
```



</Tab>
<Tab zoneid="F2BIvyas5L" title="Python">
<TabTitle>Python</TabTitle>

```Python
import os
from volcenginesdkarkruntime import Ark

client = Ark(
    # Agent Plan 专属 Base URL
    base_url="https://ark.cn-beijing.volces.com/api/plan/v3",
    # Agent Plan 专属 API Key：https://ark.volcengine.com/region:cn-beijing/openManagement?LLM=%7B%7D&OpenModelVisible=false&advancedActiveKey=agentPlan
    api_key=os.environ.get("AGENT_API_KEY")
)

if __name__ == "__main__":
    print("----- create request -----")
    resp = client.content_generation.tasks.create(
        model="doubao-seedance-2.0",
        content=[
            {
                "text": (
                    "女孩抱着狐狸，女孩睁开眼，温柔地看向镜头，狐狸友善地抱着，镜头缓缓拉出，女孩的头发被风吹动"
                ),
                "type": "text"
            },
            {
                "image_url": {
                    "url": (
                        "https://ark-project.tos-cn-beijing.volces.com/doc_image/i2v_foxrgirl.png"
                    )
                },
                "type": "image_url"
            }
        ],
        generate_audio=True,
        ratio="adaptive",
        duration=5,
        watermark=False,
    )

    print(resp)
```



</Tab>
<Tab zoneid="oCo06pzMMl" title="Java">
<TabTitle>Java</TabTitle>

```Java
package com.ark.sample;

import com.volcengine.ark.runtime.model.content.generation.*;
import com.volcengine.ark.runtime.model.content.generation.CreateContentGenerationTaskRequest.Content;
import com.volcengine.ark.runtime.service.ArkService;
import okhttp3.ConnectionPool;
import okhttp3.Dispatcher;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.TimeUnit;

public class ContentGenerationTaskExample {
    // Agent Plan 专属 API Key：https://ark.volcengine.com/region:cn-beijing/openManagement?LLM=%7B%7D&OpenModelVisible=false&advancedActiveKey=agentPlan
    static String apiKey = System.getenv("AGENT_API_KEY");
    static ConnectionPool connectionPool = new ConnectionPool(5, 1, TimeUnit.SECONDS);
    static Dispatcher dispatcher = new Dispatcher();
    static ArkService service = ArkService.builder()
           .baseUrl("https://ark.cn-beijing.volces.com/api/plan/v3") // Agent Plan 专属 Base URL
           .dispatcher(dispatcher)
           .connectionPool(connectionPool)
           .apiKey(apiKey)
           .build();

    public static void main(String[] args) {
        String model = "doubao-seedance-2.0";
        Boolean generateAudio = true;
        String ratio = "adaptive";
        Long duration = 5L;
        Boolean watermark = false;
        System.out.println("----- create request -----");
        List<Content> contents = new ArrayList<>();

        // Combination of text prompt and parameters
        contents.add(Content.builder()
                .type("text")
                .text("女孩抱着狐狸，女孩睁开眼，温柔地看向镜头，狐狸友善地抱着，镜头缓缓拉出，女孩的头发被风吹动，可以听到风声")
                .build());
        // The URL of the first frame image
        contents.add(Content.builder()
                .type("image_url")
                .imageUrl(CreateContentGenerationTaskRequest.ImageUrl.builder()
                        .url("https://ark-project.tos-cn-beijing.volces.com/doc_image/i2v_foxrgirl.png")
                        .build())
                .build());

        // Create a video generation task
        CreateContentGenerationTaskRequest createRequest = CreateContentGenerationTaskRequest.builder()
                .model(model)
                .content(contents)
                .generateAudio(generateAudio)
                .ratio(ratio)
                .duration(duration)
                .watermark(watermark)
                .build();

        CreateContentGenerationTaskResult createResult = service.createContentGenerationTask(createRequest);
        System.out.println(createResult);

        service.shutdownExecutor();
    }
}
```



</Tab>
<Tab zoneid="zzD0GkfBRf" title="Go">
<TabTitle>Go</TabTitle>

```Go
package main

import (
    "context"
    "fmt"
    "os"
    "time"

    "github.com/volcengine/volcengine-go-sdk/service/arkruntime"
    "github.com/volcengine/volcengine-go-sdk/service/arkruntime/model"
    "github.com/volcengine/volcengine-go-sdk/volcengine"
)

func main() {
    client := arkruntime.NewClientWithApiKey(
        // Agent Plan 专属 API Key：https://ark.volcengine.com/region:cn-beijing/openManagement?LLM=%7B%7D&OpenModelVisible=false&advancedActiveKey=agentPlan
        os.Getenv("AGENT_API_KEY"),
        // Agent Plan 专属 Base URL
        arkruntime.WithBaseUrl("https://ark.cn-beijing.volces.com/api/plan/v3"),
    )
    ctx := context.Background()
    modelEp := "doubao-seedance-2.0"

    // Generate a task
    fmt.Println("----- create request -----")
    createReq := model.CreateContentGenerationTaskRequest{
        Model: modelEp,
        GenerateAudio: volcengine.Bool(true),
        Ratio:         volcengine.String("adaptive"),
        Duration:      volcengine.Int64(5),
        Watermark:     volcengine.Bool(false),
        Content: []*model.CreateContentGenerationContentItem{
            {
                // Combination of text prompt and parameters
                Type: model.ContentGenerationContentItemTypeText,
                Text: volcengine.String("女孩抱着狐狸，女孩睁开眼，温柔地看向镜头，狐狸友善地抱着，镜头缓缓拉出，女孩的头发被风吹动，可以听到风声"),
            },
            {
                // The URL of the first frame image
                Type: model.ContentGenerationContentItemTypeImage,
                ImageURL: &model.ImageURL{
                    URL: "https://ark-project.tos-cn-beijing.volces.com/doc_image/i2v_foxrgirl.png",
                },
            },
        },
    }
    createResp, err := client.CreateContentGenerationTask(ctx, createReq)
    if err != nil {
        fmt.Printf("create content generation error: %v", err)
        return
    }
    taskID := createResp.ID
    fmt.Printf("Task Created with ID: %s", taskID)
}
```



</Tab>
</Tabs>


<span id="4463e4c4"></span>
## 调用图片生成 API

参见 [Seedream 5.0 Pro 教程](https://ark.volcengine.com/region:cn-beijing/docs/82379/2582774?lang=zh)、[图片生成教程](https://ark.volcengine.com/region:cn-beijing/docs/82379/1824121?lang=zh) 完成图片生成任务，需注意必须使用 Agent Plan 专属 API Key、专属 Base URL 及支持的模型，否则可能会调用失败或产生额外费用。具体参见[核心配置信息](https://ark.volcengine.com/region:cn-beijing/docs/82379/2375486?lang=zh#8d00fec2)。

通过 Agent Plan 实现文生图的示例如下：


<Tabs>
<Tab zoneid="eGH4yWAhbH" title="Curl">
<TabTitle>Curl</TabTitle>

```Bash
curl https://ark.cn-beijing.volces.com/api/plan/v3/images/generations \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $AGENT_API_KEY" \
  -d '{
    "model": "doubao-seedream-5.0-lite",
    "prompt": "充满活力的特写编辑肖像，模特眼神犀利，头戴雕塑感帽子，色彩拼接丰富，眼部焦点锐利，景深较浅，具有Vogue杂志封面的美学风格，采用中画幅拍摄，工作室灯光效果强烈。",
    "size": "2K",
    "output_format":"png",
    "watermark": false
}'
```



</Tab>
<Tab zoneid="c6Mv4ay3wK" title="Python">
<TabTitle>Python</TabTitle>

```Python
import os
# Install SDK:  pip install 'volcengine-python-sdk[ark]' .
from volcenginesdkarkruntime import Ark

client = Ark(
    # Agent Plan 专属 Base URL
    base_url="https://ark.cn-beijing.volces.com/api/plan/v3",
    # Agent Plan 专属 API Key：https://ark.volcengine.com/region:cn-beijing/openManagement?LLM=%7B%7D&OpenModelVisible=false&advancedActiveKey=agentPlan
    api_key=os.getenv('AGENT_API_KEY'),
)

imagesResponse = client.images.generate(
    model="doubao-seedream-5.0-lite",
    prompt="充满活力的特写编辑肖像，模特眼神犀利，头戴雕塑感帽子，色彩拼接丰富，眼部焦点锐利，景深较浅，具有Vogue杂志封面的美学风格，采用中画幅拍摄，工作室灯光效果强烈。",
    size="2K",
    output_format="png",
    response_format="url",
    watermark=False
)

print(imagesResponse.data[0].url)
```



</Tab>
<Tab zoneid="A2XVYW7OO9" title="Java">
<TabTitle>Java</TabTitle>

```Java
package com.ark.sample;
import com.volcengine.ark.runtime.model.images.generation.*;
import com.volcengine.ark.runtime.service.ArkService;
import okhttp3.ConnectionPool;
import okhttp3.Dispatcher;

import java.util.Arrays;
import java.util.List;
import java.util.concurrent.TimeUnit;

public class ImageGenerationsExample {
    public static void main(String[] args) {
        // Agent Plan 专属 API Key：https://ark.volcengine.com/region:cn-beijing/openManagement?LLM=%7B%7D&OpenModelVisible=false&advancedActiveKey=agentPlan
        String apiKey = System.getenv("AGENT_API_KEY");
        ConnectionPool connectionPool = new ConnectionPool(5, 1, TimeUnit.SECONDS);
        Dispatcher dispatcher = new Dispatcher();
        ArkService service = ArkService.builder()
                .baseUrl("https://ark.cn-beijing.volces.com/api/plan/v3") // Agent Plan 专属 Base URL
                .dispatcher(dispatcher)
                .connectionPool(connectionPool)
                .apiKey(apiKey)
                .build();

        GenerateImagesRequest generateRequest = GenerateImagesRequest.builder()
                .model("doubao-seedream-5.0-lite")
                .prompt("充满活力的特写编辑肖像，模特眼神犀利，头戴雕塑感帽子，色彩拼接丰富，眼部焦点锐利，景深较浅，具有Vogue杂志封面的美学风格，采用中画幅拍摄，工作室灯光效果强烈。")
                .size("2K")
                .sequentialImageGeneration("disabled")
                .outputFormat("png")
                .responseFormat(ResponseFormat.Url)
                .stream(false)
                .watermark(false)
                .build();
        ImagesResponse imagesResponse = service.generateImages(generateRequest);
        System.out.println(imagesResponse.getData().get(0).getUrl());

        service.shutdownExecutor();
    }
}
```



</Tab>
<Tab zoneid="bD4Cxt67XN" title="Go">
<TabTitle>Go</TabTitle>

```Go
package main

import (
    "context"
    "fmt"
    "os"

    "github.com/volcengine/volcengine-go-sdk/service/arkruntime"
    "github.com/volcengine/volcengine-go-sdk/service/arkruntime/model"
    "github.com/volcengine/volcengine-go-sdk/volcengine"
)

func main() {
    client := arkruntime.NewClientWithApiKey(
        // Agent Plan 专属 API Key：https://ark.volcengine.com/region:cn-beijing/openManagement?LLM=%7B%7D&OpenModelVisible=false&advancedActiveKey=agentPlan
        os.Getenv("AGENT_API_KEY"),
        // // Agent Plan 专属 Base URL
        arkruntime.WithBaseUrl("https://ark.cn-beijing.volces.com/api/plan/v3"),
    )
    ctx := context.Background()
    outputFormat := model.OutputFormatPNG


    generateReq := model.GenerateImagesRequest{
       Model:          "doubao-seedream-5.0-lite",
       Prompt:         "充满活力的特写编辑肖像，模特眼神犀利，头戴雕塑感帽子，色彩拼接丰富，眼部焦点锐利，景深较浅，具有Vogue杂志封面的美学风格，采用中画幅拍摄，工作室灯光效果强烈。",
       Size:           volcengine.String("2K"),
       OutputFormat:   &outputFormat,
       ResponseFormat: volcengine.String("url"),
       Watermark:      volcengine.Bool(false),
    }

    imagesResponse, err := client.GenerateImages(ctx, generateReq)
    if err != nil {
       fmt.Printf("generate images error: %v\n", err)
       return
    }

    fmt.Printf("%s\n", *imagesResponse.Data[0].Url)
}
```



</Tab>
<Tab zoneid="f04333dPN6" title="OpenAI">
<TabTitle>OpenAI</TabTitle>

```Python
import os
from openai import OpenAI

client = OpenAI(
    # Agent Plan 专属 Base URL
    base_url="https://ark.cn-beijing.volces.com/api/plan/v3",
    # Agent Plan 专属 API Key：https://ark.volcengine.com/region:cn-beijing/openManagement?LLM=%7B%7D&OpenModelVisible=false&advancedActiveKey=agentPlan
    api_key=os.getenv('AGENT_API_KEY'),
)

imagesResponse = client.images.generate(
    model="doubao-seedream-5.0-lite",
    prompt="充满活力的特写编辑肖像，模特眼神犀利，头戴雕塑感帽子，色彩拼接丰富，眼部焦点锐利，景深较浅，具有Vogue杂志封面的美学风格，采用中画幅拍摄，工作室灯光效果强烈。",
    size="2K",
    output_format="png",
    response_format="url",
    extra_body={
        "watermark": False,
    },
)

print(imagesResponse.data[0].url)
```



</Tab>
</Tabs>


<span id="5c2f8d1a"></span>
# 支持模型及能力

<div data-tips="true" data-tips-type="warning" data-tips-is-title="true">注意</div>


<div data-tips="true" data-tips-type="warning">Agent Plan 的套餐支持的模型不同，具体参见<a href="https://ark.volcengine.com/region:cn-beijing/docs/82379/2366394?lang=zh#3d801f5f">支持模型及 Harness</a>。</div>


<span id="7a1c4e9b"></span>
## 生视频模型

> doubao\-seedance\-1.5\-pro 即将下线，建议使用以下模型。



<span aceTableMode="list" aceTableWidth="1,1,1,1,1,"></span>
|模型 | |doubao\-seedance\-2.5【New】 |doubao\-seedance\-2.0 |doubao\-seedance\-2.0\-fast |doubao\-seedance\-2.0\-mini |
|---|---|---|---|---|---|
|文生视频 | |✓ |✓ |✓ |✓ |
|图生视频\-首帧 | |✓ |✓ |✓ |✓ |
|图生视频\-首尾帧 | |✓ |✓ |✓ |✓ |
|多模态参考 |图片参考 |✓ |✓ |✓ |✓ |
||视频参考 |✓ |✓ |✓ |✓ |
||音频参考 |✓ |✗（需搭配图片/视频） |✗（需搭配图片/视频） |✗（需搭配图片/视频） |
||组合参考<br><br><br>* 图片 + 音频<br><br>* 图片 + 视频<br><br>* 视频 + 音频<br><br>* 图片 + 视频 + 音频 |✓ |✓ |✓ |✓ |
|编辑视频 | |✓ |✓ |✓ |✓ |
|延长视频 | |✓ |✓ |✓ |✓ |
|生成有声视频 | |✓ |✓ |✓ |✓ |
|参考素材数量上限 | |50（30张图+10个视频+10个音频） |15（9张图+3个视频+3个音频） |15（9张图+3个视频+3个音频） |15（9张图+3个视频+3个音频） |
|联网搜索工具 | |✓ |✓ |✓ |✓ |
|样片模式 | |✗ |✗ |✗ |✗ |
|返回视频产物对应的尾帧图 | |✓ |✓ |✓ |✓ |
|输出视频规格 |输出分辨率 |* 480p（8bit 位深）<br><br>* 720p（8bit 位深）<br><br>* 1080p（10bit 位深） |* 480p（8bit 位深）<br><br>* 720p（8bit 位深）<br><br>* 1080p（8bit 位深）<br><br>* 4k（10bit 位深） |* 480p（8bit 位深）<br><br>* 720p（8bit 位深） |* 480p（8bit 位深）<br><br>* 720p（8bit 位深） |
||输出宽高比 |21:9, 16:9, 4:3,<br><br>1:1, 3:4, 9:16, adaptive |21:9, 16:9, 4:3,<br><br>1:1, 3:4, 9:16, adaptive |21:9, 16:9, 4:3,<br><br>1:1, 3:4, 9:16, adaptive |21:9, 16:9, 4:3,<br><br>1:1, 3:4, 9:16, adaptive |
||输出时长 |4~30 秒；\-1（在有效时长内由模型自动选择最佳时长） |4~15 秒；\-1（在有效时长内由模型自动选择最佳时长） |4~15 秒；\-1（在有效时长内由模型自动选择最佳时长） |4~15 秒；\-1（在有效时长内由模型自动选择最佳时长） |
||输出视频格式 |mp4, mov |mp4 |mp4 |mp4 |
|抵扣系数 | |* 输出视频分辨率为 480p，720p<br><br>   * 输入包含视频：210<br><br>   * 输入不含视频：350<br><br>* 输出视频分辨率为 1080p<br><br>   * 输入包含视频：230<br><br>   * 输入不含视频：385 |* 输出视频分辨率为 480p，720p<br><br>   * 输入包含视频：140<br><br>   * 输入不含视频：230<br><br>* 输出视频分辨率为 1080p<br><br>   * 输入包含视频：155<br><br>   * 输入不含视频：255<br><br>* 输出视频分辨率为 4k<br><br>   * 输入包含视频：80<br><br>   * 输入不含视频：130 |* 输入包含视频：110<br><br>* 输入不含视频：185 |* 输入包含视频：70<br><br>* 输入不含视频：115 |


<span id="0e9a63f4"></span>
## 生图模型


<span aceTableMode="list" aceTableWidth="2,2,2,2"></span>
|模型 | |doubao\-seedream\-5\-0\-pro |doubao\-seedream\-5.0\-lite |
|---|---|---|---|
|文生图 | |✓ |✓ |
|文生组图 | |暂不支持 |✓ |
|单 / 多图生图 | |✓ |✓ |
|单 / 多图生组图 | |暂不支持 |✓ |
|交互编辑 | |✓ |✗ |
|图层拆分 | |✓ |✗ |
|流式输出 | |暂不支持 |✓ |
|联网搜索 | |暂不支持 |✓ |
|模型参数 |分辨率 |1K, 1.5K, 2K |2K, 3K, 4K |
||输出格式 |png, jpeg |png, jpeg |
||提示词优化模式 |标准模式, 极速模式 |标准模式 |
||生成数量 |支持生成单图/多张图层( 1张底图+16张图层) |输入的参考图数量 + 最终生成的图片数量 ≤ 15张 |
|抵扣系数 |输入图 |* 第一张：免费<br><br>* 第二张起：10 |\- |
||输出图 |* 单图生成场景：<br><br>   * ≤ 261 万像素（分辨率 1.5K 及以下）：150<br><br>   * \> 261 万像素（分辨率 1.5K 以上）：300<br><br>* 图层拆分场景：<br><br>   * ≤ 261 万像素（分辨率 1.5K 及以下）：75<br><br>   * \> 261 万像素（分辨率 1.5K 以上）：150 |1 张成功生成的图片 = 99 AFP |




