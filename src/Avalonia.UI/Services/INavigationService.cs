using System.Collections.Generic;
using Avalonia.Plugin.Shared;

namespace Avalonia.UI.Services;

public interface INavigationService
{
    void RegisterNavigation(string key, ViewModelFactory factory);

    void RegisterNavigations(Dictionary<string, ViewModelFactory> navigations);

    object CreateViewModel(string key);

    void InvalidateCache(string key);

    void InvalidateAllCache();

    IEnumerable<string> GetNavigationKeys();
}
