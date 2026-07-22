using System.Collections.Immutable;
using System.Text.RegularExpressions;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

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
        Assert.Null(presentation.Rarity);
        Assert.Equal(["Shaper", "Searing Exarch"], presentation.Influences.ToArray());
        Assert.Equal(["Armour", "Quality"], presentation.Properties.Select(row => row.Label));
        Assert.Equal(["1,234", "+20%"], presentation.Properties.Select(row => row.Value));
        Assert.Equal(0.75, presentation.Properties[0].Progress);
        Assert.Equal("84", presentation.ItemLevel);
        Assert.Equal(["Level", "Str"], presentation.Requirements.Select(row => row.Label));
        Assert.Equal("Requires Level 68, 191 Str", presentation.RequirementsLine);
        Assert.Equal("Sockets: R-G B-W", presentation.Sockets);
        Assert.Equal(["Corrupted", "Synthesised", "Replica"], presentation.Flags.Select(flag => flag.Label));
        Assert.Equal("1.25 divine", presentation.Price);
        Assert.Equal("Account · Character", presentation.Seller);
        Assert.Equal("online · Mirage", presentation.Online);
        Assert.Equal("2h ago", presentation.Listed);
        Assert.True(presentation.HasTradeFooter);
    }

    [Fact]
    public void FromSnapshot_LiveModifierObjectShapeReachesPresentationAndWpfLineSource()
    {
        var parseResult = new PathOfExileTradeFetchResponseParser()
            .ParseFetchResponse(PathOfExileTradeFetchFixtures.LiveModifierObjectResponse());
        var offer = Assert.Single(Assert.IsType<PathOfExileTradeFetchResponse>(parseResult.Response).Result);
        var snapshot = OfferCardSnapshotMapper.Create(offer);

        var presentation = OfferCardPreviewPresentation.FromSnapshot(snapshot, DateTimeOffset.UtcNow);
        var diagnostic = OfferCardModifierPipelineDiagnostic.Create(snapshot, presentation);

        Assert.Equal(
            [
                OfferCardModifierProvenance.Enchant,
                OfferCardModifierProvenance.Implicit,
                OfferCardModifierProvenance.Explicit,
                OfferCardModifierProvenance.Crafted,
                OfferCardModifierProvenance.Fractured,
                OfferCardModifierProvenance.Utility,
                OfferCardModifierProvenance.Cosmetic,
            ],
            presentation.ModifierSections.Select(section => section.Provenance));
        Assert.Equal(
            ["+196 to Evasion Rating", "+43% to Fire Resistance"],
            presentation.ModifierSections[2].Lines.ToArray());
        Assert.True(diagnostic.RawFetchOfferPresent);
        Assert.Equal(1, diagnostic.RawJson.Enchant);
        Assert.Equal(1, diagnostic.RawJson.Implicit);
        Assert.Equal(2, diagnostic.RawJson.Explicit);
        Assert.Equal(1, diagnostic.RawJson.Crafted);
        Assert.Equal(1, diagnostic.RawJson.Fractured);
        Assert.Equal(1, diagnostic.RawJson.Utility);
        Assert.Equal(1, diagnostic.RawJson.Cosmetic);
        Assert.Equal(8, diagnostic.RawJson.Total);
        Assert.Equal(diagnostic.RawJson, diagnostic.ParsedDto);
        Assert.Equal(diagnostic.ParsedDto, diagnostic.Snapshot);
        Assert.Equal(diagnostic.Snapshot, diagnostic.Presentation);
        Assert.Equal(8, diagnostic.WpfModifierLineViewModels);
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
    public void FromSnapshot_SocketsUseTextAndPreserveAdjacentProviderGroups()
    {
        Assert.Equal(
            "Sockets: R-G-B-W-R-G",
            SocketText((0, "R", null), (0, "G", null), (0, "B", null),
                (0, "W", null), (0, "R", null), (0, "G", null)));
        Assert.Equal(
            "Sockets: R-G-B R-G-B",
            SocketText((0, "R", null), (0, "G", null), (0, "B", null),
                (1, "R", null), (1, "G", null), (1, "B", null)));
        Assert.Equal(
            "Sockets: R-G B W",
            SocketText((0, "R", null), (0, "G", null), (1, "B", null), (2, "W", null)));
        Assert.Equal(
            "Sockets: B-R A-? ?",
            SocketText((8, "B", null), (8, null, "S"), (4, "A", null),
                (4, "provider-future-colour", null), (9, null, null)));
    }

    [Fact]
    public void FromSnapshot_StandardRarityLabelsCollapseButUsefulSpecialLabelRemains()
    {
        foreach (var rarity in new[] { "Normal", "Magic", "Rare", "Unique" })
        {
            var standard = OfferCardPreviewPresentation.FromSnapshot(
                new OfferCardSnapshot { OfferId = rarity, Rarity = rarity },
                DateTimeOffset.UtcNow);
            Assert.Null(standard.Rarity);
        }

        var special = OfferCardPreviewPresentation.FromSnapshot(
            new OfferCardSnapshot { OfferId = "special", Rarity = "Foulborn Unique" },
            DateTimeOffset.UtcNow);
        Assert.Equal("Foulborn Unique", special.Rarity);
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
        Assert.Equal(
            [false, false, true, false, false, false, false],
            presentation.ModifierSections.Select(section => section.HasSeparatorBefore));
    }

    [Fact]
    public void FromSnapshot_EmptyModifierSectionsFlagsDescriptionsAndFlavourCollapseCompletely()
    {
        var presentation = OfferCardPreviewPresentation.FromSnapshot(
            new OfferCardSnapshot
            {
                OfferId = "empty-sections",
                ModifierSections =
                [
                    new OfferCardModifierSection
                    {
                        Provenance = OfferCardModifierProvenance.Crafted,
                        Lines = ["", "   "],
                    },
                ],
                Flags = new OfferCardItemFlags { Corrupted = false },
                Description = " ",
                SecondaryDescription = "",
                FlavourText = ["", "  "],
            },
            DateTimeOffset.UtcNow);

        Assert.False(presentation.HasModifierSections);
        Assert.False(presentation.HasFlags);
        Assert.False(presentation.HasDescription);
        Assert.False(presentation.HasSecondaryDescription);
        Assert.False(presentation.HasFlavourText);
    }

    [Fact]
    public void PreviewXamlUsesOneContinuousTooltipSurfaceAndOneBodyScroller()
    {
        var xaml = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "ItemCardPreviewWindow.xaml"));

        Assert.Contains("x:Name=\"ContentScrollViewer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"400\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowActivated=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowInTaskbar=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Topmost=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CardBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{StaticResource CardBackgroundBrush}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TradeFooterBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("HeaderBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("BodyBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("ScrollBarThumbBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("CardScrollBarStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("ModifierCraftedBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("ModifierFracturedBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("ModifierExplicitBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("ModifierUtilityBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("ModifierCosmeticBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("Color=\"#8ED4FF\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Color=\"#D8B36A\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HeaderNormalBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("HeaderMagicBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("HeaderRareBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("HeaderUniqueBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("CardRareBorderBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("Color=\"#FA101216\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Color=\"#FFF27A\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SocketCircleStyle", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SocketLinkBrush", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SocketVisuals", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsLinkedToNext", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessibilityText", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Sockets}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FlagCorruptedBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("RequirementsLine", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Center\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FontSize=\"21\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FontSize=\"18\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FontSize=\"15\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemTooltipTitleFontFamily", xaml, StringComparison.Ordinal);
        Assert.Contains("Fontin SmallCaps, Fontin, Georgia", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemTooltipBodyFontFamily", xaml, StringComparison.Ordinal);
        Assert.Contains("Fontin, Georgia, Segoe UI", xaml, StringComparison.Ordinal);
        Assert.Contains("TextElement.FontFamily=\"{StaticResource ItemTooltipBodyFontFamily}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"{StaticResource ItemTooltipTitleFontFamily}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"PROPERTIES\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"REQUIREMENTS\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Enchant\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Implicit\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Explicit\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SectionLabelStyle", xaml, StringComparison.Ordinal);
        var modifierBlockStart = xaml.IndexOf("x:Name=\"ModifierSectionsControl\"", StringComparison.Ordinal);
        var modifierBorderStart = xaml.LastIndexOf("<Border", modifierBlockStart, StringComparison.Ordinal);
        var modifierBlockEnd = xaml.IndexOf("ItemsSource=\"{Binding Flags}\"", modifierBlockStart, StringComparison.Ordinal);
        var modifierBlock = xaml[modifierBorderStart..modifierBlockEnd];
        Assert.Contains("x:Name=\"ModifierBlockBorder\"", modifierBlock, StringComparison.Ordinal);
        Assert.Contains("NormalModifierContentMargin", modifierBlock, StringComparison.Ordinal);
        Assert.Contains("ImplicitModifierContentMargin", modifierBlock, StringComparison.Ordinal);
        Assert.Contains("CenteredImplicitSeparatorMargin", modifierBlock, StringComparison.Ordinal);
        Assert.Contains("Height=\"1\"", modifierBlock, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment=\"Stretch\"", modifierBlock, StringComparison.Ordinal);
        Assert.Contains("Property=\"HorizontalAlignment\"", modifierBlock, StringComparison.Ordinal);
        Assert.Contains("Value=\"Stretch\"", modifierBlock, StringComparison.Ordinal);
        Assert.Contains("TextAlignment=\"Left\"", modifierBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("TextAlignment=\"Center\"", modifierBlock, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\"", modifierBlock, StringComparison.Ordinal);
        Assert.Contains("96,0,28,0", xaml, StringComparison.Ordinal);
        Assert.Contains("5,0,5,0", xaml, StringComparison.Ordinal);
        Assert.Contains("62,5,62,5", xaml, StringComparison.Ordinal);
        Assert.Contains("HasModifierSections", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HeaderBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ItemContentPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TradeFooterBorder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PinButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HeaderDragThumb\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Cursor=\"SizeAll\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PinFeedbackText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PriceCheckerTitleBarControlResources.xaml", xaml, StringComparison.Ordinal);
        Assert.Contains("{StaticResource TitleBarPinGlyph}", xaml, StringComparison.Ordinal);
        Assert.Contains("{StaticResource TitleBarCloseGeometry}", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Image", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Background=\"White\"", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SystemColors", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Single(Regex.Matches(xaml, "<ScrollViewer(?:\\s|>)", RegexOptions.CultureInvariant));
        var titleColours = new[]
        {
            BrushColour(xaml, "HeaderNormalBrush"),
            BrushColour(xaml, "HeaderMagicBrush"),
            BrushColour(xaml, "HeaderRareBrush"),
            BrushColour(xaml, "HeaderUniqueBrush"),
        };
        Assert.Equal(4, titleColours.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.NotEqual(
            BrushColour(xaml, "CardBackgroundBrush"),
            BrushColour(xaml, "HeaderRareBrush"));
        Assert.NotEqual(
            BrushColour(xaml, "ModifierCraftedBrush"),
            BrushColour(xaml, "ModifierFracturedBrush"));

        var headerIndex = xaml.IndexOf("x:Name=\"HeaderBorder\"", StringComparison.Ordinal);
        var scrollIndex = xaml.IndexOf("x:Name=\"ContentScrollViewer\"", StringComparison.Ordinal);
        var scrollEndIndex = xaml.IndexOf("</ScrollViewer>", scrollIndex, StringComparison.Ordinal);
        var footerIndex = xaml.IndexOf("x:Name=\"TradeFooterBorder\"", StringComparison.Ordinal);
        Assert.True(headerIndex < scrollIndex);
        Assert.True(scrollIndex < scrollEndIndex);
        Assert.True(scrollEndIndex < footerIndex);
        var propertiesIndex = xaml.IndexOf("ItemsSource=\"{Binding Properties}\"", StringComparison.Ordinal);
        var itemLevelIndex = xaml.IndexOf("Text=\"Item Level: \"", StringComparison.Ordinal);
        var socketsIndex = xaml.IndexOf("Text=\"{Binding Sockets}\"", StringComparison.Ordinal);
        var requirementsIndex = xaml.IndexOf("Text=\"{Binding RequirementsLine}\"", StringComparison.Ordinal);
        var modifiersIndex = xaml.IndexOf("ItemsSource=\"{Binding ModifierSections}\"", StringComparison.Ordinal);
        var flagsIndex = xaml.IndexOf("ItemsSource=\"{Binding Flags}\"", StringComparison.Ordinal);
        var descriptionIndex = xaml.IndexOf("Text=\"{Binding Description}\"", StringComparison.Ordinal);
        Assert.True(scrollIndex < propertiesIndex);
        Assert.True(propertiesIndex < itemLevelIndex);
        Assert.True(itemLevelIndex < socketsIndex);
        Assert.True(socketsIndex < requirementsIndex);
        Assert.True(requirementsIndex < modifiersIndex);
        Assert.True(modifiersIndex < flagsIndex);
        Assert.True(flagsIndex < descriptionIndex);
        Assert.True(descriptionIndex < scrollEndIndex);
        var contentAfterResources = xaml[(xaml.IndexOf("</Window.Resources>", StringComparison.Ordinal) +
            "</Window.Resources>".Length)..];
        Assert.DoesNotContain("=\"#", contentAfterResources, StringComparison.Ordinal);

        var code = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "ItemCardPreviewWindow.xaml.cs"));
        Assert.Contains(
            "mode != OfferCardWindowMode.Pinned",
            code,
            StringComparison.Ordinal);
        Assert.Contains(
            "HeaderDragThumb.Visibility = isPinned",
            code,
            StringComparison.Ordinal);
        Assert.Contains("ItemContentPanel.Measure", code, StringComparison.Ordinal);
        Assert.Contains("MeasureNaturalContentWidth", code, StringComparison.Ordinal);
        Assert.Contains("double.PositiveInfinity", code, StringComparison.Ordinal);
        Assert.Contains("CalculateWidth(measuredContentWidth, maximumWidth)", code, StringComparison.Ordinal);
        Assert.DoesNotContain(".Length", code, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility = size.IsContentScrollingRequired", code, StringComparison.Ordinal);
        Assert.Contains("OfferCardWindowSizeCalculator", code, StringComparison.Ordinal);

        var previewFactory = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "OfferCardPreviewWindowFactory.cs"));
        var pinnedFactory = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PinnedOfferCardWindowFactory.cs"));
        Assert.Contains("ItemCardPreviewWindow", previewFactory, StringComparison.Ordinal);
        Assert.Contains("ItemCardPreviewWindow", pinnedFactory, StringComparison.Ordinal);

        var presentationSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "OfferCardPreviewPresentation.cs"));
        Assert.DoesNotContain("HttpClient", presentationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BitmapImage", presentationSource, StringComparison.Ordinal);
        Assert.Contains("Sockets: ", presentationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TooltipUsesInstalledFontFamiliesWithoutBundlingFontFiles()
    {
        var repositoryRoot = RepositoryRoot();
        var fontFiles = Directory
            .EnumerateFiles(repositoryRoot, "*.*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var extension = Path.GetExtension(path);
                return (extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".otf", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".woff", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".woff2", StringComparison.OrdinalIgnoreCase)) &&
                    !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains("\\.git\\", StringComparison.OrdinalIgnoreCase);
            });

        Assert.Empty(fontFiles);
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

    private static string? SocketText(
        params (int Group, string? Colour, string? Attribute)[] sockets)
    {
        var snapshot = new OfferCardSnapshot
        {
            OfferId = "socket-text",
            Sockets = sockets
                .Select((socket, index) => new OfferCardSocket
                {
                    Index = index,
                    Group = socket.Group,
                    Colour = socket.Colour,
                    Attribute = socket.Attribute,
                })
                .ToImmutableArray(),
        };

        return OfferCardPreviewPresentation.FromSnapshot(snapshot, DateTimeOffset.UtcNow).Sockets;
    }

    private static string BrushColour(string xaml, string resourceKey)
    {
        var match = Regex.Match(
            xaml,
            $"<SolidColorBrush\\s+x:Key=\"{Regex.Escape(resourceKey)}\"\\s+Color=\"(?<colour>#[0-9A-Fa-f]+)\"",
            RegexOptions.CultureInvariant);
        return match.Success
            ? match.Groups["colour"].Value
            : throw new InvalidOperationException($"Brush resource '{resourceKey}' was not found.");
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
