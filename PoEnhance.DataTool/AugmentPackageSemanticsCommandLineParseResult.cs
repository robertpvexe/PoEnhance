using PoEnhance.DataImport;

namespace PoEnhance.DataTool;

public sealed record AugmentPackageSemanticsCommandLineParseResult
{
    public GameDataPackageSemanticAugmentationRequest? Request { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool IsValid => Request is not null && Errors.Count == 0;
}
