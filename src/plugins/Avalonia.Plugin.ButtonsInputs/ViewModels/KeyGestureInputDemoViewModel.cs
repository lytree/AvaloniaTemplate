using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Input;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

public class KeyGestureInputDemoViewModel: ObservableObject
{
    public List<Key> AcceptableKeys { get; set; } = new List<Key>()
    {
        Key.A, Key.B, Key.C,
    };
}





