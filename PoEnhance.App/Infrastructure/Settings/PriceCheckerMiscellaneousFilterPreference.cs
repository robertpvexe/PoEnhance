namespace PoEnhance.App.Infrastructure.Settings;

/// <summary>
/// Persisted visibility and order for one Miscellaneous control group.
/// <see cref="IsVisible"/> means the group should appear in Panel 2;
/// it does not enable or submit a Trade filter by itself.
/// </summary>
public sealed record PriceCheckerMiscellaneousFilterPreference(
    string Key,
    bool IsVisible,
    int Order);
