namespace PoEnhance.App.Features.PriceChecking;

internal sealed record OfferCardModifierPipelineSource
{
    public bool RawFetchOfferPresent { get; init; }

    public OfferCardModifierCounts RawJson { get; init; } = new();

    public OfferCardModifierCounts ParsedDto { get; init; } = new();
}

internal sealed record OfferCardModifierCounts
{
    public int Enchant { get; init; }

    public int Implicit { get; init; }

    public int Explicit { get; init; }

    public int Crafted { get; init; }

    public int Fractured { get; init; }

    public int Utility { get; init; }

    public int Cosmetic { get; init; }

    public int Total => Enchant + Implicit + Explicit + Crafted + Fractured + Utility + Cosmetic;

    public static OfferCardModifierCounts FromSnapshot(OfferCardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return FromSections(snapshot.ModifierSections.Select(section =>
            (section.Provenance, section.Lines.Length)));
    }

    public static OfferCardModifierCounts FromPresentation(OfferCardPreviewPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        return FromSections(presentation.ModifierSections.Select(section =>
            (section.Provenance, section.Lines.Length)));
    }

    private static OfferCardModifierCounts FromSections(
        IEnumerable<(OfferCardModifierProvenance Provenance, int Count)> sections)
    {
        var counts = sections
            .GroupBy(section => section.Provenance)
            .ToDictionary(group => group.Key, group => group.Sum(section => section.Count));
        return new OfferCardModifierCounts
        {
            Enchant = Count(OfferCardModifierProvenance.Enchant),
            Implicit = Count(OfferCardModifierProvenance.Implicit),
            Explicit = Count(OfferCardModifierProvenance.Explicit),
            Crafted = Count(OfferCardModifierProvenance.Crafted),
            Fractured = Count(OfferCardModifierProvenance.Fractured),
            Utility = Count(OfferCardModifierProvenance.Utility),
            Cosmetic = Count(OfferCardModifierProvenance.Cosmetic),
        };

        int Count(OfferCardModifierProvenance provenance) =>
            counts.GetValueOrDefault(provenance);
    }
}

internal sealed record OfferCardModifierPipelineDiagnostic
{
    public required string OfferId { get; init; }

    public string? ItemName { get; init; }

    public string? TypeLine { get; init; }

    public bool RawFetchOfferPresent { get; init; }

    public OfferCardModifierCounts RawJson { get; init; } = new();

    public OfferCardModifierCounts ParsedDto { get; init; } = new();

    public OfferCardModifierCounts Snapshot { get; init; } = new();

    public OfferCardModifierCounts Presentation { get; init; } = new();

    public int WpfModifierLineViewModels { get; init; }

    public static OfferCardModifierPipelineDiagnostic Create(
        OfferCardSnapshot snapshot,
        OfferCardPreviewPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(presentation);

        var presentationCounts = OfferCardModifierCounts.FromPresentation(presentation);
        return new OfferCardModifierPipelineDiagnostic
        {
            OfferId = snapshot.OfferId,
            ItemName = snapshot.Name,
            TypeLine = snapshot.TypeLine,
            RawFetchOfferPresent = snapshot.ModifierPipelineSource.RawFetchOfferPresent,
            RawJson = snapshot.ModifierPipelineSource.RawJson,
            ParsedDto = snapshot.ModifierPipelineSource.ParsedDto,
            Snapshot = OfferCardModifierCounts.FromSnapshot(snapshot),
            Presentation = presentationCounts,
            WpfModifierLineViewModels = presentationCounts.Total,
        };
    }
}
