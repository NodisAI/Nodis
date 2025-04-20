namespace Nodis.Core.Interfaces;

public interface INamedObject
{
    string Name { get; }

    string? Description { get; }
}