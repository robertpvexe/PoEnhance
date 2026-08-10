namespace PoEnhance.GameData;

public sealed record StatTranslationCompatibilityChange
{
    public string? Id { get; init; }

    public string? CurrentObservationId { get; init; }

    public string? HistoricalObservationId { get; init; }

    public StatTranslationCompatibilityClassification Classification { get; init; }

    public StatTranslationRuntimeRelevance RuntimeRelevance { get; init; }

    public bool ParserRisk { get; init; }

    public bool CanonicalizationRisk { get; init; }

    public bool NumericShapeRisk { get; init; }

    public bool ChangesRuntimeBehaviorInT3A { get; init; }

    public bool RequiresProviderWorkInT3B { get; init; }

    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}
