using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Skia;
using Nodis.Extensions;
using Nodis.Models;
using Nodis.Models.Workflow;

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

    public WorkflowEditor()
    {
        InitializeComponent();

        WorkflowContext = new WorkflowContext();

        canvasTransformGroup.Children.Add(canvasScaleTransform);
        canvasTransformGroup.Children.Add(canvasTranslateTransform);
        NodeCanvas.RenderTransform = canvasTransformGroup;

        backgroundTransformGroup.Children.Add(backgroundScaleTransform);
        backgroundTransformGroup.Children.Add(backgroundTranslateTransform);
        SquareMeshBackground.RenderTransform = backgroundTransformGroup;
    }

    #region Transform

    private Point rightButtonPressedPoint;
    private readonly TransformGroup canvasTransformGroup = new();
    private readonly TranslateTransform canvasTranslateTransform = new();
    private readonly ScaleTransform canvasScaleTransform = new();
    private readonly TransformGroup backgroundTransformGroup = new();
    private readonly TranslateTransform backgroundTranslateTransform = new();
    private readonly ScaleTransform backgroundScaleTransform = new();

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
                canvasTranslateTransform.X += point.Position.X - rightButtonPressedPoint.X;
                canvasTranslateTransform.Y += point.Position.Y - rightButtonPressedPoint.Y;
                // ConstrainNodeTranslate();
                CalculateBackgroundTransform();
                rightButtonPressedPoint = point.Position;
                e.Handled = true;
            }
            else if ((point.Position - rightButtonPressedPoint).ToSKPoint().LengthSquared > 25f)
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
        canvasScaleTransform.ScaleX *= scaleFactor;
        canvasScaleTransform.ScaleY *= scaleFactor;

        var mousePositionAfterTransform = e.GetPosition(NodeCanvas);

        // Adjust the translation so that the point under the mouse remains in the same position
        canvasTranslateTransform.X -= (mousePositionBeforeTransform.X - mousePositionAfterTransform.X) * canvasScaleTransform.ScaleX;
        canvasTranslateTransform.Y -= (mousePositionBeforeTransform.Y - mousePositionAfterTransform.Y) * canvasScaleTransform.ScaleY;
        // ConstrainNodeTranslate();
        CalculateBackgroundTransform();

        e.Handled = true;

        base.OnPointerWheelChanged(e);
    }

    private void CalculateBackgroundTransform()
    {
        // BUG: The background is not constrained to the canvas

        var scaleX = canvasScaleTransform.ScaleX;
        var scaleY = canvasScaleTransform.ScaleY;
        backgroundScaleTransform.ScaleX = scaleX;
        backgroundScaleTransform.ScaleY = scaleY;

        var xConstraint = 90 * scaleX;
        var yConstraint = 90 * scaleY;

        var x = canvasTranslateTransform.X;
        var y = canvasTranslateTransform.Y;
        // keep x in (-xConstraint, 0)
        if (x > 0) x -= ((int)(x / xConstraint) + 1) * xConstraint;
        else if (x < -xConstraint) x += ((int)(-x / xConstraint) + 1) * xConstraint;
        // keep y in (-yConstraint, 0)
        if (y > 0) y -= ((int)(y / yConstraint) + 1) * yConstraint;
        else if (y < -yConstraint) y += ((int)(-y / yConstraint) + 1) * yConstraint;

        backgroundTranslateTransform.X = x;
        backgroundTranslateTransform.Y = y;

        // we need to ensure that the background's bottom right corner is always visible
        var bounds = Bounds;
        SquareMeshBackground.MinWidth = bounds.Width / scaleX + x;
        SquareMeshBackground.MinHeight = bounds.Height / scaleY + y;
    }

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == WorkflowContextProperty)
        {
            if (change.OldValue is WorkflowContext oldContext)
            {
                oldContext.Nodes.CollectionChanged -= HandleNodesCollectionChanged;
            }

            if (change.NewValue is WorkflowContext newContext)
            {
                newContext.Nodes.CollectionChanged += HandleNodesCollectionChanged;
            }
        }

        base.OnPropertyChanged(change);
    }

    private record WorkflowNodeItemWrapper(WorkflowNodeItem Item, IDisposable Disposable);
    private readonly Dictionary<WorkflowNode, WorkflowNodeItemWrapper> nodeMap = new();

    private void HandleNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                HandleNodeAdded((WorkflowNode)e.NewItems![0]!);
                break;
            }
            case NotifyCollectionChangedAction.Remove:
            {
                HandleNodeRemoved((WorkflowNode)e.OldItems![0]!);
                break;
            }
            case NotifyCollectionChangedAction.Replace:
            {
                HandleNodeRemoved((WorkflowNode)e.OldItems![0]!);
                HandleNodeAdded((WorkflowNode)e.NewItems![0]!);
                break;
            }
        }
    }

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
            item.Node.X += (currentPoint.X - workflowNodeItemPointerPressedPoint.Value.X) / canvasScaleTransform.ScaleX;
            item.Node.Y += (currentPoint.Y - workflowNodeItemPointerPressedPoint.Value.Y) / canvasScaleTransform.ScaleY;
            workflowNodeItemPointerPressedPoint = currentPoint;
            e.Handled = true;
        }
    }

    private void HandleWorkflowNodeItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        workflowNodeItemPointerPressedPoint = null;
    }

    private void HandleMenuItemOnClick(object? sender, RoutedEventArgs e)
    {
        if (WorkflowContext is not { } ctx || sender is not MenuItem menuItem) return;
        var position = this.TranslatePoint(rightButtonPressedPoint, NodeCanvas) ?? new Point();
        WorkflowNode node = menuItem.Header switch
        {
            "Condition" => new WorkflowConditionNode(),
            "Constant" => new WorkflowConstantNode(),
            _ => throw new NotImplementedException()
        };
        node.X = position.X;
        node.Y = position.Y;
        ctx.Nodes.Add(node);
    }
}