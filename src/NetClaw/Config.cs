// NetClaw - 配置管理

using System.Text.Json;

namespace NetClaw;

/// <summary>配置管理</summary>
public class ConfigManager
{
    private static readonly string HomeDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".netclaw");
    
    /// <summary>
    /// 获取配置目录（优先 exe 目录，然后用户目录）
    /// </summary>
    public static string FindDataDir()
    {
        // 优先检查 exe 所在目录
        var exeDir = AppContext.BaseDirectory;
        var exeDataDir = Path.Combine(exeDir, ".netclaw");
        var exeConfigPath = Path.Combine(exeDataDir, "config.json");
        
        if (File.Exists(exeConfigPath))
            return exeDataDir;
        
        // 然后检查用户目录
        var homeConfigPath = Path.Combine(HomeDataDir, "config.json");
        if (File.Exists(homeConfigPath))
            return HomeDataDir;
        
        // 默认使用 exe 目录（新配置）
        return exeDataDir;
    }
    
    private readonly string _dataDir;
    private AppConfig _config;

    public ConfigManager()
    {
        _dataDir = FindDataDir();
        _config = new AppConfig { DataDir = _dataDir };
    }

    public AppConfig Config => _config;
    public bool Exists => File.Exists(ConfigPath);
    public string ConfigPath => Path.Combine(_dataDir, "config.json");
    public string ConfigDirectory => _dataDir;

    public async Task LoadAsync()
    {
        if (!File.Exists(ConfigPath))
            throw new FileNotFoundException($"配置文件不存在: {ConfigPath}\n请先运行 'netclaw onboard' 初始化配置。");

        var json = await File.ReadAllTextAsync(ConfigPath);
        _config = JsonSerializer.Deserialize(json, NetClawJsonContext.Default.AppConfig) ?? new AppConfig { DataDir = _dataDir };
        
        // 确保加载后 DataDir 正确
        if (string.IsNullOrEmpty(_config.DataDir))
            _config.DataDir = _dataDir;
    }

    public async Task SaveAsync()
    {
        Directory.CreateDirectory(_dataDir);
        _config.DataDir = _dataDir; // 确保保存时 DataDir 正确
        var json = JsonSerializer.Serialize(_config, NetClawJsonContext.Default.AppConfig);
        await File.WriteAllTextAsync(ConfigPath, json);
    }

    public void SetDefault()
    {
        _config = new AppConfig
        {
            DataDir = _dataDir,
            Agents = new AgentConfig
            {
                Model = "gpt-4o-mini",
                MaxTokens = 4096,
                Temperature = 0.7,
                MaxToolIterations = 20,
                Workspace = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                AutoSave = true,
                AutoSaveInterval = 5,
                SystemPrompt = ""
            },
            Providers = new Dictionary<string, ProviderConfig>(),
            DefaultProvider = "openai"
        };
    }

    public void SetProvider(string name, string apiKey, string? apiBase = null, string? defaultModel = null, bool isLocal = false)
    {
        _config.Providers[name] = new ProviderConfig { ApiKey = apiKey, ApiBase = apiBase ?? string.Empty, DefaultModel = defaultModel, IsLocal = isLocal };
    }

    public ProviderConfig? GetProvider(string? name = null)
    {
        var providerName = name ?? _config.DefaultProvider;
        return _config.Providers.TryGetValue(providerName, out var config) ? config : null;
    }

    public async Task InitializeDataDirAsync()
    {
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(Path.Combine(_dataDir, "sessions"));
        Directory.CreateDirectory(Path.Combine(_dataDir, "memory"));
        Directory.CreateDirectory(Path.Combine(_dataDir, "state"));
        Directory.CreateDirectory(Path.Combine(_dataDir, "skills"));

        // 初始化默认文件
        await InitDefaultFileAsync("IDENTITY.md", "# 身份\n\n你是 NetClaw，一个基于 .NET 构建的 AI 助手。\n");
        await InitDefaultFileAsync("SOUL.md", "# 性格\n\n你是一个友好、专业、高效的人工智能助手。\n");
        await InitDefaultFileAsync("USER.md", "# 用户信息\n\n在这里告诉我关于你自己的信息...\n");
        await InitDefaultFileAsync("AGENTS.md", "# 行为指南\n\n- 保持简洁、准确的回答\n- 使用工具前先说明意图\n- 遇到不确定的问题主动询问\n");
        await InitDefaultFileAsync("TOOLS.md", "# 工具使用指南\n\n- read_file: 读取文件内容\n- write_file: 写入内容到文件\n- list_dir: 列出目录内容\n- exec: 执行 Shell 命令\n");
        await InitDefaultFileAsync(Path.Combine("memory", "MEMORY.md"), "# 长期记忆\n\n重要的事情要记住...\n");
    }

    private async Task InitDefaultFileAsync(string fileName, string defaultContent)
    {
        var file = Path.Combine(_dataDir, fileName);
        if (!File.Exists(file))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, defaultContent);
        }
    }
}
