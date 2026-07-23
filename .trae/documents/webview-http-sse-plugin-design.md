# WebView + HTTP + SSE 插件系统设计方案（续作计划）

> **状态**：P1/P2 已完成，P3-P7 待执行
> **范围**：在现有 LYBox 插件框架上叠加 Web 前端能力，支持 Vue/React 等任意前端框架通过 HTTP 加载、IPC 双向通信、SSE 主动推送
> **基线**：Avalonia 12.1.0 + Avalonia.Controls.WebView 12.0.1 + .NET 10 + 现有 `LYBox.Plugin.Shared.Rpc` IPC 层

---

## 1. 当前进度总结

### 1.1 已完成（P1 基础设施 + P2 IPC 集成）

| 阶段 | 交付物 | 实际文件 |
|------|--------|----------|
| P1 ✅ | `IEventPusher` 抽象 | `src/LYBox.Plugin.Shared/Rpc/IEventPusher.cs` |
| P1 ✅ | `SseEventPusher` 实现 + `SseClient` | `src/LYBox.Plugin.Shared/Rpc/SseEventPusher.cs` |
| P1 ✅ | `WebHostService`（Kestrel 嵌入式主机） | `src/LYBox.Plugin.Shared/Web/WebHostService.cs` |
| P1 ✅ | `IWebPlugin` 契约 | `src/LYBox.Plugin.Shared/Web/IWebPlugin.cs` |
| P1 ✅ | 版本 bump `HostVersion 2.1.0 → 2.2.0` | `Directory.Build.props` |
| P1 ✅ | 依赖：`Avalonia.Controls.WebView 12.0.1` + `FrameworkReference Microsoft.AspNetCore.App` | `src/Directory.Packages.props`、`src/LYBox.Plugin.Shared/LYBox.Plugin.Shared.csproj`、`src/launcher/LYBox.Launcher.Desktop/LYBox.Launcher.Desktop.csproj` |
| P1 ✅ | 共享程序集清单追加 `Microsoft.AspNetCore.*` 等 | `src/LYBox.Plugin.Shared/buildTransitive/LYBox.Plugin.Shared.props` |
| P2 ✅ | `WebViewIpcHost` 注入 `IEventPusher?` + `pluginId?` | `src/LYBox.Plugin.Shared/Rpc/WebViewIpcHost.cs` |
| P2 ✅ | `Channel<T>` 注入 `IEventPusher?` + `pluginId?` | `src/LYBox.Plugin.Shared/Rpc/Channel.cs` |
| P2 ✅ | `ipc.js` 追加 `startSse(pluginId)` 函数 | `src/LYBox.Plugin.Shared/Rpc/Assets/ipc.js` |

### 1.2 P1/P2 实现与原计划的偏差（已修正）

| 偏差项 | 原计划 | 实际实现 | 原因 |
|--------|--------|----------|------|
| WebView 版本 | 12.1.0 | **12.0.1** | nuget.org 上 12.1.0 不存在，12.0.1 是最新兼容版 |
| ASP.NET Core 引用方式 | `PackageReference Microsoft.AspNetCore.Server.Kestrel` | **`FrameworkReference Microsoft.AspNetCore.App`** | .NET Core 3+ 起不再发布独立 NuGet 包 |
| `SseEventPusher` 客户端存储 | `List<SseClient>` 无锁 | **`ClientList` + lock** | 线程安全 |
| `SseClient.WriteAsync` 参数 | `string message` | **`byte[] bytes`** | 避免每次推送重复编码 |
| `WebHostService._pluginRoots` | `Dictionary<string, string>` | **`ConcurrentDictionary<string, string>`** | 线程安全 |
| `ipc.js` SSE 启动 | 自动启动（读 `__pluginId`） | **显式 `startSse(pluginId)` 调用** | 控制初始化时序，宿主注入 ipc.js 后显式调用 |
| `WebHostService.DisposeAsync` 返回类型 | `Task` | **`ValueTask`** | `IAsyncDisposable` 接口要求 |

### 1.3 待执行阶段（P3-P7）

| 阶段 | 交付物 | 依赖 |
|------|--------|------|
| **P3** | `WebPluginView` 控件 + `WebViewIpcTransport` + `WebPluginBindings` | P2 ✅ |
| **P4** | `PluginLoader.GetWebPluginRoots()` + `App.axaml.cs` 启动序列 | P3 |
| **P5** | `build.cs` wwwroot 复制 | P4 |
| **P6** | 示例插件 `LYBox.Plugin.WebTemplate`（Vue 3 CDN） | P5 |
| **P7** | 单元测试 + Windows 集成验证 | P6 |

---

## 2. P3: WebPluginView 控件

### 2.1 目标

创建一个 Avalonia `UserControl`，封装 `Avalonia.Controls.WebView`，自动完成：
1. 指向 `WebHostService.BaseUrl/{pluginId}/index.html` 加载前端页面
2. 桥接 `WebView.WebMessageReceived` → `IRpcTransport.MessageReceived`
3. 桥接 `IRpcTransport.ExecuteScriptAsync` → `WebView.InvokeScript`
4. `NavigationCompleted` 后注入 `ipc.js` → 调用 `startSse(pluginId)` → 注入绑定清单
5. 提供静态帮助类 `WebPluginBindings.Register(host, plugin)` 扫描插件 `[RpcCommand]` 类

### 2.2 新增文件（4 个）

#### 2.2.1 `src/LYBox.Plugin.Shared/Web/WebViewIpcTransport.cs`

`IRpcTransport` 实现：把 `Avalonia.Controls.WebView` 的两个原生原语桥接到 IPC 运行时。

```csharp
using Avalonia.Controls;
using LYBox.Plugin.Shared.Rpc;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// IRpcTransport 实现：桥接 Avalonia.Controls.WebView 的原生 IPC 原语。
/// JS→C#: WebView.WebMessageReceived 事件（JS 调 invokeCSharpAction(body)）。
/// C#→JS: WebView.InvokeScript(js) 执行任意 JS 表达式。
/// </summary>
public sealed class WebViewIpcTransport : IRpcTransport
{
    private readonly WebView _webView;

    public WebViewIpcTransport(WebView webView)
    {
        _webView = webView;
        _webView.WebMessageReceived += OnMessageReceived;
    }

    public event Action<string?>? MessageReceived;

    public Task<string?> ExecuteScriptAsync(string javaScript, CancellationToken cancellationToken = default)
    {
        // Avalonia.Controls.WebView 的 InvokeScript 是同步签名返回 string?
        // 包装为 Task 完成结果；cancellationToken 尽力检查
        cancellationToken.ThrowIfCancellationRequested();
        var result = _webView.InvokeScript(javaScript);
        return Task.FromResult(result);
    }

    private void OnMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        // e.Body 是 JS 经 invokeCSharpAction 发来的字符串
        MessageReceived?.Invoke(e.Body);
    }

    public void Detach()
    {
        _webView.WebMessageReceived -= OnMessageReceived;
    }
}
```

**关键点**：
- `WebView.InvokeScript` 返回 `string?`（JS 表达式求值结果），非 async，用 `Task.FromResult` 包装
- `WebMessageReceivedEventArgs.Body` 即 JS→C# 的字符串载荷
- 提供 `Detach()` 方法供 `WebPluginView` 在卸载时解绑，避免事件泄漏

#### 2.2.2 `src/LYBox.Plugin.Shared/Web/WebPluginView.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:web="clr-namespace:Avalonia.Controls;assembly=Avalonia.Controls.WebView"
             x:Class="LYBox.Plugin.Shared.Web.WebPluginView">
    <web:WebView x:Name="PART_WebView" />
</UserControl>
```

#### 2.2.3 `src/LYBox.Plugin.Shared/Web/WebPluginView.axaml.cs`

```csharp
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.VisualTree;
using LYBox.Plugin.Shared.Rpc;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// 封装 Avalonia.Controls.WebView 的 UserControl，提供 Web 插件的页面承载 + IPC 集成。
/// 使用方式：在插件 ViewModel 中通过 ViewLocator 自动解析，或手动设置 PluginId/RpcHost 属性。
/// </summary>
public partial class WebPluginView : UserControl
{
    private WebViewIpcTransport? _transport;
    private WebViewIpcHost? _host;
    private bool _initialized;

    public WebPluginView()
    {
        InitializeComponent();
    }

    /// <summary>插件 ID（路由前缀）。必须在控件附加到视觉树前设置。</summary>
    public string? PluginId { get; set; }

    /// <summary>RPC 主机。若为 null，控件仅加载页面不启用 IPC。</summary>
    public IRpcHost? RpcHost { get; set; }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        await InitializeAsync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _transport?.Detach();
        _transport = null;
        base.OnDetachedFromVisualTree(e);
    }

    private async Task InitializeAsync()
    {
        if (_initialized || PluginId is null) return;
        _initialized = true;

        var webView = this.FindControl<WebView>("PART_WebView");
        if (webView is null) return;

        // 1. 获取 WebHostService 单例
        if (!ServiceLocator.TryGetService<WebHostService>(out var webHost) || webHost is null)
            return;

        // 2. 构造 IPC 传输层 + 主机
        _transport = new WebViewIpcTransport(webView);
        _host = RpcHost as WebViewIpcHost;
        if (_host is null && RpcHost is not null)
        {
            // 如果传入的 IRpcHost 不是 WebViewIpcHost，无法注入绑定，仅做传输
            _host = null;
        }
        else if (_host is null)
        {
            // 自动创建主机（注入 SSE pusher + pluginId 以启用 SSE 推送）
            _host = new WebViewIpcHost(_transport, webHost.EventPusher, PluginId);
            RpcHost = _host;
        }
        else
        {
            // 已有主机，仅替换传输层（不重置命令注册）
            // 注意：WebViewIpcHost 构造时已绑定旧 transport，此场景需重新构造
            _host = new WebViewIpcHost(_transport, webHost.EventPusher, PluginId);
            RpcHost = _host;
        }

        // 3. 订阅 NavigationCompleted 注入引导脚本
        webView.NavigationCompleted += async (_, _) =>
        {
            try
            {
                // 3a. 注入 ipc.js（含 __lybox 运行时 + startSse 函数）
                await _host.InitializeAsync();
                // 3b. 显式启动 SSE（pluginId 由参数传入，非 __pluginId 全局变量）
                var pidJson = JsonSerializer.Serialize(PluginId);
                await _transport.ExecuteScriptAsync(
                    $"window.__lybox && window.__lybox.startSse({pidJson});");
                // 3c. 注入绑定清单（window.go.* 胶水）
                await _host.InjectBindingsAsync();
            }
            catch
            {
                // 页面已销毁或 WebView 未就绪，忽略
            }
        };

        // 4. 导航到插件入口页
        var url = $"{webHost.BaseUrl}/{PluginId}/{GetEntryPage()}";
        webView.Source = new Uri(url);
    }

    private string GetEntryPage()
    {
        // 从 IWebPlugin.EntryPage 读取，默认 index.html
        // 此处简化：WebPluginView 不直接持有 IWebPlugin 引用，
        // 由插件 ViewModel 在创建 View 时通过 PluginId 路由 + 默认 index.html
        return "index.html";
    }
}
```

**关键点**：
- 使用 `ServiceLocator.TryGetService<WebHostService>` 获取全局唯一 Kestrel 主机
- `WebViewIpcHost` 注入 `webHost.EventPusher`（SSE 推送器）+ `PluginId`（SSE 路由 key）
- `NavigationCompleted` 后三步注入：`InitializeAsync`（ipc.js）→ `startSse(pluginId)` → `InjectBindingsAsync`（绑定清单）
- `OnDetachedFromVisualTree` 调用 `_transport.Detach()` 解绑事件，避免泄漏
- `webView.Source` 指向 `http://127.0.0.1:{port}/{pluginId}/index.html`

#### 2.2.4 `src/LYBox.Plugin.Shared/Web/WebPluginBindings.cs`

静态帮助类：扫描插件程序集中所有标注 `[RpcCommand]` 的类，注册到 `IRpcHost`。

```csharp
using System.Reflection;
using LYBox.Plugin.Shared.Rpc;

namespace LYBox.Plugin.Shared.Web;

/// <summary>
/// Web 插件绑定注册帮助类。
/// 扫描 IWebPlugin 所在程序集中所有 IRpcBindingSource 实现（由源生成器针对 [RpcCommand] 类生成），
/// 调用其 RegisterBindings(host) 方法把命令注册到 IPC 主机。
/// </summary>
public static class WebPluginBindings
{
    /// <summary>
    /// 把插件程序集中所有 [RpcCommand] 类注册到 RPC 主机。
    /// 在 PluginLoader.RegisterAllPluginsAsync 阶段对每个 IWebPlugin 调用。
    /// </summary>
    public static void Register(IRpcHost host, IWebPlugin plugin)
    {
        var asm = plugin.GetType().Assembly;
        foreach (var type in asm.GetExportedTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;
            if (!typeof(IRpcBindingSource).IsAssignableFrom(type)) continue;

            try
            {
                var bindingSource = (IRpcBindingSource?)Activator.CreateInstance(type);
                bindingSource?.RegisterBindings(host);
            }
            catch
            {
                // 单个绑定类注册失败不影响其他
            }
        }
    }
}
```

**关键点**：
- 依赖 P2 已有的 `IRpcBindingSource` 接口（`RegisterBindings(IRpcHost)` + `TsDeclarations` + `JsGlue`）
- 源生成器（`LYBox.Plugin.Generators`）针对 `[RpcCommand]` 类自动生成 `IRpcBindingSource` 实现
- 单个绑定类失败不影响其他（try-catch 包裹）

### 2.3 P3 验收标准

- `Core.slnx` 构建通过，0 错误
- `WebPluginView` 可在 XAML 中实例化（无运行时异常）
- `WebViewIpcTransport` 实现 `IRpcTransport`，可被 `WebViewIpcHost` 构造时接受

---

## 3. P4: 插件系统集成

### 3.1 目标

把 `WebHostService` 接入应用启动流程，让 `IWebPlugin` 实例的 `wwwroot/` 目录被注册到 Kestrel 路由。

### 3.2 修改文件（2 个）

#### 3.2.1 `src/LYBox.Layout.Core/Services/PluginLoader.cs`

新增 `GetWebPluginRoots()` 公共方法，返回所有已加载 `IWebPlugin` 的 `(pluginId, wwwrootPath)` 元组。

**新增方法**（建议放在 `GetLoadedPlugin` 附近，约 line 510 后）：

```csharp
/// <summary>
/// 返回所有已加载 IWebPlugin 实例的 (pluginId, wwwrootPath) 元组。
/// 由 App 启动序列在 WebHostService.StartAsync 后调用，注册 Kestrel 静态文件路由。
/// </summary>
/// <remarks>
/// wwwrootPath 优先取 IWebPlugin.WwwrootPath（基于 PluginBaseDir）；
/// 若 PluginBaseDir 未设置，回退到 PluginInfo.InstallPath/wwwroot。
/// 仅返回 wwwrootPath 目录确实存在的插件（避免注册不存在的路由）。
/// </remarks>
public IEnumerable<(string pluginId, string wwwrootPath)> GetWebPluginRoots()
{
    List<PluginEntry> snapshot;
    lock (_sync)
    {
        snapshot = _entries.Values
            .Where(e => e.Plugin is IWebPlugin && e.Info.State == PluginState.Loaded)
            .ToList();
    }

    foreach (var entry in snapshot)
    {
        var webPlugin = (IWebPlugin)entry.Plugin!;
        // 注入 PluginBaseDir（基于 InstallPath），供 IWebPlugin.WwwrootPath 默认实现使用
        if (string.IsNullOrEmpty(webPlugin.PluginBaseDir))
        {
            webPlugin.PluginBaseDir = entry.Info.InstallPath;
        }

        var wwwroot = webPlugin.WwwrootPath;
        if (!string.IsNullOrEmpty(wwwroot) && Directory.Exists(wwwroot))
        {
            yield return (entry.Info.PluginId, wwwroot);
        }
    }
}
```

**关键点**：
- `PluginInfo.InstallPath` 已存在（P1 调研确认），指向插件目录（`plugins/{PluginId}/` 或 extra plugin 目录）
- `IWebPlugin.PluginBaseDir` 是 `{ get; set; }`，宿主在此方法中注入实际路径
- 仅返回 `PluginState.Loaded` 状态的插件（Error/Disabled 不注册路由）
- 仅返回 `wwwroot` 目录确实存在的插件（避免 404 路由）

#### 3.2.2 `src/launcher/LYBox.Launcher.Desktop/App.axaml.cs`

在 `Initialize()` 方法中追加 `WebHostService` 的注册与启动。

**变更点 1**：在 `services.AddUrsaServices()` / `services.AddFluentServices()` 后（约 line 93 后）追加：

```csharp
// WebHostService：嵌入式 Kestrel HTTP 资源服务（单例，随 ServiceProvider Dispose 自动停止）
services.AddSingleton<WebHostService>();
```

**变更点 2**：在 `ServiceProvider = services.BuildServiceProvider();` 后（约 line 107 后）追加：

```csharp
// 启动 Kestrel HTTP 资源服务（必须在插件 Register 阶段前启动，以便 WebPluginView 能获取 BaseUrl）
var webHost = ServiceProvider.GetRequiredService<WebHostService>();
webHost.StartAsync().GetAwaiter().GetResult();
```

**变更点 3**：在 `pluginLoader.RegisterAllPluginsAsync(ServiceProvider).GetAwaiter().GetResult();` 后、`RegisterPluginNavigationAndMenus(pluginLoader);` 前（约 line 134-135 之间）追加：

```csharp
// 注册所有 Web 插件的 wwwroot 路由到 Kestrel
foreach (var (pluginId, wwwrootPath) in pluginLoader.GetWebPluginRoots())
{
    webHost.MapPluginRoot(pluginId, wwwrootPath);
    // 对每个 Web 插件注册 RPC 绑定（[RpcCommand] 类）
    var webPlugin = pluginLoader.GetLoadedPlugin(pluginId) as IWebPlugin;
    if (webPlugin is not null)
    {
        // 注意：此时还未有 WebViewIpcHost 实例（控件尚未创建）
        // WebPluginBindings.Register 需在 WebPluginView 创建时调用，
        // 或在此处创建一个共享的 WebViewIpcHost（不推荐，因为每个 WebView 需独立 transport）
        // 决策：WebPluginBindings.Register 推迟到 WebPluginView.InitializeAsync 中调用
    }
}
```

**决策修正**：`WebPluginBindings.Register` 不在 App 启动时调用，而是推迟到 `WebPluginView.InitializeAsync` 中。因为每个 `WebView` 需要独立的 `IRpcTransport`，`WebViewIpcHost` 实例在控件创建时才构造。`WebPluginView.axaml.cs` 需补充绑定注册逻辑。

**WebPluginView.axaml.cs 补充**（在 `InitializeAsync` 的 `NavigationCompleted` 回调前追加）：

```csharp
// 注册插件的 [RpcCommand] 绑定（需在 InjectBindingsAsync 前完成）
if (ServiceLocator.TryGetService<IPluginLoader>(out var loader) && loader is PluginLoader pl)
{
    var plugin = pl.GetLoadedPlugin(PluginId) as IWebPlugin;
    if (plugin is not null)
    {
        WebPluginBindings.Register(_host, plugin);
    }
}
```

### 3.3 P4 验收标准

- `Core.slnx` 构建通过，0 错误
- 启动宿主，`WebHostService.StartAsync()` 成功（日志可见 Kestrel 监听端口）
- 无 Web 插件时 `GetWebPluginRoots()` 返回空列表，不报错
- `OnShutdownRequested` 中 `ServiceProvider.Dispose()` 触发 `WebHostService.DisposeAsync()`，Kestrel 优雅停止

---

## 4. P5: 构建系统

### 4.1 目标

`build.cs` 的 `PackPlugins` 阶段在 `DotNetPublish` 后检测插件 `wwwroot/` 目录，递归复制到 `publish/wwwroot/`。

### 4.2 修改文件（1 个）

#### 4.2.1 `build/build.cs`

在 `PackPlugins` Task 的 `DotNetPublish` 调用后（约 line 281 后，`PackPluginZips` 调用前）追加 wwwroot 复制逻辑：

```csharp
// 复制 wwwroot/ 到 publish 目录（Web 插件前端资源）
var pluginSourceDir = Path.GetDirectoryName(plugin.ProjectPath(buildContext.RootDir))!;
var wwwrootSource = Path.Combine(pluginSourceDir, "wwwroot");
if (Directory.Exists(wwwrootSource))
{
    var wwwrootDest = Path.Combine(pluginOutputDir, "wwwroot");
    if (Directory.Exists(wwwrootDest))
        Directory.Delete(wwwrootDest, recursive: true);
    CopyDirectoryRecursive(wwwrootSource, wwwrootDest);
    c.Log.Information("wwwroot/ copied for plugin: {0}", plugin.ProjectName);
}
```

**辅助方法**（在 `BuildContext` 类或 `PackPluginZips` 静态方法附近追加）：

```csharp
static void CopyDirectoryRecursive(string sourceDir, string destDir)
{
    Directory.CreateDirectory(destDir);
    foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(sourceDir, file);
        var dest = Path.Combine(destDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(file, dest, overwrite: true);
    }
}
```

**关键点**：
- 仅当插件源码目录下存在 `wwwroot/` 时才复制（非 Web 插件无此目录，不受影响）
- 复制到 `{publishDir}/wwwroot/`，与 `IWebPlugin.WwwrootPath`（`Path.Combine(PluginBaseDir, "wwwroot")`）对齐
- `PluginBaseDir` 在运行时由 `PluginLoader.GetWebPluginRoots()` 注入为 `PluginInfo.InstallPath`，即 `publish/` 目录
- zip 打包逻辑（`PackPluginZips`）无需修改，`wwwroot/` 会被自动包含（因为它在 `publishDir` 下，且文件扩展名不在排除清单中）

### 4.3 P5 验收标准

- `.\build.ps1 --build=plugin` 构建后，`bin/plugins/{PluginName}/publish/wwwroot/` 存在
- 非 Web 插件构建不受影响（无 wwwroot 目录则跳过）
- zip 包含 `wwwroot/` 子目录

---

## 5. P6: 示例插件 LYBox.Plugin.WebTemplate

### 5.1 目标

创建一个 Vue 3（CDN 引入，无构建链）示例 Web 插件，验证完整闭环：
- HTML 页面通过 Kestrel 加载
- `window.go.*` 调用 C# RPC 方法
- SSE 接收 C# 主动推送事件
- Channel 流式通道接收 C# 推送数据

### 5.2 新增文件（4 个）

#### 5.2.1 `plugins/LYBox.Plugin.WebTemplate/LYBox.Plugin.WebTemplate.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <PluginId>web-template-0000-0000-000000000001</PluginId>
    <PluginName>Web Template Plugin</PluginName>
    <PluginAuthor>LYBox</PluginAuthor>
    <PluginDescription>Vue 3 Web plugin template demonstrating HTTP loading + IPC + SSE</PluginDescription>
    <PluginVersion>1.0.0</PluginVersion>
    <Version>$(PluginVersion)</Version>
    <AssemblyVersion>$(PluginVersion)</AssemblyVersion>
    <FileVersion>$(PluginVersion)</FileVersion>
    <MinPluginSdkVersion>2.2.0</MinPluginSdkVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LYBox.Plugin.Generators" Version="$(PluginSdkVersion)" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="LYBox.Plugin.Shared" Version="$(PluginSdkVersion)" PrivateAssets="all" />
  </ItemGroup>
  <!-- wwwroot 不参与编译，但需复制到输出目录 -->
  <ItemGroup>
    <None Include="wwwroot\**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

**关键点**：
- `MinPluginSdkVersion=2.2.0` 声明需要新 SDK（含 `IWebPlugin` + `WebHostService`）
- `<None Include="wwwroot\**" CopyToOutputDirectory="PreserveNewest" />` 确保 `dotnet build` 时 wwwroot 复制到 `bin/Debug/`，支持 VS Code 调试模式（`AVALONIA_EXTRA_PLUGINS_PATH`）
- P5 的 `build.cs` 负责发布场景的 wwwroot 复制

#### 5.2.2 `plugins/LYBox.Plugin.WebTemplate/WebTemplatePlugin.cs`

```csharp
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Web;

namespace LYBox.Plugin.WebTemplate;

[GenerateMetadata]
public partial class WebTemplatePlugin : IPluginMetadata, IWebPlugin
{
    public string Name => "Web Template";
    public string Version => "1.0.0";
    public string Author => "LYBox";
    public string Description => "Vue 3 Web plugin demonstrating HTTP + IPC + SSE";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

    // IWebPlugin.PluginBaseDir 由 PluginLoader.GetWebPluginRoots() 注入
    public string PluginBaseDir { get; set; } = string.Empty;
    // WwwrootPath 和 EntryPage 使用 IWebPlugin 默认实现

    public Task InitializeAsync(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider)
        => Task.CompletedTask;
}
```

**关键点**：
- 同时实现 `IPluginMetadata`（插件元数据）+ `IWebPlugin`（Web 资源声明）
- `[GenerateMetadata]` 源生成器扫描伴生类上的 `[ViewMap]` / `[NavigationItem]` / `[Menu]` 特性
- `PluginBaseDir` 初始为空，由宿主在 `GetWebPluginRoots()` 中注入

#### 5.2.3 `plugins/LYBox.Plugin.WebTemplate/ViewModels/WebTemplatePageViewModel.cs`

```csharp
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Web;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.WebTemplate.ViewModels;

[NavigationItem("WebTemplate")]
[Menu("NAV_WebTemplate", "WebTemplate", ParentKey = null, Status = "New", Order = 998)]
[ViewMap(typeof(Pages.WebTemplatePage))]
public partial class WebTemplatePageViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _pluginId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
}
```

#### 5.2.4 `plugins/LYBox.Plugin.WebTemplate/Pages/WebTemplatePage.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:web="clr-namespace:LYBox.Plugin.Shared.Web;assembly=LYBox.Plugin.Shared"
             xmlns:vm="clr-namespace:LYBox.Plugin.WebTemplate.ViewModels"
             x:Class="LYBox.Plugin.WebTemplate.Pages.WebTemplatePage"
             x:DataType="vm:WebTemplatePageViewModel">
    <web:WebPluginView PluginId="{Binding PluginId}" />
</UserControl>
```

```csharp
// WebTemplatePage.axaml.cs
using Avalonia.Controls;

namespace LYBox.Plugin.WebTemplate.Pages;

public partial class WebTemplatePage : UserControl
{
    public WebTemplatePage()
    {
        InitializeComponent();
    }
}
```

#### 5.2.5 `plugins/LYBox.Plugin.WebTemplate/wwwroot/index.html`

Vue 3 CDN 单页，演示 RPC 调用 + SSE 事件订阅 + Channel 流式数据：

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>LYBox Web Template</title>
    <script src="https://unpkg.com/vue@3/dist/vue.global.prod.js"></script>
    <style>
        body { font-family: 'Segoe UI', system-ui, sans-serif; margin: 24px; color: #333; }
        .card { border: 1px solid #e5e7eb; border-radius: 8px; padding: 16px; margin-bottom: 16px; }
        button { background: #0078d4; color: white; border: none; padding: 8px 16px; border-radius: 4px; cursor: pointer; }
        button:hover { background: #106ebe; }
        .log { background: #f5f5f5; padding: 12px; border-radius: 4px; font-family: monospace; font-size: 12px; max-height: 200px; overflow-y: auto; }
    </style>
</head>
<body>
    <div id="app">
        <h1>LYBox Web Template (Vue 3)</h1>
        <div class="card">
            <h3>RPC 调用</h3>
            <button @click="callPing">Call C# Ping()</button>
            <button @click="callAdd">Call C# Add(2, 3)</button>
            <p>结果: {{ rpcResult }}</p>
        </div>
        <div class="card">
            <h3>SSE 事件订阅</h3>
            <button @click="subscribeEvent">订阅 userUpdated 事件</button>
            <p>收到事件: {{ eventReceived }}</p>
        </div>
        <div class="card">
            <h3>Channel 流式数据</h3>
            <button @click="openChannel">打开通道</button>
            <p>通道数据: {{ channelData }}</p>
        </div>
        <div class="card">
            <h3>日志</h3>
            <div class="log" ref="logEl">
                <div v-for="line in logs">{{ line }}</div>
            </div>
        </div>
    </div>
    <script>
        const { createApp, ref, onMounted } = Vue;
        createApp({
            setup() {
                const rpcResult = ref('');
                const eventReceived = ref('');
                const channelData = ref('');
                const logs = ref([]);
                const logEl = ref(null);

                function log(msg) {
                    const ts = new Date().toLocaleTimeString();
                    logs.value.push(`[${ts}] ${msg}`);
                    if (logs.value.length > 50) logs.value.shift();
                }

                function callPing() {
                    if (!window.go || !window.go.LYBox || !window.go.LYBox.Plugin || !window.go.LYBox.Plugin.WebTemplate || !window.go.LYBox.Plugin.WebTemplate.Api) {
                        log('window.go.* 绑定未就绪');
                        return;
                    }
                    window.go.LYBox.Plugin.WebTemplate.Api.Ping()
                        .then(r => { rpcResult.value = r; log('Ping() => ' + r); })
                        .catch(e => log('Ping() error: ' + e));
                }

                function callAdd() {
                    if (!window.go || !window.go.LYBox || !window.go.LYBox.Plugin || !window.go.LYBox.Plugin.WebTemplate || !window.go.LYBox.Plugin.WebTemplate.Api) {
                        log('window.go.* 绑定未就绪');
                        return;
                    }
                    window.go.LYBox.Plugin.WebTemplate.Api.Add(2, 3)
                        .then(r => { rpcResult.value = r; log('Add(2,3) => ' + r); })
                        .catch(e => log('Add() error: ' + e));
                }

                function subscribeEvent() {
                    window.__lybox.on('userUpdated', (data) => {
                        eventReceived.value = JSON.stringify(data);
                        log('event userUpdated: ' + JSON.stringify(data));
                    });
                    log('已订阅 userUpdated 事件');
                }

                function openChannel() {
                    if (!window.go || !window.go.LYBox || !window.go.LYBox.Plugin || !window.go.LYBox.Plugin.WebTemplate || !window.go.LYBox.Plugin.WebTemplate.Api) {
                        log('window.go.* 绑定未就绪');
                        return;
                    }
                    window.go.LYBox.Plugin.WebTemplate.Api.OpenChannel()
                        .then(ch => {
                            log('通道已打开: ' + ch.id);
                            ch.on(data => {
                                channelData.value = JSON.stringify(data);
                                log('channel data: ' + JSON.stringify(data));
                            });
                        })
                        .catch(e => log('OpenChannel error: ' + e));
                }

                onMounted(() => {
                    log('Vue app mounted, waiting for __lybox ready...');
                    // 等待 __lybox 运行时就绪
                    const checkReady = setInterval(() => {
                        if (window.__lybox) {
                            clearInterval(checkReady);
                            log('__lybox 运行时就绪');
                            log('SSE 状态: ' + (window.__lybox.startSse ? '可用' : '不可用'));
                        }
                    }, 100);
                });

                return { rpcResult, eventReceived, channelData, logs, logEl, callPing, callAdd, subscribeEvent, openChannel };
            }
        }).mount('#app');
    </script>
</body>
</html>
```

#### 5.2.6 RPC 绑定类（可选，用于演示 [RpcCommand]）

`plugins/LYBox.Plugin.WebTemplate/Api/WebTemplateApi.cs`：

```csharp
using LYBox.Plugin.Shared.Rpc;

namespace LYBox.Plugin.WebTemplate.Api;

/// <summary>
/// WebTemplate 插件的 RPC 绑定类。
/// 源生成器扫描 [RpcCommand] 方法，生成 IRpcBindingSource 实现并注册到 window.go.LYBox.Plugin.WebTemplate.Api.*。
/// </summary>
public static class WebTemplateApi
{
    [RpcCommand]
    public static Task<string> Ping()
        => Task.FromResult("pong from C#");

    [RpcCommand]
    public static Task<int> Add(int a, int b)
        => Task.FromResult(a + b);

    [RpcCommand]
    public static Task<Channel<int>> OpenChannel(IRpcHost host)
    {
        // 创建一个 C# 侧拥有的流式通道，每秒推送一个递增数字
        var channel = host.CreateChannel<int>();
        _ = Task.Run(async () =>
        {
            for (var i = 0; i < 10; i++)
            {
                await channel.WriteAsync(i);
                await Task.Delay(1000);
            }
            await channel.CloseAsync();
        });
        return Task.FromResult(channel);
    }
}
```

**注意**：`[RpcCommand]` 特性与 `IRpcBindingSource` 源生成器的具体实现取决于 `LYBox.Plugin.Generators`。若该特性尚未实现，P6 可先省略绑定类，前端通过 `window.__lybox.invoke(name, args)` 直接调用（手动注册命令）。需在实现时确认源生成器状态。

### 5.3 P6 验收标准

- `.\build.ps1 --build=all` 构建通过，0 错误
- `bin/plugins/LYBox.Plugin.WebTemplate/publish/wwwroot/index.html` 存在
- 启动宿主，导航到 WebTemplate 插件页，Vue 页面正常渲染
- 点击 "Call C# Ping()" 按钮，显示 "pong from C#"
- 点击 "订阅 userUpdated 事件" 后，C# 侧触发事件，前端收到推送

---

## 6. P7: 测试与验证

### 6.1 单元测试

新建 `tests/LYBox.Tests/WebHost/` 目录，覆盖以下用例：

| 测试类 | 测试用例 | 覆盖点 |
|--------|---------|--------|
| `WebHostServiceTests` | `Start_AssignsNonZeroPort` | Kestrel 启动后 `Port > 0` |
| `WebHostServiceTests` | `MapPluginRoot_ServesStaticFile` | 注册后 `GET /{pluginId}/index.html` 返回 200 + 内容 |
| `WebHostServiceTests` | `MapPluginRoot_RejectsPathTraversal` | `GET /{pluginId}/../other` 返回 403 |
| `WebHostServiceTests` | `SseEndpoint_SendsReadyEvent` | `GET /sse/{pluginId}` 收到 `event: ready` 首帧 |
| `SseEventPusherTests` | `PushAsync_DeliversToSubscribedClient` | 订阅后 `PushAsync` 客户端收到 `event: dispatch` |
| `SseEventPusherTests` | `PushAsync_NoClient_NoException` | 无订阅时 `PushAsync` 静默返回 |
| `SseEventPusherTests` | `PushAsync_DeadClientRemoved` | 写入失败的客户端自动从列表移除 |
| `WebViewIpcHostTests` | `EmitEventAsync_UsesEventPusher_WhenInjected` | 注入 mock pusher 后走 `PushAsync` |
| `WebViewIpcHostTests` | `EmitEventAsync_FallsBackToScript_WhenNoPusher` | 不注入 pusher 时降级到 `ExecuteScriptAsync` |
| `ChannelTests` | `WriteAsync_UsesEventPusher_WhenInjected` | 通道 `WriteAsync` 走 pusher |
| `ChannelTests` | `CloseAsync_UsesEventPusher_WhenInjected` | 通道 `CloseAsync` 走 pusher |

### 6.2 构建验证

```powershell
# 1. 构建 SDK + 宿主
.\build.ps1 --build=bin

# 2. 构建所有插件（含新 WebTemplate）
.\build.ps1 --build=plugin

# 3. 验证 wwwroot/ 已复制到 publish 目录
Test-Path bin/plugins/LYBox.Plugin.WebTemplate/publish/wwwroot/index.html

# 4. 启动宿主，手动验证
dotnet run --project src/launcher/LYBox.Launcher.Desktop
```

### 6.3 集成验证（Windows 手动）

1. 启动宿主，打开 WebTemplate 插件页
2. WebView2 DevTools（右键检查）查看：
   - Network：`index.html` 200 OK，`sse/{pluginId}` 长连接 pending
   - Console：`__lybox 运行时就绪` + `SSE 状态: 可用`
3. 点击 "Call C# Ping()"，确认返回 "pong from C#"
4. 点击 "Call C# Add(2, 3)"，确认返回 5
5. 点击 "订阅 userUpdated 事件"，然后 C# 侧触发事件，确认前端收到
6. 点击 "打开通道"，确认每秒收到递增数字，10 秒后通道关闭
7. 关闭主窗口，确认日志显示 Kestrel 停止

---

## 7. 假设与决策（含 P3-P7 新增）

| 决策项 | 选择 | 理由 |
|--------|------|------|
| `WebPluginBindings.Register` 时机 | **WebPluginView.InitializeAsync 中** | 每个 WebView 需独立 `IRpcTransport`，`WebViewIpcHost` 在控件创建时才构造 |
| `WebViewIpcTransport.ExecuteScriptAsync` | **同步包装为 Task** | `Avalonia.Controls.WebView.InvokeScript` 是同步签名返回 `string?` |
| `WebPluginView` 获取 `WebHostService` | **ServiceLocator.TryGetService** | 控件在插件 ALC 中，无法直接 DI 注入；ServiceLocator 是项目既有模式 |
| `wwwroot` 复制到输出目录 | **`<None CopyToOutputDirectory>` + build.cs 双保险** | 开发模式（`dotnet build`）用 MSBuild，发布模式用 build.cs |
| 示例插件前端 | **Vue 3 CDN 引入** | 减少工具链复杂度，聚焦验证 IPC+SSE 闭环 |
| `[RpcCommand]` 源生成器 | **P6 实现时确认状态** | 若 `LYBox.Plugin.Generators` 尚未实现 `[RpcCommand]`，P6 降级为手动 `RegisterCommand` |

---

## 8. 风险与缓解（P3-P7 新增）

| 风险 | 等级 | 缓解 |
|------|------|------|
| `Avalonia.Controls.WebView.InvokeScript` 签名与预期不符（async vs sync） | 中 | P3 实现时验证；若为 async，`WebViewIpcTransport` 直接 await |
| `WebMessageReceivedEventArgs.Body` 可能为 null | 低 | `IRpcTransport.MessageReceived` 已声明 `string?`，`WebViewIpcHost.OnMessage` 已处理 null |
| `[RpcCommand]` 源生成器未实现 | 中 | P6 降级为 `host.RegisterCommand("LYBox.Plugin.WebTemplate.Api.Ping", ...)` 手动注册 |
| `WebPluginView` 在插件 ALC 中无法解析 `WebHostService` | 中 | `WebHostService` 在 `LYBox.Plugin.Shared`（共享程序集），由宿主默认 ALC 提供，插件 ALC 转发解析 |
| Vue CDN 在离线环境不可用 | 低 | 示例插件仅用于验证；实际项目可换本地 Vue 或 Vite 构建 |

---

## 9. 执行顺序

```
P3 (WebPluginView) ──▶ P4 (插件系统集成) ──▶ P5 (构建系统) ──▶ P6 (示例插件) ──▶ P7 (测试验证)
```

每个阶段完成后：
1. 更新 TodoWrite 标记完成
2. 运行 `dotnet build Core.slnx` 确认 0 错误（P3-P4）
3. P5-P6 完成后运行 `.\build.ps1 --build=all` 全量构建
4. P7 执行完整验证清单

---

## 10. 备注

- 本方案严格遵循 AGENTS.md 中的"插件系统前提约束"：不支持热加载/卸载，所有 Web 插件在应用启动时一次性加载
- `WebHostService` 随 `ServiceProvider.Dispose()` 自动停止，符合"应用退出需优雅关闭"约束
- `IWebPlugin` 继承 `IPlugin`，不破坏现有插件契约
- SSE 端点路由 `/sse/{pluginId}` 与静态文件路由 `/{pluginId}/{**path}` 不冲突（SSE 是固定前缀）
- 本方案不引入 `Avalonia-Fluent-UI` NuGet 包，符合 UI 规范
- 所有 F# 脚本（如验证脚本）使用 `dotnet fsi script.fsx` 运行，禁止 Python
