using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Nodis.Extensions;
using Nodis.Models;
using Nodis.Models.Workflow;
using ObservableCollections;
using VYaml.Serialization;

namespace Nodis.Views.Workflow;

public partial class WorkflowEditor : UserControl
{
    public static readonly StyledProperty<WorkflowContext?> WorkflowContextProperty =
        AvaloniaProperty.Register<WorkflowEditor, WorkflowContext?>(nameof(WorkflowContext));

    public WorkflowContext? WorkflowContext
    {
        get => GetValue(WorkflowContextProperty);
        set => SetValue(WorkflowContextProperty, value);
    }

    private readonly ObservableDictionary<WorkflowNodePortConnection, WorkflowNodePortConnectionItem> connectionItems = [];

    public WorkflowEditor()
    {
        InitializeComponent();

        WorkflowContext = new WorkflowContext();  // todo: delete me, just for debug

        ConnectionItemsControl.ItemsSource = connectionItems.CreateView(p => p.Value)
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

        transformGroup.Children.Add(scaleTransform);
        transformGroup.Children.Add(translateTransform);
        TransformRoot.RenderTransform = transformGroup;

        gridDrawingBrush = GridBorder.Background.NotNull<DrawingBrush>();
        compactGridDrawingBrush = CompactGridBorder.Background.NotNull<DrawingBrush>();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != WorkflowContextProperty) return;
        connectionItems.Clear();
        foreach (var wrapper in nodeMap.Values) wrapper.Disposable.Dispose();
        if (change.NewValue is not WorkflowContext newCtx) return;
        foreach (var node in newCtx.Nodes) HandleNodeAdded(node);
        foreach (var connection in newCtx.Connections) HandleConnectionAdded(connection);
    }

    #region Transform

    private Point rightButtonPressedPoint;
    private readonly TransformGroup transformGroup = new();
    private readonly TranslateTransform translateTransform = new();
    private readonly ScaleTransform scaleTransform = new();
    private readonly DrawingBrush gridDrawingBrush, compactGridDrawingBrush;
    private const double InitialGridViewSize = 90d;
    private const int CompactGridScale = 12;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
        {
            rightButtonPressedPoint = point.Position;
            e.Handled = true;
        }

        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
        {
            if (Equals(e.Pointer.Captured, this))
            {
                translateTransform.X += point.Position.X - rightButtonPressedPoint.X;
                translateTransform.Y += point.Position.Y - rightButtonPressedPoint.Y;
                // ConstrainNodeTranslate();
                CalculateBackgroundTransform();
                rightButtonPressedPoint = point.Position;
                e.Handled = true;
            }
            else if ((point.Position - rightButtonPressedPoint).LengthSquared() > 25d)
            {
                Focus();
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (Equals(e.Pointer.Captured, this))
        {
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        base.OnPointerReleased(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var scaleFactor = e.Delta.Y > 0 ? 1.1 : 0.9;

        var mousePositionBeforeTransform = e.GetPosition(NodeCanvas);

        // Perform the scaling
        scaleTransform.ScaleX *= scaleFactor;
        scaleTransform.ScaleY *= scaleFactor;

        var mousePositionAfterTransform = e.GetPosition(NodeCanvas);

        // Adjust the translation so that the point under the mouse remains in the same position
        translateTransform.X -= (mousePositionBeforeTransform.X - mousePositionAfterTransform.X) * scaleTransform.ScaleX;
        translateTransform.Y -= (mousePositionBeforeTransform.Y - mousePositionAfterTransform.Y) * scaleTransform.ScaleY;
        // ConstrainNodeTranslate();
        CalculateBackgroundTransform();

        e.Handled = true;

        base.OnPointerWheelChanged(e);
    }

    private void CalculateBackgroundTransform()
    {
        var scaleX = scaleTransform.ScaleX;
        var scaleY = scaleTransform.ScaleY;
        var (x, y) = NodeCanvas.TranslatePoint(default, GridBorder) ?? default;

        gridDrawingBrush.DestinationRect = new RelativeRect(
            x,
            y,
            InitialGridViewSize * scaleX,
            InitialGridViewSize * scaleY,
            RelativeUnit.Absolute);
        compactGridDrawingBrush.DestinationRect = new RelativeRect(
            x,
            y,
            InitialGridViewSize * scaleX / CompactGridScale,
            InitialGridViewSize * scaleY / CompactGridScale,
            RelativeUnit.Absolute);
    }

    #endregion

    #region NodeItem

    private record WorkflowNodeItemWrapper(WorkflowNodeItem Item, IDisposable Disposable);
    private readonly Dictionary<WorkflowNode, WorkflowNodeItemWrapper> nodeMap = new();

    private void HandleNodeAdded(WorkflowNode node)
    {
        var item = new WorkflowNodeItem(node);
        item.SetValue(Canvas.LeftProperty, node.X);
        item.SetValue(Canvas.TopProperty, node.Y);
        var bindX = item.Bind(Canvas.LeftProperty, node.ObservesProperty(n => n.X, nameof(node.X)));
        var bindY = item.Bind(Canvas.TopProperty, node.ObservesProperty(n => n.Y, nameof(node.Y)));
        item.PointerPressed += HandleWorkflowNodeItemPointerPressed;
        item.PointerMoved += HandleWorkflowNodeItemPointerMoved;
        item.PointerReleased += HandleWorkflowNodeItemPointerReleased;
        item.PortEvent += HandleWorkflowNodeItemPortEvent;
        NodeCanvas.Children.Add(item);

        nodeMap.Add(node, new WorkflowNodeItemWrapper(
            item,
            new AnonymousDisposable(
                () =>
                {
                    nodeMap.Remove(node);
                    NodeCanvas.Children.Remove(item);
                    bindX.Dispose();
                    bindY.Dispose();
                    item.PointerPressed -= HandleWorkflowNodeItemPointerPressed;
                    item.PointerMoved -= HandleWorkflowNodeItemPointerMoved;
                    item.PointerReleased -= HandleWorkflowNodeItemPointerReleased;
                    item.PortEvent -= HandleWorkflowNodeItemPortEvent;
                })));
    }

    private void HandleNodeRemoved(WorkflowNode node)
    {
        if (!nodeMap.TryGetValue(node, out var wrapper)) return;
        wrapper.Disposable.Dispose();
    }

    private Point? workflowNodeItemPointerPressedPoint;

    private void HandleWorkflowNodeItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            workflowNodeItemPointerPressedPoint = point.Position;
            e.Handled = true;
        }
    }

    private void HandleWorkflowNodeItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not WorkflowNodeItem item) return;
        if (workflowNodeItemPointerPressedPoint != null)
        {
            var currentPoint = e.GetPosition(this);
            item.Node.X += (currentPoint.X - workflowNodeItemPointerPressedPoint.Value.X) / scaleTransform.ScaleX;
            item.Node.Y += (currentPoint.Y - workflowNodeItemPointerPressedPoint.Value.Y) / scaleTransform.ScaleY;
            workflowNodeItemPointerPressedPoint = currentPoint;
            e.Handled = true;
        }
    }

    private void HandleWorkflowNodeItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        workflowNodeItemPointerPressedPoint = null;
    }

    private void HandleWorkflowNodeItemPortEvent(WorkflowNodeItem sender, WorkflowNodeItemPortEventArgs e)
    {
        if (WorkflowContext == null) return;

        switch (e.Type)
        {
            case WorkflowNodeItemPortEventType.Dragging:
            {
                var startPoint = sender.TranslatePoint(sender.GetPortRelativePoint(e.StartPort), TransformRoot) ?? default;
                var endPoint = e.PointerEventArgs.GetPosition(TransformRoot);
                PreviewConnectionPath.StrokeThickness = e.StartPort.StrokeThickness;
                PreviewConnectionPath.IsVisible = true;
                UpdatePreviewConnectionPath(e.StartPort.Color, e.StartPort.Color, startPoint, endPoint);
                break;
            }
            case WorkflowNodeItemPortEventType.Drop:
            {
                PreviewConnectionPath.IsVisible = false;
                break;
            }
            case WorkflowNodeItemPortEventType.Connecting:
            {
                var startPoint = sender.TranslatePoint(sender.GetPortRelativePoint(e.StartPort), TransformRoot) ?? default;
                var targetNode = nodeMap[e.EndPort!.Owner!].Item;
                var endPoint = targetNode.TranslatePoint(targetNode.GetPortRelativePoint(e.EndPort!), TransformRoot) ?? default;
                PreviewConnectionPath.IsVisible = true;
                UpdatePreviewConnectionPath(e.StartPort.Color, e.EndPort!.Color, startPoint, endPoint);
                break;
            }
            case WorkflowNodeItemPortEventType.Connected:
            {
                PreviewConnectionPath.IsVisible = false;
                var connection = new WorkflowNodePortConnection(e.StartPort.Owner!.Id, e.StartPort.Id, e.EndPort!.Owner!.Id, e.EndPort.Id);
                if (WorkflowContext.AddConnection(connection) is { } previousConnection) HandleConnectionRemoved(previousConnection);
                HandleConnectionAdded(connection);
                break;
            }
        }

        void UpdatePreviewConnectionPath(Color startColor, Color endColor, Point startPoint, Point endPoint)
        {
            var gradientStops = PreviewConnectionPath.Stroke.To<LinearGradientBrush>()!.GradientStops;
            gradientStops[0].Color = startColor;
            gradientStops[1].Color = endColor;

            var midPointX = startPoint.X / 2 + endPoint.X / 2;
            var pathFigure = PreviewConnectionPath.Data.To<PathGeometry>()!.Figures![0].To<PathFigure>();
            pathFigure.StartPoint = startPoint;
            var bezierSegment = pathFigure.Segments![0].To<BezierSegment>();
            bezierSegment.Point1 = new Point(midPointX, startPoint.Y);
            bezierSegment.Point2 = new Point(midPointX, endPoint.Y);
            bezierSegment.Point3 = endPoint;
        }
    }


    #endregion

    #region ConnectionItem

    public partial class WorkflowNodePortConnectionItem : ObservableObject
    {
        public WorkflowNode OutputNode { get; }

        public WorkflowNode InputNode { get; }

        public WorkflowNodePort OutputPort { get; }

        public WorkflowNodePort InputPort { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Point1))]
        [NotifyPropertyChangedFor(nameof(Point2))]
        public partial Point StartPoint { get; private set; }

        public Point Point1 => new(StartPoint.X / 2 + EndPoint.X / 2, StartPoint.Y);

        public Point Point2 => new(StartPoint.X / 2 + EndPoint.X / 2, EndPoint.Y);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Point1))]
        [NotifyPropertyChangedFor(nameof(Point2))]
        public partial Point EndPoint { get; private set; }

        private readonly WorkflowNodeItem outputItem, inputItem;

        public WorkflowNodePortConnectionItem(WorkflowEditor editor, WorkflowNodePortConnection connection)
        {
            (OutputNode, (outputItem, _)) = editor.nodeMap.First(n => n.Key.Id == connection.OutputNodeId);
            OutputPort = OutputNode.GetOutputPort(connection.OutputPortId) ??
                throw new InvalidOperationException($"Start port not found: {connection.OutputPortId}");

            (InputNode, (inputItem, _)) = editor.nodeMap.First(n => n.Key.Id == connection.InputNodeId);
            InputPort = InputNode.GetInputPort(connection.InputPortId) ??
                throw new InvalidOperationException($"End port not found: {connection.InputPortId}");

            CalculateStartPoint();
            CalculateEndPoint();

            // since WorkflowNodePortConnectionItem's lifetime is shorter than WorkflowNodeItem, we don't need to unsubscribe
            OutputNode.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(WorkflowNode.X) or nameof(WorkflowNode.Y)) CalculateStartPoint();
            };
            InputNode.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(WorkflowNode.X) or nameof(WorkflowNode.Y)) CalculateEndPoint();
            };
            outputItem.SizeChanged += (_, _) => CalculateStartPoint();
            inputItem.SizeChanged += (_, _) => CalculateEndPoint();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CalculateStartPoint() =>
            StartPoint = outputItem.GetPortRelativePoint(OutputPort) + new Point(OutputNode.X, OutputNode.Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CalculateEndPoint() =>
            EndPoint = inputItem.GetPortRelativePoint(InputPort) + new Point(InputNode.X, InputNode.Y);
    }

    private void HandleConnectionAdded(WorkflowNodePortConnection connection)
    {
        connectionItems.Add(connection, new WorkflowNodePortConnectionItem(this, connection));
    }

    private void HandleConnectionRemoved(WorkflowNodePortConnection connection)
    {
        connectionItems.Remove(connection);
    }

    #endregion

    private void HandleMenuItemOnClick(object? sender, RoutedEventArgs e)
    {
        if (WorkflowContext is not { } ctx || sender is not MenuItem menuItem) return;
        var position = this.TranslatePoint(rightButtonPressedPoint, NodeCanvas) ?? new Point();
        WorkflowNode node = menuItem.Header switch
        {
            "Condition" => new WorkflowConditionNode(),
            "Constant" => new WorkflowConstantNode(),
            "Delay" => new WorkflowDelayNode(),
            _ => throw new NotImplementedException()
        };
        node.X = position.X;
        node.Y = position.Y;
        ctx.AddNode(node);
        HandleNodeAdded(node);
    }

    private async void HandleSaveButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (WorkflowContext is not { } ctx) return;
        await using var stream = File.OpenWrite("workflow.yaml");
        var memory = YamlSerializer.Serialize(ctx);
        await stream.WriteAsync(memory);
    }

    private async void HandleLoadButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await using var stream = File.OpenRead("workflow.yaml");
        WorkflowContext = await YamlSerializer.DeserializeAsync<WorkflowContext>(stream);
    }
}