namespace PoEnhance.App.Infrastructure.Settings;

internal sealed record ApplicationLeagueSelection(
    string? ProviderId,
    string DisplayText)
{
    public bool IsLegacy => string.IsNullOrWhiteSpace(ProviderId);
}
