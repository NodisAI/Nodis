using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Nodis.Models.Workflow;

namespace Nodis.ValueConverters;

public class WorkflowNodeStatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            WorkflowNodeStates.NotStarted => Brushes.DimGray,
            WorkflowNodeStates.Running => Brushes.Orange,
            WorkflowNodeStates.Completed => Brushes.Green,
            WorkflowNodeStates.Failed => Brushes.DarkRed,
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}