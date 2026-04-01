namespace Avalonia.Plugin.Shared;

public interface IPluginMetadata
{
    /// <summary>
    /// 插件名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件版本
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 插件作者
    /// </summary>
    string Author { get; }

    /// <summary>
    /// 插件描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 插件依赖
    /// </summary>
    IEnumerable<string> Dependencies { get; }

    /// <summary>
    /// 插件唯一标识
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// 初始化插件
    /// </summary>
    void Initialize();


}


