using System.Collections.Immutable;
using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class OfferCardPreviewPresentationTests
{
    [Fact]
    public void FromSnapshot_RendersOrderedFactsSocketsFlagsAndTradeFooter()
    {
        var listedAt = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        var snapshot = new OfferCardSnapshot
        {
            OfferId = "offer-1",
            Name = "Dusk Shell",
            TypeLine = "Titan Plate",
            BaseType = "Titan Plate",
            Rarity = "Rare",
            Frame = OfferCardFrameKind.Rare,
            ItemLevel = 84,
            Properties =
            [
                Property("Armour", ("1,234", 1)) with { Progress = 0.75 },
                Property("Quality", ("+20%", 1)),
            ],
            Requirements =
            [
                Property("Level", ("68", 0)),
                Property("Str", ("191", 1)),
            ],
            Sockets =
            [
                Socket(0, 0, "R"),
                Socket(1, 0, "G"),
                Socket(2, 1, "B"),
                Socket(3, 1, "W"),
            ],
            Flags = new OfferCardItemFlags
            {
                Corrupted = true,
                Synthesised = true,
                Replica = true,
            },
            Influences = new OfferCardInfluenceFacts
            {
                Shaper = true,
                Searing = true,
            },
            Description = "Description",
            SecondaryDescription = "Secondary",
            FlavourText = ["Flavour one", "Flavour two"],
            Price = new OfferCardPrice
            {
                Amount = 1.25m,
                Currency = "divine",
            },
            Seller = new OfferCardSeller
            {
                AccountName = "Account",
                LastCharacterName = "Character",
            },
            Online = new OfferCardOnlineState
            {
                Status = "online",
                League = "Mirage",
            },
            IndexedAt = listedAt,
        };

        var presentation = OfferCardPreviewPresentation.FromSnapshot(
            snapshot,
            listedAt.AddHours(2));

        Assert.Equal("Dusk Shell", presentation.ItemName);
        Assert.Equal("Titan Plate", presentation.TypeLine);
        Assert.Null(presentation.BaseType);
        Assert.Equal(["Shaper", "Searing Exarch"], presentation.Influences.ToArray());
        Assert.Equal(["Armour", "Quality"], presentation.Properties.Select(row => row.Label));
        Assert.Equal(["1,234", "+20%"], presentation.Properties.Select(row => row.Value));
        Assert.Equal(0.75, presentation.Properties[0].Progress);
        Assert.Equal("84", presentation.ItemLevel);
        Assert.Equal(["Level", "Str"], presentation.Requirements.Select(row => row.Label));
        Assert.Equal("R-G  B-W", presentation.Sockets);
        Assert.Equal(["Corrupted", "Synthesised", "Replica"], presentation.Flags.ToArray());
        Assert.Equal("1.25 divine", presentation.Price);
        Assert.Equal("Account · Character", presentation.Seller);
        Assert.Equal("online · Mirage", presentation.Online);
        Assert.Equal("2h ago", presentation.Listed);
        Assert.True(presentation.HasTradeFooter);
    }

    [Fact]
    public void FromSnapshot_MissingOptionalSectionsCollapseWithoutPlaceholders()
    {
        var presentation = OfferCardPreviewPresentation.FromSnapshot(
            new OfferCardSnapshot
            {
                OfferId = "minimal",
            },
            DateTimeOffset.UtcNow);

        Assert.False(presentation.HasItemName);
        Assert.False(presentation.HasTypeLine);
        Assert.False(presentation.HasBaseType);
        Assert.False(presentation.HasRarity);
        Assert.False(presentation.HasInfluences);
        Assert.False(presentation.HasProperties);
        Assert.False(presentation.HasItemLevel);
        Assert.False(presentation.HasRequirements);
        Assert.False(presentation.HasSockets);
        Assert.False(presentation.HasFlags);
        Assert.False(presentation.HasModifierSections);
        Assert.False(presentation.HasDescription);
        Assert.False(presentation.HasSecondaryDescription);
        Assert.False(presentation.HasFlavourText);
        Assert.False(presentation.HasTradeFooter);
        Assert.DoesNotContain("Unknown", presentation.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromSnapshot_ModifierProvenanceStaysSeparateAndOrdered()
    {
        var provenances = Enum.GetValues<OfferCardModifierProvenance>();
        var snapshot = new OfferCardSnapshot
        {
            OfferId = "mods",
            ModifierSections = provenances
                .Select(provenance => new OfferCardModifierSection
                {
                    Provenance = provenance,
                    Lines = [$"{provenance} line 1", $"{provenance} line 2"],
                })
                .ToImmutableArray(),
        };

        var presentation = OfferCardPreviewPresentation.FromSnapshot(
            snapshot,
            DateTimeOffset.UtcNow);

        Assert.Equal(provenances, presentation.ModifierSections.Select(section => section.Provenance));
        Assert.Equal(
            ["Enchant", "Implicit", "Explicit", "Crafted", "Fractured", "Utility", "Cosmetic"],
            presentation.ModifierSections.Select(section => section.Label));
        Assert.All(presentation.ModifierSections, section => Assert.Equal(2, section.Lines.Length));
    }

    [Fact]
    public void PreviewXamlKeepsHeaderAndTradeFooterOutsideScrollableContentAndCentralizesColours()
    {
        var xaml = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "ItemCardPreviewWindow.xaml"));

        Assert.Contains("x:Name=\"ContentScrollViewer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"460\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowActivated=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowInTaskbar=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Topmost=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TradeFooterBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("ModifierCraftedBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("ModifierFracturedBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("HasModifierSections", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Image", xaml, StringComparison.OrdinalIgnoreCase);
    }

    private static OfferCardProperty Property(
        string name,
        params (string Text, int Style)[] values)
    {
        return new OfferCardProperty
        {
            DisplayName = name,
            Values = values
                .Select(value => new OfferCardPropertyValue
                {
                    Text = value.Text,
                    DisplayStyleCode = value.Style,
                })
                .ToImmutableArray(),
        };
    }

    private static OfferCardSocket Socket(int index, int group, string colour)
    {
        return new OfferCardSocket
        {
            Index = index,
            Group = group,
            Colour = colour,
        };
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
