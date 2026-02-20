// NetClaw - 飞书渠道

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NetClaw.Channels;

/// <summary>飞书渠道实现</summary>
public class FeishuChannel : IChannel
{
    private readonly FeishuChannelConfig _config;
    private readonly HttpClient _httpClient;
    private readonly MessageHandler? _messageHandler;
    private string? _accessToken;
    private DateTime _tokenExpireTime;
    
    public string Name => "feishu";
    public bool IsEnabled => _config.Enabled;
    
    public FeishuChannel(FeishuChannelConfig config, MessageHandler? messageHandler = null)
    {
        _config = config;
        _messageHandler = messageHandler;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled) return;
        Console.WriteLine($"[{Name}] 渠道已启用 (Webhook: {!string.IsNullOrEmpty(_config.WebhookUrl)})");
        
        // 如果配置了 App ID/Secret，获取 access token
        if (!string.IsNullOrEmpty(_config.AppId) && !string.IsNullOrEmpty(_config.AppSecret))
        {
            await RefreshAccessTokenAsync(cancellationToken);
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _httpClient.Dispose();
        return Task.CompletedTask;
    }
    
    /// <summary>发送消息 (通过 Webhook)</summary>
    public async Task SendMessageAsync(string chatId, string message, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_config.WebhookUrl))
        {
            await SendWebhookMessageAsync(message, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(_accessToken))
        {
            await SendApiMessageAsync(chatId, message, cancellationToken);
        }
        else
        {
            Console.WriteLine($"[{Name}] 未配置 Webhook URL 或 Access Token，无法发送消息");
        }
    }
    
    /// <summary>通过 Webhook 发送消息</summary>
    private async Task SendWebhookMessageAsync(string message, CancellationToken cancellationToken)
    {
        var payload = new FeishuTextMessage
        {
            Content = new FeishuTextContent { Text = message }
        };
        
        var json = JsonSerializer.Serialize(payload, ChannelJsonContext.Default.FeishuTextMessage);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync(_config.WebhookUrl!, content, cancellationToken);
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
            Console.WriteLine($"[{Name}] 发送失败: {result}");
    }
    
    /// <summary>通过 API 发送消息</summary>
    private async Task SendApiMessageAsync(string chatId, string message, CancellationToken cancellationToken)
    {
        var textContent = JsonSerializer.Serialize(new FeishuTextContent { Text = message }, ChannelJsonContext.Default.FeishuTextContent);
        var payload = new FeishuApiMessage
        {
            ReceiveIdType = "chat_id",
            Content = textContent,
            MsgType = "text"
        };
        
        var json = JsonSerializer.Serialize(payload, ChannelJsonContext.Default.FeishuApiMessage);
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://open.feishu.cn/open-apis/im/v1/messages?receive_id_type=chat_id&receive_id={chatId}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        
        await _httpClient.SendAsync(request, cancellationToken);
    }
    
    /// <summary>发送富文本/卡片消息</summary>
    public async Task SendInteractiveAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_config.WebhookUrl)) return;
        
        var payload = new FeishuInteractiveMessage
        {
            Card = new FeishuCard
            {
                Elements = new List<FeishuCardElement>
                {
                    new FeishuCardElement
                    {
                        Tag = "div",
                        Text = new FeishuCardText { Tag = "plain_text", Content = message }
                    }
                }
            }
        };
        
        var json = JsonSerializer.Serialize(payload, ChannelJsonContext.Default.FeishuInteractiveMessage);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PostAsync(_config.WebhookUrl!, content, cancellationToken);
    }
    
    /// <summary>刷新 Access Token</summary>
    private async Task RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_config.AppId) || string.IsNullOrEmpty(_config.AppSecret)) return;
        
        var url = "https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal";
        var payload = new FeishuTokenRequest
        {
            AppId = _config.AppId,
            AppSecret = _config.AppSecret
        };
        
        var json = JsonSerializer.Serialize(payload, ChannelJsonContext.Default.FeishuTokenRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        
        using var doc = JsonDocument.Parse(result);
        if (doc.RootElement.TryGetProperty("tenant_access_token", out var token))
        {
            _accessToken = token.GetString();
            if (doc.RootElement.TryGetProperty("expire", out var expire))
            {
                _tokenExpireTime = DateTime.UtcNow.AddSeconds(expire.GetInt32() - 300);
            }
        }
    }
    
    /// <summary>处理事件回调</summary>
    public async Task<string> HandleEventAsync(Stream body, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(body);
        var json = await reader.ReadToEndAsync(cancellationToken);
        
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // URL 验证
            if (root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "url_verification")
            {
                var challenge = root.TryGetProperty("challenge", out var c) ? c.GetString() ?? "" : "";
                var response = new FeishuChallengeResponse { Challenge = challenge };
                return JsonSerializer.Serialize(response, ChannelJsonContext.Default.FeishuChallengeResponse);
            }
            
            // 处理消息事件
            if (root.TryGetProperty("header", out var header))
            {
                var eventType = header.TryGetProperty("event_type", out var et) ? et.GetString() : "";
                if (eventType == "im.message.receive_v1" && root.TryGetProperty("event", out var eventEl))
                {
                    var sender = eventEl.GetProperty("sender");
                    var senderId = sender.GetProperty("sender_id").GetProperty("user_id").GetString() ?? "";
                    var senderName = sender.GetProperty("sender_id").TryGetProperty("union_id", out var un) ? un.GetString() ?? senderId : senderId;
                    
                    var message = eventEl.GetProperty("message");
                    var chatId = message.TryGetProperty("chat_id", out var ci) ? ci.GetString() ?? "" : "";
                    var content = message.TryGetProperty("content", out var cnt) ? cnt.GetString() ?? "" : "";
                    var msgType = message.TryGetProperty("message_type", out var mt) ? mt.GetString() : "text";
                    
                    // 解析消息内容
                    string textContent = content;
                    if (msgType == "text")
                    {
                        try
                        {
                            var contentDoc = JsonDocument.Parse(content);
                            if (contentDoc.RootElement.TryGetProperty("content", out var tc))
                                textContent = tc.GetString() ?? "";
                        }
                        catch { }
                    }
                    
                    if (_messageHandler != null && !string.IsNullOrEmpty(textContent))
                    {
                        var channelMsg = new ChannelMessage
                        {
                            Channel = Name,
                            ChatId = chatId,
                            SenderId = senderId,
                            SenderName = senderName,
                            Content = textContent
                        };
                        
                        if (_config.AllowFrom.Count == 0 || _config.AllowFrom.Contains(senderId))
                            await _messageHandler(channelMsg, cancellationToken);
                    }
                }
            }
            
            return "{}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{Name}] 解析事件失败: {ex.Message}");
            return "{}";
        }
    }
}
