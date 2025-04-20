using System.Globalization;
using Avalonia.Data.Converters;

namespace Nodis.Frontend.ValueConverters;

public class CollectionConverters
{
    public static IValueConverter IsNotEmpty { get; } = new IsNotEmptyConverter();

    private class IsNotEmptyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ICollection collection)
            {
                return collection.Count > 0;
            }

            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}