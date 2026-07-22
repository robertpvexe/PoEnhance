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

    public string? Sockets { get; init; }

    public ImmutableArray<string> Flags { get; init; } = [];

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

    public bool HasInfluences => Influences.Length > 0;

    public bool HasProperties => Properties.Length > 0;

    public bool HasItemLevel => ItemLevel is not null;

    public bool HasRequirements => Requirements.Length > 0;

    public bool HasSockets => Sockets is not null;

    public bool HasFlags => Flags.Length > 0;

    public bool HasModifierSections => ModifierSections.Length > 0;

    public bool HasDescription => Description is not null;

    public bool HasSecondaryDescription => SecondaryDescription is not null;

    public bool HasFlavourText => FlavourText.Length > 0;

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

        return new OfferCardPreviewPresentation
        {
            ItemName = DisplayText(snapshot.Name),
            TypeLine = typeLine,
            BaseType = baseType,
            Rarity = DisplayText(snapshot.Rarity),
            Frame = snapshot.Frame,
            Influences = CreateInfluences(snapshot.Influences),
            Properties = snapshot.Properties
                .Select(CreateFact)
                .Where(fact => fact.HasLabel || fact.HasValue || fact.HasProgress)
                .ToImmutableArray(),
            ItemLevel = snapshot.ItemLevel?.ToString(CultureInfo.InvariantCulture),
            Requirements = snapshot.Requirements
                .Select(CreateFact)
                .Where(fact => fact.HasLabel || fact.HasValue || fact.HasProgress)
                .ToImmutableArray(),
            Sockets = FormatSockets(snapshot.Sockets),
            Flags = CreateFlags(snapshot.Flags),
            ModifierSections = snapshot.ModifierSections
                .Select(CreateModifierSection)
                .Where(section => section.Lines.Length > 0)
                .ToImmutableArray(),
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
                : null);
    }

    private static OfferCardPreviewModifierSection CreateModifierSection(
        OfferCardModifierSection section)
    {
        return new OfferCardPreviewModifierSection(
            section.Provenance,
            ModifierLabel(section.Provenance),
            section.Lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToImmutableArray());
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

    private static ImmutableArray<string> CreateFlags(OfferCardItemFlags flags)
    {
        var values = ImmutableArray.CreateBuilder<string>();
        AddWhenTrue(values, flags.Corrupted, "Corrupted");
        AddWhenTrue(values, flags.Mirrored, "Mirrored");
        AddWhenTrue(values, flags.Synthesised, "Synthesised");
        AddWhenTrue(values, flags.Fractured, "Fractured");
        AddWhenTrue(values, flags.Split, "Split");
        AddWhenTrue(values, flags.Duplicated, "Duplicated");
        AddWhenTrue(values, flags.Replica, "Replica");
        AddWhenTrue(values, flags.Veiled, "Veiled");
        AddWhenTrue(values, flags.IsRelic, "Relic");
        AddWhenTrue(values, flags.Ruthless, "Ruthless");
        return values.ToImmutable();
    }

    private static string? FormatSockets(ImmutableArray<OfferCardSocket> sockets)
    {
        var groupOrder = new List<int>();
        var groups = new Dictionary<int, List<string>>();
        foreach (var socket in sockets.OrderBy(socket => socket.Index))
        {
            if (!groups.TryGetValue(socket.Group, out var group))
            {
                group = [];
                groups.Add(socket.Group, group);
                groupOrder.Add(socket.Group);
            }

            var display = DisplayText(socket.Colour) ?? DisplayText(socket.Attribute);
            if (display is not null)
            {
                group.Add(display);
            }
        }

        var renderedGroups = groupOrder
            .Select(group => string.Join("-", groups[group]))
            .Where(group => group.Length > 0)
            .ToArray();
        return renderedGroups.Length == 0
            ? null
            : string.Join("  ", renderedGroups);
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

    private static string? DisplayText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

internal sealed record OfferCardPreviewFact(string? Label, string? Value, double? Progress)
{
    public bool HasLabel => Label is not null;

    public bool HasValue => Value is not null;

    public bool HasProgress => Progress.HasValue;
}

internal sealed record OfferCardPreviewModifierSection(
    OfferCardModifierProvenance Provenance,
    string Label,
    ImmutableArray<string> Lines);
