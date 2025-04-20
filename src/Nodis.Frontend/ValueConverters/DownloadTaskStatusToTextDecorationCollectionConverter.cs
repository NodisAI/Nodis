using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Nodis.Frontend.ValueConverters;

public class DownloadTaskStatusToTextDecorationCollectionConverter : IValueConverter
{
    private readonly TextDecorationCollection canceledTextDecoration =
    [
        new TextDecoration
        {
            Location = TextDecorationLocation.Strikethrough
        },
    ];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DownloadTaskStatus.Canceled ? canceledTextDecoration : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}