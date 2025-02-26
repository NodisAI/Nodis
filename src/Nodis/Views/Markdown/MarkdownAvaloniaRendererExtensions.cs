using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaDocs = Avalonia.Controls.Documents;

namespace Nodis.Views.Markdown;

public static class MarkdownAvaloniaRendererExtensions
{
    public static AvaloniaDocs.Inline WrapWithContainer(this Control element)
    {
        return new AvaloniaDocs.Span
        {
            BaselineAlignment = BaselineAlignment.Center,
            Inlines =
            {
                new AvaloniaDocs.InlineUIContainer
                {
                    Child = element
                }
            }
        };
    }
}