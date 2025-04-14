using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Markdig;

namespace Nodis.Frontend.Views;

public class MarkdownViewer : TemplatedControl
{
    private static readonly MarkdownAvaloniaRenderer Renderer = new();

    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownViewer, string>(nameof(Markdown), string.Empty);

    public string Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public ContentControl RenderedContent { get; } = new();

    private async void RenderProcessAsync(string? markdown, CancellationToken cancellationToken)
    {
        try
        {
            RenderedContent.Content = null;
            if (string.IsNullOrWhiteSpace(markdown)) return;

            var document =
                await Task.Run(
                    () => Markdig.Markdown.Parse(
                        markdown,
                        new MarkdownPipelineBuilder()
                            .UseEmphasisExtras()
                            .UseGridTables()
                            .UsePipeTables()
                            .UseTaskLists()
                            .UseAutoLinks()
                            .Build()),
                    cancellationToken);

            Renderer.RenderDocumentTo(RenderedContent, document, cancellationToken);
        }
        catch
        {
            // ignored
        }
    }

    private CancellationTokenSource? renderProcessCancellationTokenSource;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Sender is not MarkdownViewer markdownViewer || change.Property != MarkdownProperty)
            return;

        if (markdownViewer.renderProcessCancellationTokenSource is { } cts)
            cts.Cancel();

        cts = markdownViewer.renderProcessCancellationTokenSource = new CancellationTokenSource();
        markdownViewer.RenderProcessAsync(change.NewValue as string, cts.Token);
    }
}