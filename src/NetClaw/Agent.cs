// NetClaw - Agent 核心

using Microsoft.Extensions.Logging;

namespace NetClaw;

/// <summary>AI Agent 核心实现</summary>
public class AgentLoop
{
    private readonly ILLMProvider _llmProvider;
    private readonly IMemory _memory;
    private readonly Dictionary<string, ITool> _tools;
    private readonly AgentConfig _config;
    private readonly ILogger? _logger;

    public AgentLoop(ILLMProvider llmProvider, IMemory memory, IEnumerable<ITool> tools, AgentConfig config, ILogger? logger = null)
    {
        _llmProvider = llmProvider;
        _memory = memory;
        _config = config;
        _logger = logger;
        _tools = tools.ToDictionary(t => t.Name, t => t);
    }

    public async Task<AgentResult> RunAsync(string sessionId, string userMessage, CancellationToken cancellationToken = default)
    {
        var result = new AgentResult();
        var messages = await _memory.GetMessagesAsync(sessionId);

        if (messages.Count == 0)
            messages.Add(Message.System(await BuildSystemPrompt()));

        messages.Add(Message.User(userMessage));

        var toolDefinitions = _tools.Values.Select(t => t.GetDefinition()).ToList();
        var iterations = 0;

        while (iterations < _config.MaxToolIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            iterations++;
            _logger?.LogDebug("Agent 迭代 {Iteration}", iterations);

            var response = await _llmProvider.ChatAsync(messages, toolDefinitions, _config.Model, _config.MaxTokens, _config.Temperature, cancellationToken);
            result.TotalInputTokens += response.InputTokens;
            result.TotalOutputTokens += response.OutputTokens;

            if (!response.HasToolCalls)
            {
                await _memory.AddMessageAsync(sessionId, Message.Assistant(response.Content));
                result.Response = response.Content;
                return result;
            }

            messages.Add(new Message { Role = MessageRole.Assistant, Content = response.Content, ToolCalls = response.ToolCalls });

            foreach (var toolCall in response.ToolCalls!)
            {
                result.ToolCallsCount++;
                _logger?.LogInformation("执行工具: {ToolName}", toolCall.Function.Name);

                var toolResult = _tools.TryGetValue(toolCall.Function.Name, out var tool)
                    ? await tool.ExecuteAsync(toolCall.Function.Arguments, cancellationToken)
                    : $"错误: 未知的工具 '{toolCall.Function.Name}'";

                messages.Add(new Message { Role = MessageRole.Tool, Content = toolResult, ToolCallId = toolCall.Id, Name = toolCall.Function.Name });
            }
        }

        result.Response = "已达到最大工具迭代次数，请尝试简化请求。";
        return result;
    }

    private async Task<string> BuildSystemPrompt()
    {
        var parts = new List<string>();

        // 身份
        var identity = await _memory.GetIdentityAsync();
        if (!string.IsNullOrEmpty(identity)) parts.Add($"# 身份\n{identity}");

        // 灵魂/性格
        var soul = await _memory.GetSoulAsync();
        if (!string.IsNullOrEmpty(soul)) parts.Add($"# 性格\n{soul}");

        // Agent 行为指南
        var agents = await _memory.GetAgentsAsync();
        if (!string.IsNullOrEmpty(agents)) parts.Add($"# 行为指南\n{agents}");

        // 用户信息
        var user = await _memory.GetUserAsync();
        if (!string.IsNullOrEmpty(user)) parts.Add($"# 用户信息\n{user}");

        // 长期记忆
        var memory = await _memory.GetMemoryAsync();
        if (!string.IsNullOrEmpty(memory)) parts.Add($"# 长期记忆\n{memory}");

        // 工作目录
        parts.Add($"# 工作目录\n当前工作目录: {_config.Workspace}");

        // 工具描述（包含技能工具）
        parts.Add("# 可用工具");
        foreach (var tool in _tools.Values) parts.Add($"- {tool.Name}: {tool.Description}");

        // 工具详细说明
        var toolsMd = await _memory.GetToolsAsync();
        if (!string.IsNullOrEmpty(toolsMd)) parts.Add($"# 工具使用指南\n{toolsMd}");

        parts.Add("\n当需要使用工具时，请返回工具调用。可以在单个响应中调用多个工具。");

        return string.Join("\n\n", parts);
    }

    public async Task ClearSessionAsync(string sessionId) => await _memory.ClearSessionAsync(sessionId);
}
