# WebView WebTemplate 插件实现方案

## 摘要

本方案完成用户原始需求"使用 Avalonia Controls WebView 12 设计 HTML 加载方案，支持 Vue/React，HTTP 资源服务 + IPC 双向通讯 + SSE 主动推送"的**最后一公里**：创建一个可运行示例插件 `LYBox.Plugin.WebTemplate`，端到端验证 HTTP 资源加载、JS↔C# IPC 双向调用、C#→JS SSE 主动推送三大能力，并进行全量构建验证。

**当前状态**：P1-P5 已全部完成（WebView IPC 基础设施 + 源生成器 + Kestrel 资源服务 + 构建系统 wwwroot 复制），`Core.slnx` 构建 0 错误。仅剩 P6（示例插件）+ P7（全量验证）。

---

## 当前状态分析（已完成的基础设施）

经只读探查确认，以下子系统已就绪：

### 1. WebView IPC 传输层（`src/LYBox.Plugin.Shared/Web/`）
| 文件 | 状态 | 职责 |
|------|------|------|
| `WebViewIpcTransport.cs` | ✅ | 桥接 `NativeWebView.WebMessageReceived` + `InvokeScript` 到 `IRpcTransport` |
| `WebHostService.cs` | ✅ | 嵌入式 Kestrel（`127.0.0.1:0`），`MapPluginRoot` + SSE `/sse/{pluginId}` + 静态 `/{pluginId}/{**path}` |
| `WebPluginBindings.cs` | ✅ | 反射扫描 `IRpcBindingSource` 实现，注册到 `IRpcHost` |
| `IWebPlugin.cs` | ✅ | `IWebPlugin : IPlugin`，新增 `PluginBaseDir`/`WwwrootPath`/`EntryPage` |
| `WebPluginView.axaml(.cs)` | ✅ | 封装 `NativeWebView` 的 UserControl，`PluginId` StyledProperty，自动初始化 IPC + SSE + 绑定 |

### 2. RPC 运行时（`src/LYBox.Plugin.Shared/Rpc/`）
| 文件 | 状态 | 职责 |
|------|------|------|
| `IRpcHost.cs` | ✅ | `IRpcHost`（RegisterCommand/EmitEventAsync/CreateChannel）+ `IRpcBindingSource`（static abstract TsDeclarations/JsGlue） |
| `WebViewIpcHost.cs` | ✅ | Wails v2 前缀信封分发（C/E/X），`InitializeAsync` 注入 ipc.js，`InjectBindingsAsync` 下发命令清单 |
| `SseEventPusher.cs` | ✅ | SSE 客户端管理，`PushAsync(pluginId, eventType, json)` |
| `Channel.cs` | ✅ | 流式通道，SSE 优先 / InvokeScript 降级 |
| `RpcEnvelope.cs` | ✅ | `PrefixCall='C'`/`PrefixEvent='E'`/`PrefixChannelClose='X'` + JSON 序列化选项 |
| `Assets/ipc.js` | ✅ | 嵌入式引导脚本，`window.__lybox` 运行时 + `window.go` 绑定 + `startSse(pluginId)` |

### 3. 源生成器（`src/LYBox.Plugin.Generators/`）
| 文件 | 状态 | 职责 |
|------|------|------|
| `RpcCommandGenerator.cs` | ✅ | 扫描 `[RpcCommand]` 方法，生成 `IRpcBindingSource` partial 实现 + TS 声明 + JS 胶水 |

### 4. 宿主集成（`src/launcher/LYBox.Launcher.Desktop/`）
| 位置 | 状态 | 职责 |
|------|------|------|
| `App.axaml.cs` L109 | ✅ | `services.AddSingleton<WebHostService>()` DI 注册 |
| `App.axaml.cs` L139,176-202 | ✅ | `InitializeWebHost(pluginLoader)` 启动 Kestrel + 注册路由 |
| `PluginLoader.cs` L527-563 | ✅ | `GetWebPluginRoots()` 扫描已加载 IWebPlugin，注入 PluginBaseDir |

### 5. 构建系统（`build/build.cs`）
| 位置 | 状态 | 职责 |
|------|------|------|
| L284,293-322 | ✅ | `CopyPluginWwwroot` + `CopyDirectoryRecursive` 复制插件 wwwroot 到发布目录 |

### 关键 API 约定（实现 P6 时必须遵守）
- **控件类名**：`NativeWebView`（非 `WebView`），命名空间 `Avalonia.Controls`，程序集 `Avalonia.Controls.WebView`
- **前端调用入口**：`window.go.{Namespace}.{Class}.{Method}(...args): Promise<T>`（由 `ipc.js` 的 `setBindings` 构建）
- **C#→JS 事件**：`host.EmitEventAsync(name, data)` → 前端 `__lybox.on(name, cb)` 接收
- **SSE 自动启动**：`WebPluginView` 在 `NavigationCompleted` 后自动调用 `window.__lybox.startSse(pluginId)`
- **PluginId 一致性**：`.csproj` 的 `PluginId`、`IPluginMetadata.PluginId` 属性、`WebPluginView.PluginId` 绑定值三者必须相同（作为 Kestrel 路由前缀 + SSE 通道 key）
- **源生成器约束**：含 `[RpcCommand]` 方法的类必须声明 `partial`，实例方法所在类须有公共无参构造函数

---

## 提议变更

### P6：创建 `LYBox.Plugin.WebTemplate` 示例插件

**目标**：端到端演示三大能力——(1) JS 调 C# RPC、(2) C# 主动推送 SSE 事件、(3) HTTP 静态资源加载。

**设计决策**：
- 前端使用**原生 HTML/JS**（无构建步骤），聚焦演示 IPC/SSE 机制本身。`wwwroot/` 可直接替换为 Vue/React 构建产物——基础设施完全框架无关。
- C#→JS 推送由页面 code-behind 的 `DispatcherTimer` 驱动，每 2 秒 `EmitEventAsync("tick", ...)`，前端通过 `__lybox.on('tick', cb)` 接收并更新计数器。
- 菜单 Header 使用字面量字符串（非 resx 资源键），省略资源文件以保持插件精简。

**PluginId**（全链路唯一标识）：`8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d`

#### 文件 1：`plugins/LYBox.Plugin.WebTemplate/LYBox.Plugin.WebTemplate.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <PluginId>8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d</PluginId>
    <PluginName>Web Template</PluginName>
    <PluginAuthor>AvaloniaTemplate</PluginAuthor>
    <PluginDescription>WebView + HTTP + IPC + SSE demo plugin with vanilla HTML/JS frontend</PluginDescription>
    <PluginVersion>1.0.0</PluginVersion>
    <MinPluginSdkVersion>2.0.0</MinPluginSdkVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LYBox.Plugin.Generators" Version="$(PluginSdkVersion)"
      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="LYBox.Plugin.Shared" Version="$(PluginSdkVersion)" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

**为什么**：与 `LYBox.Plugin.Template.csproj` 结构一致，`$(PluginSdkVersion)` 动态引用本地 NuGet 源，`MinPluginSdkVersion=2.0.0` 与其他插件对齐。

#### 文件 2：`plugins/LYBox.Plugin.WebTemplate/WebTemplatePlugin.cs`

```csharp
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Web;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.WebTemplate;

[GenerateMetadata]
public partial class WebTemplatePlugin : IPluginMetadata, IWebPlugin
{
    public string Name => "Web Template";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "WebView + HTTP + IPC + SSE demo plugin with vanilla HTML/JS frontend";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d";

    // IWebPlugin：由 PluginLoader.GetWebPluginRoots() 注入插件安装路径
    public string PluginBaseDir { get; set; } = string.Empty;

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider) => Task.CompletedTask;
}
```

**为什么**：实现 `IPluginMetadata`（手写元数据）+ `IWebPlugin`（声明前端资源位置）。`[GenerateMetadata]` 触发源生成器自动生成 `IPlugin` 的 `GetViewDefinitions`/`GetNavigationItems`/`GetMenuItems`/`ShutdownAsync` 等方法（扫描 ViewModel 上的 `[ViewMap]`/`[NavigationItem]`/`[Menu]` 特性）。`PluginBaseDir` 由宿主 `PluginLoader` 在加载时注入。

#### 文件 3：`plugins/LYBox.Plugin.WebTemplate/Rpc/GreetCommands.cs`

```csharp
using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.WebTemplate.Rpc;

/// <summary>
/// 演示 [RpcCommand] 绑定。前端通过 window.go.LYBox.Plugin.WebTemplate.Rpc.GreetCommands.* 调用。
/// 类必须 partial（源生成器生成 IRpcBindingSource 实现），实例方法需公共无参构造函数。
/// </summary>
public partial class GreetCommands
{
    [RpcCommand]
    public Task<string> GreetAsync(string name)
        => Task.FromResult($"Hello, {name}! 这是来自 C# 的问候。");

    [RpcCommand]
    public Task<int> AddAsync(int a, int b)
        => Task.FromResult(a + b);

    [RpcCommand]
    public Task<object> GetPluginInfoAsync()
        => Task.FromResult<object>(new
        {
            id = "8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d",
            name = "Web Template",
            version = "1.0.0",
            serverTime = DateTime.Now.ToString("o")
        });
}
```

**为什么**：演示三种 RPC 返回类型——`string`、`int`、`object`（匿名类型序列化为 JSON）。前端通过 `window.go.LYBox.Plugin.WebTemplate.Rpc.GreetCommands.GreetAsync("World")` 调用，返回 Promise。

#### 文件 4：`plugins/LYBox.Plugin.WebTemplate/ViewModels/WebTemplatePageViewModel.cs`

```csharp
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.ViewModels;

namespace LYBox.Plugin.WebTemplate.ViewModels;

[NavigationItem("WebTemplateDemo")]
[Menu("Web Template Demo", "WebTemplateDemo", ParentKey = null, Status = "New", Order = 998)]
[ViewMap(typeof(Pages.WebTemplatePage))]
public partial class WebTemplatePageViewModel : ViewModelBase
{
    /// <summary>PluginId 与 WebTemplatePlugin.PluginId 一致，用于 Kestrel 路由 + SSE 通道。</summary>
    [ObservableProperty]
    private string _pluginId = "8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d";

    [ObservableProperty]
    private bool _isPushing = true;

    [ObservableProperty]
    private string _statusMessage = "WebView 加载中...";
}
```

**为什么**：三件套特性（`[NavigationItem]`+`[Menu]`+`[ViewMap]`）注册导航项、菜单项、视图映射。`PluginId` 属性供页面 XAML 绑定到 `WebPluginView.PluginId`。`IsPushing` 控制 SSE 推送定时器。菜单 Header 用字面量（省略 resx）。继承 `ViewModelBase`（来自 `LYBox.Plugin.Shared.ViewModels`）。

#### 文件 5：`plugins/LYBox.Plugin.WebTemplate/Pages/WebTemplatePage.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:web="clr-namespace:LYBox.Plugin.Shared.Web;assembly=LYBox.Plugin.Shared"
             xmlns:vm="clr-namespace:LYBox.Plugin.WebTemplate.ViewModels"
             x:Class="LYBox.Plugin.WebTemplate.Pages.WebTemplatePage"
             x:DataType="vm:WebTemplatePageViewModel">
    <Grid RowDefinitions="Auto,*">
        <!-- 顶部控制栏：SSE 推送开关 + 状态 -->
        <Border Grid.Row="0" Padding="12,8" Background="{DynamicResource FluentSubtleBrush}">
            <StackPanel Orientation="Horizontal" Spacing="12" VerticalAlignment="Center">
                <TextBlock Text="SSE 推送：" VerticalAlignment="Center" />
                <ToggleSwitch IsChecked="{Binding IsPushing}" OnContent="开启" OffContent="停止" />
                <TextBlock Text="{Binding StatusMessage}" VerticalAlignment="Center"
                           Classes="Secondary" />
            </StackPanel>
        </Border>
        <!-- WebView 承载区 -->
        <web:WebPluginView Grid.Row="1" x:Name="WebView" PluginId="{Binding PluginId}" />
    </Grid>
</UserControl>
```

**为什么**：`web:WebPluginView` 是共享层提供的 WebView 封装控件，`PluginId` 绑定到 VM。顶部控制栏用 Ursa `ToggleSwitch`（遵守 AGENTS.md 组件选型规则）。`x:Name="WebView"` 供 code-behind 访问 `RpcHost`。

#### 文件 6：`plugins/LYBox.Plugin.WebTemplate/Pages/WebTemplatePage.axaml.cs`

```csharp
using Avalonia.Controls;
using Avalonia.Threading;
using LYBox.Plugin.Shared.Rpc;
using LYBox.Plugin.Shared.Web;
using LYBox.Plugin.WebTemplate.ViewModels;

namespace LYBox.Plugin.WebTemplate.Pages;

public partial class WebTemplatePage : UserControl
{
    private DispatcherTimer? _pushTimer;
    private int _tickCount;

    public WebTemplatePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _pushTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Normal, OnPushTick);
        _pushTimer.Start();
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _pushTimer?.Stop();
        _pushTimer = null;
    }

    private async void OnPushTick(object? sender, EventArgs e)
    {
        // ToggleSwitch 关闭时暂停推送
        if (DataContext is WebTemplatePageViewModel { IsPushing: false }) return;

        // WebPluginView.RpcHost 在 NavigationCompleted + ipc.js 注入后就绪
        if (WebView?.RpcHost is not { } host) return;

        _tickCount++;
        try
        {
            await host.EmitEventAsync("tick", new
            {
                count = _tickCount,
                time = DateTime.Now.ToString("HH:mm:ss"),
                message = $"这是 C# 第 {_tickCount} 次主动推送"
            });
        }
        catch
        {
            // 页面未就绪或已销毁，忽略
        }
    }
}
```

**为什么**：`DispatcherTimer` 每 2 秒通过 `EmitEventAsync("tick", ...)` 推送事件——优先走 SSE 通道（高频推送），无 SSE 时降级 InvokeScript。`WebView.RpcHost` 为 null 时跳过（控件未就绪）。`IsPushing=false` 时暂停。`Unloaded` 停止定时器防泄漏。

#### 文件 7：`plugins/LYBox.Plugin.WebTemplate/wwwroot/index.html`

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>LYBox Web Template</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: -apple-system, "Segoe UI", Roboto, sans-serif; padding: 24px; color: #1a1a1a; background: #fafafa; }
        h1 { font-size: 24px; margin-bottom: 16px; }
        h2 { font-size: 18px; margin: 20px 0 10px; color: #444; }
        .card { background: #fff; border: 1px solid #e0e0e0; border-radius: 8px; padding: 16px; margin-bottom: 16px; }
        .row { display: flex; gap: 8px; align-items: center; margin-bottom: 8px; }
        input { padding: 6px 10px; border: 1px solid #ccc; border-radius: 4px; font-size: 14px; }
        button { padding: 6px 14px; background: #0078d4; color: #fff; border: none; border-radius: 4px; cursor: pointer; font-size: 14px; }
        button:hover { background: #106ebe; }
        .result { margin-top: 8px; padding: 8px; background: #f0f6ff; border-radius: 4px; font-family: monospace; font-size: 13px; min-height: 20px; }
        #sse-log { max-height: 200px; overflow-y: auto; font-size: 12px; }
        .sse-entry { padding: 2px 0; border-bottom: 1px solid #eee; }
        .badge { display: inline-block; padding: 2px 8px; background: #0078d4; color: #fff; border-radius: 10px; font-size: 11px; }
    </style>
</head>
<body>
    <h1>LYBox Web Template <span class="badge" id="tick-badge">0</span></h1>
    <p>WebView + HTTP + IPC + SSE 端到端演示</p>

    <div class="card">
        <h2>1. JS → C# RPC 调用</h2>
        <div class="row">
            <input type="text" id="name-input" value="World" placeholder="输入名字" />
            <button onclick="callGreet()">GreetAsync</button>
        </div>
        <div class="result" id="greet-result">（等待调用）</div>

        <div class="row" style="margin-top:12px">
            <input type="number" id="a-input" value="3" style="width:60px" />
            <span>+</span>
            <input type="number" id="b-input" value="5" style="width:60px" />
            <button onclick="callAdd()">AddAsync</button>
        </div>
        <div class="result" id="add-result">（等待调用）</div>

        <div class="row" style="margin-top:12px">
            <button onclick="callInfo()">GetPluginInfoAsync</button>
        </div>
        <div class="result" id="info-result">（等待调用）</div>
    </div>

    <div class="card">
        <h2>2. C# → JS SSE 主动推送</h2>
        <p>每 2 秒接收一次 <code>tick</code> 事件（经 SSE 通道）：</p>
        <div id="sse-log" style="background:#f9f9f9;padding:8px;border-radius:4px;">
            <div class="sse-entry" style="color:#999;">（等待推送...）</div>
        </div>
    </div>

    <script>
        // 等待 __lybox 运行时就绪后注册事件监听
        (function waitForLybox() {
            if (window.__lybox) {
                initListener();
            } else {
                setTimeout(waitForLybox, 100);
            }
        })();

        function initListener() {
            // 订阅 C# 主动推送的 tick 事件
            window.__lybox.on('tick', function (data) {
                if (!data) return;
                var badge = document.getElementById('tick-badge');
                badge.textContent = data.count;

                var log = document.getElementById('sse-log');
                var entry = document.createElement('div');
                entry.className = 'sse-entry';
                entry.textContent = '[' + data.time + '] #' + data.count + ' ' + data.message;
                log.insertBefore(entry, log.firstChild);
            });
        }

        async function callGreet() {
            var name = document.getElementById('name-input').value;
            try {
                var result = await window.go.LYBox.Plugin.WebTemplate.Rpc.GreetCommands.GreetAsync(name);
                document.getElementById('greet-result').textContent = result;
            } catch (e) {
                document.getElementById('greet-result').textContent = '错误: ' + e.message;
            }
        }

        async function callAdd() {
            var a = parseInt(document.getElementById('a-input').value, 10);
            var b = parseInt(document.getElementById('b-input').value, 10);
            try {
                var result = await window.go.LYBox.Plugin.WebTemplate.Rpc.GreetCommands.AddAsync(a, b);
                document.getElementById('add-result').textContent = a + ' + ' + b + ' = ' + result;
            } catch (e) {
                document.getElementById('add-result').textContent = '错误: ' + e.message;
            }
        }

        async function callInfo() {
            try {
                var result = await window.go.LYBox.Plugin.WebTemplate.Rpc.GreetCommands.GetPluginInfoAsync();
                document.getElementById('info-result').textContent = JSON.stringify(result, null, 2);
            } catch (e) {
                document.getElementById('info-result').textContent = '错误: ' + e.message;
            }
        }
    </script>
</body>
</html>
```

**为什么**：原生 HTML/JS 演示页，无构建步骤。三段演示：(1) `callGreet`/`callAdd`/`callInfo` 调用 `window.go.*` RPC；(2) `__lybox.on('tick', cb)` 接收 SSE 推送；(3) 等待 `__lybox` 就绪的轮询机制（因 ipc.js 在 NavigationCompleted 后注入，此时 HTML 已加载）。**替换为 Vue/React 时**：将 `wwwroot/` 内容替换为 `npm run build` 产物即可，IPC 调用代码不变。

#### 文件 8：`plugins/LYBox.Plugin.WebTemplate/Properties/launchSettings.json`

```json
{
  "profiles": {
    "Debug Plugin - Launcher": {
      "commandName": "Executable",
      "executablePath": "$(SolutionDir)bin\\bin\\LYBox.Launcher.Desktop.exe",
      "environmentVariables": {
        "AVALONIA_EXTRA_PLUGINS_PATH": "$(TargetDir)"
      }
    }
  }
}
```

**为什么**：与 `LYBox.Plugin.Template` 的 launchSettings 一致，VS Code 调试时通过 `AVALONIA_EXTRA_PLUGINS_PATH` 指向插件输出目录实现开发期加载。注意：Web 插件的 `wwwroot` 需在 `bin/Debug/net10.0/wwwroot/` 下存在（构建时由 `CopyPluginWwwroot` 复制，开发期需手动确保源 `wwwroot/` 目录存在——已在文件 7 创建）。

#### 修改：`Plugins.slnx`

在 `/plugins/` 文件夹内追加一行：
```xml
<Project Path="plugins/LYBox.Plugin.WebTemplate/LYBox.Plugin.WebTemplate.csproj" />
```

完整修改后：
```xml
<Solution>
    <Folder Name="/plugins/">
        <Project Path="plugins/LYBox.Plugin.ButtonsInputs/LYBox.Plugin.ButtonsInputs.csproj" />
        <Project Path="plugins/LYBox.Plugin.DateTime/LYBox.Plugin.DateTime.csproj" />
        <Project Path="plugins/LYBox.Plugin.DialogFeedbacks/LYBox.Plugin.DialogFeedbacks.csproj" />
        <Project Path="plugins/LYBox.Plugin.LayoutDisplay/LYBox.Plugin.LayoutDisplay.csproj" />
        <Project Path="plugins/LYBox.Plugin.NavigationMenus/LYBox.Plugin.NavigationMenus.csproj" />
        <Project Path="plugins/LYBox.Plugin.Downloader/LYBox.Plugin.Downloader.csproj" />
        <Project Path="plugins/LYBox.Plugin.TDLSharp/LYBox.Plugin.TDLSharp.csproj" />
        <Project Path="plugins/LYBox.Plugin.Template/LYBox.Plugin.Template.csproj" />
        <Project Path="plugins/LYBox.Plugin.ScottPlot/LYBox.Plugin.ScottPlot.csproj" />
        <Project Path="plugins/LYBox.Plugin.ProDataGrid/LYBox.Plugin.ProDataGrid.csproj" />
        <Project Path="plugins/LYBox.Plugin.WebTemplate/LYBox.Plugin.WebTemplate.csproj" />
    </Folder>
</Solution>
```

---

### P7：全量构建验证

按依赖顺序执行 4 步验证：

#### P7.1 — Core.slnx 构建（已验证通过，复核）
```powershell
dotnet build Core.slnx
```
**预期**：0 错误（约 1894 个既有 nullable 警告，与本变更无关）。

#### P7.2 — SDK NuGet 包构建（插件还原前置依赖）
```powershell
.\build.ps1 --build=bin
```
**预期**：`bin/nuget/` 下生成 `LYBox.Plugin.Generators.1.0.0.nupkg` + `LYBox.Plugin.Shared.1.0.0.nupkg`（版本号取 `HostVersion=2.2.0`，实际以 `Directory.Build.props` 为准）。Launcher 发布到 `bin/bin/`。

#### P7.3 — Plugins.slnx 构建（验证 WebTemplate 编译）
```powershell
dotnet build Plugins.slnx
```
**预期**：0 错误。关键验证点：
- `GreetCommands.cs` 的 `[RpcCommand]` 被源生成器正确扫描，生成 `IRpcBindingSource` partial 实现
- `WebTemplatePlugin.cs` 的 `[GenerateMetadata]` 触发 `IPlugin` 实现生成，`IWebPlugin` 接口满足
- `WebTemplatePage.axaml` 的 `web:WebPluginView` 命名空间引用可解析
- `WebTemplatePage.axaml.cs` 的 `WebView.RpcHost`（`IRpcHost?`）类型可解析

#### P7.4 — 全量构建（验证 wwwroot 复制 + zip 打包）
```powershell
.\build.ps1 --build=all
```
**预期**：
- `bin/plugins/LYBox.Plugin.WebTemplate/publish/` 包含 `LYBox.Plugin.WebTemplate.dll` + `wwwroot/index.html` + `plugin.json`
- `bin/plugins/zip/LYBox.Plugin.WebTemplate-1.0.0.zip` 生成（排除 .pdb/.xml/.deps.json/.runtimeconfig.json）

#### P7.5 — 运行时冒烟测试（手动）
1. 启动 Launcher：`dotnet run --project src/launcher/LYBox.Launcher.Desktop`
2. 左侧 NavMenu 应出现 "Web Template Demo" 菜单项
3. 点击进入 WebTemplate 页面
4. **验证 HTTP 加载**：WebView 区域显示 HTML 页面（标题 "LYBox Web Template"）
5. **验证 JS→C# RPC**：点击 "GreetAsync" 按钮，结果显示 "Hello, World! 这是来自 C# 的问候。"
6. **验证 SSE 推送**：页面右上角计数器每 2 秒递增，日志区出现推送记录
7. **验证推送开关**：关闭顶部 ToggleSwitch，SSE 推送停止；重新开启恢复

---

## 假设与决策

| # | 假设/决策 | 理由 |
|---|----------|------|
| 1 | 前端用原生 HTML/JS，不引入 Vue/React 构建链 | 聚焦演示 IPC/SSE 机制；基础设施框架无关，wwwroot 可直接替换为任何前端构建产物 |
| 2 | 菜单 Header 用字面量字符串，省略 resx 资源文件 | 保持插件精简；`ILocalizationService` 找不到资源键时回退为字面量 |
| 3 | C#→JS 推送由页面 code-behind 的 DispatcherTimer 驱动 | 最直接的方式演示 SSE；ViewModel 无需访问 RpcHost（WebPluginView.RpcHost 非绑定属性） |
| 4 | PluginId = `8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d` | 合法 UUID v4 格式，全链路（csproj/代码/XAML 绑定/Kestrel 路由/SSE 通道）一致 |
| 5 | `WebTemplatePlugin` 同时实现 `IPluginMetadata` + `IWebPlugin` | `IWebPlugin : IPlugin`，源生成器生成 `IPlugin` 方法实现，`IWebPlugin` 额外要求 `PluginBaseDir` 属性 |
| 6 | 不创建 `Converters/PushToggleConverter.cs` | ToggleSwitch 直接绑定 bool 属性，无需转换器 |
| 7 | `wwwroot/index.html` 在开发期直接放源目录 | 构建时 `CopyPluginWwwroot` 复制到输出；开发期 `AVALONIA_EXTRA_PLUGINS_PATH` 指向 `bin/Debug/net10.0`，需确保 wwwroot 也在该目录（构建会复制） |

---

## 验证步骤汇总

| 步骤 | 命令 | 预期结果 | 依赖 |
|------|------|---------|------|
| P7.1 | `dotnet build Core.slnx` | 0 错误 | 无 |
| P7.2 | `.\build.ps1 --build=bin` | `bin/nuget/*.nupkg` + `bin/bin/LYBox.Launcher.Desktop.exe` | P7.1 |
| P7.3 | `dotnet build Plugins.slnx` | 0 错误（含 WebTemplate） | P7.2（本地 NuGet 源） |
| P7.4 | `.\build.ps1 --build=all` | `bin/plugins/LYBox.Plugin.WebTemplate/publish/wwwroot/index.html` + zip | P7.2 |
| P7.5 | 手动启动 + 点击 | HTML 加载 + RPC 响应 + SSE 计数 | P7.4 |

---

## 实施顺序

1. **P6.1** — 创建 `plugins/LYBox.Plugin.WebTemplate/` 目录
2. **P6.2** — 写 8 个文件（csproj → WebTemplatePlugin.cs → GreetCommands.cs → ViewModel → Page.axaml → Page.axaml.cs → index.html → launchSettings.json）
3. **P6.3** — 修改 `Plugins.slnx` 追加项目引用
4. **P7.1** — `dotnet build Core.slnx`（复核）
5. **P7.2** — `.\build.ps1 --build=bin`
6. **P7.3** — `dotnet build Plugins.slnx`
7. **P7.4** — `.\build.ps1 --build=all`
8. **P7.5** — 手动冒烟测试（告知用户验证步骤）
