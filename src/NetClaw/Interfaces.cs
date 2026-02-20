// NetClaw - 接口定义

namespace NetClaw;

/// <summary>LLM 提供者接口</summary>
public interface ILLMProvider
{
    string Name { get; }
    Task<ChatResponse> ChatAsync(List<Message> messages, List<ToolDefinition>? tools = null,
        string? model = null, int? maxTokens = null, double? temperature = null,
        CancellationToken cancellationToken = default);
}

/// <summary>工具接口</summary>
public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolDefinition GetDefinition();
    Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default);
}

/// <summary>记忆存储接口</summary>
public interface IMemory
{
    // 会话管理
    Task AddMessageAsync(string sessionId, Message message);
    Task<List<Message>> GetMessagesAsync(string sessionId);
    Task ClearSessionAsync(string sessionId);
    Task<List<string>> GetSessionListAsync();
    
    // 身份与记忆
    Task<string?> GetIdentityAsync();
    Task SetIdentityAsync(string content);
    Task<string?> GetSoulAsync();
    Task SetSoulAsync(string content);
    Task<string?> GetMemoryAsync();
    Task SetMemoryAsync(string content);
    Task<string?> GetUserAsync();
    Task SetUserAsync(string content);
    Task<string?> GetAgentsAsync();
    Task SetAgentsAsync(string content);
    Task<string?> GetToolsAsync();
    Task SetToolsAsync(string content);
    
    // 状态持久化
    Task<string?> GetLastChannelAsync();
    Task SetLastChannelAsync(string channel);
    
    // 技能管理
    Task<List<Skill>> GetSkillsAsync();
    Task<Skill?> GetSkillAsync(string name);
    Task SaveSkillAsync(Skill skill);
    Task DeleteSkillAsync(string name);
}
