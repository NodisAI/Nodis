using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Nodis.Extensions;

public static class VisualExtension
{
    public static bool IsChildOf(this StyledElement? child, StyledElement parent, StyledElement? stopAt = null)
    {
        while (child is not null and not TopLevel)
        {
            if (child == stopAt)
            {
                return false;
            }
            if (child == parent)
            {
                return true;
            }

            child = child.Parent;
        }
        return false;
    }

    public static IEnumerable<TTarget> EnumerateAncestors<TTarget>(this StyledElement? child, string? targetName = null) where TTarget : StyledElement
    {
        while (child is not null)
        {
            if (child is TTarget t && (targetName is null || t.Name == targetName))
            {
                yield return t;
            }

            child = child.Parent;
        }
    }

    public static TTarget? FindParent<TTarget>(this StyledElement? child, string? targetName = null) where TTarget : StyledElement =>
        EnumerateAncestors<TTarget>(child, targetName).FirstOrDefault();

    /// <summary>
    /// 寻找类型为TTarget的UI元素，如果遇到TStop类型的元素则停止寻找，返回null
    /// </summary>
    /// <typeparam name="TTarget"></typeparam>
    /// <typeparam name="TStopAt"></typeparam>
    /// <param name="child"></param>
    /// <param name="targetName"></param>
    /// <returns></returns>
    // ReSharper disable once InconsistentNaming
    public static TTarget? FindParent<TTarget, TStopAt>(this StyledElement? child, string? targetName = null)
        where TTarget : StyledElement where TStopAt : StyledElement
    {
        foreach (var parent in EnumerateAncestors<StyledElement>(child))
        {
            if (parent is TStopAt)
            {
                return null;
            }

            if (parent is TTarget t && (targetName is null || t.Name == targetName))
            {
                return t;
            }
        }

        return null;
    }

    public static IEnumerable<TTarget> EnumerateDescendants<TTarget>(this StyledElement? parent, string? targetName = null) where TTarget : StyledElement
    {
        if (parent is null) yield break;

        foreach (var child in parent.GetLogicalChildren())
        {
            switch (child)
            {
                case TTarget t when targetName is null || t.Name == targetName:
                {
                    yield return t;
                    break;
                }
                case StyledElement styledElement:
                {
                    foreach (var descendant in EnumerateDescendants<TTarget>(styledElement, targetName))
                    {
                        yield return descendant;
                    }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 递归寻找子元素，深度优先
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="targetName"></param>
    /// <typeparam name="TTarget"></typeparam>
    /// <returns></returns>
    public static TTarget? FindChild<TTarget>(this StyledElement? parent, string? targetName = null) where TTarget : StyledElement =>
        EnumerateDescendants<TTarget>(parent, targetName).FirstOrDefault();

    public static ScrollViewer GetScrollViewer(this ItemsControl itemsControl) =>
        itemsControl.FindChild<ScrollViewer>() ?? throw new InvalidOperationException("ScrollViewer not found");
}