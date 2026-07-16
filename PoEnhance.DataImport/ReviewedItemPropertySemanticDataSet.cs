using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed record ReviewedItemPropertySemanticDataSet
{
    public int SchemaVersion { get; init; }

    public string? ReviewVersion { get; init; }

    public IReadOnlyList<ItemPropertySemanticDescriptor> Descriptors { get; init; } = [];
}
