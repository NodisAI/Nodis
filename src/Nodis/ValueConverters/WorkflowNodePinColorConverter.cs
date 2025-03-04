using System.Globalization;
using Avalonia.Data.Converters;

namespace Nodis.ValueConverters;

public class WorkflowNodePinColorConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        return values[0] is true ? values[1] : null;
    }
}