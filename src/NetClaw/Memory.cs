// NetClaw - 记忆存储

using System.Collections.Concurrent;
using System.Text.Json;

namespace NetClaw;

/// <summary>基于文件系统的记忆存储</summary>
public class FileMemory : IMemory
{
    private readonly string _dataDir;
    private readonly ConcurrentDictionary<string, List<Message>> _sessionCache = new();
    private readonly object _fileLock = new();

    public FileMemory(string dataDir)
    {
        _dataDir = dataDir;
        InitializeDirectories();
    }

    private void InitializeDirectories()
    {
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(Path.Combine(_dataDir, "sessions"));
        Directory.CreateDirectory(Path.Combine(_dataDir, "memory"));
        Directory.CreateDirectory(Path.Combine(_dataDir, "state"));
        Directory.CreateDirectory(Path.Combine(_dataDir, "skills"));
    }

    #region 会话管理

    public Task AddMessageAsync(string sessionId, Message message)
    {
        var sessionFile = GetSessionFile(sessionId);
        lock (_fileLock)
        {
            if (!_sessionCache.TryGetValue(sessionId, out var messages))
            {
                messages = new List<Message>();
                _sessionCache[sessionId] = messages;
            }
            messages.Add(message);
            File.WriteAllText(sessionFile, JsonSerializer.Serialize(messages, NetClawJsonContext.Default.ListMessage));
        }
        return Task.CompletedTask;
    }

    public async Task<List<Message>> GetMessagesAsync(string sessionId)
    {
        if (_sessionCache.TryGetValue(sessionId, out var cached)) return cached;
        var sessionFile = GetSessionFile(sessionId);
        if (File.Exists(sessionFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(sessionFile);
                var messages = JsonSerializer.Deserialize(json, NetClawJsonContext.Default.ListMessage) ?? new List<Message>();
                _sessionCache[sessionId] = messages;
                return messages;
            }
            catch { return new List<Message>(); }
        }
        return new List<Message>();
    }

    public Task ClearSessionAsync(string sessionId)
    {
        _sessionCache.TryRemove(sessionId, out _);
        var sessionFile = GetSessionFile(sessionId);
        if (File.Exists(sessionFile)) File.Delete(sessionFile);
        return Task.CompletedTask;
    }

    public Task<List<string>> GetSessionListAsync()
    {
        var sessionsDir = Path.Combine(_dataDir, "sessions");
        return Task.FromResult(Directory.GetFiles(sessionsDir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderByDescending(f => f)
            .ToList());
    }

    private string GetSessionFile(string sessionId) => Path.Combine(_dataDir, "sessions", $"{sessionId}.json");

    #endregion

    #region 身份与记忆

    public async Task<string?> GetIdentityAsync() => await ReadMdFileAsync("IDENTITY.md");
    public Task SetIdentityAsync(string content) => WriteMdFileAsync("IDENTITY.md", content);
    public async Task<string?> GetSoulAsync() => await ReadMdFileAsync("SOUL.md");
    public Task SetSoulAsync(string content) => WriteMdFileAsync("SOUL.md", content);
    public async Task<string?> GetMemoryAsync() => await ReadMdFileAsync(Path.Combine("memory", "MEMORY.md"));
    public Task SetMemoryAsync(string content) => WriteMdFileAsync(Path.Combine("memory", "MEMORY.md"), content);
    public async Task<string?> GetUserAsync() => await ReadMdFileAsync("USER.md");
    public Task SetUserAsync(string content) => WriteMdFileAsync("USER.md", content);
    public async Task<string?> GetAgentsAsync() => await ReadMdFileAsync("AGENTS.md");
    public Task SetAgentsAsync(string content) => WriteMdFileAsync("AGENTS.md", content);
    public async Task<string?> GetToolsAsync() => await ReadMdFileAsync("TOOLS.md");
    public Task SetToolsAsync(string content) => WriteMdFileAsync("TOOLS.md", content);

    #endregion

    #region 状态持久化

    public async Task<string?> GetLastChannelAsync()
    {
        var stateFile = Path.Combine(_dataDir, "state", "last_channel.json");
        if (!File.Exists(stateFile)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(stateFile);
            var state = JsonSerializer.Deserialize(json, NetClawJsonContext.Default.DictionaryStringString);
            return state?.GetValueOrDefault("channel");
        }
        catch { return null; }
    }

    public async Task SetLastChannelAsync(string channel)
    {
        var stateFile = Path.Combine(_dataDir, "state", "last_channel.json");
        var state = new Dictionary<string, string> { ["channel"] = channel };
        var json = JsonSerializer.Serialize(state, NetClawJsonContext.Default.DictionaryStringString);
        await File.WriteAllTextAsync(stateFile, json);
    }

    #endregion

    #region 技能管理 (SKILL.md 目录格式)

    /// <summary>获取所有技能</summary>
    public async Task<List<Skill>> GetSkillsAsync()
    {
        var skillsDir = Path.Combine(_dataDir, "skills");
        if (!Directory.Exists(skillsDir)) return new List<Skill>();
        
        var skills = new List<Skill>();
        foreach (var dir in Directory.GetDirectories(skillsDir))
        {
            var skillFile = Path.Combine(dir, "SKILL.md");
            if (File.Exists(skillFile))
            {
                try
                {
                    var skill = await ParseSkillMdAsync(skillFile);
                    if (skill != null) skills.Add(skill);
                }
                catch { }
            }
        }
        return skills;
    }

    /// <summary>获取指定技能</summary>
    public async Task<Skill?> GetSkillAsync(string name)
    {
        var skillDir = FindSkillDir(name);
        if (skillDir == null) return null;
        
        var skillFile = Path.Combine(skillDir, "SKILL.md");
        if (!File.Exists(skillFile)) return null;
        
        return await ParseSkillMdAsync(skillFile);
    }

    /// <summary>保存技能 (SKILL.md 格式)</summary>
    public async Task SaveSkillAsync(Skill skill)
    {
        // 使用 DisplayName 作为目录名，Name 作为工具名
        var dirName = SanitizeDirName(skill.DisplayName ?? skill.Name);
        var skillDir = Path.Combine(_dataDir, "skills", dirName);
        Directory.CreateDirectory(skillDir);
        
        var skillFile = Path.Combine(skillDir, "SKILL.md");
        var content = BuildSkillMd(skill);
        await File.WriteAllTextAsync(skillFile, content);
    }

    /// <summary>删除技能</summary>
    public Task DeleteSkillAsync(string name)
    {
        var skillDir = FindSkillDir(name);
        if (skillDir != null && Directory.Exists(skillDir))
        {
            Directory.Delete(skillDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    /// <summary>查找技能目录</summary>
    private string? FindSkillDir(string name)
    {
        var skillsDir = Path.Combine(_dataDir, "skills");
        if (!Directory.Exists(skillsDir)) return null;
        
        // 先尝试直接匹配目录名
        var dir = Path.Combine(skillsDir, name);
        if (Directory.Exists(dir)) return dir;
        
        // 遍历所有目录，匹配 SKILL.md 中的 name
        foreach (var d in Directory.GetDirectories(skillsDir))
        {
            var skillFile = Path.Combine(d, "SKILL.md");
            if (File.Exists(skillFile))
            {
                try
                {
                    var content = File.ReadAllText(skillFile);
                    var meta = ParseYamlFrontmatter(content);
                    if (meta != null && meta.TryGetValue("name", out var skillName) && skillName == name)
                        return d;
                }
                catch { }
            }
        }
        return null;
    }

    /// <summary>解析 SKILL.md 文件</summary>
    private async Task<Skill?> ParseSkillMdAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        var meta = ParseYamlFrontmatter(content);
        if (meta == null) return null;
        
        var dirName = Path.GetDirectoryName(filePath)?.Split(Path.DirectorySeparatorChar).Last() ?? "";
        
        return new Skill
        {
            Name = meta.GetValueOrDefault("name", dirName),
            DisplayName = dirName,
            Description = meta.GetValueOrDefault("description", ""),
            Dependencies = meta.GetValueOrDefault("dependencies"),
            Content = content,
            CreatedAt = File.GetCreationTime(filePath),
            UpdatedAt = File.GetLastWriteTime(filePath)
        };
    }

    /// <summary>解析 YAML frontmatter</summary>
    private Dictionary<string, string>? ParseYamlFrontmatter(string content)
    {
        if (!content.StartsWith("---")) return null;
        
        var endIndex = content.IndexOf("\n---", 3);
        if (endIndex == -1) return null;
        
        var yaml = content[3..endIndex].Trim();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var line in yaml.Split('\n'))
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim().Trim('"').Trim('\'');
                result[key] = value;
            }
        }
        return result;
    }

    /// <summary>构建 SKILL.md 内容</summary>
    private string BuildSkillMd(Skill skill)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {skill.Name}");
        sb.AppendLine($"description: {skill.Description}");
        if (!string.IsNullOrEmpty(skill.Dependencies))
            sb.AppendLine($"dependencies: {skill.Dependencies}");
        sb.AppendLine("---");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(skill.Content))
        {
            // 如果 Content 已包含 frontmatter，提取 body 部分
            var content = skill.Content;
            if (content.StartsWith("---"))
            {
                var endIndex = content.IndexOf("\n---", 3);
                if (endIndex > 0)
                    content = content[(endIndex + 4)..].TrimStart();
            }
            sb.Append(content);
        }
        return sb.ToString();
    }

    /// <summary>清理目录名</summary>
    private static string SanitizeDirName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new System.Text.StringBuilder();
        foreach (var c in name)
        {
            result.Append(invalid.Contains(c) ? '_' : c);
        }
        return result.ToString().Trim();
    }

    #endregion

    #region 辅助方法

    private async Task<string?> ReadMdFileAsync(string fileName)
    {
        var file = Path.Combine(_dataDir, fileName);
        return File.Exists(file) ? await File.ReadAllTextAsync(file) : null;
    }

    private Task WriteMdFileAsync(string fileName, string content)
    {
        var file = Path.Combine(_dataDir, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return Task.CompletedTask;
    }

    #endregion
}
