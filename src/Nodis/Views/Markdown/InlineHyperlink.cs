using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;

namespace Nodis.Views.Markdown;

public class InlineHyperlink : InlineUIContainer
{
    private readonly Underline _underline;

    public Span Content => _underline;

    public InlineHyperlink(string? href)
    {
        _underline = new Underline();

        var textBlock = new TextBlock
        {
            Inlines = [_underline]
        };

        var button = new Button
        {
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = textBlock
        };

        if (href is not null)
        {
            button.Click += (_, _) => OpenUrl(new Uri(href, UriKind.RelativeOrAbsolute));
            ToolTip.SetTip(button, href);
        }

        Child = button;
    }
    
    private static void OpenUrl(Uri url)
    {
        // todo
    }
}