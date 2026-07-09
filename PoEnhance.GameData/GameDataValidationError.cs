namespace PoEnhance.GameData;

public sealed record GameDataValidationError(string Code, string Path, string Message);
