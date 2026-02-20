// NetClaw - LLM 提供者

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetClaw;

/// <summary>OpenAI 兼容的 LLM 提供者 (支持本地模型如 Ollama)</summary>
public class OpenAIProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiBase;
    private readonly string? _defaultModel;
    private readonly bool _isLocal;

    public string Name => "openai";

    public OpenAIProvider(string apiKey, string? apiBase = null, string? defaultModel = null, bool isLocal = false)
    {
        _apiKey = apiKey;
        _apiBase = apiBase ?? "https://api.openai.com/v1";
        _defaultModel = defaultModel;
        _isLocal = isLocal;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        
        if (!string.IsNullOrEmpty(_apiKey))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<ChatResponse> ChatAsync(List<Message> messages, List<ToolDefinition>? tools = null,
        string? model = null, int? maxTokens = null, double? temperature = null,
        CancellationToken cancellationToken = default)
    {
        // 使用 Dictionary 构建，避免匿名类型序列化问题
        var messageList = messages.Select(m =>
        {
            var msg = new Dictionary<string, object?>
            {
                ["role"] = m.Role.ToString().ToLower(),
                ["content"] = m.Content
            };
            if (!string.IsNullOrEmpty(m.ToolCallId)) msg["tool_call_id"] = m.ToolCallId;
            if (!string.IsNullOrEmpty(m.Name)) msg["name"] = m.Name;
            if (m.ToolCalls != null && m.ToolCalls.Count > 0)
            {
                msg["tool_calls"] = m.ToolCalls.Select(tc => new Dictionary<string, object?>
                {
                    ["id"] = tc.Id,
                    ["type"] = tc.Type,
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = tc.Function.Name,
                        ["arguments"] = tc.Function.Arguments
                    }
                }).ToList();
            }
            return msg;
        }).ToList();

        var request = new Dictionary<string, object?>
        {
            ["model"] = model ?? _defaultModel ?? "gpt-4o-mini",
            ["messages"] = messageList,
            ["max_tokens"] = maxTokens ?? 4096,
            ["temperature"] = temperature ?? 0.7
        };

        if (tools != null && tools.Count > 0)
        {
            request["tools"] = tools.Select(t => new Dictionary<string, object?>
            {
                ["type"] = t.Type,
                ["function"] = new Dictionary<string, object?>
                {
                    ["name"] = t.Function.Name,
                    ["description"] = t.Function.Description,
                    ["parameters"] = t.Function.Parameters
                }
            }).ToList();
        }

        var json = JsonSerializer.Serialize(request, NetClawJsonContext.Default.DictionaryStringObject);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_apiBase}/chat/completions", content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"API 错误: {response.StatusCode} - {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        var choice = root.GetProperty("choices")[0];
        var message = choice.GetProperty("message");

        var toolCalls = message.TryGetProperty("tool_calls", out var tcArr)
            ? tcArr.EnumerateArray().Select(tc => new ToolCall
            {
                Id = tc.GetProperty("id").GetString() ?? "",
                Type = tc.GetProperty("type").GetString() ?? "function",
                Function = new ToolCallFunction
                {
                    Name = tc.GetProperty("function").GetProperty("name").GetString() ?? "",
                    Arguments = tc.GetProperty("function").GetProperty("arguments").GetString() ?? "{}"
                }
            }).ToList()
            : null;

        return new ChatResponse
        {
            Content = message.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "",
            ToolCalls = toolCalls,
            InputTokens = root.TryGetProperty("usage", out var usage) ? usage.GetProperty("prompt_tokens").GetInt32() : 0,
            OutputTokens = root.TryGetProperty("usage", out usage) ? usage.GetProperty("completion_tokens").GetInt32() : 0
        };
    }
}

/// <summary>Anthropic Claude 提供者</summary>
public class AnthropicProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string? _defaultModel;

    public string Name => "anthropic";

    public AnthropicProvider(string apiKey, string? defaultModel = null)
    {
        _apiKey = apiKey;
        _defaultModel = defaultModel;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<ChatResponse> ChatAsync(List<Message> messages, List<ToolDefinition>? tools = null,
        string? model = null, int? maxTokens = null, double? temperature = null,
        CancellationToken cancellationToken = default)
    {
        var systemMessage = messages.FirstOrDefault(m => m.Role == MessageRole.System);
        var otherMessages = messages.Where(m => m.Role != MessageRole.System).ToList();

        // 使用 Dictionary 构建，避免匿名类型序列化问题
        var request = new Dictionary<string, object?>
        {
            ["model"] = model ?? _defaultModel ?? "claude-3-5-sonnet-20241022",
            ["max_tokens"] = maxTokens ?? 4096,
            ["messages"] = ConvertMessages(otherMessages)
        };
        if (systemMessage != null) request["system"] = systemMessage.Content;
        if (tools != null && tools.Count > 0)
        {
            request["tools"] = tools.Select(t => new Dictionary<string, object?>
            {
                ["name"] = t.Function.Name,
                ["description"] = t.Function.Description,
                ["input_schema"] = t.Function.Parameters
            }).ToList();
        }

        var json = JsonSerializer.Serialize(request, NetClawJsonContext.Default.DictionaryStringObject);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"API 错误: {response.StatusCode} - {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        var contentArr = root.GetProperty("content");

        string? textContent = null;
        var toolCalls = new List<ToolCall>();

        foreach (var item in contentArr.EnumerateArray())
        {
            var type = item.GetProperty("type").GetString();
            if (type == "text")
                textContent = item.GetProperty("text").GetString();
            else if (type == "tool_use")
            {
                toolCalls.Add(new ToolCall
                {
                    Id = item.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(),
                    Type = "function",
                    Function = new ToolCallFunction
                    {
                        Name = item.GetProperty("name").GetString() ?? "",
                        Arguments = item.GetProperty("input").GetRawText()
                    }
                });
            }
        }

        return new ChatResponse
        {
            Content = textContent ?? "",
            ToolCalls = toolCalls.Count > 0 ? toolCalls : null,
            InputTokens = root.TryGetProperty("usage", out var usage) ? usage.GetProperty("input_tokens").GetInt32() : 0,
            OutputTokens = root.TryGetProperty("usage", out usage) ? usage.GetProperty("output_tokens").GetInt32() : 0
        };
    }

    private List<Dictionary<string, object?>> ConvertMessages(List<Message> messages)
    {
        var result = new List<Dictionary<string, object?>>();
        foreach (var msg in messages)
        {
            if (msg.Role == MessageRole.Tool)
            {
                result.Add(new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = new[] { new Dictionary<string, object?> { ["type"] = "tool_result", ["tool_use_id"] = msg.ToolCallId, ["content"] = msg.Content } }
                });
            }
            else if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                var contentList = new List<Dictionary<string, object?>>();
                if (!string.IsNullOrEmpty(msg.Content))
                    contentList.Add(new Dictionary<string, object?> { ["type"] = "text", ["text"] = msg.Content });
                foreach (var tc in msg.ToolCalls)
                    contentList.Add(new Dictionary<string, object?> { ["type"] = "tool_use", ["id"] = tc.Id, ["name"] = tc.Function.Name, ["input"] = JsonNode.Parse(tc.Function.Arguments) });
                result.Add(new Dictionary<string, object?> { ["role"] = "assistant", ["content"] = contentList });
            }
            else
            {
                result.Add(new Dictionary<string, object?>
                {
                    ["role"] = msg.Role.ToString().ToLower(),
                    ["content"] = new[] { new Dictionary<string, object?> { ["type"] = "text", ["text"] = msg.Content } }
                });
            }
        }
        return result;
    }
}

/// <summary>LLM 提供者工厂</summary>
public static class ProviderFactory
{
    public static ILLMProvider Create(string providerName, string apiKey, string? apiBase = null, string? defaultModel = null, bool isLocal = false)
    {
        return providerName.ToLower() switch
        {
            "openai" => new OpenAIProvider(apiKey, apiBase ?? "https://api.openai.com/v1", defaultModel),
            "openrouter" => new OpenAIProvider(apiKey, apiBase ?? "https://openrouter.ai/api/v1", defaultModel),
            "deepseek" => new OpenAIProvider(apiKey, apiBase ?? "https://api.deepseek.com/v1", defaultModel),
            "zhipu" => new OpenAIProvider(apiKey, apiBase ?? "https://open.bigmodel.cn/api/paas/v4", defaultModel),
            "moonshot" => new OpenAIProvider(apiKey, apiBase ?? "https://api.moonshot.cn/v1", defaultModel),
            "anthropic" => new AnthropicProvider(apiKey, defaultModel),
            "ollama" => new OpenAIProvider("", apiBase ?? "http://localhost:11434/v1", defaultModel ?? "llama3.2", isLocal: true),
            "local" => new OpenAIProvider(apiKey, apiBase ?? "http://localhost:8080/v1", defaultModel, isLocal: true),
            "custom" => new OpenAIProvider(apiKey, apiBase ?? "", defaultModel, isLocal),
            _ => throw new ArgumentException($"未知的提供者: {providerName}")
        };
    }

    public static string[] GetSupportedProviders() => new[] { "openai", "openrouter", "anthropic", "deepseek", "zhipu", "moonshot", "ollama", "local", "custom" };
    public static bool IsLocalProvider(string providerName) => providerName.ToLower() is "ollama" or "local" or "custom";
}
