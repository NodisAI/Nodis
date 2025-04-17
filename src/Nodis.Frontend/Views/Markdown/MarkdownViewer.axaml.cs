using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
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

    public static readonly DirectProperty<MarkdownViewer, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, bool>(nameof(IsBusy), o => o.IsBusy);

    public bool IsBusy
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            RaisePropertyChanged(IsBusyProperty, !value, value);
        }
    }

    private async void RenderProcessAsync(string? markdown, CancellationToken cancellationToken)
    {
        IsBusy = true;
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
        catch (Exception e)
        {
            RenderedContent.Content = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Text = "Error rendering markdown\n" + e.Message
            };
        }
        finally
        {
            IsBusy = false;
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