# NetClaw

[中文文档](README.zh-CN.md) | English

A lightweight AI assistant built on .NET, inspired by OpenClaw / PicoClaw / ZeroClaw.

## Features

- 🪶 **Lightweight**: AOT compiled, single-file deployment, fast startup
- 🔌 **Multi-LLM Support**: Cloud APIs + Local models (Ollama, vLLM, etc.)
- 🛠️ **Built-in Tools**: File read/write, directory listing, shell execution
- 💾 **Memory System**: Identity, personality, long-term memory, user info separated
- 🔧 **Skill System**: SKILL.md format with YAML frontmatter support
- 📢 **Multi-Channel**: QQ, DingTalk, Feishu message integration
- 🔒 **Sandbox**: Dangerous commands auto-filtered

## Quick Start

### Requirements

- .NET 10 SDK

### Installation & Run

```bash
# Initialize configuration
dotnet run --project src/NetClaw -- onboard

# Send a single message
dotnet run --project src/NetClaw -- agent -m "Hello"

# Interactive mode
dotnet run --project src/NetClaw -- agent
```

## Commands

| Command | Description |
|---------|-------------|
| `onboard` | Initialize configuration, set API key or local model |
| `agent` | Chat with AI |
| `status` | Show current configuration status |
| `clear` | Clear conversation history |
| `skill` | Skill management |
| `memory` | Memory management |
| `gateway` | Start message gateway server |
| `channel` | Channel configuration management |

### Agent Options

```
-m, --message    Send a single message
-p, --provider   Specify LLM provider (override default)
--model          Specify model (override default)
-w, --workdir    Specify working directory (override default)
-s, --session    Specify session ID
```

### Skill Management

```bash
# List all skills
netclaw skill list

# Add a skill
netclaw skill add -n "translate" -d "Translate text"

# Delete a skill
netclaw skill delete -n "translate"
```

### Memory Management

```bash
# Show memory content
netclaw memory show -t user      # User info
netclaw memory show -t identity  # Identity settings
netclaw memory show -t soul      # Personality settings
netclaw memory show -t memory    # Long-term memory

# Edit memory content
netclaw memory edit -t user
```

### Message Gateway

```bash
# Start gateway (listen on port 8080)
netclaw gateway

# Specify port
netclaw gateway -p 9090

# View channel configuration
netclaw channel list

# Enable a channel
netclaw channel enable -n dingtalk

# Configure channel parameters
netclaw channel config -n dingtalk -k webhook_url -v "https://oapi.dingtalk.com/robot/send?access_token=xxx"
```

## Channel Integration

Supports three messaging platforms:

| Channel | Protocol | Description |
|---------|----------|-------------|
| **QQ** | OneBot | Compatible with NapCat, Lagrange, etc. |
| **DingTalk** | Webhook | Custom bot + Enterprise internal bot |
| **Feishu** | Webhook | Custom bot + Enterprise self-built app |

### QQ Configuration

```bash
# Enable QQ channel
netclaw channel enable -n qq

# Configure OneBot API address
netclaw channel config -n qq -k api_url -v "http://localhost:3000"

# Configure WebSocket (receive messages)
netclaw channel config -n qq -k websocket_url -v "ws://localhost:3001"
```

### DingTalk Configuration

```bash
# Enable DingTalk channel
netclaw channel enable -n dingtalk

# Configure Webhook
netclaw channel config -n dingtalk -k webhook_url -v "https://oapi.dingtalk.com/robot/send?access_token=xxx"

# Configure signing secret (optional)
netclaw channel config -n dingtalk -k secret -v "SECxxx"
```

### Feishu Configuration

```bash
# Enable Feishu channel
netclaw channel enable -n feishu

# Configure Webhook
netclaw channel config -n feishu -k webhook_url -v "https://open.feishu.cn/open-apis/bot/v2/hook/xxx"
```

## Supported LLM Providers

### Cloud APIs

| Provider | Default Model | API Address |
|----------|---------------|-------------|
| openai | gpt-4o, gpt-4o-mini | https://api.openai.com/v1 |
| openrouter | anthropic/claude-3.5-sonnet | https://openrouter.ai/api/v1 |
| anthropic | claude-3-5-sonnet | https://api.anthropic.com |
| deepseek | deepseek-chat, deepseek-coder | https://api.deepseek.com/v1 |
| zhipu | glm-4-plus, glm-4-flash | https://open.bigmodel.cn/api/paas/v4 |
| moonshot | moonshot-v1-8k | https://api.moonshot.cn/v1 |

### Local Models

| Provider | Default API Address | Description |
|----------|---------------------|-------------|
| ollama | http://localhost:11434/v1 | Ollama local models |
| local | http://localhost:8080/v1 | Generic local models (vLLM, LM Studio, etc.) |
| custom | Custom | Custom OpenAI-compatible API |

### Ollama Example

```bash
# 1. Ensure Ollama is running
ollama serve

# 2. Run onboard, select "Local Model"
dotnet run --project src/NetClaw -- onboard

# 3. Select ollama, set model name (e.g., llama3.2, qwen2.5)
```

## Built-in Tools

| Tool | Description |
|------|-------------|
| `read_file` | Read file content |
| `write_file` | Write content to file |
| `list_dir` | List directory contents |
| `exec` | Execute shell commands |
| `skill_*` | Custom skills (auto-loaded) |

## Directory Structure

### Config Directory (~/.netclaw/)

Stores configuration, memory, skills and other **non-work content**, completely separated from working directory:

```
~/.netclaw/
├── config.json       # Configuration file
├── sessions/         # Conversation sessions and history
├── memory/           # Long-term memory
│   └── MEMORY.md
├── state/            # Persistent state (last channel, etc.)
├── skills/           # Custom skills (SKILL.md format)
├── IDENTITY.md       # Agent identity settings
├── SOUL.md           # Agent soul/personality
├── AGENTS.md         # Agent behavior guidelines
├── TOOLS.md          # Tool usage instructions
└── USER.md           # User preferences
```

### Working Directory (default ~/)

The directory where Agent actually operates files. Can be set during onboard or temporarily specified with `-w` parameter:

```bash
# Temporarily specify working directory
dotnet run --project src/NetClaw -- agent -w /path/to/project
```

## Configuration File

Configuration file located at `~/.netclaw/config.json`

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

## Security Features

- Dangerous commands auto-blocked (`rm -rf`, `format`, `shutdown`, etc.)
- Command execution timeout limit (default 30 seconds, max 5 minutes)

## Project Structure

```
netclaw/
├── NetClaw.slnx
├── README.md
├── README.zh-CN.md
└── src/
    └── NetClaw/
        ├── NetClaw.csproj
        ├── Program.cs       # Entry point and commands
        ├── Models.cs        # Data models
        ├── Interfaces.cs    # Interface definitions
        ├── Providers.cs     # LLM providers
        ├── Tools.cs         # Built-in tools
        ├── Memory.cs        # Memory management
        ├── Agent.cs         # Agent core
        ├── Config.cs        # Configuration management
        ├── Gateway.cs       # HTTP gateway
        ├── JsonContext.cs   # JSON serialization (AOT)
        └── Channels/        # Channel implementations
            ├── IChannel.cs
            ├── DingTalkChannel.cs
            ├── FeishuChannel.cs
            ├── QQChannel.cs
            └── ChannelJsonContext.cs
```

## Development

```bash
# Build
dotnet build

# Publish AOT version
dotnet publish -c Release -r win-x64 --self-contained

# Run
dotnet run --project src/NetClaw -- [command]
```

## License

MIT License
