# WebView 插件系统 — 构建修复与 P5-P7 完成

## 概述

本方案修复 P1-P3 基础设施中的预存编译错误（阻塞 P4 验证），并完成剩余的 P5-P7 阶段。P4 代码变更已应用，但因 P3 代码无法编译而无法验证。

**已完成（无需修改）：**
- **P4**：`PluginLoader.GetWebPluginRoots()` + `App.InitializeWebHost()` — 代码已应用并经读取确认

**本方案实施：**
- **P3-fix**：修复 3 个预存编译错误（类型名错误 + 缺失包引用）
- **P5**：构建系统 wwwroot 复制（`build.cs`）
- **P6**：示例 Web 插件 `LYBox.Plugin.WebTemplate`（8 个新文件）
- **P7**：全量构建验证

---

## 当前状态分析

### 预存编译错误（经 Phase 1 探索确认根因）

| 错误 | 根因 | 影响文件 |
|------|------|---------|
| `RpcCommandGenerator.cs(4): CS0234: System.Text.Json 不存在` | Generators 项目目标 `netstandard2.1`，该框架不包含 `System.Text.Json`（需 NuGet 包） | `src/LYBox.Plugin.Generators/LYBox.Plugin.Generators.csproj` |
| `WebViewIpcTransport.cs(24,26): CS0246: WebView 未找到` | P3 代码使用 `WebView` 类型名，但 Avalonia.Controls.WebView 12.0.1 程序集中实际类名为 `NativeWebView` | `WebViewIpcTransport.cs`、`WebPluginView.axaml`、`WebPluginView.axaml.cs` |

**证据链：**
1. `packages/avalonia.controls.webview/12.0.1/lib/net10.0-android36.0/Avalonia.Controls.WebView.xml` 中的类型声明：
   - `T:Avalonia.Controls.NativeWebView`（第 145 行）— 实际控件类
   - `T:Avalonia.Controls.NativeWebDialog`（第 30 行）— 独立窗口对话框
   - **不存在** `T:Avalonia.Controls.WebView` 类型
2. `NativeWebView` 提供的 API 与 P3 代码使用的一致：
   - `E:Avalonia.Controls.NativeWebView.WebMessageReceived`（第 163 行）
   - `P:Avalonia.Controls.NativeWebView.Source`（第 175 行）
   - `M:Avalonia.Controls.NativeWebView.InvokeScript(System.String)`（第 217 行）
3. Generators 项目 `LYBox.Plugin.Generators.csproj` 目标 `netstandard2.1`，仅有 `Microsoft.CodeAnalysis.CSharp` 引用，无 `System.Text.Json` 包引用
4. `RpcCommandGenerator.cs` 第 4 行 `using System.Text.Json;` + 第 107 行 `JsonSerializer.Serialize(manifest)` 需要 `System.Text.Json` 包

### P4 已应用变更（经读取确认，无需修改）

- `PluginLoader.cs` 第 7 行 `using LYBox.Plugin.Shared.Web;` + 第 527-563 行 `GetWebPluginRoots()` 方法 ✅
- `App.axaml.cs` 第 9 行 using + 第 108-109 行 DI 注册 + 第 139 行 `InitializeWebHost(pluginLoader)` 调用 + 第 170-202 行方法 ✅

### 关键集成点（经 Phase 1 探索确认）

1. **`build.cs`** `PackPlugins` 任务（第 262-288 行）：
   - 第 276-281 行：`DotNetPublish` 到 `publish/` 目录
   - 第 283 行：`c.Log.Information("Plugin published...")`
   - 第 286 行：`PackPluginZips(c, buildContext)` 打包 zip
   - `CopyPluginWwwroot` 调用插入点：第 281 行之后、第 283 行之前

2. **`Plugins.slnx`**：当前列 10 个插件项目，需新增 `LYBox.Plugin.WebTemplate`

3. **`Directory.Build.props`**：`HostVersion = 2.2.0`，插件 csproj 引用 `$(PluginSdkVersion)`（= HostVersion 别名）

4. **`System.Text.Json` 版本**：.NET 10 运行时自带 `System.Text.Json`，Generators 项目目标 `netstandard2.1` 需引入 NuGet 包。使用 `9.0.0` 版本（netstandard2.1 兼容的最新稳定版，避免 10.x 可能的 API 差异）

---

## P3-fix：修复预存编译错误

### P3-fix.1 — 修改 `src/LYBox.Plugin.Generators/LYBox.Plugin.Generators.csproj`

**目的**：为 `netstandard2.1` 目标添加 `System.Text.Json` 包引用，使 `RpcCommandGenerator.cs` 中的 `JsonSerializer.Serialize` 可编译。

**变更内容**：在 `<ItemGroup>` 中 `Microsoft.CodeAnalysis.CSharp` 引用之后，追加：

```xml
<PackageReference Include="System.Text.Json" Version="9.0.0" PrivateAssets="all" />
```

**设计决策**：
- 版本 `9.0.0`：netstandard2.1 兼容的最新稳定版。Generators 是源生成器（编译期运行），不参与运行时，版本与宿主 net10.0 的 System.Text.Json 无需严格对齐。
- `PrivateAssets="all"`：源生成器依赖不传递到消费方（插件项目），避免版本污染。

### P3-fix.2 — 修改 `src/LYBox.Plugin.Shared/Web/WebViewIpcTransport.cs`

**目的**：将类型名 `WebView` 修正为 `NativeWebView`（Avalonia.Controls.WebView 12.0.1 的实际控件类名）。

**变更内容**（3 处替换，全部 `WebView` → `NativeWebView`）：

1. 第 24 行：`private readonly WebView _webView;` → `private readonly NativeWebView _webView;`
2. 第 26 行：`public WebViewIpcTransport(WebView webView)` → `public WebViewIpcTransport(NativeWebView webView)`
3. 第 1 行 using 保持不变：`using Avalonia.Controls;`（`NativeWebView` 在此命名空间）

**注意**：`WebMessageReceivedEventArgs`（第 43 行）类型名保持不变。该类型虽无 XML 文档，但 `NativeWebView.WebMessageReceived` 事件使用 `<inheritdoc/>` 继承自基接口，事件参数类型极可能就是 `WebMessageReceivedEventArgs`。若构建后此类型仍报错，将在构建验证阶段查找正确类型名。

### P3-fix.3 — 修改 `src/LYBox.Plugin.Shared/Web/WebPluginView.axaml`

**目的**：XAML 中控件标签名修正。

**变更内容**：

```xml
<!-- 原 -->
<web:WebView x:Name="PART_WebView" />

<!-- 改为 -->
<web:NativeWebView x:Name="PART_WebView" />
```

XML 命名空间声明 `xmlns:web="clr-namespace:Avalonia.Controls;assembly=Avalonia.Controls.WebView"` 保持不变（`NativeWebView` 确实在 `Avalonia.Controls` 命名空间）。

### P3-fix.4 — 修改 `src/LYBox.Plugin.Shared/Web/WebPluginView.axaml.cs`

**目的**：代码后台 `FindControl` 泛型参数修正。

**变更内容**：

第 91 行：`var webView = this.FindControl<WebView>("PART_WebView");` → `var webView = this.FindControl<NativeWebView>("PART_WebView");`

### P3-fix.5 — 验证构建

```powershell
dotnet build Core.slnx
```

**预期**：0 error。`WebMessageReceivedEventArgs` 若仍报错，在此阶段查找正确类型名并修正。

---

## P5：构建系统 — wwwroot 复制

### P5.1 — 修改 `build/build.cs`

**目的**：在插件发布（`DotNetPublish`）后、打包 zip 前，把插件源码目录下的 `wwwroot/` 复制到发布目录。

**变更内容**：

1. 在 `PackPlugins` 任务中，第 281 行（`DotNetPublish` 调用结束）之后、第 283 行（`c.Log.Information("Plugin published...")`）之前，插入：

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
- 仅当源 `wwwroot/` 存在时复制：传统插件无 wwwroot，不受影响。
- 复制到 `{publishDir}/wwwroot/`：与 `IWebPlugin.WwwrootPath = Path.Combine(PluginBaseDir, "wwwroot")` 约定一致。
- 递归复制保留子目录结构（前端 `assets/`、`css/`、`js/` 等）。
- `overwrite: true`：支持重复构建覆盖。

---

## P6：示例 Web 插件 — `LYBox.Plugin.WebTemplate`

**目的**：提供完整的 Vue 3 CDN 示例，演示 RPC 调用（JS→C#）+ SSE 推送（C#→JS）全链路。

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

### P6.7 — 新建 `plugins/LYBox.Plugin.WebTemplate/Converters/PushToggleConverter.cs`

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

### P6.8 — 新建 `plugins/LYBox.Plugin.WebTemplate/wwwroot/index.html`

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

### P6.9 — 修改 `Plugins.slnx`

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

**预期**：0 error（P3-fix 修复后，Generators + Shared + Core 全部编译通过）。

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
1. P1-P3 基础设施文件（`WebHostService.cs`、`WebViewIpcHost.cs`、`ipc.js`、`WebPluginBindings.cs`、`IWebPlugin.cs`）实现正确，仅需修复类型名和包引用。
2. `WebMessageReceivedEventArgs` 是 `NativeWebView.WebMessageReceived` 事件的正确参数类型（无 XML 文档但 API 模式合理）。若 P7.2 构建仍报此类型错误，将检查 `NativeWebView` 基接口的事件签名并修正。
3. P4 代码变更（`GetWebPluginRoots` + `InitializeWebHost`）实现正确，无需修改。
4. Vue 3 CDN 在开发/演示环境可访问；生产环境需本地化（本方案不处理）。
5. `RpcCommandGenerator` 已随 `LYBox.Plugin.Generators` NuGet 包分发，插件经 `PackageReference OutputItemType="Analyzer"` 引入。

### 关键决策
| 决策 | 理由 |
|------|------|
| `WebView` → `NativeWebView` | XML 文档明确 `T:Avalonia.Controls.NativeWebView` 是实际控件类，无 `WebView` 类型 |
| `System.Text.Json` 版本 `9.0.0` | netstandard2.1 兼容；源生成器编译期运行，无需与宿主 net10.0 严格对齐 |
| `WebMessageReceivedEventArgs` 暂不修改 | 无 XML 文档但 API 模式合理；若构建报错再修正，避免过度修改 |
| wwwroot 复制放在 `DotNetPublish` 后、`PackPluginZips` 前 | 确保发布目录与 zip 包都包含前端资源 |
| Vue 3 CDN 而非本地构建 | 示例最小化，零构建工具链；生产场景改本地化 |

---

## 验证步骤汇总

| 步骤 | 命令 | 预期结果 |
|------|------|---------|
| 1 | `dotnet build Core.slnx` | 0 error（P3-fix 验证） |
| 2 | `.\build.ps1 --build=bin` | `bin/nuget/` 生成 2 个 nupkg |
| 3 | `dotnet build Plugins.slnx` | 0 error，WebTemplate 编译成功 |
| 4 | `.\build.ps1 --build=all` | wwwroot/index.html 出现在 publish/ 和 zip 中 |
| 5 | `dotnet run --project src/launcher/LYBox.Launcher.Desktop` | WebHostService 启动日志 + Web 页面可交互 |

---

## 文件清单

### 修改文件（6 个）
| 文件 | 变更 |
|------|------|
| `src/LYBox.Plugin.Generators/LYBox.Plugin.Generators.csproj` | +`System.Text.Json 9.0.0` 包引用 |
| `src/LYBox.Plugin.Shared/Web/WebViewIpcTransport.cs` | `WebView` → `NativeWebView`（3 处） |
| `src/LYBox.Plugin.Shared/Web/WebPluginView.axaml` | `<web:WebView>` → `<web:NativeWebView>` |
| `src/LYBox.Plugin.Shared/Web/WebPluginView.axaml.cs` | `FindControl<WebView>` → `FindControl<NativeWebView>` |
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
