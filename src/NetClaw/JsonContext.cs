// NetClaw - JSON 序列化上下文 (Source Generator)

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetClaw;

[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(List<Message>))]
[JsonSerializable(typeof(ToolCall))]
[JsonSerializable(typeof(ToolCallFunction))]
[JsonSerializable(typeof(ToolDefinition))]
[JsonSerializable(typeof(ToolFunction))]
[JsonSerializable(typeof(ToolParameters))]
[JsonSerializable(typeof(ToolProperty))]
[JsonSerializable(typeof(AgentConfig))]
[JsonSerializable(typeof(ProviderConfig))]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(Skill))]
[JsonSerializable(typeof(ChannelsConfig))]
[JsonSerializable(typeof(DingTalkChannelConfig))]
[JsonSerializable(typeof(FeishuChannelConfig))]
[JsonSerializable(typeof(QQChannelConfig))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(List<Dictionary<string, object?>>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class NetClawJsonContext : JsonSerializerContext
{
}
