using System.Globalization;
using Avalonia.Layout;
using Irihi.Avalonia.Shared.Converters;

namespace Ursa.Themes.Semi.Converters;

public class FormContentHeightToAlignmentConverter : MarkupValueConverter
{
    public double Threshold { get; set; } = 32;

    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double d) return VerticalAlignment.Center;
        return d > Threshold ? VerticalAlignment.Top : VerticalAlignment.Center;
    }
}
