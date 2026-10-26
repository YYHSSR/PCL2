using System.Globalization;
using System.Windows.Data;

namespace MeloongCore.Wpf;

/// <summary>
/// Multiplies the bound value, using <paramref name="parameter"/> as the multiplier.
/// </summary>
public class MultiplicationConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is null) return 0;
        if (!double.TryParse(value.ToString(), out double before)) return 0;
        double scale = 1;
        if (parameter is not null) double.TryParse(parameter.ToString(), out scale);
        return before * scale;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is null) return Binding.DoNothing;
        if (!double.TryParse(value.ToString(), out double before)) return Binding.DoNothing;
        double scale = 1;
        if (parameter is not null) double.TryParse(parameter.ToString(), out scale);
        if (scale == 0) return Binding.DoNothing;
        return before / scale;
    }
}