using IconPacks.Avalonia.EvaIcons;

namespace Nodis.Interfaces;

public interface IMainWindowPage
{
    string? Title { get; }
    PackIconEvaIconsKind Icon { get; }
}