# WebView 插件系统 — 构建验证与高优先级优化计划

> **状态**：P1-P6 源码已全部落地，本轮聚焦 P7 全量构建验证 + 3 项阻塞型/高价值优化。
> **前置文件**：`f:\Code\Dotnet\AvaloniaTemplate\.trae\documents\webview-webtemplate-plugin-plan.md`（P1-P6 设计文档，528 行）

---

## 一、Summary（摘要）

用户原始需求——"使用 Avalonia Controls WebView 12 设计 HTML 加载方案，支持 Vue/React，HTTP 资源服务 + IPC 双向通讯 + SSE 主动推送，全局单一 HTTP 服务，路由区分插件页面"——的**源码实现已完整就位**（P1-P6 共 20+ 文件）。

本轮计划完成两件事：
1. **P7 全量构建验证**：执行从 Core.slnx 到 `--build=all` 的完整构建链，确认 wwwroot 复制 + zip 打包成功，并输出手动冒烟测试步骤。
2. **3 项高优先级优化**（其中 O1 是 P7 能通过的前提）：
   - **O1 构建容错**（阻塞型）：TDLSharp 插件的 6 个预存编译错误当前会让 `PackPlugins` 任务在遍历到 TDLSharp 时抛异常终止，导致排在后面的 WebTemplate 永远无法 publish/复制 wwwroot/打 zip。需让构建支持"单插件失败不阻塞其他插件"。
   - **O2 开发模式 wwwroot 定位**（高价值）：VS Code 调试走 `AVALONIA_EXTRA_PLUGINS_PATH` 指向 `bin/Debug/net10.0`，此目录无 wwwroot。WebHostService 需在开发模式下回退到插件源码目录的 wwwroot。
   - **O3 Linux WPE 实验性后端降级警告**（安全性）：AGENTS.md 明确 Linux WPE 后端实验性（EGL 未完成），WebPluginView 应检测平台并给出降级提示。

---

## 二、Current State Analysis（现状分析）

### 2.1 已完成清单（P1-P6）

| 阶段 | 内容 | 文件数 | 状态 |
|------|------|--------|------|
| P1 | WebView IPC 传输层（`IRpcTransport` / `WebViewIpcTransport`） | 2 | ✅ |
| P2 | RPC 运行时（`WebViewIpcHost` / `RpcEnvelope` / `Channel` / `IEventPusher` / `SseEventPusher` / `ipc.js`） | 7 | ✅ |
| P3 | 源生成器（`RpcCommandGenerator` 扫描 `[RpcCommand]` 生成 `IRpcBindingSource`） | 1 | ✅ |
| P4 | 宿主集成（`WebHostService` Kestrel + SSE + 静态资源 / `App.axaml.cs` 注册启动） | 2 | ✅ |
| P5 | 构建系统（`build.cs` 的 `CopyPluginWwwroot` + `PackPluginZips`） | 已内嵌 | ✅ |
| P6 | WebTemplate 示例插件（8 文件：csproj / Plugin / Rpc / VM / Page / wwwroot / launchSettings） | 8 | ✅ |

### 2.2 关键架构事实（经探查确认）

- **NativeWebView**（非 `WebView`）：`Avalonia.Controls` 命名空间，`Avalonia.Controls.WebView` 程序集，版本 12.0.1
- **IPC 双原语**：C#→JS 用 `InvokeScript(string)`，JS→C# 用 `WebMessageReceived` 事件（fire-and-forget，由 ipc.js 的 callback-ID 表 + C# 侧 `resolve` 回推弥补）
- **SSE 路由**：`GET /sse/{pluginId}` — `event: dispatch` + `data: {"name":"...","data":...}`
- **静态资源路由**：`GET /{pluginId}/{**path}` — 服务插件 wwwroot
- **全局单一 Kestrel**：`127.0.0.1:0`（自动分配端口），`WebHostService` 为 DI Singleton
- **版本真相源**：`Directory.Build.props` → `HostVersion=2.2.0` = `PluginSdkVersion=2.2.0`
- **PluginId 全链路一致**：`8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d`（csproj / IPluginMetadata / WebPluginView 绑定 / Kestrel 路由 / SSE 通道）
- **构建容错现状**：`PackPlugins` 任务 `foreach` 遍历所有插件，单插件 `DotNetPublish` 抛异常会终止整个循环

### 2.3 未完成项

| 项 | 状态 | 原因 |
|----|------|------|
| `bin/nuget/*.nupkg` | ❌ 不存在 | 从未执行 `--build=bin` |
| `bin/plugins/LYBox.Plugin.WebTemplate/publish/wwwroot/` | ❌ 不存在 | 从未执行 `--build=plugin`/`all` |
| `bin/plugins/zip/LYBox.Plugin.WebTemplate-1.0.0.zip` | ❌ 不存在 | 同上 |
| 运行时冒烟测试 | ❌ 未执行 | 需先有构建产物 |

### 2.4 阻塞问题

**TDLSharp 预存编译错误**（6 个，位于 `plugins/LYBox.Plugin.TDLSharp/Services/TdlService.Upload.cs`）：
- `CS0029`：`TdApi.InputFile` 无法隐式转换为 `InputPhoto` / `InputDocument`
- `CS0117`：`InputMessagePhoto`/`InputMessageDocument` 缺少 `Width`/`Height`/`Thumbnail`/`DisableContentTypeDetection` 定义
- **根因**：TdLib API 版本不匹配（预存问题，非本轮引入）
- **影响**：`PackPlugins` 遍历到 TDLSharp 时 `DotNetPublish` 抛异常 → 循环终止 → WebTemplate 永远无法构建

---

## 三、Proposed Changes（提议变更）

### O1：构建容错 — 单插件失败不阻塞其他插件（阻塞型，P7 前置）

**文件**：`f:\Code\Dotnet\AvaloniaTemplate\build\build.cs`

**位置**：`Task("PackPlugins")` 内 `foreach (var plugin in buildContext.PluginProjects)` 循环（约 L269-L287）

**现状代码**（L269-L287）：
```csharp
foreach (var plugin in buildContext.PluginProjects)
{
    var pluginOutputDir = Path.Combine(buildContext.PluginPackagesDir, plugin.ProjectName, "publish");
    c.EnsureDirectoryExists(pluginOutputDir);
    var pluginMsBuild = buildContext.CreatePluginMSBuildSettings(plugin);
    c.DotNetPublish(plugin.ProjectPath(buildContext.RootDir), new DotNetPublishSettings
    {
        Configuration = buildContext.BuildConfiguration,
        OutputDirectory = pluginOutputDir,
        MSBuildSettings = pluginMsBuild
    });
    CopyPluginWwwroot(c, buildContext, plugin, pluginOutputDir);
    c.Log.Information("Plugin published: {0} -> {1}", plugin.ProjectName, pluginOutputDir);
}
PackPluginZips(c, buildContext);
```

**变更后**：用 try/catch 包裹单插件 publish，失败时记录错误并继续：
```csharp
var failedPlugins = new List<string>();
foreach (var plugin in buildContext.PluginProjects)
{
    var pluginOutputDir = Path.Combine(buildContext.PluginPackagesDir, plugin.ProjectName, "publish");
    c.EnsureDirectoryExists(pluginOutputDir);
    var pluginMsBuild = buildContext.CreatePluginMSBuildSettings(plugin);
    try
    {
        c.DotNetPublish(plugin.ProjectPath(buildContext.RootDir), new DotNetPublishSettings
        {
            Configuration = buildContext.BuildConfiguration,
            OutputDirectory = pluginOutputDir,
            MSBuildSettings = pluginMsBuild
        });
        CopyPluginWwwroot(c, buildContext, plugin, pluginOutputDir);
        c.Log.Information("Plugin published: {0} -> {1}", plugin.ProjectName, pluginOutputDir);
    }
    catch (Exception ex)
    {
        c.Log.Error("插件 {0} 发布失败，跳过（不影响其他插件）: {1}", plugin.ProjectName, ex.Message);
        failedPlugins.Add(plugin.ProjectName);
    }
}
PackPluginZips(c, buildContext);
if (failedPlugins.Count > 0)
{
    c.Log.Warning("以下 {0} 个插件发布失败: {1}", failedPlugins.Count, string.Join(", ", failedPlugins));
}
```

**Why**：TDLSharp 的预存错误与本轮 WebView 工作无关，不应阻塞 WebTemplate 的构建与打包。此变更让构建系统具备"最佳努力"语义。
**How**：try/catch 包裹单插件 publish + wwwroot 复制；失败插件记入列表，循环结束后统一告警；`PackPluginZips` 已有 `Directory.Exists` 检查（L333），失败插件自动跳过 zip。

---

### O2：开发模式 wwwroot 定位（高价值）

**文件**：`f:\Code\Dotnet\AvaloniaTemplate\src\LYBox.Plugin.Shared\Web\WebHostService.cs`

**问题**：VS Code 调试配置 `AVALONIA_EXTRA_PLUGINS_PATH` 指向插件的 `bin/Debug/net10.0`，此目录无 wwwroot。`App.axaml.cs` 的 `InitializeWebHost` 调 `pluginLoader.GetWebPluginRoots()` 返回的是插件加载目录（bin 输出），`WebHostService.MapPluginRoot` 会因 `Directory.Exists(wwwrootPath)` 为 false 抛 `DirectoryNotFoundException`。

**变更方案**：在 `MapPluginRoot` 中增加开发模式回退——当 bin 目录下 wwwroot 不存在时，尝试从环境变量 `LYBOX_PLUGIN_SRC_{PluginId}` 或约定的源码路径定位 wwwroot。

**具体变更**（`MapPluginRoot` 方法，约 L54-L63）：
```csharp
public void MapPluginRoot(string pluginId, string wwwrootPath)
{
    if (string.IsNullOrWhiteSpace(pluginId))
        throw new ArgumentException("pluginId 不能为空", nameof(pluginId));
    if (string.IsNullOrWhiteSpace(wwwrootPath))
        throw new ArgumentException("wwwrootPath 不能为空", nameof(wwwrootPath));

    // 开发模式回退：bin 目录下无 wwwroot 时，尝试源码目录
    var resolvedPath = wwwrootPath;
    if (!Directory.Exists(resolvedPath))
    {
        var devPath = ResolveDevWwwroot(pluginId);
        if (devPath is not null && Directory.Exists(devPath))
        {
            resolvedPath = devPath;
            _logger?.LogWarning("插件 {PluginId} 使用开发模式 wwwroot: {Path}", pluginId, devPath);
        }
    }

    if (!Directory.Exists(resolvedPath))
        throw new DirectoryNotFoundException($"wwwroot 目录不存在: {wwwrootPath}");

    _pluginRoots[pluginId] = resolvedPath;
}

private static string? ResolveDevWwwroot(string pluginId)
{
    // 优先：环境变量 LYBOX_PLUGIN_SRC_{PluginId} 指定插件源码根
    var envKey = $"LYBOX_PLUGIN_SRC_{pluginId.Replace("-", "_")}";
    var envPath = Environment.GetEnvironmentVariable(envKey);
    if (!string.IsNullOrEmpty(envPath))
    {
        var candidate = Path.Combine(envPath, "wwwroot");
        if (Directory.Exists(candidate)) return candidate;
    }

    // 回退：AVALONIA_EXTRA_PLUGINS_PATH 的父目录向上找 plugins/{ProjectName}/wwwroot
    var extraPath = Environment.GetEnvironmentVariable("AVALONIA_EXTRA_PLUGINS_PATH");
    if (!string.IsNullOrEmpty(extraPath))
    {
        // extraPath 形如 .../bin/Debug/net10.0，向上 4 级到插件源码根
        var dir = new DirectoryInfo(extraPath);
        for (var i = 0; i < 6 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir.FullName, "wwwroot");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
    }

    return null;
}
```

**Why**：开发期无需 `dotnet publish` 即可调试 WebView 插件，大幅提升迭代效率。
**How**：`MapPluginRoot` 先检查传入路径，不存在时调 `ResolveDevWwwroot` 尝试环境变量 + 向上遍历。需确认 `WebHostService` 已有 `ILogger?` 字段（若无则用 `Console.Error`）。

**前置确认**：读 `WebHostService.cs` 全文，确认是否已有 `_logger` 字段及构造函数签名。

---

### O3：Linux WPE 实验性后端降级警告（安全性）

**文件**：`f:\Code\Dotnet\AvaloniaTemplate\src\LYBox.Plugin.Shared\Web\WebPluginView.axaml.cs`

**变更**：在 `OnLoaded` 或 `NavigationCompleted` 初始化路径中，检测 `OperatingSystem.IsLinux()` 时输出一次警告日志。

```csharp
// 在 WebPluginView.axaml.cs 的初始化路径中（Loaded 事件或 PART_WebView.Navigated 处理）
if (OperatingSystem.IsLinux())
{
    // WPE WebKit 后端为实验性（EGL 支持未完成，issue #14 open）
    // 生产环境需评估稳定性；不稳定则降级用 NativeWebDialog 独立窗口
    System.Diagnostics.Debug.WriteLine(
        "[WebPluginView] 警告：Linux WPE WebKit 后端为实验性，嵌入式 WebView 可能不稳定。" +
        "如遇渲染问题，可降级为 NativeWebDialog（WebKitGTK 独立窗口）。");
}
```

**Why**：AGENTS.md WebView IPC 调研结论明确 Linux WPE 后端实验性，需在运行时提醒开发者。
**How**：在 WebPluginView 初始化时检测平台，输出 Debug 警告。不阻塞加载，仅提示。

---

### P7：全量构建验证（5 步）

> O1 实施后执行，否则 TDLSharp 错误会阻塞 WebTemplate。

| 步骤 | 命令 | 预期结果 |
|------|------|---------|
| P7.1 | `dotnet build Core.slnx` | 0 error（已有 7 个 nullable warning 无关） |
| P7.2 | `.\build.ps1 --build=bin` | `bin/nuget/LYBox.Plugin.Generators.2.2.0.nupkg` + `LYBox.Plugin.Shared.2.2.0.nupkg` 生成 |
| P7.3 | `dotnet build Plugins.slnx` | WebTemplate 0 error 0 warning（TDLSharp 6 个预存 error 不计入本轮） |
| P7.4 | `.\build.ps1 --build=all` | `bin/plugins/LYBox.Plugin.WebTemplate/publish/wwwroot/index.html` 存在 + `bin/plugins/zip/LYBox.Plugin.WebTemplate-1.0.0.zip` 存在（TDLSharp 失败但不阻塞） |
| P7.5 | 手动冒烟测试 | 见下方步骤 |

### P7.5 手动冒烟测试步骤

1. 启动应用：`dotnet run --project src/launcher/LYBox.Launcher.Desktop`
2. 左侧 NavMenu 出现 "Web Template Demo" 菜单项（Status=New，Order=998）
3. 点击进入，WebView 加载 `http://127.0.0.1:{port}/8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d/index.html`
4. 点击 "Greet" 按钮 → 调 `GreetAsync("LYBox")` → 显示 "Hello, LYBox! 这是来自 C# 的问候。"
5. 点击 "Add" 按钮 → 调 `AddAsync(3, 5)` → 显示 "3 + 5 = 8"
6. 点击 "Plugin Info" 按钮 → 调 `GetPluginInfoAsync()` → 显示 JSON（含 id/name/version/serverTime）
7. SSE 推送：页面右上角计数器每 2 秒 +1，显示 "C# 第 N 次推送 @ HH:mm:ss"
8. ToggleSwitch 切换 "停止" → SSE 推送暂停（计数器停止）；切回 "开启" → 恢复

---

## 四、Assumptions & Decisions（假设与决策）

| # | 决策 | 理由 |
|---|------|------|
| D1 | O1 用 try/catch 而非 MSBuild `ContinueOnError` | Cake 的 `DotNetPublish` 是 ICakeContext 扩展方法，无 ContinueOnError 参数；try/catch 是 Cake 任务层的标准容错模式 |
| D2 | O2 用环境变量 + 向上遍历双重回退 | 环境变量精确但需手动设；向上遍历利用 `AVALONIA_EXTRA_PLUGINS_PATH` 约定自动定位，零配置 |
| D3 | O3 仅 Debug 警告不阻塞 | WPE 实验性但可能可用；阻塞会过度限制 Linux 开发；警告 + 文档足矣 |
| D4 | 不修复 TDLSharp 预存错误 | 与本轮 WebView 工作无关；TdLib API 版本升级需独立评估 |
| D5 | 不引入 Vite/Webpack dev server 代理 | 本轮聚焦验证 + 阻塞型优化；Vue/React HMR 集成作为后续阶段 |
| D6 | 不输出 .d.ts TypeScript 声明 | 源生成器已生成 `TsDeclarations` 字符串但无 emit 步骤；作为后续阶段 |

---

## 五、Verification（验证步骤）

1. **O1 验证**：`.\build.ps1 --build=all` 退出码 0（或非零但 WebTemplate 产物存在）；控制台输出 "以下 1 个插件发布失败: LYBox.Plugin.TDLSharp"
2. **O2 验证**：设 `AVALONIA_EXTRA_PLUGINS_PATH=plugins/LYBox.Plugin.WebTemplate/bin/Debug/net10.0`，`dotnet run` 启动后 WebView 正常加载页面（无 DirectoryNotFoundException）
3. **O3 验证**：Linux 上运行时 Debug 输出包含 WPE 警告（Windows 上无此输出）
4. **P7.4 验证**：
   - `Test-Path bin/plugins/LYBox.Plugin.WebTemplate/publish/wwwroot/index.html` → True
   - `Test-Path bin/plugins/zip/LYBox.Plugin.WebTemplate-1.0.0.zip` → True
5. **P7.5 验证**：按上述 8 步手动测试全部通过

---

## 六、实施顺序

1. **读文件**（前置确认）：`WebHostService.cs` 全文、`WebPluginView.axaml.cs` 全文、`build.cs` 的 `Task("PackPlugins")` 完整段
2. **O1**：编辑 `build.cs` — try/catch 包裹单插件 publish
3. **O2**：编辑 `WebHostService.cs` — `MapPluginRoot` 增加开发模式回退
4. **O3**：编辑 `WebPluginView.axaml.cs` — Linux 平台警告
5. **P7.1-P7.4**：依次执行构建命令
6. **P7.5**：输出手动冒烟测试步骤给用户
