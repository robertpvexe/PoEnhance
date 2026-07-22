using System.Collections.Immutable;
using System.Globalization;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed record OfferCardPreviewPresentation
{
    public string? ItemName { get; init; }

    public string? TypeLine { get; init; }

    public string? BaseType { get; init; }

    public string? Rarity { get; init; }

    public OfferCardFrameKind? Frame { get; init; }

    public ImmutableArray<string> Influences { get; init; } = [];

    public ImmutableArray<OfferCardPreviewFact> Properties { get; init; } = [];

    public string? ItemLevel { get; init; }

    public ImmutableArray<OfferCardPreviewFact> Requirements { get; init; } = [];

    public string? RequirementsLine { get; init; }

    public string? Sockets { get; init; }

    public ImmutableArray<OfferCardPreviewFlag> Flags { get; init; } = [];

    public ImmutableArray<OfferCardPreviewModifierSection> ModifierSections { get; init; } = [];

    public string? Description { get; init; }

    public string? SecondaryDescription { get; init; }

    public ImmutableArray<string> FlavourText { get; init; } = [];

    public string? Price { get; init; }

    public string? Seller { get; init; }

    public string? Online { get; init; }

    public string? Listed { get; init; }

    public bool HasItemName => ItemName is not null;

    public bool HasTypeLine => TypeLine is not null;

    public bool HasBaseType => BaseType is not null;

    public bool HasRarity => Rarity is not null;

    public bool UsesSpecialFrameAccent => Frame is
        OfferCardFrameKind.DivinationCard or
        OfferCardFrameKind.Quest or
        OfferCardFrameKind.Prophecy or
        OfferCardFrameKind.Necropolis or
        OfferCardFrameKind.Gold or
        OfferCardFrameKind.BreachSkill;

    public bool UsesRelicAccent => Frame is
        OfferCardFrameKind.Foil or
        OfferCardFrameKind.SupporterFoil ||
        Flags.Any(flag => flag.Kind == OfferCardPreviewFlagKind.Relic);

    public bool HasInfluences => Influences.Length > 0;

    public bool HasProperties => Properties.Length > 0;

    public bool HasItemLevel => ItemLevel is not null;

    public bool HasRequirements => Requirements.Length > 0;

    public bool HasRequirementsLine => RequirementsLine is not null;

    public bool HasSockets => Sockets is not null;

    public bool HasFlags => Flags.Length > 0;

    public bool HasModifierSections => ModifierSections.Length > 0;

    public bool HasDescription => Description is not null;

    public bool HasSecondaryDescription => SecondaryDescription is not null;

    public bool HasFlavourText => FlavourText.Length > 0;

    public bool HasDescriptionContent => HasDescription ||
        HasSecondaryDescription ||
        HasFlavourText;

    public bool HasPrice => Price is not null;

    public bool HasSeller => Seller is not null;

    public bool HasOnline => Online is not null;

    public bool HasListed => Listed is not null;

    public bool HasTradeFooter => Price is not null ||
        Seller is not null ||
        Online is not null ||
        Listed is not null;

    public static OfferCardPreviewPresentation FromSnapshot(
        OfferCardSnapshot snapshot,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var typeLine = DisplayText(snapshot.TypeLine);
        var baseType = DisplayText(snapshot.BaseType);
        if (string.Equals(typeLine, baseType, StringComparison.Ordinal))
        {
            baseType = null;
        }

        var requirements = snapshot.Requirements
            .Select(CreateFact)
            .Where(fact => fact.HasLabel || fact.HasValue || fact.HasProgress)
            .ToImmutableArray();

        return new OfferCardPreviewPresentation
        {
            ItemName = DisplayText(snapshot.Name),
            TypeLine = typeLine,
            BaseType = baseType,
            Rarity = UsefulRarity(snapshot.Rarity),
            Frame = snapshot.Frame,
            Influences = CreateInfluences(snapshot.Influences),
            Properties = snapshot.Properties
                .Select(CreateFact)
                .Where(fact => fact.HasLabel || fact.HasValue || fact.HasProgress)
                .ToImmutableArray(),
            ItemLevel = snapshot.ItemLevel?.ToString(CultureInfo.InvariantCulture),
            Requirements = requirements,
            RequirementsLine = FormatRequirements(requirements),
            Sockets = FormatSockets(snapshot.Sockets),
            Flags = CreateFlags(snapshot.Flags),
            ModifierSections = CreateModifierSections(snapshot.ModifierSections),
            Description = DisplayText(snapshot.Description),
            SecondaryDescription = DisplayText(snapshot.SecondaryDescription),
            FlavourText = snapshot.FlavourText
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToImmutableArray(),
            Price = FormatPrice(snapshot.Price),
            Seller = FormatSeller(snapshot.Seller),
            Online = FormatOnline(snapshot.Online),
            Listed = snapshot.IndexedAt.HasValue
                ? PriceCheckerRelativeTimeFormatter.Format(snapshot.IndexedAt, now)
                : null,
        };
    }

    private static OfferCardPreviewFact CreateFact(OfferCardProperty property)
    {
        var values = string.Join(
            " / ",
            property.Values
                .Select(value => value.Text)
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(property.Suffix) && values.Length > 0)
        {
            values += property.Suffix;
        }

        return new OfferCardPreviewFact(
            DisplayText(property.DisplayName),
            DisplayText(values),
            property.Progress is { } progress && double.IsFinite(progress)
                ? progress
                : null,
            property.Values.Any(value => value.DisplayStyleCode == 1));
    }

    private static ImmutableArray<OfferCardPreviewModifierSection> CreateModifierSections(
        ImmutableArray<OfferCardModifierSection> sourceSections)
    {
        var sections = sourceSections
            .Select(section => new
            {
                section.Provenance,
                Lines = section.Lines
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToImmutableArray(),
            })
            .Where(section => section.Lines.Length > 0)
            .ToArray();
        var result = ImmutableArray.CreateBuilder<OfferCardPreviewModifierSection>(sections.Length);
        var sawImplicit = false;
        var separatedRemainingModifiers = false;
        foreach (var section in sections)
        {
            var separatesRemainingModifiers = sawImplicit &&
                !separatedRemainingModifiers &&
                section.Provenance != OfferCardModifierProvenance.Implicit;
            result.Add(new OfferCardPreviewModifierSection(
                section.Provenance,
                ModifierLabel(section.Provenance),
                section.Lines,
                separatesRemainingModifiers));
            sawImplicit |= section.Provenance == OfferCardModifierProvenance.Implicit;
            separatedRemainingModifiers |= separatesRemainingModifiers;
        }

        return result.ToImmutable();
    }

    private static string? FormatRequirements(
        ImmutableArray<OfferCardPreviewFact> requirements)
    {
        var parts = requirements
            .Select(requirement =>
            {
                if (requirement.Label is null)
                {
                    return requirement.Value;
                }

                if (requirement.Value is null)
                {
                    return requirement.Label;
                }

                return requirement.Label.Equals("Level", StringComparison.OrdinalIgnoreCase)
                    ? $"Level {requirement.Value}"
                    : $"{requirement.Value} {requirement.Label}";
            })
            .Where(part => part is not null)
            .ToArray();
        return parts.Length == 0 ? null : $"Requires {string.Join(", ", parts)}";
    }

    private static ImmutableArray<string> CreateInfluences(OfferCardInfluenceFacts influences)
    {
        var values = ImmutableArray.CreateBuilder<string>();
        AddWhenTrue(values, influences.Shaper, "Shaper");
        AddWhenTrue(values, influences.Elder, "Elder");
        AddWhenTrue(values, influences.Crusader, "Crusader");
        AddWhenTrue(values, influences.Hunter, "Hunter");
        AddWhenTrue(values, influences.Redeemer, "Redeemer");
        AddWhenTrue(values, influences.Warlord, "Warlord");
        AddWhenTrue(values, influences.Searing, "Searing Exarch");
        AddWhenTrue(values, influences.Tangled, "Eater of Worlds");
        return values.ToImmutable();
    }

    private static ImmutableArray<OfferCardPreviewFlag> CreateFlags(OfferCardItemFlags flags)
    {
        var values = ImmutableArray.CreateBuilder<OfferCardPreviewFlag>();
        AddFlagWhenTrue(values, flags.Corrupted, "Corrupted", OfferCardPreviewFlagKind.Corrupted);
        AddFlagWhenTrue(values, flags.Mirrored, "Mirrored", OfferCardPreviewFlagKind.Mirrored);
        AddFlagWhenTrue(values, flags.Synthesised, "Synthesised", OfferCardPreviewFlagKind.Special);
        AddFlagWhenTrue(values, flags.Fractured, "Fractured", OfferCardPreviewFlagKind.Fractured);
        AddFlagWhenTrue(values, flags.Split, "Split", OfferCardPreviewFlagKind.Default);
        AddFlagWhenTrue(values, flags.Duplicated, "Duplicated", OfferCardPreviewFlagKind.Default);
        AddFlagWhenTrue(values, flags.Replica, "Replica", OfferCardPreviewFlagKind.Special);
        AddFlagWhenTrue(values, flags.Veiled, "Veiled", OfferCardPreviewFlagKind.Special);
        AddFlagWhenTrue(values, flags.IsRelic, "Relic", OfferCardPreviewFlagKind.Relic);
        AddFlagWhenTrue(values, flags.Ruthless, "Ruthless", OfferCardPreviewFlagKind.Default);
        return values.ToImmutable();
    }

    private static string? FormatSockets(ImmutableArray<OfferCardSocket> sockets)
    {
        if (sockets.Length == 0)
        {
            return null;
        }

        var result = new System.Text.StringBuilder("Sockets: ");
        for (var index = 0; index < sockets.Length; index++)
        {
            var socket = sockets[index];
            result.Append(SocketText(socket.Colour, socket.Attribute));
            if (index + 1 < sockets.Length)
            {
                result.Append(socket.Group == sockets[index + 1].Group ? '-' : ' ');
            }
        }

        return result.ToString();
    }

    private static char SocketText(string? colour, string? attribute)
    {
        var providerColour = DisplayText(colour)?.ToUpperInvariant();
        if (providerColour is not null)
        {
            return providerColour switch
            {
                "R" or "RED" => 'R',
                "G" or "GREEN" => 'G',
                "B" or "BLUE" => 'B',
                "W" or "WHITE" => 'W',
                "A" or "ABYSS" => 'A',
                _ => '?',
            };
        }

        return DisplayText(attribute)?.ToUpperInvariant() switch
        {
            "S" or "STRENGTH" => 'R',
            "D" or "DEXTERITY" => 'G',
            "I" or "INTELLIGENCE" => 'B',
            "G" or "GENERIC" => 'W',
            "A" or "ABYSS" => 'A',
            _ => '?',
        };
    }

    private static string? FormatPrice(OfferCardPrice? price)
    {
        if (price is null)
        {
            return null;
        }

        var amount = price.Amount?.ToString("0.############################", CultureInfo.InvariantCulture);
        var currency = DisplayText(price.Currency);
        return DisplayText(string.Join(" ", new[] { amount, currency }
            .Where(value => value is not null)));
    }

    private static string? FormatSeller(OfferCardSeller seller)
    {
        var account = DisplayText(seller.AccountName);
        var character = DisplayText(seller.LastCharacterName);
        if (account is null)
        {
            return character;
        }

        return character is null || string.Equals(account, character, StringComparison.Ordinal)
            ? account
            : $"{account} · {character}";
    }

    private static string? FormatOnline(OfferCardOnlineState? online)
    {
        if (online is null)
        {
            return null;
        }

        return DisplayText(string.Join(" · ", new[]
        {
            DisplayText(online.Status),
            DisplayText(online.League),
        }.Where(value => value is not null)));
    }

    private static string ModifierLabel(OfferCardModifierProvenance provenance) => provenance switch
    {
        OfferCardModifierProvenance.Enchant => "Enchant",
        OfferCardModifierProvenance.Implicit => "Implicit",
        OfferCardModifierProvenance.Explicit => "Explicit",
        OfferCardModifierProvenance.Crafted => "Crafted",
        OfferCardModifierProvenance.Fractured => "Fractured",
        OfferCardModifierProvenance.Utility => "Utility",
        OfferCardModifierProvenance.Cosmetic => "Cosmetic",
        _ => provenance.ToString(),
    };

    private static void AddWhenTrue(
        ImmutableArray<string>.Builder values,
        bool? condition,
        string value)
    {
        if (condition == true)
        {
            values.Add(value);
        }
    }

    private static void AddFlagWhenTrue(
        ImmutableArray<OfferCardPreviewFlag>.Builder values,
        bool? condition,
        string label,
        OfferCardPreviewFlagKind kind)
    {
        if (condition == true)
        {
            values.Add(new OfferCardPreviewFlag(label, kind));
        }
    }

    private static string? UsefulRarity(string? rarity)
    {
        var value = DisplayText(rarity);
        return value is not null &&
            (value.Equals("Normal", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("Magic", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("Rare", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("Unique", StringComparison.OrdinalIgnoreCase))
            ? null
            : value;
    }

    private static string? DisplayText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

internal sealed record OfferCardPreviewFact(
    string? Label,
    string? Value,
    double? Progress,
    bool IsAugmented)
{
    public bool HasLabel => Label is not null;

    public bool HasValue => Value is not null;

    public bool HasProgress => Progress.HasValue;
}

internal sealed record OfferCardPreviewModifierSection(
    OfferCardModifierProvenance Provenance,
    string Label,
    ImmutableArray<string> Lines,
    bool HasSeparatorBefore);

internal sealed record OfferCardPreviewFlag(string Label, OfferCardPreviewFlagKind Kind);

internal enum OfferCardPreviewFlagKind
{
    Default,
    Corrupted,
    Mirrored,
    Fractured,
    Special,
    Relic,
}
