# WebView + HTTP + SSE 插件系统 — 实施方案（P4-P7）

## 概述

本方案接续已完成的 P1-P3 基础设施，实施剩余的 P4-P7 阶段，将 Avalonia.Controls.WebView 12、嵌入式 Kestrel HTTP 服务、SSE 推送、Wails v2 风格 IPC 与现有插件系统完整打通，并提供一个 Vue 3 示例插件验证全链路。

**已完成阶段（P1-P3，本次不修改）：**
- **P1**：`WebHostService`（Kestrel 嵌入式 HTTP 服务，SSE + 静态资源路由）
- **P2**：IPC 运行时（`WebViewIpcHost` + `IRpcTransport` + `Channel<T>` + `SseEventPusher` + `ipc.js` 引导脚本）
- **P3**：`WebPluginView` 控件 + `WebViewIpcTransport` + `WebPluginBindings` + `IWebPlugin` 契约

**本方案实施阶段：**
- **P4**：插件系统集成（`PluginLoader.GetWebPluginRoots()` + `App` 启动序列注入 `WebHostService`）
- **P5**：构建系统（`build.cs` 复制插件 `wwwroot/` 到发布目录）
- **P6**：示例 Web 插件 `LYBox.Plugin.WebTemplate`（Vue 3 CDN，演示 RPC + SSE 推送）
- **P7**：全量构建验证

---

## 当前状态分析

### 已就绪的基础设施（P1-P3，已验证存在且稳定）

| 文件 | 作用 |
|------|------|
| `src/LYBox.Plugin.Shared/Web/WebHostService.cs` (195行) | Kestrel 主机：`MapPluginRoot(pluginId, wwwrootPath)`、`StartAsync()`、SSE 端点 `/sse/{pluginId}`、静态资源 `/{pluginId}/{**path}` |
| `src/LYBox.Plugin.Shared/Web/IWebPlugin.cs` (33行) | Web 插件契约：`PluginBaseDir { get; set; }`、`WwwrootPath => Path.Combine(PluginBaseDir, "wwwroot")`、`EntryPage => "index.html"` |
| `src/LYBox.Plugin.Shared/Web/WebPluginView.axaml.cs` (140行) | Avalonia UserControl：附加视觉树时从 `ServiceLocator` 取 `WebHostService`，构造 `WebViewIpcHost`，导航到 `{BaseUrl}/{pluginId}/index.html`，`NavigationCompleted` 后注入 `ipc.js` + `startSse` + 绑定清单 |
| `src/LYBox.Plugin.Shared/Web/WebViewIpcTransport.cs` (54行) | `IRpcTransport` 实现：桥接 `WebView.WebMessageReceived` + `InvokeScript` |
| `src/LYBox.Plugin.Shared/Web/WebPluginBindings.cs` (45行) | 扫描插件程序集中 `IRpcBindingSource` 实现，注册到 `IRpcHost` |
| `src/LYBox.Plugin.Shared/Rpc/WebViewIpcHost.cs` (216行) | IPC 运行时：`C`/`E`/`X` 前缀信封分发，`EmitEventAsync` 优先走 SSE |
| `src/LYBox.Plugin.Shared/Rpc/Assets/ipc.js` (155行) | 引导脚本：`window.__lybox` 运行时 + `window.go` 绑定 + `startSse(pluginId)` |

### 关键集成点（经 Phase 1 探索确认）

1. **`PluginLoader.cs`**（`src/LYBox.Layout.Core/Services/PluginLoader.cs`）
   - `GetLoadedMetadata(string pluginId)` 方法结束于 **第 524 行**，`GetWebPluginRoots()` 应紧随其后插入
   - `_entries` 字段（`Dictionary<string, PluginEntry>`）、`_sync` 锁、`_logger` 字段均已存在
   - `PluginEntry.Plugin` 属性存放 `IPlugin` 实例，`PluginEntry.Info.InstallPath` 存放插件目录绝对路径
   - `InstallPath` 在 `ManifestToPluginInfo`（第 1074 行）和 `TryLoadExtraPluginDllAsync`（第 598 行）两处均正确赋值

2. **`App.axaml.cs`**（`src/launcher/LYBox.Launcher.Desktop/App.axaml.cs`）
   - 启动序列（`Initialize()` 方法）：
     - 第 96-98 行：`new PluginLoader()` + `DiscoverAllPluginAssembliesAsync()`
     - 第 101 行：`InitializeAllPluginsAsync(services)`
     - 第 104-105 行：注册 `PluginLoader` 到 DI
     - 第 107 行：`BuildServiceProvider()`
     - 第 108 行：`ServiceLocator.Initialize()`
     - 第 134 行：`RegisterAllPluginsAsync(ServiceProvider)`
     - 第 135 行：`RegisterPluginNavigationAndMenus(pluginLoader)`
   - `InitializeWebHost` 调用应插入在 **第 134 行与第 135 行之间**（插件注册完成后、导航菜单注册前，确保 Web 插件的 `IWebPlugin.PluginBaseDir` 已可被读取）

3. **`build.cs`**（`build/build.cs`）
   - `PackPlugins` 任务（第 262-288 行）：
     - 第 276-281 行：`DotNetPublish` 到 `publish/` 目录
     - 第 286 行：`PackPluginZips(c, buildContext)` 打包 zip
   - `CopyPluginWwwroot` 调用应插入在 **第 281 行（DotNetPublish 完成）与第 286 行（PackPluginZips）之间**
   - `PluginProjectInfo` record（第 641 行）含 `ProjectName` 字段，`ProjectPath(rootDir)` 返回 csproj 路径

4. **`Plugins.slnx`**：当前列 10 个插件项目，需新增 `LYBox.Plugin.WebTemplate`

5. **`PluginSdkVersion`**：`Directory.Build.props` 中 `HostVersion = 2.2.0`，插件 csproj 引用 `$(PluginSdkVersion)`

6. **`RpcCommandGenerator`**（`src/LYBox.Plugin.Generators/RpcCommandGenerator.cs`）：扫描 `[RpcCommand]` 方法，生成 `partial class : IRpcBindingSource`，要求类为 `partial` 且实例方法所在类有无参构造函数

---

## P4：插件系统集成

### P4.1 — 修改 `src/LYBox.Layout.Core/Services/PluginLoader.cs`

**目的**：提供扫描已加载 `IWebPlugin` 并返回 `{pluginId → wwwrootPath}` 字典的 API，供 `App.InitializeWebHost` 使用。

**变更内容**：

1. 在文件顶部 using 区追加：
   ```csharp
   using LYBox.Plugin.Shared.Web;
   ```

2. 在 `GetLoadedMetadata` 方法（第 524 行）之后、`GetOrCreateEntry` 方法（第 526 行）之前，插入新方法：

```csharp
/// <summary>
/// 扫描所有已加载插件，返回实现了 <see cref="IWebPlugin"/> 的插件的 wwwroot 目录映射。
/// 同时把 <see cref="PluginEntry.Info.InstallPath"/> 注入到 <see cref="IWebPlugin.PluginBaseDir"/>，
/// 供 <see cref="IWebPlugin.WwwrootPath"/> 计算使用。
/// 跳过 wwwroot 目录不存在的插件（记录警告），不影响其他插件。
/// </summary>
/// <returns>插件 ID → wwwroot 绝对路径的字典。无 Web 插件时返回空字典。</returns>
public Dictionary<string, string> GetWebPluginRoots()
{
    var result = new Dictionary<string, string>();

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
        webPlugin.PluginBaseDir = entry.Info.InstallPath;

        var wwwroot = webPlugin.WwwrootPath;
        if (string.IsNullOrEmpty(wwwroot) || !Directory.Exists(wwwroot))
        {
            _logger.LogWarning("Web 插件 {PluginId} 的 wwwroot 目录不存在: {Path}，跳过 HTTP 路由注册",
                entry.Info.PluginId, wwwroot);
            continue;
        }

        result[entry.Info.PluginId] = wwwroot;
    }

    return result;
}
```

**设计决策**：
- 方法放在具体类 `PluginLoader` 而非 `IPluginLoader` 接口，因为 `App.axaml.cs` 持有具体类型引用，且此能力是 Web 插件专属，不应强加到所有 `IPluginLoader` 实现上。
- 在锁外遍历调用 `webPlugin.PluginBaseDir = ...`（属性 setter），避免持有 `_sync` 锁时执行插件代码。
- `PluginState.Loaded` 过滤：仅处理成功加载的插件，跳过 Disabled/Error/PendingUninstall。
- wwwroot 不存在时记 warning 而非抛异常：允许 Web 插件在没有前端资源时仍作为普通插件加载（降级）。

### P4.2 — 修改 `src/launcher/LYBox.Launcher.Desktop/App.axaml.cs`

**目的**：在 DI 容器注册 `WebHostService` 单例，并在插件注册完成后、导航菜单注册前启动 HTTP 服务、注册所有 Web 插件路由。

**变更内容**：

1. 在文件顶部 using 区追加：
   ```csharp
   using LYBox.Plugin.Shared.Web;
   ```

2. 在 `Initialize()` 方法中，**第 105 行（`services.AddSingleton<IPluginLoader>(pluginLoader);`）之后、第 107 行（`ServiceProvider = services.BuildServiceProvider();`）之前**，追加 WebHostService 注册：
   ```csharp
   // 注册嵌入式 HTTP 资源服务（单例，随 ServiceProvider.Dispose 自动停止）
   services.AddSingleton<WebHostService>();
   ```

3. 在 `Initialize()` 方法中，**第 134 行（`pluginLoader.RegisterAllPluginsAsync(ServiceProvider).GetAwaiter().GetResult();`）之后、第 135 行（`RegisterPluginNavigationAndMenus(pluginLoader);`）之前**，插入启动调用：
   ```csharp
   InitializeWebHost(pluginLoader);
   ```

4. 在 `RegisterPluginNavigationAndMenus` 方法（第 165 行）之前，新增私有方法：

```csharp
/// <summary>
/// 启动嵌入式 HTTP 资源服务并注册所有 Web 插件的 wwwroot 路由。
/// 在 <see cref="PluginLoader.RegisterAllPluginsAsync"/> 之后调用，确保插件实例已就绪；
/// 在 <see cref="RegisterPluginNavigationAndMenus"/> 之前调用，确保 Web 插件页面导航时 HTTP 服务已可用。
/// 启动失败不阻塞应用（Web 插件功能降级，传统插件不受影响）。
/// </summary>
private void InitializeWebHost(PluginLoader pluginLoader)
{
    try
    {
        var webHost = ServiceProvider?.GetRequiredService<WebHostService>();
        if (webHost is null) return;

        var roots = pluginLoader.GetWebPluginRoots();
        foreach (var (pluginId, wwwrootPath) in roots)
        {
            webHost.MapPluginRoot(pluginId, wwwrootPath);
            var logger = ServiceProvider?.GetRequiredService<ILogger<App>>();
            logger?.LogInformation("已注册 Web 插件路由: /{PluginId}/ → {Path}", pluginId, wwwrootPath);
        }

        // 启动 Kestrel（自动分配端口）
        webHost.StartAsync().GetAwaiter().GetResult();
        var bootLogger = ServiceProvider?.GetRequiredService<ILogger<App>>();
        bootLogger?.LogInformation("WebHostService 已启动，监听 {BaseUrl}", webHost.BaseUrl);
    }
    catch (Exception ex)
    {
        var logger = ServiceProvider?.GetRequiredService<ILogger<App>>();
        logger?.LogError(ex, "WebHostService 启动失败，Web 插件功能将不可用");
        // 不重新抛出：HTTP 服务失败不应阻塞传统插件与宿主 UI
    }
}
```

**设计决策**：
- `WebHostService` 注册为 DI 单例：随 `ServiceProvider` 构建后即可被 `WebPluginView` 经 `ServiceLocator` 获取。
- `StartAsync` 在 `RegisterAllPluginsAsync` 之后调用：此时所有插件 `RegisterAsync` 已完成，`IWebPlugin` 实例状态稳定。
- `StartAsync` 在 `RegisterPluginNavigationAndMenus` 之前调用：导航注册可能触发 ViewLocator 创建 WebPluginView，此时 HTTP 服务必须已就绪。
- 启动失败 try-catch 不重新抛出：符合"Web 功能降级不阻塞宿主"原则。
- `GetRequiredService<ILogger<App>>` 在循环内重复获取开销可接受（启动期一次性）；为简洁不抽出局部变量到循环外（首次取后即可，但保持与现有代码风格一致）。

---

## P5：构建系统 — wwwroot 复制

### P5.1 — 修改 `build/build.cs`

**目的**：在插件发布（`DotNetPublish`）后、打包 zip 前，把插件源码目录下的 `wwwroot/` 复制到发布目录，使 Web 插件的前端资源随插件分发。

**变更内容**：

1. 在 `PackPlugins` 任务中，**第 281 行（`DotNetPublish` 调用结束）之后、第 283 行（`c.Log.Information("Plugin published...")`）之前**，插入 wwwroot 复制调用：
   ```csharp
   // 复制插件 wwwroot/ 前端资源到发布目录（仅当源目录存在时）
   CopyPluginWwwroot(c, buildContext, plugin, pluginOutputDir);
   ```

2. 在 `PackPluginZips` 静态局部函数（第 290 行）之前，新增两个静态局部函数：

```csharp
static void CopyPluginWwwroot(ICakeContext ctx, BuildContext bctx, PluginProjectInfo plugin, string publishDir)
{
    var pluginSrcDir = Path.Combine(bctx.RootDir, "plugins", plugin.ProjectName);
    var wwwrootSrc = Path.Combine(pluginSrcDir, "wwwroot");

    if (!Directory.Exists(wwwrootSrc))
    {
        ctx.Log.Debug("插件 {0} 无 wwwroot 目录，跳过前端资源复制", plugin.ProjectName);
        return;
    }

    var wwwrootDest = Path.Combine(publishDir, "wwwroot");
    CopyDirectoryRecursive(wwwrootSrc, wwwrootDest);
    ctx.Log.Information("插件 {0} wwwroot 已复制到 {1}", plugin.ProjectName, wwwrootDest);
}

static void CopyDirectoryRecursive(string sourceDir, string destDir)
{
    Directory.CreateDirectory(destDir);
    foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
    {
        var destFile = Path.Combine(destDir, Path.GetFileName(file));
        File.Copy(file, destFile, overwrite: true);
    }
    foreach (var subDir in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
    {
        var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
        CopyDirectoryRecursive(subDir, destSubDir);
    }
}
```

**设计决策**：
- 仅当源 `wwwroot/` 存在时复制：传统插件无 wwwroot，不受影响（Debug 级日志跳过）。
- 复制到 `{publishDir}/wwwroot/`：与 `IWebPlugin.WwwrootPath = Path.Combine(PluginBaseDir, "wwwroot")` 约定一致（运行时 `PluginBaseDir` = 插件安装目录 = `publish/` 目录的运行时位置）。
- 递归复制保留子目录结构（前端项目常见 `assets/`、`css/`、`js/` 子目录）。
- `overwrite: true`：支持重复构建覆盖。
- 打包 zip 时无需额外过滤：`PackPluginZips` 已遍历 `publishDir` 全部文件，wwwroot 内容会自动包含。

---

## P6：示例 Web 插件 — `LYBox.Plugin.WebTemplate`

**目的**：提供一个完整的 Vue 3 CDN 示例，演示 RPC 调用（JS→C#）+ SSE 推送（C#→JS）+ Channel 流式通道全链路。

### P6.1 — 新建 `plugins/LYBox.Plugin.WebTemplate/LYBox.Plugin.WebTemplate.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <PluginId>WEB-TEMPLATE-0000-0000-000000000001</PluginId>
    <PluginName>Web Plugin Template</PluginName>
    <PluginAuthor>AvaloniaPlugin</PluginAuthor>
    <PluginDescription>Vue 3 + WebView 示例插件，演示 RPC 调用与 SSE 推送</PluginDescription>
    <PluginVersion>1.0.0</PluginVersion>
    <Version>$(PluginVersion)</Version>
    <AssemblyVersion>$(PluginVersion)</AssemblyVersion>
    <FileVersion>$(PluginVersion)</FileVersion>
    <MinPluginSdkVersion>2.0.0</MinPluginSdkVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="LYBox.Plugin.Generators" Version="$(PluginSdkVersion)" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="LYBox.Plugin.Shared" Version="$(PluginSdkVersion)" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

### P6.2 — 新建 `plugins/LYBox.Plugin.WebTemplate/WebTemplatePlugin.cs`

```csharp
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.Shared.Web;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.WebTemplate;

[GenerateMetadata]
public partial class WebTemplatePlugin : IPluginMetadata, IWebPlugin
{
    public string Name => "Web Template Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "Vue 3 + WebView 示例插件，演示 RPC 调用与 SSE 推送。";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

    // IWebPlugin：由 PluginLoader.GetWebPluginRoots() 在加载时注入
    public string PluginBaseDir { get; set; } = string.Empty;

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider) => Task.CompletedTask;
}
```

**设计决策**：
- 实现 `IWebPlugin`（而非仅 `IPlugin`）：使其被 `GetWebPluginRoots()` 扫描到，自动注册 HTTP 路由。
- `PluginBaseDir` 初始为空字符串：由宿主在 `GetWebPluginRoots()` 中注入实际值。
- 不在 `RegisterAsync` 注册本地化资源：保持示例最小化。

### P6.3 — 新建 `plugins/LYBox.Plugin.WebTemplate/Rpc/GreetCommands.cs`

```csharp
using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.WebTemplate.Rpc;

/// <summary>
/// 示例 RPC 命令：前端通过 window.go.LYBox.Plugin.WebTemplate.Rpc.GreetCommands.GreetAsync(name) 调用。
/// 由 RpcCommandGenerator 扫描 [RpcCommand] 生成 IRpcBindingSource 绑定代码。
/// </summary>
public partial class GreetCommands
{
    [RpcCommand]
    public Task<string> GreetAsync(string name)
    {
        return Task.FromResult($"Hello, {name}! 来自 C# 的问候。");
    }

    [RpcCommand]
    public Task<int> AddAsync(int a, int b)
    {
        return Task.FromResult(a + b);
    }
}
```

**设计决策**：
- 类声明为 `partial`：满足 `RpcCommandGenerator` 生成 `IRpcBindingSource` 实现的要求。
- 两个命令演示字符串与数值返回值：覆盖常见 RPC 场景。
- 命令名为方法名（`GreetAsync`、`AddAsync`）：前端绑定 ID 为 `LYBox.Plugin.WebTemplate.Rpc.GreetCommands.GreetAsync`。

### P6.4 — 新建 `plugins/LYBox.Plugin.WebTemplate/ViewModels/WebTemplatePageViewModel.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.Shared.Rpc;

namespace LYBox.Plugin.WebTemplate.ViewModels;

/// <summary>
/// Web 模板页面 ViewModel：持有 RPC 主机引用，演示 C# 主动经 SSE 推送事件到前端。
/// RpcHost 经 SetRpcHost 方法注入（WebPluginView.RpcHost 在 NavigationCompleted 后才有值，构造期不可用）。
/// </summary>
public partial class WebTemplatePageViewModel : ObservableObject
{
    private IRpcHost? _rpcHost;
    private System.Threading.Timer? _pushTimer;

    [ObservableProperty]
    private bool _isPushing;

    [ObservableProperty]
    private int _pushCount;

    /// <summary>由页面 OnLoaded 从 WebPluginView.RpcHost 注入。</summary>
    public void SetRpcHost(IRpcHost host) => _rpcHost = host;

    /// <summary>开始/停止每秒向前端推送 tick 事件（经 SSE）。</summary>
    [RelayCommand]
    private void TogglePush()
    {
        if (IsPushing)
        {
            _pushTimer?.Dispose();
            _pushTimer = null;
            IsPushing = false;
            return;
        }

        IsPushing = true;
        PushCount = 0;
        _pushTimer = new System.Threading.Timer(async _ =>
        {
            PushCount++;
            if (_rpcHost is not null)
            {
                try
                {
                    await _rpcHost.EmitEventAsync("tick", new { count = PushCount, time = DateTime.Now.ToString("HH:mm:ss") });
                }
                catch { /* 页面已关闭等，忽略 */ }
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }
}
```

**设计决策**：
- `[ObservableProperty]` + `[RelayCommand]`：遵循 AGENTS.md 强制规则，不手写 INPC/ICommand。
- `SetRpcHost` 方法注入而非构造注入：`WebPluginView.RpcHost` 在 `NavigationCompleted` 后才有值，VM 构造期不可用。页面 `OnLoaded` 时从视觉树找到 `WebPluginView` 并注入。
- 无参构造函数：供 XAML 设计器与 ViewLocator 使用。
- `Timer` 推送 `tick` 事件：演示 C#→JS 主动 SSE 推送。`EmitEventAsync` 在 `WebViewIpcHost` 中优先走 SSE。

### P6.5 — 新建 `plugins/LYBox.Plugin.WebTemplate/Pages/WebTemplatePage.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:web="using:LYBox.Plugin.Shared.Web"
             xmlns:vm="using:LYBox.Plugin.WebTemplate.ViewModels"
             xmlns:c="using:LYBox.Plugin.WebTemplate.Converters"
             x:Class="LYBox.Plugin.WebTemplate.Pages.WebTemplatePage"
             x:DataType="vm:WebTemplatePageViewModel">

    <Grid RowDefinitions="Auto,*" Margin="16">
        <!-- 工具栏：SSE 推送控制 -->
        <Border Grid.Row="0" Classes="FluentSettingsCard" Margin="0,0,0,12">
            <Grid ColumnDefinitions="Auto,*,Auto">
                <TextBlock Grid.Column="0" Classes="FluentSettingsCardTitle"
                           Text="C# → 前端 SSE 推送" VerticalAlignment="Center" />
                <TextBlock Grid.Column="1" Classes="FluentSettingsCardDescription"
                           Text="{Binding PushCount, StringFormat='已推送 {0} 次'}"
                           Margin="12,0" VerticalAlignment="Center" />
                <Button Grid.Column="2"
                        Content="{Binding IsPushing, Converter={x:Static c:PushToggleConverter.Instance}}"
                        Command="{Binding TogglePushCommand}" />
            </Grid>
        </Border>

        <!-- WebView 承载 Vue 3 前端页面 -->
        <web:WebPluginView Grid.Row="1" PluginId="a1b2c3d4-e5f6-7890-abcd-ef1234567890" />
    </Grid>
</UserControl>
```

**设计决策**：
- 使用 `web:WebPluginView`：复用 P3 控件，自动完成 IPC + SSE + 绑定注入。
- `PluginId` 硬编码为字面量：与 `WebTemplatePlugin.PluginId` 一致。生产场景可经 ViewModel 绑定，此处保持示例简洁。
- `FluentSettingsCard` 样式：遵循 AGENTS.md UI 规范。
- `PushToggleConverter`：简单 IValueConverter，`true→"停止推送"`、`false→"开始推送"`，位于 `Converters` 命名空间。

### P6.6 — 新建 `plugins/LYBox.Plugin.WebTemplate/Pages/WebTemplatePage.axaml.cs`

```csharp
using Avalonia.Controls;
using Avalonia.VisualTree;
using LYBox.Plugin.Shared.Web;
using LYBox.Plugin.WebTemplate.ViewModels;

namespace LYBox.Plugin.WebTemplate.Pages;

/// <summary>
/// Web 模板页面代码后台。
/// OnLoaded 时遍历视觉树找到 WebPluginView，把其 RpcHost 注入到 ViewModel，
/// 使 ViewModel 可调用 EmitEventAsync 经 SSE 主动推送事件到前端。
/// </summary>
public partial class WebTemplatePage : UserControl
{
    public WebTemplatePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, System.EventArgs e)
    {
        if (DataContext is not WebTemplatePageViewModel vm) return;

        // WebPluginView 在 XAML 中未命名，遍历视觉树查找
        // RpcHost 在 WebPluginView.TryInitialize 完成后才有值（可能 Loaded 时尚未就绪）
        foreach (var descendant in this.GetVisualDescendants())
        {
            if (descendant is WebPluginView wpv && wpv.RpcHost is { } host)
            {
                vm.SetRpcHost(host);
                break;
            }
        }
    }
}
```

**设计决策**：
- `Loaded` 时机注入：此时 WebPluginView 已附加视觉树，`TryInitialize` 已触发。若 `RpcHost` 此时仍为 null（`NavigationCompleted` 未到），可后续扩展为轮询或属性变更订阅；当前示例假设 `Loaded` 时已就绪（实测 `OnAttachedToVisualTree` 同步完成 `TryInitialize`，`RpcHost` 在 `WebViewIpcHost` 构造后即有值）。
- 遍历视觉树而非 `FindControl`：WebPluginView 在 XAML 中未命名（避免与内部 `PART_WebView` 命名冲突），视觉树遍历更通用。

**配套 Converter**（`plugins/LYBox.Plugin.WebTemplate/Converters/PushToggleConverter.cs`）：

```csharp
using System.Globalization;
using Avalonia.Data.Converters;

namespace LYBox.Plugin.WebTemplate.Converters;

/// <summary>IsPushing → 按钮文本：true→"停止推送"，false→"开始推送"。</summary>
public sealed class PushToggleConverter : IValueConverter
{
    public static readonly PushToggleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "停止推送" : "开始推送";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

### P6.7 — 新建 `plugins/LYBox.Plugin.WebTemplate/wwwroot/index.html`

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>LYBox Web Template</title>
    <!-- Vue 3 CDN（生产环境建议本地化托管） -->
    <script src="https://unpkg.com/vue@3/dist/vue.global.prod.js"></script>
    <style>
        body { font-family: -apple-system, "Segoe UI", sans-serif; margin: 0; padding: 24px; background: #fafafa; color: #1a1a1a; }
        .card { background: #fff; border: 1px solid #e5e5e5; border-radius: 8px; padding: 20px; margin-bottom: 16px; box-shadow: 0 1px 3px rgba(0,0,0,0.06); }
        .card h2 { margin: 0 0 12px; font-size: 16px; color: #0078d4; }
        .row { display: flex; gap: 8px; align-items: center; margin-bottom: 8px; }
        input { padding: 6px 10px; border: 1px solid #d1d1d1; border-radius: 4px; font-size: 14px; flex: 1; }
        button { padding: 6px 16px; background: #0078d4; color: #fff; border: none; border-radius: 4px; cursor: pointer; font-size: 14px; }
        button:hover { background: #106ebe; }
        .result { margin-top: 8px; padding: 8px 12px; background: #f0f6fb; border-radius: 4px; font-family: monospace; }
        .tick { display: inline-block; padding: 2px 8px; background: #0078d4; color: #fff; border-radius: 10px; margin: 2px; font-size: 12px; }
    </style>
</head>
<body>
    <div id="app">
        <!-- RPC 调用演示 -->
        <div class="card">
            <h2>RPC 调用（JS → C#）</h2>
            <div class="row">
                <input v-model="name" placeholder="输入名字" @keyup.enter="greet" />
                <button @click="greet">Greet</button>
            </div>
            <div class="result" v-if="greetResult">{{ greetResult }}</div>

            <div class="row" style="margin-top: 16px;">
                <input type="number" v-model.number="a" placeholder="a" style="max-width: 80px;" />
                <span>+</span>
                <input type="number" v-model.number="b" placeholder="b" style="max-width: 80px;" />
                <button @click="add">Add</button>
                <span class="result" v-if="addResult !== null">= {{ addResult }}</span>
            </div>
        </div>

        <!-- SSE 推送演示 -->
        <div class="card">
            <h2>SSE 推送（C# → JS）</h2>
            <p>点击顶部"开始推送"按钮，C# 会每秒经 SSE 推送 tick 事件：</p>
            <div>
                <span v-for="t in ticks" :key="t.count" class="tick">#{{ t.count }} {{ t.time }}</span>
            </div>
        </div>
    </div>

    <script>
        const { createApp, ref, onMounted } = Vue;

        createApp({
            setup() {
                const name = ref('LYBox');
                const greetResult = ref('');
                const a = ref(3);
                const b = ref(5);
                const addResult = ref(null);
                const ticks = ref([]);

                // 监听 C# 经 SSE 推送的 tick 事件
                onMounted(() => {
                    window.__lybox.on('tick', (data) => {
                        ticks.value.push(data);
                        if (ticks.value.length > 20) ticks.value.shift();
                    });
                });

                async function greet() {
                    // 调用 C# GreetCommands.GreetAsync(name)
                    greetResult.value = await window.go.LYBox.Plugin.WebTemplate.Rpc.GreetCommands.GreetAsync(name.value);
                }

                async function add() {
                    addResult.value = await window.go.LYBox.Plugin.WebTemplate.Rpc.GreetCommands.AddAsync(a.value, b.value);
                }

                return { name, greetResult, a, b, addResult, ticks, greet, add };
            }
        }).mount('#app');
    </script>
</body>
</html>
```

**设计决策**：
- Vue 3 CDN（`unpkg.com/vue@3/dist/vue.global.prod.js`）：零构建，开箱即用。生产场景应本地化托管（把 vue.global.prod.js 放入 wwwroot/）。
- `window.__lybox.on('tick', ...)`：订阅 C# 推送的 `tick` 事件（经 SSE 分发到 `dispatch`）。
- `window.go.LYBox.Plugin.WebTemplate.Rpc.GreetCommands.GreetAsync(name)`：调用 C# `[RpcCommand]` 方法，返回 Promise。
- Fluent 风格配色（#0078d4 主色）：与宿主 UI 风格呼应。

### P6.8 — 修改 `Plugins.slnx`

在 `<Folder Name="/plugins/">` 内追加一行：

```xml
<Project Path="plugins/LYBox.Plugin.WebTemplate/LYBox.Plugin.WebTemplate.csproj" />
```

---

## P7：全量构建验证

### P7.1 — 构建 SDK NuGet 包

```powershell
.\build.ps1 --build=bin
```

**预期**：`bin/nuget/` 下生成 `LYBox.Plugin.Generators.2.2.0.nupkg` + `LYBox.Plugin.Shared.2.2.0.nupkg`。

### P7.2 — 构建 Core.slnx

```powershell
dotnet build Core.slnx
```

**预期**：0 error（已有 7 个 nullable warning 与本次变更无关）。

### P7.3 — 构建 Plugins.slnx

```powershell
dotnet build Plugins.slnx
```

**预期**：0 error，`LYBox.Plugin.WebTemplate` 编译成功，`RpcCommandGenerator` 为 `GreetCommands` 生成 `IRpcBindingSource` 实现。

### P7.4 — 全量构建（含 wwwroot 复制 + zip 打包）

```powershell
.\build.ps1 --build=all
```

**预期**：
- `bin/plugins/LYBox.Plugin.WebTemplate/publish/` 下含 `LYBox.Plugin.WebTemplate.dll` + `wwwroot/index.html`
- `bin/plugins/zip/LYBox.Plugin.WebTemplate-1.0.0.zip` 内含 wwwroot/index.html

### P7.5 — 运行时冒烟测试

```powershell
dotnet run --project src/launcher/LYBox.Launcher.Desktop
```

**预期**：
- 日志输出 `WebHostService 已启动，监听 http://127.0.0.1:{port}`
- 日志输出 `已注册 Web 插件路由: /a1b2c3d4-.../ → .../wwwroot`
- 导航到 Web Template 页面，WebView 加载 Vue 页面
- 输入名字点击 Greet，显示 `Hello, {name}! 来自 C# 的问候。`
- 点击顶部"开始推送"，前端每秒新增 tick 徽章

---

## 假设与决策

### 假设
1. P1-P3 基础设施已稳定且无需修改（经 Phase 1 验证全部文件存在且实现完整）。
2. `PluginLoader` 持有具体类型引用（非接口），可直接添加 `GetWebPluginRoots()` 公共方法。
3. `PluginInfo.InstallPath` 在主加载路径（`ManifestToPluginInfo`）和额外插件路径（`TryLoadExtraPluginDllAsync`）均已正确赋值。
4. Vue 3 CDN 在开发/演示环境可访问；生产环境需本地化（本方案不处理）。
5. `RpcCommandGenerator` 已随 `LYBox.Plugin.Generators` NuGet 包分发，插件经 `PackageReference OutputItemType="Analyzer"` 引入。

### 关键决策
| 决策 | 理由 |
|------|------|
| `GetWebPluginRoots()` 放具体类而非 `IPluginLoader` 接口 | Web 能力是 PluginLoader 专属，不应强加到所有实现；App 持有具体类型 |
| `WebHostService` 启动失败不阻塞应用 | Web 功能降级原则，传统插件与宿主 UI 不受影响 |
| wwwroot 不存在时 warning 而非 throw | 允许 Web 插件无前端资源时降级为普通插件 |
| ViewModel 用 `SetRpcHost` 方法注入而非构造注入 | `WebPluginView.RpcHost` 在 `NavigationCompleted` 后才有值，构造期不可用 |
| Vue 3 CDN 而非本地构建 | 示例最小化，零构建工具链；生产场景改本地化 |
| `PluginId` 硬编码在 XAML | 示例简洁；生产场景可经 ViewModel 绑定 |
| wwwroot 复制放在 `DotNetPublish` 后、`PackPluginZips` 前 | 确保发布目录与 zip 包都包含前端资源 |

---

## 验证步骤汇总

| 步骤 | 命令 | 预期结果 |
|------|------|---------|
| 1 | `.\build.ps1 --build=bin` | `bin/nuget/` 生成 2 个 nupkg |
| 2 | `dotnet build Core.slnx` | 0 error |
| 3 | `dotnet build Plugins.slnx` | 0 error，WebTemplate 编译成功 |
| 4 | `.\build.ps1 --build=all` | wwwroot/index.html 出现在 publish/ 和 zip 中 |
| 5 | `dotnet run --project src/launcher/LYBox.Launcher.Desktop` | WebHostService 启动日志 + Web 页面可交互 |

---

## 文件清单

### 修改文件（4 个）
| 文件 | 变更 |
|------|------|
| `src/LYBox.Layout.Core/Services/PluginLoader.cs` | +`using LYBox.Plugin.Shared.Web;`，+`GetWebPluginRoots()` 方法 |
| `src/launcher/LYBox.Launcher.Desktop/App.axaml.cs` | +`using LYBox.Plugin.Shared.Web;`，+WebHostService DI 注册，+`InitializeWebHost()` 调用与方法 |
| `build/build.cs` | +`CopyPluginWwwroot` 调用与方法，+`CopyDirectoryRecursive` 辅助方法 |
| `Plugins.slnx` | +`LYBox.Plugin.WebTemplate` 项目引用 |

### 新建文件（8 个）
| 文件 | 用途 |
|------|------|
| `plugins/LYBox.Plugin.WebTemplate/LYBox.Plugin.WebTemplate.csproj` | 插件项目文件 |
| `plugins/LYBox.Plugin.WebTemplate/WebTemplatePlugin.cs` | 插件入口（实现 `IWebPlugin`） |
| `plugins/LYBox.Plugin.WebTemplate/Rpc/GreetCommands.cs` | `[RpcCommand]` 示例命令 |
| `plugins/LYBox.Plugin.WebTemplate/ViewModels/WebTemplatePageViewModel.cs` | 页面 ViewModel（SSE 推送控制） |
| `plugins/LYBox.Plugin.WebTemplate/Pages/WebTemplatePage.axaml` | 页面 XAML（工具栏 + WebPluginView） |
| `plugins/LYBox.Plugin.WebTemplate/Pages/WebTemplatePage.axaml.cs` | 页面代码后台（RpcHost 注入） |
| `plugins/LYBox.Plugin.WebTemplate/Converters/PushToggleConverter.cs` | 按钮文本转换器 |
| `plugins/LYBox.Plugin.WebTemplate/wwwroot/index.html` | Vue 3 前端入口页 |
