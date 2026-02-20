// NetClaw - 渠道 JSON 序列化上下文 (Source Generator)

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetClaw.Channels;

#region 钉钉消息类型

public class DingTalkTextMessage
{
    [JsonPropertyName("msgtype")]
    public string MsgType => "text";
    
    [JsonPropertyName("text")]
    public DingTalkTextContent Text { get; set; } = new();
}

public class DingTalkTextContent
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class DingTalkMarkdownMessage
{
    [JsonPropertyName("msgtype")]
    public string MsgType => "markdown";
    
    [JsonPropertyName("markdown")]
    public DingTalkMarkdownContent Markdown { get; set; } = new();
}

public class DingTalkMarkdownContent
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

#endregion

#region 飞书消息类型

public class FeishuTextMessage
{
    [JsonPropertyName("msg_type")]
    public string MsgType => "text";
    
    [JsonPropertyName("content")]
    public FeishuTextContent Content { get; set; } = new();
}

public class FeishuTextContent
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

public class FeishuInteractiveMessage
{
    [JsonPropertyName("msg_type")]
    public string MsgType => "interactive";
    
    [JsonPropertyName("card")]
    public FeishuCard Card { get; set; } = new();
}

public class FeishuCard
{
    [JsonPropertyName("elements")]
    public List<FeishuCardElement> Elements { get; set; } = new();
}

public class FeishuCardElement
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "div";
    
    [JsonPropertyName("text")]
    public FeishuCardText? Text { get; set; }
}

public class FeishuCardText
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "plain_text";
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class FeishuApiMessage
{
    [JsonPropertyName("receive_id_type")]
    public string ReceiveIdType { get; set; } = "chat_id";
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
    
    [JsonPropertyName("msg_type")]
    public string MsgType { get; set; } = "text";
}

public class FeishuTokenRequest
{
    [JsonPropertyName("app_id")]
    public string AppId { get; set; } = "";
    
    [JsonPropertyName("app_secret")]
    public string AppSecret { get; set; } = "";
}

public class FeishuChallengeResponse
{
    [JsonPropertyName("challenge")]
    public string Challenge { get; set; } = "";
}

#endregion

#region QQ OneBot 消息类型

public class QQGroupMessage
{
    [JsonPropertyName("group_id")]
    public long GroupId { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class QQPrivateMessage
{
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class QQEmptyRequest
{
}

#endregion

#region Gateway 响应类型

public class GatewayStatus
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "NetClaw Gateway";
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";
    
    [JsonPropertyName("channels")]
    public List<string> Channels { get; set; } = new();
    
    [JsonPropertyName("time")]
    public string Time { get; set; } = "";
}

public class GatewayHealth
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok";
}

public class GatewaySuccess
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;
}

public class GatewayError
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = "";
}

#endregion

/// <summary>渠道 JSON 序列化上下文</summary>
[JsonSerializable(typeof(DingTalkTextMessage))]
[JsonSerializable(typeof(DingTalkMarkdownMessage))]
[JsonSerializable(typeof(FeishuTextMessage))]
[JsonSerializable(typeof(FeishuInteractiveMessage))]
[JsonSerializable(typeof(FeishuApiMessage))]
[JsonSerializable(typeof(FeishuTokenRequest))]
[JsonSerializable(typeof(FeishuChallengeResponse))]
[JsonSerializable(typeof(QQGroupMessage))]
[JsonSerializable(typeof(QQPrivateMessage))]
[JsonSerializable(typeof(QQEmptyRequest))]
[JsonSerializable(typeof(GatewayStatus))]
[JsonSerializable(typeof(GatewayHealth))]
[JsonSerializable(typeof(GatewaySuccess))]
[JsonSerializable(typeof(GatewayError))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class ChannelJsonContext : JsonSerializerContext
{
}
