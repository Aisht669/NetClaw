// NetClaw - 钉钉渠道

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NetClaw.Channels;

/// <summary>钉钉渠道实现</summary>
public class DingTalkChannel : IChannel
{
    private readonly DingTalkChannelConfig _config;
    private readonly HttpClient _httpClient;
    private readonly MessageHandler? _messageHandler;
    private readonly string? _dataDir;
    
    public string Name => "dingtalk";
    public bool IsEnabled => _config.Enabled;
    
    public DingTalkChannel(DingTalkChannelConfig config, MessageHandler? messageHandler = null, string? dataDir = null)
    {
        _config = config;
        _messageHandler = messageHandler;
        _dataDir = dataDir;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }
    
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled) return Task.CompletedTask;
        Console.WriteLine($"[{Name}] 渠道已启用 (Webhook: {!string.IsNullOrEmpty(_config.WebhookUrl)})");
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _httpClient.Dispose();
        return Task.CompletedTask;
    }
    
    /// <summary>发送消息 (通过 Webhook)</summary>
    public async Task SendMessageAsync(string chatId, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_config.WebhookUrl))
        {
            Console.WriteLine($"[{Name}] 未配置 Webhook URL，无法发送消息");
            return;
        }
        
        var url = _config.WebhookUrl;
        
        // 如果有密钥，添加签名
        if (!string.IsNullOrEmpty(_config.Secret))
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var stringToSign = $"{timestamp}\n{_config.Secret}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_config.Secret));
            var sign = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            var signEncoded = Uri.EscapeDataString(sign);
            url += $"&timestamp={timestamp}&sign={signEncoded}";
        }
        
        var payload = new DingTalkTextMessage
        {
            Text = new DingTalkTextContent { Content = message }
        };
        
        var json = JsonSerializer.Serialize(payload, ChannelJsonContext.Default.DingTalkTextMessage);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
            Console.WriteLine($"[{Name}] 发送失败: {result}");
    }
    
    /// <summary>发送 Markdown 消息</summary>
    public async Task SendMarkdownAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_config.WebhookUrl)) return;
        
        var url = _config.WebhookUrl;
        if (!string.IsNullOrEmpty(_config.Secret))
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var stringToSign = $"{timestamp}\n{_config.Secret}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_config.Secret));
            var sign = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            url += $"&timestamp={timestamp}&sign={Uri.EscapeDataString(sign)}";
        }
        
        var payload = new DingTalkMarkdownMessage
        {
            Markdown = new DingTalkMarkdownContent { Title = title, Text = message }
        };
        
        var json = JsonSerializer.Serialize(payload, ChannelJsonContext.Default.DingTalkMarkdownMessage);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PostAsync(url, content, cancellationToken);
    }
    
    /// <summary>处理 Webhook 回调</summary>
    public async Task HandleWebhookAsync(Stream body, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(body);
        var json = await reader.ReadToEndAsync(cancellationToken);
        
        // 解析钉钉回调消息
        // 格式参考: https://open.dingtalk.com/document/orgapp/receive-message
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var msgType = root.TryGetProperty("msgtype", out var mt) ? mt.GetString() : "text";
            var senderId = root.TryGetProperty("senderNick", out var sn) ? sn.GetString() ?? "" : "";
            var chatId = root.TryGetProperty("conversationId", out var ci) ? ci.GetString() ?? "" : "";
            
            string content = "";
            if (msgType == "text" && root.TryGetProperty("text", out var textEl))
            {
                content = textEl.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            }
            
            if (_messageHandler != null && !string.IsNullOrEmpty(content))
            {
                var channelMsg = new ChannelMessage
                {
                    Channel = Name,
                    ChatId = chatId,
                    SenderId = senderId,
                    SenderName = senderId,
                    Content = content,
                    IsGroup = chatId.StartsWith("cid")
                };
                
                // 检查权限
                if (_config.AllowFrom.Count > 0 && !_config.AllowFrom.Contains(senderId ?? ""))
                    return;
                
                await _messageHandler(channelMsg, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{Name}] 解析消息失败: {ex.Message}");
        }
    }
}
