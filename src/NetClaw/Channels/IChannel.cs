// NetClaw - 渠道接口

namespace NetClaw.Channels;

/// <summary>消息渠道接口</summary>
public interface IChannel
{
    /// <summary>渠道名称</summary>
    string Name { get; }
    
    /// <summary>是否已启用</summary>
    bool IsEnabled { get; }
    
    /// <summary>启动渠道服务</summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>停止渠道服务</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
    
    /// <summary>发送消息</summary>
    Task SendMessageAsync(string chatId, string message, CancellationToken cancellationToken = default);
}

/// <summary>接收到的消息</summary>
public class ChannelMessage
{
    /// <summary>渠道名称</summary>
    public string Channel { get; set; } = string.Empty;
    
    /// <summary>聊天ID (群ID或用户ID)</summary>
    public string ChatId { get; set; } = string.Empty;
    
    /// <summary>发送者ID</summary>
    public string SenderId { get; set; } = string.Empty;
    
    /// <summary>发送者名称</summary>
    public string? SenderName { get; set; }
    
    /// <summary>消息内容</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>消息ID (用于回复)</summary>
    public string? MessageId { get; set; }
    
    /// <summary>是否是群消息</summary>
    public bool IsGroup { get; set; }
    
    /// <summary>是否 @ 机器人</summary>
    public bool IsMentioned { get; set; }
}

/// <summary>消息处理委托</summary>
public delegate Task MessageHandler(ChannelMessage message, CancellationToken cancellationToken);
