// NetClaw - 轻量级 AI 助手 (基于 .NET 重写的 OpenClaw)

using System.CommandLine;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetClaw.Channels;
using Spectre.Console;

namespace NetClaw;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("NetClaw - 基于 .NET 的轻量级 AI 助手");

        // onboard 命令
        var onboardCommand = new Command("onboard", "初始化配置");
        onboardCommand.SetAction(async (parseResult, ct) =>
        {
            var configManager = new ConfigManager();
            await RunOnboardAsync(configManager);
            return 0;
        });
        rootCommand.Subcommands.Add(onboardCommand);

        // agent 命令
        var agentCommand = new Command("agent", "与 AI 对话");
        var messageOption = new Option<string?>("--message", "-m") { Description = "发送单条消息" };
        var providerOption = new Option<string?>("--provider", "-p") { Description = "指定 LLM 提供者" };
        var modelOption = new Option<string?>("--model") { Description = "指定模型" };
        var workdirOption = new Option<string?>("--workdir", "-w") { Description = "指定工作目录" };
        var sessionOption = new Option<string?>("--session", "-s") { Description = "指定会话 ID" };
        agentCommand.Options.Add(messageOption);
        agentCommand.Options.Add(providerOption);
        agentCommand.Options.Add(modelOption);
        agentCommand.Options.Add(workdirOption);
        agentCommand.Options.Add(sessionOption);

        agentCommand.SetAction(async (parseResult, ct) =>
        {
            var message = parseResult.GetValue(messageOption);
            var provider = parseResult.GetValue(providerOption);
            var model = parseResult.GetValue(modelOption);
            var workdir = parseResult.GetValue(workdirOption);
            var session = parseResult.GetValue(sessionOption);

            try
            {
                var configManager = new ConfigManager();
                await configManager.LoadAsync();

                if (!string.IsNullOrEmpty(provider)) configManager.Config.DefaultProvider = provider;
                if (!string.IsNullOrEmpty(model)) configManager.Config.Agents.Model = model;
                if (!string.IsNullOrEmpty(workdir)) configManager.Config.Agents.Workspace = workdir;

                using var host = Host.CreateDefaultBuilder().ConfigureLogging(l => l.SetMinimumLevel(LogLevel.Warning)).Build();
                var logger = host.Services.GetService(typeof(ILogger<AgentService>)) as ILogger<AgentService>;
                var service = new AgentService(configManager, logger);

                await service.RunAsync(message ?? string.Empty, string.IsNullOrEmpty(message), session);
            }
            catch (FileNotFoundException ex) { AnsiConsole.MarkupLine($"[red]错误:[/] {Markup.Escape(ex.Message)}"); return 1; }
            catch (Exception ex) { AnsiConsole.MarkupLine($"[red]错误:[/] {Markup.Escape(ex.Message)}"); return 1; }
            return 0;
        });
        rootCommand.Subcommands.Add(agentCommand);

        // status 命令
        var statusCommand = new Command("status", "显示状态");
        statusCommand.SetAction(async (parseResult, ct) =>
        {
            try
            {
                var configManager = new ConfigManager();
                if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在，请先运行 'netclaw onboard'[/]"); return 0; }
                await configManager.LoadAsync();
                await ShowStatusAsync(configManager);
            }
            catch (Exception ex) { AnsiConsole.MarkupLine($"[red]错误:[/] {Markup.Escape(ex.Message)}"); }
            return 0;
        });
        rootCommand.Subcommands.Add(statusCommand);

        // clear 命令
        var clearCommand = new Command("clear", "清除对话历史");
        clearCommand.SetAction(async (parseResult, ct) =>
        {
            var configManager = new ConfigManager();
            if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在[/]"); return 0; }
            await configManager.LoadAsync();
            var sessionsDir = Path.Combine(configManager.Config.DataDir, "sessions");
            if (Directory.Exists(sessionsDir))
            {
                foreach (var file in Directory.GetFiles(sessionsDir, "*.json")) File.Delete(file);
                AnsiConsole.MarkupLine("[green]对话历史已清除[/]");
            }
            return 0;
        });
        rootCommand.Subcommands.Add(clearCommand);

        // skill 命令
        var skillCommand = new Command("skill", "技能管理");
        
        var skillListCommand = new Command("list", "列出所有技能");
        skillListCommand.SetAction(async (parseResult, ct) =>
        {
            var configManager = new ConfigManager();
            if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在[/]"); return 0; }
            await configManager.LoadAsync();
            var memory = new FileMemory(configManager.Config.DataDir);
            var skills = await memory.GetSkillsAsync();
            
            if (skills.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]暂无技能[/]");
            }
            else
            {
                var table = new Table();
                table.AddColumn("显示名");
                table.AddColumn("工具名");
                table.AddColumn("描述");
                foreach (var skill in skills)
                    table.AddRow(skill.DisplayName, skill.GetToolName(), Markup.Escape(skill.Description));
                AnsiConsole.Write(table);
            }
            return 0;
        });
        skillCommand.Subcommands.Add(skillListCommand);

        var skillAddCommand = new Command("add", "添加技能");
        var skillNameOption = new Option<string>("--name", "-n") { Description = "技能名称 (英文，用于工具调用)", Required = true };
        var skillDisplayOption = new Option<string>("--display", "-d") { Description = "显示名称 (可以是中文)" };
        var skillDescOption = new Option<string>("--desc") { Description = "技能描述" };
        var skillFileOption = new Option<string?>("--file", "-f") { Description = "从文件读取技能内容" };
        skillAddCommand.Options.Add(skillNameOption);
        skillAddCommand.Options.Add(skillDisplayOption);
        skillAddCommand.Options.Add(skillDescOption);
        skillAddCommand.Options.Add(skillFileOption);
        skillAddCommand.SetAction(async (parseResult, ct) =>
        {
            var name = parseResult.GetValue(skillNameOption)!;
            var display = parseResult.GetValue(skillDisplayOption) ?? name;
            var desc = parseResult.GetValue(skillDescOption) ?? $"技能: {display}";
            var file = parseResult.GetValue(skillFileOption);
            
            var configManager = new ConfigManager();
            if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在[/]"); return 0; }
            await configManager.LoadAsync();
            var memory = new FileMemory(configManager.Config.DataDir);
            
            string content;
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                content = await File.ReadAllTextAsync(file);
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]请输入技能内容 (SKILL.md 格式，Ctrl+Z 然后回车结束):[/]");
                AnsiConsole.MarkupLine("[dim]示例:[/]");
                AnsiConsole.MarkupLine("[dim]---[/]");
                AnsiConsole.MarkupLine($"[dim]name: {name}[/]");
                AnsiConsole.MarkupLine($"[dim]description: {desc}[/]");
                AnsiConsole.MarkupLine("[dim]---[/]");
                AnsiConsole.MarkupLine("[dim]# 技能说明[/]");
                AnsiConsole.MarkupLine("[dim]你的技能提示词内容...[/]");
                AnsiConsole.WriteLine();
                
                var lines = new List<string>();
                string? line;
                while ((line = Console.ReadLine()) != null) lines.Add(line);
                content = string.Join("\n", lines);
            }
            
            var skill = new Skill
            {
                Name = name,
                DisplayName = display,
                Description = desc,
                Content = content
            };
            await memory.SaveSkillAsync(skill);
            AnsiConsole.MarkupLine($"[green]技能 '{display}' 已添加 (工具名: {skill.GetToolName()})[/]");
            return 0;
        });
        skillCommand.Subcommands.Add(skillAddCommand);

        var skillDeleteCommand = new Command("delete", "删除技能");
        var skillDeleteNameOption = new Option<string>("--name", "-n") { Description = "技能名称", Required = true };
        skillDeleteCommand.Options.Add(skillDeleteNameOption);
        skillDeleteCommand.SetAction(async (parseResult, ct) =>
        {
            var name = parseResult.GetValue(skillDeleteNameOption)!;
            var configManager = new ConfigManager();
            if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在[/]"); return 0; }
            await configManager.LoadAsync();
            var memory = new FileMemory(configManager.Config.DataDir);
            await memory.DeleteSkillAsync(name);
            AnsiConsole.MarkupLine($"[green]技能 '{name}' 已删除[/]");
            return 0;
        });
        skillCommand.Subcommands.Add(skillDeleteCommand);

        var skillShowCommand = new Command("show", "显示技能内容");
        var skillShowNameOption = new Option<string>("--name", "-n") { Description = "技能名称", Required = true };
        skillShowCommand.Options.Add(skillShowNameOption);
        skillShowCommand.SetAction(async (parseResult, ct) =>
        {
            var name = parseResult.GetValue(skillShowNameOption)!;
            var configManager = new ConfigManager();
            if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在[/]"); return 0; }
            await configManager.LoadAsync();
            var memory = new FileMemory(configManager.Config.DataDir);
            var skill = await memory.GetSkillAsync(name);
            if (skill == null)
            {
                AnsiConsole.MarkupLine($"[red]技能 '{name}' 不存在[/]");
            }
            else
            {
                AnsiConsole.WriteLine(skill.Content);
            }
            return 0;
        });
        skillCommand.Subcommands.Add(skillShowCommand);

        rootCommand.Subcommands.Add(skillCommand);

        // memory 命令
        var memoryCommand = new Command("memory", "记忆管理");
        
        var memoryShowCommand = new Command("show", "显示记忆内容");
        var memoryTypeOption = new Option<string>("--type", "-t") { Description = "记忆类型 (identity/soul/memory/user/agents/tools)" };
        memoryShowCommand.Options.Add(memoryTypeOption);
        memoryShowCommand.SetAction(async (parseResult, ct) =>
        {
            var type = parseResult.GetValue(memoryTypeOption) ?? "memory";
            var configManager = new ConfigManager();
            if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在[/]"); return 0; }
            await configManager.LoadAsync();
            var memory = new FileMemory(configManager.Config.DataDir);
            
            string? content = type.ToLower() switch
            {
                "identity" => await memory.GetIdentityAsync(),
                "soul" => await memory.GetSoulAsync(),
                "memory" => await memory.GetMemoryAsync(),
                "user" => await memory.GetUserAsync(),
                "agents" => await memory.GetAgentsAsync(),
                "tools" => await memory.GetToolsAsync(),
                _ => null
            };
            
            if (string.IsNullOrEmpty(content))
                AnsiConsole.MarkupLine($"[dim]暂无 {type} 内容[/]");
            else
                AnsiConsole.WriteLine(content);
            return 0;
        });
        memoryCommand.Subcommands.Add(memoryShowCommand);

        var memoryEditCommand = new Command("edit", "编辑记忆内容");
        var memoryEditTypeOption = new Option<string>("--type", "-t") { Description = "记忆类型", Required = true };
        memoryEditCommand.Options.Add(memoryEditTypeOption);
        memoryEditCommand.SetAction(async (parseResult, ct) =>
        {
            var type = parseResult.GetValue(memoryEditTypeOption)!;
            var configManager = new ConfigManager();
            if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在[/]"); return 0; }
            await configManager.LoadAsync();
            var memory = new FileMemory(configManager.Config.DataDir);
            
            AnsiConsole.MarkupLine($"[dim]请输入 {type} 内容 (Ctrl+Z 然后回车结束):[/]");
            var lines = new List<string>();
            string? line;
            while ((line = Console.ReadLine()) != null) lines.Add(line);
            var content = string.Join("\n", lines);
            
            switch (type.ToLower())
            {
                case "identity": await memory.SetIdentityAsync(content); break;
                case "soul": await memory.SetSoulAsync(content); break;
                case "memory": await memory.SetMemoryAsync(content); break;
                case "user": await memory.SetUserAsync(content); break;
                case "agents": await memory.SetAgentsAsync(content); break;
                case "tools": await memory.SetToolsAsync(content); break;
                default: AnsiConsole.MarkupLine($"[red]未知类型: {type}[/]"); return 0;
            }
            AnsiConsole.MarkupLine($"[green]{type} 已更新[/]");
            return 0;
        });
        memoryCommand.Subcommands.Add(memoryEditCommand);

        rootCommand.Subcommands.Add(memoryCommand);

        // gateway 命令
        var gatewayCommand = new Command("gateway", "启动消息网关");
        var gatewayHostOption = new Option<string>("--host", "-h") { Description = "监听地址" };
        var gatewayPortOption = new Option<int>("--port", "-p") { Description = "监听端口" };
        gatewayCommand.Options.Add(gatewayHostOption);
        gatewayCommand.Options.Add(gatewayPortOption);
        gatewayCommand.SetAction(async (parseResult, ct) =>
        {
            try
            {
                var configManager = new ConfigManager();
                if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在，请先运行 'netclaw onboard'[/]"); return 1; }
                await configManager.LoadAsync();
                
                var channelsConfig = configManager.Config.Channels ?? new ChannelsConfig();
                
                // 命令行参数覆盖配置文件
                var host = parseResult.GetValue(gatewayHostOption);
                if (!string.IsNullOrEmpty(host)) channelsConfig.Host = host;
                var port = parseResult.GetValue(gatewayPortOption);
                if (port > 0) channelsConfig.Port = port;
                
                using var gateway = new Gateway(channelsConfig, configManager);
                using var cts = new CancellationTokenSource();
                
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };
                
                AnsiConsole.MarkupLine($"[green]NetClaw Gateway 启动中...[/]");
                AnsiConsole.MarkupLine($"[dim]监听地址: http://{host}:{port}/[/]");
                AnsiConsole.MarkupLine("[dim]按 Ctrl+C 停止[/]");
                AnsiConsole.WriteLine();
                
                await gateway.StartAsync(cts.Token);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]错误: {Markup.Escape(ex.Message)}[/]");
                return 1;
            }
            return 0;
        });
        rootCommand.Subcommands.Add(gatewayCommand);

        // channel 命令 - 渠道配置管理
        var channelCommand = new Command("channel", "渠道配置管理");
        
        var channelListCommand = new Command("list", "列出渠道配置");
        channelListCommand.SetAction(async (parseResult, ct) =>
        {
            var configManager = new ConfigManager();
            if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在[/]"); return 0; }
            await configManager.LoadAsync();
            
            var channels = configManager.Config.Channels;
            var table = new Table();
            table.AddColumn("渠道");
            table.AddColumn("状态");
            table.AddColumn("配置");
            
            if (channels?.DingTalk != null)
                table.AddRow("钉钉", channels.DingTalk.Enabled ? "[green]启用[/]" : "[dim]禁用[/]", channels.DingTalk.WebhookUrl ?? "无 Webhook");
            else
                table.AddRow("钉钉", "[dim]未配置[/]", "-");
            
            if (channels?.Feishu != null)
                table.AddRow("飞书", channels.Feishu.Enabled ? "[green]启用[/]" : "[dim]禁用[/]", channels.Feishu.WebhookUrl ?? "无 Webhook");
            else
                table.AddRow("飞书", "[dim]未配置[/]", "-");
            
            if (channels?.QQ != null)
                table.AddRow("QQ", channels.QQ.Enabled ? "[green]启用[/]" : "[dim]禁用[/]", channels.QQ.ApiUrl ?? "无 API");
            else
                table.AddRow("QQ", "[dim]未配置[/]", "-");
            
            AnsiConsole.Write(table);
            return 0;
        });
        channelCommand.Subcommands.Add(channelListCommand);
        
        var channelEnableCommand = new Command("enable", "启用渠道");
        var channelEnableNameOption = new Option<string>("--name", "-n") { Description = "渠道名称 (dingtalk/feishu/qq)", Required = true };
        channelEnableCommand.Options.Add(channelEnableNameOption);
        channelEnableCommand.SetAction(async (parseResult, ct) =>
        {
            var name = parseResult.GetValue(channelEnableNameOption)!.ToLower();
            var configManager = new ConfigManager();
            if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在[/]"); return 0; }
            await configManager.LoadAsync();
            
            configManager.Config.Channels ??= new ChannelsConfig();
            switch (name)
            {
                case "dingtalk":
                    configManager.Config.Channels.DingTalk ??= new DingTalkChannelConfig();
                    configManager.Config.Channels.DingTalk.Enabled = true;
                    break;
                case "feishu":
                    configManager.Config.Channels.Feishu ??= new FeishuChannelConfig();
                    configManager.Config.Channels.Feishu.Enabled = true;
                    break;
                case "qq":
                    configManager.Config.Channels.QQ ??= new QQChannelConfig();
                    configManager.Config.Channels.QQ.Enabled = true;
                    break;
                default:
                    AnsiConsole.MarkupLine($"[red]未知渠道: {name}[/]");
                    return 0;
            }
            
            await configManager.SaveAsync();
            AnsiConsole.MarkupLine($"[green]渠道 '{name}' 已启用[/]");
            return 0;
        });
        channelCommand.Subcommands.Add(channelEnableCommand);
        
        var channelConfigCommand = new Command("config", "配置渠道参数");
        var channelConfigNameOption = new Option<string>("--name", "-n") { Description = "渠道名称", Required = true };
        var channelConfigKeyOption = new Option<string>("--key", "-k") { Description = "配置项名称", Required = true };
        var channelConfigValueOption = new Option<string>("--value", "-v") { Description = "配置值", Required = true };
        channelConfigCommand.Options.Add(channelConfigNameOption);
        channelConfigCommand.Options.Add(channelConfigKeyOption);
        channelConfigCommand.Options.Add(channelConfigValueOption);
        channelConfigCommand.SetAction(async (parseResult, ct) =>
        {
            var name = parseResult.GetValue(channelConfigNameOption)!.ToLower();
            var key = parseResult.GetValue(channelConfigKeyOption)!;
            var value = parseResult.GetValue(channelConfigValueOption)!;
            
            var configManager = new ConfigManager();
            if (!configManager.Exists) { AnsiConsole.MarkupLine("[red]配置不存在[/]"); return 0; }
            await configManager.LoadAsync();
            
            configManager.Config.Channels ??= new ChannelsConfig();
            
            switch (name)
            {
                case "dingtalk":
                    configManager.Config.Channels.DingTalk ??= new DingTalkChannelConfig();
                    SetChannelConfig(configManager.Config.Channels.DingTalk, key, value);
                    break;
                case "feishu":
                    configManager.Config.Channels.Feishu ??= new FeishuChannelConfig();
                    SetChannelConfig(configManager.Config.Channels.Feishu, key, value);
                    break;
                case "qq":
                    configManager.Config.Channels.QQ ??= new QQChannelConfig();
                    SetChannelConfig(configManager.Config.Channels.QQ, key, value);
                    break;
                case "gateway":
                    if (key == "host") configManager.Config.Channels.Host = value;
                    else if (key == "port" && int.TryParse(value, out var port)) configManager.Config.Channels.Port = port;
                    break;
                default:
                    AnsiConsole.MarkupLine($"[red]未知渠道: {name}[/]");
                    return 0;
            }
            
            await configManager.SaveAsync();
            AnsiConsole.MarkupLine($"[green]{name}.{key} = {value}[/]");
            return 0;
        });
        channelCommand.Subcommands.Add(channelConfigCommand);
        
        rootCommand.Subcommands.Add(channelCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }

    static async Task RunOnboardAsync(ConfigManager configManager)
    {
        AnsiConsole.MarkupLine("[bold green]欢迎使用 NetClaw![/]");
        AnsiConsole.MarkupLine("[dim]让我们来配置你的环境[/]");
        AnsiConsole.WriteLine();

        // 选择提供者类型
        var providerTypes = new[] { "云端 API (OpenAI, DeepSeek 等)", "本地模型 (Ollama, vLLM 等)", "自定义 API" };
        var selectedType = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("选择 [green]提供者类型[/]:").AddChoices(providerTypes));

        string[] providers;
        if (selectedType == providerTypes[0])
            providers = new[] { "openai", "openrouter", "anthropic", "deepseek", "zhipu", "moonshot" };
        else if (selectedType == providerTypes[1])
            providers = new[] { "ollama", "local" };
        else
            providers = new[] { "custom" };

        var selectedProvider = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("选择你的 [green]LLM 提供者[/]:").AddChoices(providers));

        // 本地模型不需要 API Key
        string apiKey = "";
        bool isLocal = ProviderFactory.IsLocalProvider(selectedProvider);
        
        if (!isLocal)
        {
            apiKey = AnsiConsole.Prompt(new TextPrompt<string>($"输入你的 [green]{selectedProvider} API 密钥[/]:").Secret());
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]本地模型无需 API 密钥[/]");
        }

        // API Base
        string? apiBase = null;
        var defaultApiBase = selectedProvider switch
        {
            "ollama" => "http://localhost:11434/v1",
            "local" => "http://localhost:8080/v1",
            _ => null
        };

        if (AnsiConsole.Confirm($"是否设置自定义 API 地址?{(defaultApiBase != null ? $" (默认: {defaultApiBase})" : "")}", defaultApiBase != null))
            apiBase = AnsiConsole.Prompt(new TextPrompt<string>("输入 API 地址:"));

        // 默认模型
        var defaultModels = selectedProvider switch
        {
            "openai" => new[] { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo" },
            "openrouter" => new[] { "anthropic/claude-3.5-sonnet", "openai/gpt-4o" },
            "anthropic" => new[] { "claude-3-5-sonnet-20241022", "claude-3-opus-20240229", "claude-3-haiku-20240307" },
            "deepseek" => new[] { "deepseek-chat", "deepseek-coder" },
            "zhipu" => new[] { "glm-4-plus", "glm-4", "glm-4-flash" },
            "moonshot" => new[] { "moonshot-v1-8k", "moonshot-v1-32k" },
            "ollama" => new[] { "llama3.2", "llama3.1", "qwen2.5", "deepseek-r1" },
            "local" => new[] { "default" },
            _ => Array.Empty<string>()
        };

        string? defaultModel = defaultModels.Length > 0
            ? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("选择 [green]默认模型[/]:").AddChoices(defaultModels))
            : AnsiConsole.Prompt(new TextPrompt<string>("输入模型名称:"));

        // 工作目录
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var workspace = AnsiConsole.Prompt(new TextPrompt<string>($"设置 [green]工作目录[/]:").DefaultValue(homeDir));

        // AutoSave 设置
        var autoSave = AnsiConsole.Confirm("启用自动保存记忆?", true);
        var autoSaveInterval = 5;
        if (autoSave)
            autoSaveInterval = AnsiConsole.Prompt(new TextPrompt<int>("自动保存间隔 (分钟):").DefaultValue(5));

        configManager.SetDefault();
        configManager.SetProvider(selectedProvider, apiKey, apiBase, defaultModel, isLocal);
        configManager.Config.DefaultProvider = selectedProvider;
        configManager.Config.Agents.Model = defaultModel ?? "gpt-4o-mini";
        configManager.Config.Agents.Workspace = workspace;
        configManager.Config.Agents.AutoSave = autoSave;
        configManager.Config.Agents.AutoSaveInterval = autoSaveInterval;

        await configManager.SaveAsync();
        await configManager.InitializeDataDirAsync();

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ 配置已保存![/]");
        AnsiConsole.MarkupLine($"[dim]配置目录: {configManager.Config.DataDir}[/]");
        AnsiConsole.MarkupLine($"[dim]工作目录: {configManager.Config.Agents.Workspace}[/]");
        AnsiConsole.MarkupLine($"[dim]自动保存: {(autoSave ? $"每 {autoSaveInterval} 分钟" : "关闭")}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]现在你可以开始使用了:[/]");
        AnsiConsole.MarkupLine("  [blue]netclaw agent -m \"你好!\"[/]");
        AnsiConsole.MarkupLine("  [blue]netclaw agent[/] [dim](交互模式)[/]");
        AnsiConsole.MarkupLine("  [blue]netclaw skill add -n \"翻译\"[/] [dim](添加技能)[/]");
        AnsiConsole.MarkupLine("  [blue]netclaw memory edit -t user[/] [dim](编辑用户信息)[/]");
    }

    static async Task ShowStatusAsync(ConfigManager configManager)
    {
        var table = new Table();
        table.AddColumn("设置项");
        table.AddColumn("值");
        table.AddRow("配置目录", configManager.Config.DataDir);
        table.AddRow("工作目录", configManager.Config.Agents.Workspace);
        table.AddRow("默认提供者", configManager.Config.DefaultProvider);
        table.AddRow("模型", configManager.Config.Agents.Model);
        table.AddRow("最大令牌数", configManager.Config.Agents.MaxTokens.ToString());
        table.AddRow("温度", configManager.Config.Agents.Temperature.ToString());
        table.AddRow("自动保存", configManager.Config.Agents.AutoSave ? $"每 {configManager.Config.Agents.AutoSaveInterval} 分钟" : "关闭");
        table.AddRow("已配置提供者", string.Join(", ", configManager.Config.Providers.Keys));
        
        var memory = new FileMemory(configManager.Config.DataDir);
        var skills = await memory.GetSkillsAsync();
        table.AddRow("技能数量", skills.Count.ToString());
        
        var sessions = await memory.GetSessionListAsync();
        table.AddRow("会话数量", sessions.Count.ToString());
        
        AnsiConsole.Write(table);
    }
    
    /// <summary>设置渠道配置</summary>
    static void SetChannelConfig(object config, string key, string value)
    {
        var type = config.GetType();
        var prop = type.GetProperty(key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (prop == null)
        {
            AnsiConsole.MarkupLine($"[red]未知配置项: {key}[/]");
            return;
        }
        
        if (prop.PropertyType == typeof(string))
            prop.SetValue(config, value);
        else if (prop.PropertyType == typeof(bool))
            prop.SetValue(config, value.ToLower() == "true" || value == "1");
        else if (prop.PropertyType == typeof(int) && int.TryParse(value, out var intVal))
            prop.SetValue(config, intVal);
        else if (prop.PropertyType == typeof(List<string>))
        {
            var list = (List<string>?)prop.GetValue(config) ?? new List<string>();
            list.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            prop.SetValue(config, list);
        }
        else
            AnsiConsole.MarkupLine($"[red]不支持的配置类型: {prop.PropertyType.Name}[/]");
    }
}

/// <summary>Agent 服务</summary>
public class AgentService
{
    private readonly ConfigManager _configManager;
    private readonly ILogger? _logger;

    public AgentService(ConfigManager configManager, ILogger? logger = null)
    {
        _configManager = configManager;
        _logger = logger;
    }

    public async Task RunAsync(string message, bool interactive = false, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        var config = _configManager.Config;
        var providerConfig = _configManager.GetProvider();

        if (providerConfig == null || (string.IsNullOrEmpty(providerConfig.ApiKey) && !providerConfig.IsLocal))
        {
            AnsiConsole.MarkupLine("[red]错误: 未配置 API 密钥，请先运行 'netclaw onboard'[/]");
            return;
        }

        var provider = ProviderFactory.Create(
            _configManager.Config.DefaultProvider, 
            providerConfig.ApiKey,
            string.IsNullOrEmpty(providerConfig.ApiBase) ? null : providerConfig.ApiBase, 
            providerConfig.DefaultModel,
            providerConfig.IsLocal);

        var memory = new FileMemory(config.DataDir);
        
        // 加载技能作为工具
        var tools = new List<ITool> { new ReadFileTool(), new WriteFileTool(), new ListDirTool(), new ShellTool() };
        var skills = await memory.GetSkillsAsync();
        foreach (var skill in skills)
        {
            tools.Add(new SkillTool(skill, async input =>
            {
                // 技能执行：使用 SKILL.md 内容作为系统提示
                var skillMessages = new List<Message>
                {
                    Message.System(skill.Content),
                    Message.User(input)
                };
                var response = await provider.ChatAsync(skillMessages, null, config.Agents.Model, config.Agents.MaxTokens, config.Agents.Temperature);
                return response.Content;
            }));
        }

        var agent = new AgentLoop(provider, memory, tools, config.Agents, _logger);

        if (interactive)
            await RunInteractiveAsync(agent, memory, sessionId, cancellationToken);
        else
            await RunSingleAsync(agent, sessionId, message, cancellationToken);
    }

    private async Task RunSingleAsync(AgentLoop agent, string? sessionId, string message, CancellationToken cancellationToken)
    {
        sessionId ??= $"session_{DateTime.Now:yyyyMMdd}";
        AnsiConsole.MarkupLine("[dim]思考中...[/]");

        var result = await agent.RunAsync(sessionId, message, cancellationToken);

        if (result.ToolCallsCount > 0)
            AnsiConsole.MarkupLine($"\n[dim]工具调用: {result.ToolCallsCount} 次[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]NetClaw:[/]");
        AnsiConsole.WriteLine(result.Response);
        AnsiConsole.MarkupLine($"\n[dim]令牌: {result.TotalInputTokens} 输入 / {result.TotalOutputTokens} 输出[/]");
    }

    private async Task RunInteractiveAsync(AgentLoop agent, FileMemory memory, string? sessionId, CancellationToken cancellationToken)
    {
        sessionId ??= $"session_{DateTime.Now:yyyyMMdd}";

        AnsiConsole.MarkupLine("[green]NetClaw 交互模式[/]");
        AnsiConsole.MarkupLine($"[dim]会话: {sessionId}[/]");
        AnsiConsole.MarkupLine("[dim]输入消息后按回车发送。输入 'exit' 或 'quit' 退出。[/]");
        AnsiConsole.MarkupLine("[dim]输入 'clear' 清除对话历史。输入 'help' 查看帮助。[/]");
        AnsiConsole.WriteLine();

        // 自动保存定时器
        var autoSaveCts = new CancellationTokenSource();
        var config = _configManager.Config;
        PeriodicTimer? autoSaveTimer = null;
        
        if (config.Agents.AutoSave)
        {
            autoSaveTimer = new PeriodicTimer(TimeSpan.FromMinutes(config.Agents.AutoSaveInterval));
            _ = Task.Run(async () =>
            {
                while (await autoSaveTimer.WaitForNextTickAsync(autoSaveCts.Token))
                {
                    // 可以在这里添加自动保存逻辑
                    _logger?.LogDebug("自动保存触发");
                }
            }, autoSaveCts.Token);
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                AnsiConsole.Markup("[yellow]你:[/] ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input)) continue;
                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) || input.Equals("quit", StringComparison.OrdinalIgnoreCase)) break;

                if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    await agent.ClearSessionAsync(sessionId);
                    AnsiConsole.MarkupLine("[dim]对话已清除[/]");
                    continue;
                }

                if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine("[dim]命令:[/]");
                    AnsiConsole.MarkupLine("[dim]  clear - 清除对话历史[/]");
                    AnsiConsole.MarkupLine("[dim]  exit/quit - 退出[/]");
                    AnsiConsole.MarkupLine("[dim]  new - 开始新会话[/]");
                    continue;
                }

                if (input.Equals("new", StringComparison.OrdinalIgnoreCase))
                {
                    sessionId = $"session_{DateTime.Now:yyyyMMdd_HHmmss}";
                    AnsiConsole.MarkupLine($"[dim]新会话: {sessionId}[/]");
                    continue;
                }

                try
                {
                    AnsiConsole.MarkupLine("[dim]思考中...[/]");
                    var result = await agent.RunAsync(sessionId, input, cancellationToken);

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[blue]NetClaw:[/]");
                    AnsiConsole.WriteLine(result.Response);

                    if (result.ToolCallsCount > 0)
                        AnsiConsole.MarkupLine($"\n[dim]工具使用: {result.ToolCallsCount} 次[/]");

                    AnsiConsole.WriteLine();
                }
                catch (Exception ex) { AnsiConsole.MarkupLine($"[red]错误:[/] {Markup.Escape(ex.Message)}"); }
            }
        }
        finally
        {
            autoSaveCts.Cancel();
            autoSaveTimer?.Dispose();
        }
    }
}