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
            WorkflowNodeStatus.NotStarted => Brushes.DimGray,
            WorkflowNodeStatus.Running => Brushes.Orange,
            WorkflowNodeStatus.Completed => Brushes.Green,
            WorkflowNodeStatus.Failed => Brushes.DarkRed,
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}