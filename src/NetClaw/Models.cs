// NetClaw - 数据模型

using System.Text.Json.Serialization;

namespace NetClaw;

/// <summary>消息角色</summary>
public enum MessageRole { System, User, Assistant, Tool }

/// <summary>聊天消息</summary>
public class Message
{
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ToolCallId { get; set; }
    public string? Name { get; set; }
    public List<ToolCall>? ToolCalls { get; set; }

    public static Message System(string content) => new() { Role = MessageRole.System, Content = content };
    public static Message User(string content) => new() { Role = MessageRole.User, Content = content };
    public static Message Assistant(string content) => new() { Role = MessageRole.Assistant, Content = content };
}

/// <summary>工具调用</summary>
public class ToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public ToolCallFunction Function { get; set; } = new();
}

/// <summary>工具调用函数</summary>
public class ToolCallFunction
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}

/// <summary>工具定义</summary>
public class ToolDefinition
{
    public string Type { get; set; } = "function";
    public ToolFunction Function { get; set; } = new();
}

/// <summary>工具函数定义</summary>
public class ToolFunction
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ToolParameters Parameters { get; set; } = new();
}

/// <summary>工具参数定义</summary>
public class ToolParameters
{
    public string Type { get; set; } = "object";
    public Dictionary<string, ToolProperty> Properties { get; set; } = new();
    public List<string> Required { get; set; } = new();
}

/// <summary>工具属性定义</summary>
public class ToolProperty
{
    public string Type { get; set; } = "string";
    public string Description { get; set; } = string.Empty;
    public List<string>? Enum { get; set; }
}

/// <summary>Agent 配置</summary>
public class AgentConfig
{
    public string Model { get; set; } = "gpt-4o-mini";
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.7;
    public int MaxToolIterations { get; set; } = 20;
    public string Workspace { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string SystemPrompt { get; set; } = "你是一个有用的 AI 助手。";
    public bool AutoSave { get; set; } = true;
    public int AutoSaveInterval { get; set; } = 5;
}

/// <summary>LLM 提供者配置</summary>
public class ProviderConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiBase { get; set; } = string.Empty;
    public string? DefaultModel { get; set; }
    public bool IsLocal { get; set; } = false;
}

/// <summary>应用配置</summary>
public class AppConfig
{
    public AgentConfig Agents { get; set; } = new();
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
    public string DefaultProvider { get; set; } = "openai";
    public string DataDir { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".netclaw");
    public string? LastChannel { get; set; }
    public ChannelsConfig? Channels { get; set; }
}

/// <summary>渠道配置</summary>
public class ChannelsConfig
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8080;
    public DingTalkChannelConfig? DingTalk { get; set; }
    public FeishuChannelConfig? Feishu { get; set; }
    public QQChannelConfig? QQ { get; set; }
}

/// <summary>钉钉渠道配置</summary>
public class DingTalkChannelConfig
{
    public bool Enabled { get; set; }
    public string? WebhookUrl { get; set; }
    public string? Secret { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public List<string> AllowFrom { get; set; } = new();
}

/// <summary>飞书渠道配置</summary>
public class FeishuChannelConfig
{
    public bool Enabled { get; set; }
    public string? WebhookUrl { get; set; }
    public string? Secret { get; set; }
    public string? AppId { get; set; }
    public string? AppSecret { get; set; }
    public string? EncryptKey { get; set; }
    public string? VerificationToken { get; set; }
    public List<string> AllowFrom { get; set; } = new();
}

/// <summary>QQ 渠道配置</summary>
public class QQChannelConfig
{
    public bool Enabled { get; set; }
    public string? ApiUrl { get; set; } = "http://localhost:3000";
    public string? AccessToken { get; set; }
    public string? WebSocketUrl { get; set; }
    public List<string> AllowGroups { get; set; } = new();
    public List<string> AllowUsers { get; set; } = new();
}

/// <summary>聊天响应</summary>
public class ChatResponse
{
    public string Content { get; set; } = string.Empty;
    public List<ToolCall>? ToolCalls { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;
}

/// <summary>Agent 执行结果</summary>
public class AgentResult
{
    public string Response { get; set; } = string.Empty;
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int ToolCallsCount { get; set; }
}

/// <summary>技能定义 (SKILL.md 格式)</summary>
public class Skill
{
    /// <summary>技能名称 (ASCII安全，用于工具调用)</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>显示名称 (可以是中文)</summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>技能描述 (用于 AI 判断何时使用)</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>SKILL.md 完整内容 (包含 YAML frontmatter + Markdown body)</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>依赖包 (可选)</summary>
    public string? Dependencies { get; set; }
    
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    
    /// <summary>获取 ASCII 安全的工具名称</summary>
    public string GetToolName()
    {
        if (string.IsNullOrEmpty(Name)) return "skill_unknown";
        // 检查是否全是 ASCII 字母数字下划线
        if (Name.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-'))
            return $"skill_{Name}";
        // 非 ASCII 名称，用哈希
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Name));
        return $"skill_{Convert.ToHexString(bytes[..4]).ToLower()}";
    }
}
