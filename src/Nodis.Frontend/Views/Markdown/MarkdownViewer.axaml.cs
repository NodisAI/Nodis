using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

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

    public static readonly StyledProperty<string> UrlRootProperty =
        AvaloniaProperty.Register<MarkdownViewer, string>(nameof(UrlRoot), string.Empty);

    /// <summary>
    /// Url root will apply to all links (hyperlinks, images, etc.) in the markdown.
    /// </summary>
    public string UrlRoot
    {
        get => GetValue(UrlRootProperty);
        set => SetValue(UrlRootProperty, value);
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

    private MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private async void RenderProcessAsync(string? markdown, CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            RenderedContent.Content = null;
            if (string.IsNullOrWhiteSpace(markdown)) return;

            var urlRoot = UrlRoot;
            var document = await Task.Run(
                () =>
                {
                    var doc = Markdig.Markdown.Parse(markdown, Pipeline);
                    foreach (var linkInline in doc.Descendants().OfType<LinkInline>())
                    {
                        if (!Uri.TryCreate(linkInline.Url, UriKind.Relative, out _)) continue;
                        var absoluteUrl = $"{urlRoot.TrimEnd('/')}/{linkInline.Url.TrimStart('/')}";
                        if (Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
                        {
                            linkInline.Url = uri.ToString();
                        }
                    }
                    return doc;
                }, cancellationToken);

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