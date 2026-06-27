# 插件 SDK 版本契约 — 开发 / 打包 / 部署指南

本仓库采用 **宿主与 SDK 同版本号** + **运行时 SDK 契约校验** 机制，确保插件与宿主之间的 API 兼容性可在编译期与运行时双重保证。本文档面向插件作者、宿主维护者、CI/发版人员。

---

## 1. 概念总览

### 1.1 两层版本号

| 层级 | MSBuild 属性 | 当前值 | 作用域 | 影响什么 |
|------|--------------|--------|--------|----------|
| **宿主+SDK 同步版本** | `HostVersion` | `2.1.0` | Launcher / UI / Platforms.* / Generators / Shared | 宿主程序集 `AssemblyVersion` / `FileVersion`；SDK NuGet 包版本；`PluginSdkContract.CurrentVersion` 常量 |
| **插件自身版本** | `PluginVersion`（各插件 csproj） | 例如 `1.0.0` | 单个插件 | 该插件的 `plugin.json` version、zip 包名 |

> 注：`PluginSdkVersion` 仍作为别名保留（等价于 `$(HostVersion)`），仅为旧脚本/旧插件 csproj 提供向后兼容，不再单独 bump。

**真相源**：仓库根 [`Directory.Build.props`](../Directory.Build.props)
```xml
<PropertyGroup>
    <HostVersion>2.1.0</HostVersion>
    <PluginSdkVersion>$(HostVersion)</PluginSdkVersion>   <!-- 别名，等价 -->
</PropertyGroup>
```

**链式继承**：[`src/Directory.Build.props`](../src/Directory.Build.props) 通过 `<Import>` 引用根 props，把 `HostVersion` 同步给所有宿主项目。SDK 项目（`IsPluginSdkProject=true`）在自己的 csproj 中也直接使用 `$(HostVersion)` 作为 `Version`。

### 1.2 SDK 契约的运行时含义

`HostVersion` 同时承担 SDK 契约版本语义——它代表 **宿主与插件共享的 API 表面**：
- 升 `HostVersion` Major → 公共 API 破坏性变更，所有插件需重新编译
- 升 Minor → 新增 API，旧插件仍兼容
- 升 Build → 无 API 变更的 bug 修复

宿主与所有插件引用同一份 `Avalonia.Plugin.Shared` NuGet 包 → 编译期就能保证双方看到的契约一致；运行时再通过 `PluginSdkContract.CurrentVersion` 常量做一道兜底校验，防止 **运行时被替换为不兼容旧宿主** 或 **插件清单声明了更高 SDK 需求**。

---

## 2. 开发指南

### 2.1 新建插件

模板参考 [plugins/Avalonia.Plugin.Template](../plugins/Avalonia.Plugin.Template)。最小 csproj：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>

    <!-- 插件元数据 -->
    <PluginId>YOUR-UUID-HERE</PluginId>
    <PluginName>My Plugin</PluginName>
    <PluginAuthor>Author</PluginAuthor>
    <PluginDescription>Description</PluginDescription>
    <PluginVersion>1.0.0</PluginVersion>

    <!-- 声明本插件所需的最低 Plugin SDK 契约版本 -->
    <!-- 不填默认 "0.0.0"（无约束）；填值后宿主运行时会比对 PluginSdkContract.CurrentVersion -->
    <MinPluginSdkVersion>2.1.0</MinPluginSdkVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia.Plugin.Generators" Version="1.0.0"
      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="Avalonia.Plugin.Shared" Version="1.0.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

### 2.2 `MinPluginSdkVersion` 声明规则

- **何时声明**：插件用到了某个 SDK 版本才引入的 API（如新接口成员、新属性）。
- **取值原则**：填入你 **实际依赖的最低 SDK 版本**，不是当前最新版本。
- **不声明的情况**：插件只用了 1.0.0 就稳定存在的 API，可不填 `<MinPluginSdkVersion>`，构建时 `plugin.json` 会写 `"0.0.0"`，宿主视作无约束。

```xml
<!-- 仅用 1.0 起就稳定的 API -->
<MinPluginSdkVersion></MinPluginSdkVersion>
<!-- 等价于 -->
<!-- （不写该 property） -->
```

### 2.3 在插件代码中读取当前 SDK 版本（可选）

```csharp
using Avalonia.Plugin.Shared;

var current = PluginSdkContract.CurrentVersion;  // 编译期常量，字符串形式，例如 "2.1.0"
```

该常量由 `Avalonia.Plugin.Shared` NuGet 包内的 `GeneratePluginSdkContract` MSBuild target 在编译期生成（位于 `obj/PluginSdkContract.g.cs`），值来自编译时的 `$(HostVersion)`（SDK 与宿主同版本）。**插件与宿主编译时引用同一 NuGet 包版本，所以两侧常量值必然一致**。

### 2.4 接口默认成员的兼容性策略

[`IPluginMetadata.MinPluginSdkVersion`](../src/Avalonia.Plugin.Shared/IPluginMetadata.cs) 使用 **接口默认成员**：

```csharp
public interface IPluginMetadata
{
    string MinPluginSdkVersion => "0.0.0";
}
```

含义：
- 旧插件未实现该成员 → 自动获得 `"0.0.0"` 默认值 → 视为无约束 → 兼容宿主任意 SDK 版本
- 新插件显式实现 → 提供具体所需最低版本 → 宿主据此校验

> ⚠️ 默认成员 **不能** 在 metadata 类的 `[GenerateMetadata]` 源生成器扫描范围内显式重写为不同值——`plugin.json` 才是运行时权威来源（见下）。

### 2.5 manifest 字段优先级

运行时校验读取的是 `plugin.json` 中的 `minPluginSdkVersion`，不是接口成员：

```
插件 .csproj <MinPluginSdkVersion>
    ↓ GeneratePluginManifest target（Avalonia.Plugin.Shared.targets）
plugin.json: "minPluginSdkVersion": "..."
    ↓ PluginLoader 读取
PluginInfo.MinPluginSdkVersion
    ↓ IsPluginSdkCompatible(...)
通过 / 拒绝
```

`build.cs` 的 `EnsurePluginManifest` fallback 也会写入此字段，确保手动 publish 场景仍含版本信息。

---

## 3. 打包指南

### 3.1 构建产物概览

```
bin/
├── bin/                                      # 宿主 launcher 发布目录
│   └── Avalonia.Launcher.Desktop(.exe) + 运行时依赖
├── nuget/                                    # SDK NuGet 包（开发期分发用，与宿主同版本）
│   ├── Avalonia.Plugin.Generators.{HostVersion}.nupkg
│   └── Avalonia.Plugin.Shared.{HostVersion}.nupkg
└── plugins/
    ├── {PluginName}/publish/                 # 插件可加载目录
    │   ├── {PluginName}.dll
    │   ├── plugin.json                       # ← 含 minPluginSdkVersion
    │   └── shared-assemblies.txt
    └── zip/
        └── {PluginName}-{PluginVer}.zip      # 分发用压缩包
```

> **重要**：SDK NuGet 包与宿主 launcher 共用同一版本号（`HostVersion`），且在 `--build=bin` 一次性产出。不再有独立的 `nuget` 构建目标，二者一起发版。

### 3.2 标准构建流程

```powershell
# 1. 一键构建宿主 + SDK NuGet 包（统一发版，产物在 bin/bin 与 bin/nuget）
.\build.ps1 --build=bin

# 2. 构建并打包所有插件（依赖上一步产出的本地 NuGet 包做 restore）
.\build.ps1 --build=plugin

# 或一步到位
.\build.ps1 --build=all
```

> `--build=bin` 已合并旧版 `bin` + `nuget` 两个目标。`--build=nuget` 作为兼容别名仍可使用，行为与 `--build=bin` 等价（同时产出 launcher + NuGet 包）。

**Linux/macOS**：用 `./build.sh` 替代 `.\build.ps1`。

### 3.3 两层版本覆盖

发版时可通过命令行临时覆盖 csproj 真相源：

| 参数 | 覆盖 | 优先级 |
|------|------|--------|
| `--host-version=2.2.0` | 宿主+SDK 同步版本 | 仅本次构建（同时影响 launcher 程序集版本、NuGet 包版本、`PluginSdkContract.CurrentVersion` 常量） |
| `--plugin-version=1.2.0` | 插件版本 | 仅本次构建（影响所有插件） |
| `--package-version=2.2.0` | **所有层**（紧急发版兼容回退） | 覆盖上述未显式指定的层 |

```powershell
# 紧急 hotfix：所有层统一覆盖
.\build.ps1 --build=all --package-version=2.1.1

# 正式发版：宿主与 SDK 同步 bump（默认走 csproj 真相源）
# 修改 Directory.Build.props 中的 HostVersion 后：
.\build.ps1 --build=bin    # 一次性产出 launcher + NuGet 包
.\build.ps1 --build=plugin --plugin-version=1.2.0
```

### 3.4 构建单插件

```powershell
.\build.ps1 --build=plugin --plugin=Avalonia.Plugin.Template
# 多个用逗号分隔
.\build.ps1 --build=plugin --plugin=Avalonia.Plugin.Template,Avalonia.Plugin.ButtonsInputs
```

### 3.5 验证 manifest

构建后检查 `plugin.json` 是否含 `minPluginSdkVersion`：

```powershell
type bin\plugins\Avalonia.Plugin.Template\publish\plugin.json
```

预期输出：
```json
{
  "pluginId": "TEMPLATE-PLUGIN-0000-0000-000000000001",
  "name": "Plugin Template",
  "version": "1.0.0",
  "author": "AvaloniaPlugin",
  "description": "Template for creating new Avalonia plugins",
  "assembly": "Avalonia.Plugin.Template.dll",
  "dependencies": [],
  "minPluginSdkVersion": "1.0.0"
}
```

### 3.6 推送 NuGet 包

```powershell
# bin 目标已包含 NuGet 打包，--nuget-api-key 在 PackBin 完成后自动推送
.\build.ps1 --build=bin --nuget-api-key=<YOUR_KEY>
```

包推送后，第三方插件作者可通过 NuGet feed 引用新版本 SDK。

---

## 4. 部署指南

### 4.1 部署目录结构

宿主运行时扫描两个位置加载插件：

1. `{AppBaseDir}/plugins/` — 默认插件目录
2. `AVALONIA_EXTRA_PLUGINS_PATH` 环境变量 — 开发期临时加载路径

每个插件是一个 **子目录**（不是 zip）：

```
{AppDir}/
├── Avalonia.Launcher.Desktop.exe
├── plugins/
│   ├── MyPlugin/
│   │   ├── MyPlugin.dll
│   │   ├── plugin.json          ← 必需
│   │   └── shared-assemblies.txt
│   └── AnotherPlugin/
│       └── ...
└── ...
```

### 4.2 安装插件

#### 方式 A：解压 zip 包到 plugins 目录

```powershell
# 假设用户拿到 Avalonia.Plugin.Template-1.0.0.zip
Expand-Archive .\Avalonia.Plugin.Template-1.0.0.zip -DestinationPath .\plugins\Avalonia.Plugin.Template\
```

#### 方式 B：通过 `AVALONIA_EXTRA_PLUGINS_PATH` 临时加载（开发期）

VS Code launch.json 已为每个插件配置了对应 launch config，设置环境变量指向插件 `bin/Debug/net10.0` 输出目录，可直接热加载调试。

```powershell
$env:AVALONIA_EXTRA_PLUGINS_PATH = "F:\Code\Dotnet\AvaloniaTemplate\plugins\Avalonia.Plugin.Template\bin\Debug\net10.0"
dotnet run --project src/launcher/Avalonia.Launcher.Desktop
```

### 4.3 运行时校验流程

宿主加载每个插件时会执行：

```
读 plugin.json → MinPluginSdkVersion
    ↓
IsPluginSdkCompatible(MinPluginSdkVersion, PluginSdkContract.CurrentVersion)
    ↓
通过 → 继续加载流程
不通过 → 标记 PluginState.Error，写入错误信息，拒绝加载
```

**SemVer 比对规则**（[`PluginLoader.IsPluginSdkCompatible`](../src/Avalonia.UI/Services/PluginLoader.cs)）：
- `null` / 空 / 解析失败 → 通过（保守策略，避免误拒合法插件）
- 仅比较 `Major.Minor.Build` 三段，预发布标签（`-beta` 等）忽略
- `Major` 不等：`current > required` 才通过
- `Minor` 不等：`current > required` 才通过
- `Build`：`current >= required` 才通过

### 4.4 不兼容时的用户表现

插件出现在列表中但状态为 `Error`，错误信息形如：

```
Plugin requires Plugin SDK >= 1.4.0, but host provides 1.3.0.
Update the host application or contact the plugin author.
```

错误状态会写回 `plugin.json` 的 `state` 字段，UI 据此显示。用户解决方案：
1. **升级宿主** 到提供 `>= 1.4.0` SDK 的版本
2. 或 **联系插件作者** 提供与当前宿主兼容的版本

---

## 5. 发版流程

### 5.1 升级宿主+SDK 同步版本（破坏性变更 / 业务发版）

宿主与 SDK 共用同一版本号，bump 时只改一处：

修改 [Directory.Build.props](../Directory.Build.props)：

```xml
<PropertyGroup>
    <HostVersion>2.2.0</HostVersion>            <!-- ← 宿主与 SDK 同步升级 -->
    <!-- PluginSdkVersion 自动取 $(HostVersion)，无需修改 -->
</PropertyGroup>
```

然后一次性构建并推送：
```powershell
.\build.ps1 --build=bin                          # 产出 launcher + NuGet 包
.\build.ps1 --build=bin --nuget-api-key=<KEY>    # 推送 NuGet 包
.\build.ps1 --build=plugin                       # 构建所有插件（依赖新 SDK 包）
```

或一行搞定：`.\build.ps1 --build=all --nuget-api-key=<KEY>`。

### 5.2 升级插件版本

修改插件 csproj：

```xml
<PluginVersion>1.1.0</PluginVersion>
```

```powershell
.\build.ps1 --build=plugin --plugin=YourPlugin
```

---

## 6. 关键文件参考

| 文件 | 作用 |
|------|------|
| [Directory.Build.props](../Directory.Build.props) | 仓库级版本真相源（`HostVersion`，`PluginSdkVersion` 为别名等价于 `HostVersion`） |
| [src/Directory.Build.props](../src/Directory.Build.props) | 链式继承根 props，宿主项目用 `HostVersion`；SDK 项目在自身 csproj 中显式取 `HostVersion` |
| [src/Avalonia.Plugin.Shared/Avalonia.Plugin.Shared.csproj](../src/Avalonia.Plugin.Shared/Avalonia.Plugin.Shared.csproj) | `GeneratePluginSdkContract` target：编译期生成 `PluginSdkContract.g.cs`（值取 `HostVersion`） |
| [src/Avalonia.Plugin.Shared/PluginSdkContract.g.cs](../src/Avalonia.Plugin.Shared/obj/Release/net10.0/PluginSdkContract.g.cs) | 自动生成：`CurrentVersion` 常量 |
| [src/Avalonia.Plugin.Shared/IPluginMetadata.cs](../src/Avalonia.Plugin.Shared/IPluginMetadata.cs) | `MinPluginSdkVersion` 接口默认成员 |
| [src/Avalonia.Plugin.Shared/Models/PluginManifest.cs](../src/Avalonia.Plugin.Shared/Models/PluginManifest.cs) | manifest DTO（含 `MinPluginSdkVersion`） |
| [src/Avalonia.Plugin.Shared/buildTransitive/Avalonia.Plugin.Shared.targets](../src/Avalonia.Plugin.Shared/buildTransitive/Avalonia.Plugin.Shared.targets) | `GeneratePluginManifest` target：写 `plugin.json` |
| [src/Avalonia.UI/Services/PluginLoader.cs](../src/Avalonia.UI/Services/PluginLoader.cs) | 运行时 `IsPluginSdkCompatible` 校验 |
| [build/build.cs](../build/build.cs) | Cake 构建：`bin` 目标合并 SDK NuGet 包 + launcher publish；`EnsurePluginManifest` fallback；版本覆盖参数 |

---

## 7. 常见问题

### Q1: 插件加载报 "Plugin requires Plugin SDK >= X.Y.Z, but host provides A.B.C"？

- **插件作者**：检查 csproj `<MinPluginSdkVersion>` 是否过高，降到实际依赖的最低版本。
- **用户**：升级宿主程序到提供 `>= X.Y.Z` SDK 的版本。

### Q2: 升级 `HostVersion` 后所有插件都需要重新编译吗？

宿主与 SDK 共用 `HostVersion`，因此升级宿主版本即升级 SDK 契约版本：

- **Minor / Build 升级**：不需要。旧插件兼容（运行时校验通过）。
- **Major 升级**：需要。旧插件可能用到破坏性变更的 API，应重新编译并更新 `<MinPluginSdkVersion>`。

### Q3: 临时跳过校验？

不支持。校验是宿主侧强制的，无法绕过。如确实需要加载不兼容插件，请与插件作者协调降低 `MinPluginSdkVersion` 声明。

### Q4: NuGet 包版本与 `PluginSdkContract.CurrentVersion` 关系？

二者都来自 `$(HostVersion)`（SDK 与宿主同步发版），因此必然相等：
- NuGet 包版本 → 包文件名 `Avalonia.Plugin.Shared.2.1.0.nupkg`
- `PluginSdkContract.CurrentVersion` → `"2.1.0"`（编译期注入）
- 宿主 `Avalonia.Launcher.Desktop` 程序集版本 → `2.1.0`

### Q5: `build.cs` 的 `EnsurePluginManifest` 是什么？

[`build.cs`](../build/build.cs) 的 fallback：当 MSBuild target 因故未生成 `plugin.json`（例如手动 `dotnet publish`），打包脚本会主动写一份 manifest，确保所有发布产物都含 `minPluginSdkVersion` 字段。
