using System;

namespace Avalonia.Plugin.Shared.Attributes;

/// <summary>
/// 菜单项特性，用于标记ViewModel并自动生成菜单项
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class MenuAttribute : Attribute
{
    /// <summary>
    /// 菜单项标题
    /// </summary>
    public string Header { get; set; }

    /// <summary>
    /// 菜单项键
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// 父菜单项键
    /// </summary>
    public string? ParentKey { get; set; }

    /// <summary>
    /// 菜单项状态
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// 菜单项顺序
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 页面分组
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// 初始化菜单项特性
    /// </summary>
    /// <param name="header">菜单项标题</param>
    /// <param name="key">菜单项键</param>
    public MenuAttribute(string header, string key)
    {
        Header = header;
        Key = key;
        ParentKey = null;
        Status = null;
        Order = 0;
        Group = null;
    }
}
