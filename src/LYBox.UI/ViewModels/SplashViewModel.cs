using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Irihi.Avalonia.Shared.Contracts;

namespace LYBox.UI.ViewModels;

public partial class SplashViewModel: ObservableObject, IDialogContext
{
    [ObservableProperty] private double _progress;
    private Random _r = new();
    private IDisposable? _timerDisposable;

    public SplashViewModel()
    {
        _timerDisposable = DispatcherTimer.Run(OnUpdate, TimeSpan.FromMilliseconds(20), DispatcherPriority.Default);
    }

    private bool OnUpdate()
    {
        Progress += 10 * _r.NextDouble();
        if (Progress <= 100)
        {
            return true;
        }
        else
        {
            _timerDisposable?.Dispose();
            _timerDisposable = null;
            RequestClose?.Invoke(this, true);
            return false;
        }
    }
    
    public void Close()
    {
        _timerDisposable?.Dispose();
        _timerDisposable = null;
        RequestClose?.Invoke(this, false);
    }

    public event EventHandler<object?>? RequestClose;
}
