// NetClaw - 内置工具

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NetClaw;

/// <summary>文件读取工具</summary>
public class ReadFileTool : ITool
{
    public string Name => "read_file";
    public string Description => "读取文件内容";
    public ToolDefinition GetDefinition() => new()
    {
        Function = new ToolFunction
        {
            Name = Name,
            Description = Description,
            Parameters = new ToolParameters
            {
                Properties = new Dictionary<string, ToolProperty>
                {
                    ["path"] = new ToolProperty { Type = "string", Description = "文件的绝对路径" }
                },
                Required = new List<string> { "path" }
            }
        }
    };

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize(arguments, NetClawJsonContext.Default.DictionaryStringJsonElement);
            if (args == null || !args.TryGetValue("path", out var pathElement))
                return Task.FromResult("错误: 缺少必需参数 'path'");

            var path = pathElement.GetString();
            if (string.IsNullOrEmpty(path)) return Task.FromResult("错误: 路径不能为空");
            if (!File.Exists(path)) return Task.FromResult($"错误: 文件不存在: {path}");

            var content = File.ReadAllText(path);
            return Task.FromResult(content.Length > 50000 ? content[..50000] + "\n... (已截断)" : content);
        }
        catch (Exception ex) { return Task.FromResult($"错误: {ex.Message}"); }
    }
}

/// <summary>文件写入工具</summary>
public class WriteFileTool : ITool
{
    public string Name => "write_file";
    public string Description => "写入内容到文件";
    public ToolDefinition GetDefinition() => new()
    {
        Function = new ToolFunction
        {
            Name = Name,
            Description = Description,
            Parameters = new ToolParameters
            {
                Properties = new Dictionary<string, ToolProperty>
                {
                    ["path"] = new ToolProperty { Type = "string", Description = "文件的绝对路径" },
                    ["content"] = new ToolProperty { Type = "string", Description = "要写入的内容" }
                },
                Required = new List<string> { "path", "content" }
            }
        }
    };

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize(arguments, NetClawJsonContext.Default.DictionaryStringJsonElement);
            if (args == null) return Task.FromResult("错误: 无效的参数");
            if (!args.TryGetValue("path", out var pathElement)) return Task.FromResult("错误: 缺少参数 'path'");
            if (!args.TryGetValue("content", out var contentElement)) return Task.FromResult("错误: 缺少参数 'content'");

            var path = pathElement.GetString();
            var content = contentElement.GetString();
            if (string.IsNullOrEmpty(path)) return Task.FromResult("错误: 路径不能为空");

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, content ?? string.Empty);
            return Task.FromResult($"文件写入成功: {path}");
        }
        catch (Exception ex) { return Task.FromResult($"错误: {ex.Message}"); }
    }
}

/// <summary>列出目录工具</summary>
public class ListDirTool : ITool
{
    public string Name => "list_dir";
    public string Description => "列出目录内容";
    public ToolDefinition GetDefinition() => new()
    {
        Function = new ToolFunction
        {
            Name = Name,
            Description = Description,
            Parameters = new ToolParameters
            {
                Properties = new Dictionary<string, ToolProperty>
                {
                    ["path"] = new ToolProperty { Type = "string", Description = "目录的绝对路径" }
                },
                Required = new List<string> { "path" }
            }
        }
    };

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize(arguments, NetClawJsonContext.Default.DictionaryStringJsonElement);
            if (args == null || !args.TryGetValue("path", out var pathElement))
                return Task.FromResult("错误: 缺少必需参数 'path'");

            var path = pathElement.GetString();
            if (string.IsNullOrEmpty(path)) return Task.FromResult("错误: 路径不能为空");
            if (!Directory.Exists(path)) return Task.FromResult($"错误: 目录不存在: {path}");

            var entries = Directory.GetFileSystemEntries(path)
                .Select(p => Directory.Exists(p) ? $"[目录] {Path.GetFileName(p)}/" : $"[文件] {Path.GetFileName(p)}");
            return Task.FromResult(string.Join("\n", entries));
        }
        catch (Exception ex) { return Task.FromResult($"错误: {ex.Message}"); }
    }
}

/// <summary>Shell 命令执行工具</summary>
public class ShellTool : ITool
{
    private static readonly string[] DangerousCommands = { "rm -rf", "del /f", "rmdir /s", "format", "mkfs", "diskpart", "dd if=", "shutdown", "reboot", "poweroff" };

    public string Name => "exec";
    public string Description => "执行 Shell 命令";
    public ToolDefinition GetDefinition() => new()
    {
        Function = new ToolFunction
        {
            Name = Name,
            Description = Description,
            Parameters = new ToolParameters
            {
                Properties = new Dictionary<string, ToolProperty>
                {
                    ["command"] = new ToolProperty { Type = "string", Description = "要执行的命令" },
                    ["timeout"] = new ToolProperty { Type = "integer", Description = "超时秒数 (默认: 30)" }
                },
                Required = new List<string> { "command" }
            }
        }
    };

    public async Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize(arguments, NetClawJsonContext.Default.DictionaryStringJsonElement);
            if (args == null || !args.TryGetValue("command", out var cmdElement))
                return "错误: 缺少必需参数 'command'";

            var command = cmdElement.GetString();
            if (string.IsNullOrEmpty(command)) return "错误: 命令不能为空";

            foreach (var dangerous in DangerousCommands)
                if (command.Contains(dangerous, StringComparison.OrdinalIgnoreCase))
                    return $"错误: 命令被阻止 (包含危险模式: {dangerous})";

            var timeout = args.TryGetValue("timeout", out var timeoutElement) ? Math.Min(timeoutElement.GetInt32(), 300) : 30;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
                    Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
            var error = await process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            var result = new List<string>();
            if (!string.IsNullOrEmpty(output)) result.Add(output);
            if (!string.IsNullOrEmpty(error)) result.Add($"[错误输出] {error}");
            result.Add($"[退出码: {process.ExitCode}]");
            return string.Join("\n", result);
        }
        catch (OperationCanceledException) { return "错误: 命令超时"; }
        catch (Exception ex) { return $"错误: {ex.Message}"; }
    }
}

/// <summary>技能调用工具</summary>
public class SkillTool : ITool
{
    private readonly Skill _skill;
    private readonly Func<string, Task<string>> _executeSkill;

    public string Name => _skill.GetToolName();
    public string Description => _skill.Description;

    public SkillTool(Skill skill, Func<string, Task<string>> executeSkill)
    {
        _skill = skill;
        _executeSkill = executeSkill;
    }

    public ToolDefinition GetDefinition() => new()
    {
        Function = new ToolFunction
        {
            Name = Name,
            Description = _skill.Description,
            Parameters = new ToolParameters
            {
                Properties = new Dictionary<string, ToolProperty>
                {
                    ["input"] = new ToolProperty { Type = "string", Description = "技能输入参数" }
                },
                Required = new List<string>()
            }
        }
    };

    public async Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize(arguments, NetClawJsonContext.Default.DictionaryStringJsonElement);
            var input = args?.TryGetValue("input", out var inputElement) == true ? inputElement.GetString() ?? "" : "";
            return await _executeSkill(input);
        }
        catch (Exception ex) { return $"错误: {ex.Message}"; }
    }
}
