using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Nodis.Frontend.ValueConverters;

public class NodeStatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            NodeStates.NotStarted => Brushes.DimGray,
            NodeStates.Running => Brushes.Orange,
            NodeStates.Completed => Brushes.GreenYellow,
            NodeStates.Failed => Brushes.Red,
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}