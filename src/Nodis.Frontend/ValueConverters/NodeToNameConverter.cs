using System.Globalization;
using Avalonia.Data.Converters;

namespace Nodis.Frontend.ValueConverters;

public class NodeToNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            INamedObject namedObject => namedObject.Name,
            _ => null
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}