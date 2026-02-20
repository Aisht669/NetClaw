// NetClaw - Gateway HTTP 服务器

using System.Net;
using System.Text;
using System.Text.Json;
using NetClaw.Channels;

namespace NetClaw;

/// <summary>Gateway - HTTP 服务器和渠道管理</summary>
public class Gateway : IDisposable
{
    private readonly ChannelsConfig _config;
    private readonly ConfigManager _configManager;
    private readonly HttpListener _listener;
    private readonly Dictionary<string, IChannel> _channels = new();
    private readonly Dictionary<string, string> _sessions = new(); // chatId -> sessionId
    private CancellationTokenSource? _cts;
    private bool _disposed;
    
    public Gateway(ChannelsConfig config, ConfigManager configManager)
    {
        _config = config;
        _configManager = configManager;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{config.Host}:{config.Port}/");
    }
    
    /// <summary>启动 Gateway</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // 初始化渠道
        await InitializeChannelsAsync();
        
        // 启动 HTTP 服务器
        try
        {
            _listener.Start();
            Console.WriteLine($"[Gateway] 服务器启动: http://{_config.Host}:{_config.Port}/");
            Console.WriteLine($"[Gateway] 已启用渠道: {string.Join(", ", _channels.Keys)}");
            
            // 处理请求循环
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = HandleRequestAsync(context, _cts.Token);
                }
                catch (HttpListenerException) when (_cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Gateway] 请求处理错误: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway] 启动失败: {ex.Message}");
        }
    }
    
    /// <summary>停止 Gateway</summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();
        
        foreach (var channel in _channels.Values)
        {
            await channel.StopAsync();
        }
        
        _listener.Stop();
        Console.WriteLine("[Gateway] 服务器已停止");
    }
    
    /// <summary>初始化渠道</summary>
    private async Task InitializeChannelsAsync()
    {
        if (_config.DingTalk?.Enabled == true)
        {
            var channel = new DingTalkChannel(_config.DingTalk, HandleMessageAsync, _configManager.Config.DataDir);
            _channels["dingtalk"] = channel;
            await channel.StartAsync();
        }
        
        if (_config.Feishu?.Enabled == true)
        {
            var channel = new FeishuChannel(_config.Feishu, HandleMessageAsync);
            _channels["feishu"] = channel;
            await channel.StartAsync();
        }
        
        if (_config.QQ?.Enabled == true)
        {
            var channel = new QQChannel(_config.QQ, HandleMessageAsync);
            _channels["qq"] = channel;
            await channel.StartAsync();
        }
    }
    
    /// <summary>处理 HTTP 请求</summary>
    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;
        
        try
        {
            var path = request.Url?.AbsolutePath ?? "/";
            
            // 路由
            var result = path switch
            {
                "/" => HandleRootAsync(response),
                "/health" => HandleHealthAsync(response),
                "/dingtalk" when _channels.TryGetValue("dingtalk", out var ch) => HandleDingTalkWebhookAsync((DingTalkChannel)ch, request, response, cancellationToken),
                "/feishu" when _channels.TryGetValue("feishu", out var ch) => HandleFeishuEventAsync((FeishuChannel)ch, request, response, cancellationToken),
                _ => HandleNotFoundAsync(response)
            };
            
            await result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway] 请求错误: {ex.Message}");
            response.StatusCode = 500;
            var error = new GatewayError { Error = ex.Message };
            await WriteJsonAsync(response, error, ChannelJsonContext.Default.GatewayError);
        }
        finally
        {
            response.Close();
        }
    }
    
    /// <summary>处理根路径</summary>
    private Task HandleRootAsync(HttpListenerResponse response)
    {
        var status = new GatewayStatus
        {
            Name = "NetClaw Gateway",
            Version = "1.0.0",
            Channels = _channels.Keys.ToList(),
            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        return WriteJsonAsync(response, status, ChannelJsonContext.Default.GatewayStatus);
    }
    
    /// <summary>健康检查</summary>
    private Task HandleHealthAsync(HttpListenerResponse response)
    {
        var health = new GatewayHealth { Status = "ok" };
        return WriteJsonAsync(response, health, ChannelJsonContext.Default.GatewayHealth);
    }
    
    /// <summary>处理钉钉 Webhook</summary>
    private async Task HandleDingTalkWebhookAsync(DingTalkChannel channel, HttpListenerRequest request, HttpListenerResponse response, CancellationToken cancellationToken)
    {
        await channel.HandleWebhookAsync(request.InputStream, cancellationToken);
        var success = new GatewaySuccess { Success = true };
        await WriteJsonAsync(response, success, ChannelJsonContext.Default.GatewaySuccess);
    }
    
    /// <summary>处理飞书事件</summary>
    private async Task HandleFeishuEventAsync(FeishuChannel channel, HttpListenerRequest request, HttpListenerResponse response, CancellationToken cancellationToken)
    {
        var result = await channel.HandleEventAsync(request.InputStream, cancellationToken);
        await WriteResponseAsync(response, result);
    }
    
    /// <summary>404 处理</summary>
    private Task HandleNotFoundAsync(HttpListenerResponse response)
    {
        response.StatusCode = 404;
        var error = new GatewayError { Error = "Not found" };
        return WriteJsonAsync(response, error, ChannelJsonContext.Default.GatewayError);
    }
    
    /// <summary>处理消息回调</summary>
    private async Task HandleMessageAsync(ChannelMessage message, CancellationToken cancellationToken)
    {
        var previewLength = Math.Min(50, message.Content.Length);
        Console.WriteLine($"[{message.Channel}] 收到消息: {message.SenderName}: {message.Content[..previewLength]}...");
        
        try
        {
            // 获取或创建会话 ID
            var sessionKey = $"{message.Channel}:{message.ChatId}";
            if (!_sessions.TryGetValue(sessionKey, out var sessionId))
            {
                sessionId = $"session_{DateTime.Now:yyyyMMdd}_{message.Channel}_{message.ChatId}";
                _sessions[sessionKey] = sessionId;
            }
            
            // 调用 Agent 处理消息
            var reply = await ProcessWithAgentAsync(message.Content, sessionId, cancellationToken);
            
            // 发送回复
            if (_channels.TryGetValue(message.Channel, out var channel) && !string.IsNullOrEmpty(reply))
            {
                await channel.SendMessageAsync(message.ChatId, reply, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway] 消息处理错误: {ex.Message}");
        }
    }
    
    /// <summary>使用 Agent 处理消息</summary>
    private async Task<string> ProcessWithAgentAsync(string message, string sessionId, CancellationToken cancellationToken)
    {
        var config = _configManager.Config;
        var providerConfig = _configManager.GetProvider();
        
        if (providerConfig == null || (string.IsNullOrEmpty(providerConfig.ApiKey) && !providerConfig.IsLocal))
            return "错误: 未配置 API 密钥";
        
        var provider = ProviderFactory.Create(
            config.DefaultProvider,
            providerConfig.ApiKey,
            string.IsNullOrEmpty(providerConfig.ApiBase) ? null : providerConfig.ApiBase,
            providerConfig.DefaultModel,
            providerConfig.IsLocal);
        
        var memory = new FileMemory(config.DataDir);
        var tools = new List<ITool> { new ReadFileTool(), new WriteFileTool(), new ListDirTool(), new ShellTool() };
        
        var agent = new AgentLoop(provider, memory, tools, config.Agents, null);
        var result = await agent.RunAsync(sessionId, message, cancellationToken);
        
        return result.Response;
    }
    
    /// <summary>写入 JSON 响应</summary>
    private Task WriteJsonAsync<T>(HttpListenerResponse response, T data, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo)
    {
        response.ContentType = "application/json; charset=utf-8";
        return WriteResponseAsync(response, JsonSerializer.Serialize(data, jsonTypeInfo));
    }
    
    /// <summary>写入响应</summary>
    private Task WriteResponseAsync(HttpListenerResponse response, string content)
    {
        var buffer = Encoding.UTF8.GetBytes(content);
        response.ContentLength64 = buffer.Length;
        return response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _cts?.Dispose();
        _listener.Close();
        
        foreach (var channel in _channels.Values)
        {
            if (channel is IDisposable disposable)
                disposable.Dispose();
        }
    }
}