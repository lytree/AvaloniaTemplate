using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using AvaloniaFluentUI.Locale;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Layout.Fluent.Models;
using LYBox.Layout.Fluent.Services;

namespace LYBox.Layout.Fluent.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    public override string Title => "Fluent LYBox.Layout.Fluent";

    [ObservableProperty]
    private Vector _scrollViewerOffset = new Vector();

    public HomeViewModel()
    {
#if DEBUG
        Debug.WriteLine("HomeViewModel Init");
#endif
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;

        // 功能入口卡片：工具箱与插件管理
        FeatureItemSource = ButtonItemModel.CreateList(
            ("Introduction", "Introduction", "Introduction", "工具箱：浏览所有可用工具与插件页面。"),
            ("PluginManagement", "PluginManagement", "PluginManagement", "插件管理：安装、卸载、启用、禁用插件。"),
            ("Settings", "Settings", "Settings", "应用设置：主题、语言、窗口效果与插件目录。")
        );
    }

    public List<ButtonItemModel> FeatureItemSource { get; }

    // Localized string properties
    public string GettingStartedTitle => LocalizationService.Instance.GetString("GettingStarted");
    public string GettingStartedContent => LocalizationService.Instance.GetString("GettingStartedContent");
    public string GitHubRepoTitle => LocalizationService.Instance.GetString("GitHubRepo");
    public string GitHubRepoContent => LocalizationService.Instance.GetString("GitHubRepoContent");
    public string CodeSamplesTitle => LocalizationService.Instance.GetString("CodeSamples");
    public string CodeSamplesContent => LocalizationService.Instance.GetString("CodeSamplesContent");
    public string SendFeedbackTitle => LocalizationService.Instance.GetString("SendFeedback");
    public string SendFeedbackContent => LocalizationService.Instance.GetString("SendFeedbackContent");
    public string SectionFeatures => "功能入口";

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(GettingStartedTitle));
        OnPropertyChanged(nameof(GettingStartedContent));
        OnPropertyChanged(nameof(GitHubRepoTitle));
        OnPropertyChanged(nameof(GitHubRepoContent));
        OnPropertyChanged(nameof(CodeSamplesTitle));
        OnPropertyChanged(nameof(CodeSamplesContent));
        OnPropertyChanged(nameof(SendFeedbackTitle));
        OnPropertyChanged(nameof(SendFeedbackContent));
        OnPropertyChanged(nameof(SectionFeatures));
    }
}
