namespace v2rayN.Converters;

public class StarGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var weight = value switch
        {
            int i => Math.Max(0d, i),
            double d when double.IsFinite(d) => Math.Max(0d, d),
            _ => Math.Max(0d, value?.ToString().ToInt() ?? 0)
        };

        return new GridLength(weight, GridUnitType.Star);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return null;
    }
}
