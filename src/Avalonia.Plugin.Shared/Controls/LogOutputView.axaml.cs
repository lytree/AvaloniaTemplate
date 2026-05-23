using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace Avalonia.Plugin.Shared.Controls;

public partial class LogOutputView : UserControl
{
    public static readonly StyledProperty<ObservableCollection<string>> LogEntriesProperty =
        AvaloniaProperty.Register<LogOutputView, ObservableCollection<string>>(nameof(LogEntries));

    public ObservableCollection<string> LogEntries
    {
        get => GetValue(LogEntriesProperty);
        set => SetValue(LogEntriesProperty, value);
    }

    private ObservableCollection<string>? _currentCollection;

    public LogOutputView()
    {
        InitializeComponent();
        LogEntriesProperty.Changed.AddClassHandler<LogOutputView>(OnLogEntriesChanged);
    }

    private void OnLogEntriesChanged(LogOutputView sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_currentCollection is not null)
        {
            _currentCollection.CollectionChanged -= OnCollectionChanged;
        }

        _currentCollection = e.NewValue as ObservableCollection<string>;

        if (_currentCollection is not null)
        {
            _currentCollection.CollectionChanged += OnCollectionChanged;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
        {
            LogListBox.ScrollIntoView(e.NewItems.Count == 1
                ? e.NewItems[0]!
                : e.NewItems[e.NewItems.Count - 1]!);
        }
    }

    private async void OnCopyLogEntry(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: string line })
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(line);
            }
        }
    }

    private void OnClearLog(object? sender, RoutedEventArgs e)
    {
        LogEntries?.Clear();
    }
}
