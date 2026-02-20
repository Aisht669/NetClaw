# NetClaw

中文文档 | [English](README.md)

基于 .NET 的轻量级 AI 助手，灵感来自 OpenClaw / PicoClaw / ZeroClaw。

## 特性

- 🪶 **轻量级**: AOT 编译，单文件发布，启动快速
- 🔌 **多 LLM 支持**: 云端 API + 本地模型 (Ollama, vLLM 等)
- 🛠️ **内置工具**: 文件读写、目录列表、Shell 命令执行
- 💾 **记忆系统**: 身份、性格、长期记忆、用户信息分离存储
- 🔧 **技能系统**: SKILL.md 格式，支持 YAML frontmatter
- 📢 **多渠道接入**: QQ、钉钉、飞书消息推送
- 🔒 **安全沙箱**: 危险命令自动过滤

## 快速开始

### 环境要求

- .NET 10 SDK

### 安装与运行

```bash
# 初始化配置
dotnet run --project src/NetClaw -- onboard

# 发送单条消息
dotnet run --project src/NetClaw -- agent -m "你好"

# 交互模式
dotnet run --project src/NetClaw -- agent
```

## 命令说明

| 命令 | 说明 |
|------|------|
| `onboard` | 初始化配置，设置 API 密钥或本地模型 |
| `agent` | 与 AI 对话 |
| `status` | 显示当前配置状态 |
| `clear` | 清除对话历史 |
| `skill` | 技能管理 |
| `memory` | 记忆管理 |
| `gateway` | 启动消息网关 |
| `channel` | 渠道配置管理 |

### Agent 命令选项

```
-m, --message    发送单条消息
-p, --provider   指定 LLM 提供者 (覆盖默认配置)
--model          指定模型 (覆盖默认配置)
-w, --workdir    指定工作目录 (覆盖默认配置)
-s, --session    指定会话 ID
```

### 技能管理

```bash
# 列出所有技能
netclaw skill list

# 添加技能
netclaw skill add -n "翻译" -d "翻译文本"

# 删除技能
netclaw skill delete -n "翻译"
```

### 记忆管理

```bash
# 显示记忆内容
netclaw memory show -t user      # 用户信息
netclaw memory show -t identity  # 身份设定
netclaw memory show -t soul      # 性格设定
netclaw memory show -t memory    # 长期记忆

# 编辑记忆内容
netclaw memory edit -t user
```

### 消息网关

```bash
# 启动网关 (监听 8080 端口)
netclaw gateway

# 指定端口
netclaw gateway -p 9090

# 查看渠道配置
netclaw channel list

# 启用渠道
netclaw channel enable -n dingtalk

# 配置渠道参数
netclaw channel config -n dingtalk -k webhook_url -v "https://oapi.dingtalk.com/robot/send?access_token=xxx"
```

## 渠道接入

支持三种消息渠道：

| 渠道 | 协议 | 说明 |
|------|------|------|
| **QQ** | OneBot | 兼容 NapCat、Lagrange 等 |
| **钉钉** | Webhook | 自定义机器人 + 企业内部机器人 |
| **飞书** | Webhook | 自定义机器人 + 企业自建应用 |

### QQ 配置

```bash
# 启用 QQ 渠道
netclaw channel enable -n qq

# 配置 OneBot API 地址
netclaw channel config -n qq -k api_url -v "http://localhost:3000"

# 配置 WebSocket (接收消息)
netclaw channel config -n qq -k websocket_url -v "ws://localhost:3001"
```

### 钉钉配置

```bash
# 启用钉钉渠道
netclaw channel enable -n dingtalk

# 配置 Webhook
netclaw channel config -n dingtalk -k webhook_url -v "https://oapi.dingtalk.com/robot/send?access_token=xxx"

# 配置签名密钥 (可选)
netclaw channel config -n dingtalk -k secret -v "SECxxx"
```

### 飞书配置

```bash
# 启用飞书渠道
netclaw channel enable -n feishu

# 配置 Webhook
netclaw channel config -n feishu -k webhook_url -v "https://open.feishu.cn/open-apis/bot/v2/hook/xxx"
```

## 支持的 LLM 提供者

### 云端 API

| 提供者 | 默认模型 | API 地址 |
|--------|----------|----------|
| openai | gpt-4o, gpt-4o-mini | https://api.openai.com/v1 |
| openrouter | anthropic/claude-3.5-sonnet | https://openrouter.ai/api/v1 |
| anthropic | claude-3-5-sonnet | https://api.anthropic.com |
| deepseek | deepseek-chat, deepseek-coder | https://api.deepseek.com/v1 |
| zhipu | glm-4-plus, glm-4-flash | https://open.bigmodel.cn/api/paas/v4 |
| moonshot | moonshot-v1-8k | https://api.moonshot.cn/v1 |

### 本地模型

| 提供者 | 默认 API 地址 | 说明 |
|--------|--------------|------|
| ollama | http://localhost:11434/v1 | Ollama 本地模型 |
| local | http://localhost:8080/v1 | 通用本地模型 (vLLM, LM Studio 等) |
| custom | 自定义 | 自定义 OpenAI 兼容 API |

### 使用 Ollama 示例

```bash
# 1. 确保 Ollama 已启动
ollama serve

# 2. 运行 onboard，选择 "本地模型"
dotnet run --project src/NetClaw -- onboard

# 3. 选择 ollama，设置模型名称 (如 llama3.2, qwen2.5)
```

## 内置工具

| 工具 | 说明 |
|------|------|
| `read_file` | 读取文件内容 |
| `write_file` | 写入内容到文件 |
| `list_dir` | 列出目录内容 |
| `exec` | 执行 Shell 命令 |
| `skill_*` | 自定义技能 (自动加载) |

## 目录结构

### 配置目录 (~/.netclaw/)

存放配置、记忆、技能等 **非工作内容**，与工作目录完全分离：

```
~/.netclaw/
├── config.json       # 配置文件
├── sessions/         # 对话会话和历史
├── memory/           # 长期记忆
│   └── MEMORY.md
├── state/            # 持久化状态 (最后频道等)
├── skills/           # 自定义技能 (SKILL.md 格式)
├── IDENTITY.md       # Agent 身份设定
├── SOUL.md           # Agent 灵魂/性格
├── AGENTS.md         # Agent 行为指南
├── TOOLS.md          # 工具使用说明
└── USER.md           # 用户偏好
```

### 工作目录 (默认 ~/)

Agent 实际操作文件的目录，可以在 onboard 时设置，也可以用 `-w` 参数临时指定：

```bash
# 临时指定工作目录
dotnet run --project src/NetClaw -- agent -w /path/to/project
```

## 配置文件

配置文件位于 `~/.netclaw/config.json`

```json
{
  "data_dir": "~/.netclaw",
  "agents": {
    "model": "llama3.2",
    "max_tokens": 4096,
    "temperature": 0.7,
    "max_tool_iterations": 20,
    "workspace": "~",
    "auto_save": true,
    "auto_save_interval": 5
  },
  "providers": {
    "ollama": {
      "api_key": "",
      "api_base": "http://localhost:11434/v1",
      "default_model": "llama3.2",
      "is_local": true
    }
  },
  "channels": {
    "host": "0.0.0.0",
    "port": 8080,
    "dingtalk": {
      "enabled": false,
      "webhook_url": null,
      "secret": null
    },
    "feishu": {
      "enabled": false,
      "webhook_url": null
    },
    "qq": {
      "enabled": false,
      "api_url": "http://localhost:3000",
      "access_token": null,
      "websocket_url": null
    }
  },
  "default_provider": "ollama"
}
```

## 安全特性

- 危险命令自动拦截 (`rm -rf`, `format`, `shutdown` 等)
- 命令执行超时限制 (默认 30 秒，最大 5 分钟)

## 项目结构

```
netclaw/
├── NetClaw.slnx
├── README.md
├── README.zh-CN.md
└── src/
    └── NetClaw/
        ├── NetClaw.csproj
        ├── Program.cs       # 入口和命令定义
        ├── Models.cs        # 数据模型
        ├── Interfaces.cs    # 接口定义
        ├── Providers.cs     # LLM 提供者
        ├── Tools.cs         # 内置工具
        ├── Memory.cs        # 记忆管理
        ├── Agent.cs         # Agent 核心
        ├── Config.cs        # 配置管理
        ├── Gateway.cs       # HTTP 网关
        ├── JsonContext.cs   # JSON 序列化 (AOT)
        └── Channels/        # 渠道实现
            ├── IChannel.cs
            ├── DingTalkChannel.cs
            ├── FeishuChannel.cs
            ├── QQChannel.cs
            └── ChannelJsonContext.cs
```

## 开发

```bash
# 构建
dotnet build

# 发布 AOT 版本
dotnet publish -c Release -r win-x64 --self-contained

# 运行
dotnet run --project src/NetClaw -- [命令]
```

## 许可证

MIT License
