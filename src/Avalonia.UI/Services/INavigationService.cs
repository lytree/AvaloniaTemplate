using System.Collections.Generic;

namespace Avalonia.UI.Services;

public interface INavigationService
{
    /// <summary>
    /// 注册导航项
    /// </summary>
    /// <param name="key">导航键</param>
    /// <param name="factory">ViewModel 工厂方法</param>
    void RegisterNavigation(string key, ViewModelFactory factory);

    /// <summary>
    /// 注册多个导航项
    /// </summary>
    /// <param name="navigations">导航项字典</param>
    void RegisterNavigations(Dictionary<string, ViewModelFactory> navigations);

    /// <summary>
    /// 创建 ViewModel
    /// </summary>
    /// <param name="key">导航键</param>
    /// <returns>ViewModel 实例</returns>
    object CreateViewModel(string key);

    /// <summary>
    /// 获取所有导航键
    /// </summary>
    /// <returns>导航键集合</returns>
    IEnumerable<string> GetNavigationKeys();
}

/// <summary>
/// ViewModel 工厂委托
/// </summary>
public delegate object ViewModelFactory();
