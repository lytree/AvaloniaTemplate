# AvaloniaTemplate — AGENTS.md

Compact guidance for OpenCode agents working in this repository.

## Build & Run

- **Build system**: Cake Frosting (`build/build.cs` — .NET 10 file-based app, Cake 6.1.0). Call via `.\build.ps1` (Windows) or `./build.sh` (Linux/macOS).
  ```
  .\build.ps1 --build=all                    # default: bin (launcher + NuGet) + plugin
  .\build.ps1 --build=bin                    # build launcher + pack SDK NuGet packages (host & SDK 同版本号，统一发版)
  .\build.ps1 --build=plugin                 # build & zip all plugins
  .\build.ps1 --configuration=Debug          # override config (default: Release)
  .\build.ps1 --package-version=1.2.3        # set version (default: 1.0.0)
  .\build.ps1 --runtime-identifier=win-x64   # set RID for launcher publish
  .\build.ps1 --self-contained=true          # self-contained launcher publish
  .\build.ps1 --nuget-api-key=<KEY>          # push packages to nuget.org
  ```
- **Build order matters**: `--build=bin` must run first before `--build=plugin` (or use `--build=all`), because `--build=bin` now packs the SDK NuGet packages and plugins depend on `Avalonia.Plugin.Generators` + `Avalonia.Plugin.Shared` locally built NuGet packages.
- **Direct `dotnet build`** works for individual projects, but plugins may fail to restore without the local NuGet packages pre-built (use `--build=bin` or ensure `bin/nuget/` has the `.nupkg` files). `--build=nuget` is kept as a compatibility alias for `--build=bin`.
- **Run launcher**: `dotnet run --project src/launcher/Avalonia.Launcher.Desktop`
- **VS Code debug**: Use the "Debug Plugin - {Name}" launch configs — each sets `AVALONIA_EXTRA_PLUGINS_PATH` to the plugin's `bin/Debug/net10.0` output for live dev loading.
- **No tests**, no CI workflows, no linters/formatters configured.

## Architecture

### Two solutions
| Solution | Contents |
|----------|----------|
| `Core.slnx` | Host: Generators, Shared, UI, Launcher, Platforms.Abstractions |
| `Plugins.slnx` | Generators, Shared, all `plugins/*` projects (10 plugins) |

### Project layers (src/)
```
Avalonia.Plugin.Generators/        Roslyn incremental source generator (netstandard2.1, IsRoslynComponent)
Avalonia.Plugin.Shared/            Shared contracts: IPlugin, IPluginMetadata, ViewLocator, ServiceLocator, attributes, controls
Avalonia.Platforms.Abstractions/   Cross-platform abstraction base classes (empty README only)
Avalonia.UI/                       Host app: ViewModels, Views, Services (EF Core, navigation, menu, localization, ZLogger)
Avalonia.Launcher.Desktop/         Desktop entry point (Program.cs → App.axaml.cs). Sets AvaloniaUseCompiledBindingsByDefault=true.
```

### Platform-specific projects
`src/platforms/` contains:
- `Avalonia.Platforms.Windows` — `net10.0-windows10.0.19041.0`
- `Avalonia.Platforms.MacOs` — `net10.0-macos15.0`
- `Avalonia.Platforms.Linux` — `net10.0`

### Plugin projects (plugins/)
Each plugin is a `net10.0` library referencing `Avalonia.Plugin.Generators` (analyzer, `OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`) and `Avalonia.Plugin.Shared` (`PrivateAssets="all"`). Plugin metadata is declared via MSBuild properties:
```xml
<PluginId>UUID</PluginId>
<PluginName>...</PluginName>
<PluginAuthor>...</PluginAuthor>
<PluginDescription>...</PluginDescription>
<PluginVersion>1.0.0</PluginVersion>  <!-- optional, falls back to <Version> -->
```

10 plugins: ButtonsInputs, DateTime, DialogFeedbacks, Downloader, LayoutDisplay, NavigationMenus, ProDataGrid, ScottPlot, TDLSharp, Template.

### App startup flow
```
Program.cs → App.Initialize()
  1. Build DI container via ServiceCollectionExtensions.AddAvaloniaServices()
  2. ServiceLocator.Initialize(provider) — static gateway for plugin code
  3. InitializeDatabase() — SQLite via EF Core (AppDbContext)
  4. InitializeLocalization() — restore saved locale
  5. LoadPluginsAsync() — discover, load, and register all plugins
  6. OnFrameworkInitializationCompleted() → show splash, then MainWindow
```

### Plugin loading & assembly exclusion
- Each plugin loads in an isolated, collectible `AssemblyLoadContext`
- Framework/shared assemblies are forwarded to the default context (exclusion list in `Avalonia.Plugin.Shared.props`/`.targets`)
- Plugins auto-generate `plugin.json` manifests via the `GeneratePluginManifest` target (from `Avalonia.Plugin.Shared.targets`)
- Discovery: scans `{AppBaseDir}/plugins/` and `AVALONIA_EXTRA_PLUGINS_PATH` env var
- Built output: `bin/plugins/{Name}/publish/` (publish directory) + `bin/plugins/zip/{Name}-{Version}.zip` (stripped of .pdb, .xml, .deps.json, .runtimeconfig.json)

## Key Patterns (don't break these)

| Pattern | What to know |
|---------|-------------|
| **ServiceLocator** | Static `IServiceProvider` wrapper for plugins. Initialized once in `App.Initialize()`. Check `TryGetService<T>()` before `GetService<T>()`. |
| **ViewLocator** | Global `IDataTemplate` using `ConditionalWeakTable` for cache (leak-free VM→View cycle). Registered in XAML — `ContentControl.Content="{Binding Content}"` auto-resolves. |
| **Navigation** | Key-based `NavigationService` + `WeakReferenceMessenger` pub/sub ("JumpTo" message). Plugins register nav items in `IPlugin.GetNavigationItems()`. |
| **Menu hierarchy** | Flat menu items with optional `parentKey`. `MenuItemTreeBuilder.BuildTree()` resolves the tree. `MenuConfigurationService` manages add/remove. |
| **Source generator** | `[GenerateMetadata]` on a class implementing `IPluginMetadata` → auto-generates `IPlugin` impl. Scans companion classes for `[ViewMap]`, `[NavigationItem]`, `[Menu]` attributes. |
| **Localization** | `ILocalizationService` stacks `.resx` `ResourceManager` instances. Plugins register theirs in `Initialize()`. |
| **Plugin lifecycle** | `NotInstalled → Installed → Loaded → Disabled → PendingUninstall`. State changes fire events for UI. |

## UI 组件与样式规范（强制）

所有 Host UI 与插件 UI 必须遵守以下选型与风格规则。违反规则的 UI 代码视为需重构。

### 1. 组件选型优先级（从高到低）

| 优先级 | 来源 | 用法示例 | 适用场景 |
|--------|------|---------|---------|
| 1 | **Irihi.Ursa**（`u:` 命名空间） | `<u:Button />`、`<u:Banner />`、`<u:NavMenu />`、`<u:Form />`、`<u:NumericUpDown />`、`<u:TagInput />`、`<u:IPv4Box />`、`<u:TimeBox />`、`<u:Avatar />`、`<u:Card />`、`<u:Badge />`、`<u:Loading />`、`<u:Breadcrumb />`、`<u:Dialog />`、`<u:Drawer />` | 默认首选。所有通用控件优先用 Ursa。 |
| 2 | **Avalonia 内置控件**（无 `u:` 前缀） | `<Button />`、`<TextBox />`、`<CheckBox />`、`<ComboBox />`、`<ListBox />`、`<TreeView />`、`<TabControl />`、`<ProgressBar />`、`<Slider />`、`<DatePicker />`、`<DataGrid />` | Ursa 未覆盖或场景不适合 Ursa 时使用。DataGrid 已应用 `<datagrid:DataGridFluentTheme />`。 |
| 3 | **项目自定义 Fluent 补充样式**（`src/Avalonia.UI/Theme/FluentDesign/FluentDesignStyles.axaml`） | `Button.FluentSettingsCard`、`Border.FluentInfoBadge`、`ProgressBar.circular.FluentProgressRing`、`Button.FluentBreadcrumbItem`、`Border.FluentContentDialogSurface` | Ursa 未提供的 WinUI 风格控件。详见下表。 |
| 4 | **CommunityToolkit.Mvvm** | `ObservableObject`、`[ObservableProperty]`、`[RelayCommand]` | ViewModel 基础设施（与组件选型并列，但所有 VM 必须用此库）。 |

**禁止**：直接引入 `Avalonia-Fluent-UI`（`AvaloniaFluentUI` NuGet/项目引用）整库。该库与 Irihi.Ursa 大量功能重叠且未发布到 NuGet，仅在风格上作为参考。需要 WinUI 风格控件时，使用上述第 3 级的项目内补充样式。

### 2. 自定义 Fluent 补充样式速查表

所有补充样式位于 `src/Avalonia.UI/Theme/FluentDesign/FluentDesignStyles.axaml`，通过 `UrsaSemiTheme` 自动加载，无需手动 `<StyleInclude>`。

| 类名 | 控件类型 | 替代的 WinUI 控件 | 用途 |
|------|---------|------------------|------|
| `FluentSettingsCard` | `Border` 或 `Button` | `SettingsExpander` / `SettingCard` | 设置页条目：左图标 + 标题 + 描述 + 右内容 |
| `FluentSettingsCardTitle` / `FluentSettingsCardDescription` / `FluentSettingsCardIconHost` | `TextBlock` / `Border` | — | SettingsCard 内部子元素样式 |
| `FluentInfoBadge` (+ `.FluentInfoBadgeCritical/Warning/Informational/Success`) | `Border` | `InfoBadge` | 数值或状态徽章 |
| `FluentInfoBadgeText` | `TextBlock` | — | InfoBadge 内数字 |
| `FluentInfoBadgeDot` | `Ellipse` | `InfoBadge` (dot) | 点状徽章 |
| `FluentProgressRing` (+ `.Small` / `.Large`) | `ProgressBar` (Classes=`circular`) | `ProgressRing` | 圆环进度（确定性或 `IsIndeterminate="True"`） |
| `FluentBreadcrumbItem` | `Button` | `BreadcrumbBar` 项 | 面包屑导航可点击项 |
| `FluentBreadcrumbCurrent` / `FluentBreadcrumbSeparator` | `TextBlock` | — | 当前节点 / 分隔符 |
| `FluentContentDialogSurface` / `FluentContentDialogTitle` / `FluentContentDialogBody` / `FluentContentDialogButtonRow` | `Border` / `TextBlock` / `StackPanel` | `ContentDialog` | 模态对话框外观（控件仍走 Ursa `Dialog` API） |
| `FluentNumeric` | `u:NumericUpDown` | `NumberBox` | Ursa NumericUpDown 的 Fluent 边框微调 |
| `FluentTagInput` | `u:TagInput` | — | Ursa TagInput 的 Fluent 边框微调 |

**示例**：
```xml
<!-- SettingsCard -->
<Border Classes="FluentSettingsCard">
    <Grid ColumnDefinitions="Auto,*,Auto">
        <Border Classes="FluentSettingsCardIconHost" Grid.Column="0">
            <Image Source="{DynamicResource FluentIconSettings}" Width="16" Height="16" />
        </Border>
        <StackPanel Grid.Column="1" Margin="12,0" VerticalAlignment="Center">
            <TextBlock Classes="FluentSettingsCardTitle" Text="主题" />
            <TextBlock Classes="FluentSettingsCardDescription" Text="选择浅色或深色外观" />
        </StackPanel>
        <u:ToggleSwitch Grid.Column="2" IsChecked="{Binding EnableDarkMode}" />
    </Grid>
</Border>

<!-- InfoBadge -->
<Border Classes="FluentInfoBadge FluentInfoBadgeCritical" VerticalAlignment="Top">
    <TextBlock Classes="FluentInfoBadgeText" Text="3" />
</Border>

<!-- ProgressRing -->
<ProgressBar Classes="circular FluentProgressRing" IsIndeterminate="True" />

<!-- Breadcrumb -->
<ItemsControl ItemsSource="{Binding BreadcrumbSegments}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate><StackPanel Orientation="Horizontal" /></ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <Button Classes="FluentBreadcrumbItem" Content="{Binding Title}" Command="{Binding NavigateCommand}" />
                <TextBlock Classes="FluentBreadcrumbSeparator" Text="/" />
            </StackPanel>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

### 3. 样式风格约束（限定 Fluent-UI 风格）

- **唯一允许的视觉风格**：Fluent Design System（WinUI 3 风格）。
- **禁止**直接使用 Semi 风格的硬编码色值。Semi 资源键（如 `SemiColorText0`、`SemiColorText1`、`SemiColorText2`、`SemiColorDanger`、`SemiColorWarning`、`SemiColorSuccess`、`SemiColorPrimary`）**仅作为动态资源引用**，由 `UrsaSemiTheme` 的 ThemeDictionary 自动映射到 Fluent 配色，不允许在 XAML 中写死颜色字面量（如 `#FF0078D4`）。
- **颜色资源层级**：
  1. Fluent 语义资源（首选）：`FluentAccentBrush`、`FluentAccentPointeroverBrush`、`FluentAccentPressedBrush`、`FluentCardBackgroundBrush`、`FluentCardStrokeBrush`、`FluentSubtleBrush`、`FluentSubtleHoverBrush`、`FluentSubtlePressedBrush`
  2. Semi 语义资源（次选，由 UrsaSemiTheme 自动适配到 Fluent 配色）：`SemiColorText0/1/2`、`SemiColorPrimary`、`SemiColorDanger`、`SemiColorWarning`、`SemiColorSuccess`、`SemiColorInfo`
  3. 字面量颜色（仅用于阴影 `BoxShadow`、`Opacity` mask 等无法用语义资源表达的场景）：使用 `#XXRRGGBB` 格式，且必须注释说明原因
- **圆角规范**：卡片 8px、徽章/小按钮 4px、点状元素圆形（`CornerRadius="0"` + `CornerRadius` 全值 = 宽/2）。
- **间距规范**：内边距遵循 12/16/24 三档；元素间用 `Spacing` 而非 `Margin`。
- **动画规范**：颜色/画刷过渡统一用 `BrushTransition`，时长 `0:0:0.15`；阴影过渡用 `BoxShadowsTransition`。复杂动画引用 `Theme/Animations/` 下的 `DefaultSizeAnimations`、`NavMenuSizeAnimations`、`SemiPopupAnimations`。
- **主题入口**：所有样式通过 `src/Avalonia.UI/Theme/UrsaSemiTheme.axaml` 注册，应用入口 `App.axaml` 仅引用 `<fluent:FluentTheme />` + `<theme:UrsaSemiTheme />` + `<sizeanimations:SemiPopupAnimations />` + `<datagrid:DataGridFluentTheme />`，**不要**在 `App.axaml` 中追加额外 `<StyleInclude>`。

### 4. 图标使用规则（优先 Fluent-UI icon）

- **首选图标集**：Fluent Icons（Microsoft Fluent UI System Icons）。资源位于 `src/Avalonia.UI/Theme/Icons/Fluent/`，按 `Regular/Filled` × `16/20/24/28/32/48` 切分。
- **图标资源键命名规范**：`FluentIcon{Size}{Variant}{Name}`，例如：
  - `FluentIcon24RegularSettings`
  - `FluentIcon20FilledWarning`
  - `FluentIcon16RegularChevronDown`
- **图标引用方式**（按控件类型选择）：
  1. **`PathIcon` / `Image`**（首选，矢量）：
     ```xml
     <PathIcon Data="{DynamicResource FluentIcon24RegularSettings}" Width="20" Height="20" />
     <!-- 或 -->
     <Image Source="{DynamicResource FluentIcon24RegularSettings}" Width="20" Height="20" />
     ```
  2. **`Button.Content`**（按钮内图标）：
     ```xml
     <Button Classes="FluentSettingsCard">
         <PathIcon Data="{DynamicResource FluentIcon24RegularSettings}" />
     </Button>
     ```
  3. **Ursa `IconButton`**（推荐用于纯图标按钮）：
     ```xml
     <u:IconButton Icon="{DynamicResource FluentIcon24RegularSettings}" />
     ```
- **次选图标集**：项目自定义 `Semi` 风格图标（`src/Avalonia.UI/Theme/Icons/_index.axaml` 中以 `SemiIcon` 开头的资源键，如 `SemiIconChevronDown`）。仅当 Fluent Icons 中找不到对应图标时使用，且需在代码注释中说明原因。
- **禁止**：硬编码 `Geometry.Parse("...")` 字面量路径。所有路径必须以 `StreamGeometry` 资源形式定义在 `Theme/Icons/` 下。
- **新增 Fluent 图标流程**：
  1. 从 [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) 获取 SVG path
  2. 转换为 `<StreamGeometry x:Key="FluentIcon{Size}{Variant}{Name}">path data</StreamGeometry>`
  3. 追加到对应尺寸的 `Theme/Icons/Fluent/{Variant}{Size}.axaml`
  4. 在 XAML 中以 `{DynamicResource FluentIcon...}` 引用

### 5. ViewModel 与数据绑定

- **ViewModel 基类**：所有 VM 继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` 或项目 `ViewModelBase`。
- **属性**：用 `[ObservableProperty]` 自动生成 INPC。**禁止**手写 `private T _field; public T Foo { get => _field; set => SetProperty(ref _field, value); }`。
- **命令**：用 `[RelayCommand]` 自动生成 `ICommand`。**禁止**手写 `RelayCommand`/`DelegateCommand` 实例。
- **CompiledBindings**：`AvaloniaUseCompiledBindingsByDefault=true`（已全局开启）。所有 `Binding` 必须有正确的 `x:DataType`，避免运行时反射开销。
- **MVVM Toolkit 源生成器**：partial VM 类必须标注 `[INotifyPropertyChanged]` 或继承 `ObservableObject`，否则 `[ObservableProperty]` 不会生成。

## Package & Framework Versions

All versions centralized as MSBuild properties in `src/Directory.Packages.props`:
- Avalonia: `12.0.3` (`$(AvaloniaVersion)`)
- Irihi.Ursa: `2.0.*` (`$(IrihiUrsaVersion)`)
- CommunityToolkit.Mvvm: `8.4.2` (`$(CommunityToolkit)`)
- EF Core: `10.0.8` (`$(EfCoreVersion)`)
- Microsoft.Extensions.DI: `10.0.8` (`$(MicrosoftExtensionsDI)`)
- Microsoft.Extensions.Localization: `10.0.8`
- AvaloniaUI.DiagnosticsSupport: `2.2.1`
- ProDataGrid: `12.0.0`
- ScottPlot: `5.1.58`
- ZLogger: `2.1.0`
- Plugin NuGet packages: `Avalonia.Plugin.Generators` + `Avalonia.Plugin.Shared`, version `1.0.0`, built locally to `bin/nuget/`

## NuGet Configuration

- **Root `nuget.config`**: sets `globalPackagesFolder` to `<repo>/packages` (local cache, tracked as packages/ in `.gitignore` exception for `packages/nuget/`)
- **`plugins/nuget.config`**: inherits root config, adds `AvaloniaPluginLocal` feed pointing at `<repo>/bin/nuget` — this is how plugins resolve the locally-built `Avalonia.Plugin.Generators` and `Avalonia.Plugin.Shared` packages

## Platform Targeting

`src/Environment.props` manages platform-specific TFMs:
- Windows: `net10.0-windows10.0.19041.0` + defines `Platforms_Windows`
- macOS: `net10.0-macos15.0` + defines `Platforms_MacOs` + `SupportedOSPlatformVersion=10.15`
- Linux: `net10.0` (no platform suffix) + defines `Platforms_Linux`
- Dev mode auto-detects OS via `[System.OperatingSystem]::IsWindows()` etc.
- CI uses `PublishBuilding=true` + `PublishPlatform=windows|linux|macos`
- Release+Windows → `OutputType=WinExe`

## Installed Skills (local)

Three Avalonia/Zafiro skills in `.agents/skills/` (from `sickn33/antigravity-awesome-skills`):
- `avalonia-layout-zafiro` — XAML layout conventions
- `avalonia-viewmodels-zafiro` — ViewModel/Wizard patterns
- `avalonia-zafiro-development` — mandatory conventions and rules

Skills are active and should be used when their patterns apply.

## Plugin .csproj template (for new plugins)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <PluginId>...</PluginId>
    <PluginName>...</PluginName>
    <PluginAuthor>...</PluginAuthor>
    <PluginDescription>...</PluginDescription>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia.Plugin.Generators" Version="1.0.0"
      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="Avalonia.Plugin.Shared" Version="1.0.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

## Gotchas

- `.slnx` format (not `.sln`) — .NET 10 XML solution format
- The build script (`build/build.cs`) discovers plugins by scanning all `*.csproj` under `plugins/` — `PluginId` etc. are read from .csproj XML
- `Core.slnx` and `Plugins.slnx` share `src/Avalonia.Plugin.Generators` and `src/Avalonia.Plugin.Shared`
- Plugin NuGet packages must be built locally before plugins can restore. Build with `.\build.ps1 --build=bin` first; packages go to `bin/nuget/`. The `plugins/nuget.config` adds this as a local feed.
- `AvaloniaUseCompiledBindingsByDefault` is set to `true` in the launcher project — follow this convention for new plugins
- `Directory.Build.props` at `src/` imports `Environment.props` and sets default `TargetFramework=net10.0` (overridden per-platform)
- The Generators project targets `netstandard2.1` (Roslyn source generator constraint) while everything else targets `net10.0`
- No `opencode.json` or `CLAUDE.md` in the repo — this `AGENTS.md` is the sole instruction file
