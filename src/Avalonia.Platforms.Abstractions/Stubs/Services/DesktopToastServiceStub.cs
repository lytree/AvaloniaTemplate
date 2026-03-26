using Avalonia.Platforms.Abstraction.Models;
using Avalonia.Platforms.Abstraction.Services;

namespace Avalonia.Platforms.Abstraction.Stubs.Services;

/// <inheritdoc />
public class DesktopToastServiceStub : IDesktopToastService
{
    /// <inheritdoc />
    public async Task ShowToastAsync(DesktopToastContent content)
    {
    }

    /// <inheritdoc />
    public async Task ShowToastAsync(string title, string body, Action? activated = null)
    {
    }

    /// <inheritdoc />
    public void ActivateNotificationAction(Guid id)
    {
    }
}