namespace PoEnhance.GameData;

public sealed record GameDataValidationResult(IReadOnlyList<GameDataValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
