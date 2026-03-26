using Avalonia.Platforms.Abstraction.Services;

namespace Avalonia.Platforms.Abstraction.Stubs.Services;

/// <inheritdoc />
public class SystemEventsServiceStub : ISystemEventsService
{
    /// <inheritdoc />
    public event EventHandler? TimeChanged;
}