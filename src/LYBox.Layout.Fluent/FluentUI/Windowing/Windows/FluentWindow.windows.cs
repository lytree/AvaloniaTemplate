using System.Runtime.CompilerServices;
using AvaloniaFluentUI.Controls.Interop;

namespace AvaloniaFluentUI.Windowing;

public partial class FluentWindow
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitializeWindowPlatform()
    {
        IsWindows = true;
        IsWindows11 = OSVersionHelper.IsWindows11();
    }
}
