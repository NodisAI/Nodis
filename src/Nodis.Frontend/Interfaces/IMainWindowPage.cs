using IconPacks.Avalonia.EvaIcons;

namespace Nodis.Frontend.Interfaces;

public interface IMainWindowPage
{
    string? Title { get; }
    PackIconEvaIconsKind Icon { get; }
}