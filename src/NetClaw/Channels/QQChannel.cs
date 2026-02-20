// NetClaw - QQ 渠道 (基于 OneBot 协议，支持 NapCat/Lagrange 等)

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NetClaw.Channels;

/// <summary>QQ 渠道实现 (OneBot 协议)</summary>
public class QQChannel : IChannel
{
    private readonly QQChannelConfig _config;
    private readonly HttpClient _httpClient;
    private readonly MessageHandler? _messageHandler;
    private System.Net.WebSockets.ClientWebSocket? _webSocket;
    private CancellationTokenSource? _wsCts;
    
    public string Name => "qq";
    public bool IsEnabled => _config.Enabled;
    
    public QQChannel(QQChannelConfig config, MessageHandler? messageHandler = null)
    {
        _config = config;
        _messageHandler = messageHandler;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        
        if (!string.IsNullOrEmpty(_config.AccessToken))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.AccessToken);
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled) return;
        Console.WriteLine($"[{Name}] 渠道已启用 (API: {_config.ApiUrl})");
        
        // 启动 WebSocket 连接接收消息
        if (!string.IsNullOrEmpty(_config.WebSocketUrl))
        {
            _ = StartWebSocketAsync(cancellationToken);
        }
    }
    
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _wsCts?.Cancel();
        if (_webSocket != null)
        {
            await _webSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
            _webSocket.Dispose();
        }
        _httpClient.Dispose();
    }
    
    /// <summary>发送消息</summary>
    public async Task SendMessageAsync(string chatId, string message, CancellationToken cancellationToken = default)
    {
        // 判断是群还是私聊
        var isGroup = chatId.StartsWith("group_");
        var realId = isGroup ? chatId[6..] : chatId;
        
        if (isGroup)
        {
            var payload = new QQGroupMessage { GroupId = long.Parse(realId), Message = message };
            await CallApiAsync("send_group_msg", payload, ChannelJsonContext.Default.QQGroupMessage, cancellationToken);
        }
        else
        {
            var payload = new QQPrivateMessage { UserId = long.Parse(realId), Message = message };
            await CallApiAsync("send_private_msg", payload, ChannelJsonContext.Default.QQPrivateMessage, cancellationToken);
        }
    }
    
    /// <summary>发送群消息</summary>
    public async Task SendGroupMessageAsync(long groupId, string message, CancellationToken cancellationToken = default)
    {
        var payload = new QQGroupMessage { GroupId = groupId, Message = message };
        await CallApiAsync("send_group_msg", payload, ChannelJsonContext.Default.QQGroupMessage, cancellationToken);
    }
    
    /// <summary>发送私聊消息</summary>
    public async Task SendPrivateMessageAsync(long userId, string message, CancellationToken cancellationToken = default)
    {
        var payload = new QQPrivateMessage { UserId = userId, Message = message };
        await CallApiAsync("send_private_msg", payload, ChannelJsonContext.Default.QQPrivateMessage, cancellationToken);
    }
    
    /// <summary>调用 OneBot API</summary>
    private async Task<JsonElement?> CallApiAsync<T>(string endpoint, T payload, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
    {
        var url = $"{_config.ApiUrl?.TrimEnd('/')}/{endpoint}";
        var json = JsonSerializer.Serialize(payload, jsonTypeInfo);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            
            using var doc = JsonDocument.Parse(result);
            if (doc.RootElement.TryGetProperty("status", out var status) && status.GetString() == "failed")
            {
                var errMsg = doc.RootElement.TryGetProperty("wording", out var wording) ? wording.GetString() : "未知错误";
                Console.WriteLine($"[{Name}] API 调用失败: {errMsg}");
                return null;
            }
            return doc.RootElement.GetProperty("data").Clone();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{Name}] API 调用异常: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>启动 WebSocket 连接</summary>
    private async Task StartWebSocketAsync(CancellationToken cancellationToken)
    {
        _webSocket = new System.Net.WebSockets.ClientWebSocket();
        _wsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        try
        {
            await _webSocket.ConnectAsync(new Uri(_config.WebSocketUrl!), _wsCts.Token);
            Console.WriteLine($"[{Name}] WebSocket 已连接");
            
            var buffer = new byte[8192];
            while (_webSocket.State == System.Net.WebSockets.WebSocketState.Open && !_wsCts.Token.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _wsCts.Token);
                if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleWebSocketMessageAsync(json, _wsCts.Token);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{Name}] WebSocket 错误: {ex.Message}");
        }
    }
    
    /// <summary>处理 WebSocket 消息</summary>
    private async Task HandleWebSocketMessageAsync(string json, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // 处理心跳
            if (root.TryGetProperty("meta_event_type", out var metaType) && metaType.GetString() == "heartbeat")
                return;
            
            // 处理消息事件
            if (root.TryGetProperty("post_type", out var postType) && postType.GetString() == "message")
            {
                var messageType = root.TryGetProperty("message_type", out var mt) ? mt.GetString() : "";
                var sender = root.TryGetProperty("sender", out var s) ? s : default;
                
                var senderId = sender.TryGetProperty("user_id", out var uid) ? uid.GetInt64().ToString() : "";
                var senderName = sender.TryGetProperty("nickname", out var nick) ? nick.GetString() ?? senderId : senderId;
                var content = root.TryGetProperty("raw_message", out var raw) ? raw.GetString() ?? "" : "";
                var messageId = root.TryGetProperty("message_id", out var mid) ? mid.GetInt32().ToString() : "";
                
                string chatId;
                bool isGroup;
                
                if (messageType == "group")
                {
                    var groupId = root.TryGetProperty("group_id", out var gid) ? gid.GetInt64().ToString() : "";
                    chatId = $"group_{groupId}";
                    isGroup = true;
                    
                    // 检查群权限
                    if (_config.AllowGroups.Count > 0 && !_config.AllowGroups.Contains(groupId))
                        return;
                }
                else
                {
                    chatId = senderId;
                    isGroup = false;
                }
                
                // 检查用户权限
                if (_config.AllowUsers.Count > 0 && !_config.AllowUsers.Contains(senderId))
                    return;
                
                if (_messageHandler != null && !string.IsNullOrEmpty(content))
                {
                    var channelMsg = new ChannelMessage
                    {
                        Channel = Name,
                        ChatId = chatId,
                        SenderId = senderId,
                        SenderName = senderName,
                        Content = content,
                        MessageId = messageId,
                        IsGroup = isGroup,
                        IsMentioned = root.TryGetProperty("atme", out var atMe) && atMe.GetBoolean()
                    };
                    
                    await _messageHandler(channelMsg, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{Name}] 解析消息失败: {ex.Message}");
        }
    }
    
    /// <summary>获取登录信息</summary>
    public async Task<(long UserId, string Nickname)?> GetLoginInfoAsync(CancellationToken cancellationToken = default)
    {
        var data = await CallApiAsync("get_login_info", new QQEmptyRequest(), ChannelJsonContext.Default.QQEmptyRequest, cancellationToken);
        if (data == null) return null;
        
        var userId = data.Value.TryGetProperty("user_id", out var uid) ? uid.GetInt64() : 0;
        var nickname = data.Value.TryGetProperty("nickname", out var nick) ? nick.GetString() ?? "" : "";
        return (userId, nickname);
    }
    
    /// <summary>获取群列表</summary>
    public async Task<List<(long GroupId, string GroupName)>> GetGroupListAsync(CancellationToken cancellationToken = default)
    {
        var data = await CallApiAsync("get_group_list", new QQEmptyRequest(), ChannelJsonContext.Default.QQEmptyRequest, cancellationToken);
        if (data == null) return new List<(long, string)>();
        
        var result = new List<(long, string)>();
        if (data.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in data.Value.EnumerateArray())
            {
                var groupId = group.TryGetProperty("group_id", out var gid) ? gid.GetInt64() : 0;
                var groupName = group.TryGetProperty("group_name", out var gn) ? gn.GetString() ?? "" : "";
                if (groupId > 0) result.Add((groupId, groupName));
            }
        }
        return result;
    }
}
