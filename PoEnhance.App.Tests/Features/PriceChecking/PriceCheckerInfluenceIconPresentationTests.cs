using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerInfluenceIconPresentationTests
{
    [Fact]
    public void AllocateAlternatingSides_PlacesIconsNearestThenOutward()
    {
        var items = new[] { "I1", "I2", "I3", "I4" };

        var (left, right) = PriceCheckerInfluenceIconCatalog.AllocateAlternatingSides(items);

        Assert.Equal(["I3", "I1"], left);
        Assert.Equal(["I2", "I4"], right);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AllocateAlternatingSides_SupportsArbitraryCounts(int count)
    {
        var items = Enumerable.Range(1, count).Select(index => $"I{index}").ToArray();

        var (left, right) = PriceCheckerInfluenceIconCatalog.AllocateAlternatingSides(items);

        Assert.Equal(count, left.Count + right.Count);
        Assert.Equal(items.Where((_, index) => index % 2 == 0).Reverse(), left);
        Assert.Equal(items.Where((_, index) => index % 2 == 1), right);
    }

    [Theory]
    [InlineData(TradeSearchInfluenceKind.Shaper, "shaper.png", "Shaper")]
    [InlineData(TradeSearchInfluenceKind.Elder, "elder.png", "Elder")]
    [InlineData(TradeSearchInfluenceKind.Crusader, "crusader.png", "Crusader")]
    [InlineData(TradeSearchInfluenceKind.Redeemer, "redeemer.png", "Redeemer")]
    [InlineData(TradeSearchInfluenceKind.Hunter, "hunter.png", "Hunter")]
    [InlineData(TradeSearchInfluenceKind.Warlord, "warlord.png", "Warlord")]
    [InlineData(TradeSearchInfluenceKind.SearingExarch, "searing-exarch.png", "Searing Exarch")]
    [InlineData(TradeSearchInfluenceKind.EaterOfWorlds, "eater-of-worlds.png", "Eater of Worlds")]
    public void InfluenceCatalog_MapsRecognizedKindsToPackagedAssets(
        TradeSearchInfluenceKind kind,
        string fileName,
        string displayLabel)
    {
        Assert.True(PriceCheckerInfluenceIconCatalog.TryGetAssetFileName(kind, out var mappedFileName));
        Assert.Equal(fileName, mappedFileName);
        Assert.True(PriceCheckerInfluenceIconCatalog.TryGetPackUri(kind, out var uri));
        Assert.Contains(fileName, uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains(";component/Assets/Influences/", uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.True(PriceCheckerInfluenceIconCatalog.TryGetDisplayLabel(kind, out var label));
        Assert.Equal(displayLabel, label);

        var assetPath = Path.Combine(
            FindRepositoryRoot(),
            "PoEnhance.App",
            "Assets",
            "Influences",
            fileName);
        Assert.True(File.Exists(assetPath), $"Missing packaged asset {assetPath}");
    }

    [Fact]
    public void InfluenceCatalog_UnknownKindHasNoAsset()
    {
        Assert.False(PriceCheckerInfluenceIconCatalog.TryGetAssetFileName(
            TradeSearchInfluenceKind.Unknown,
            out _));
        Assert.False(PriceCheckerInfluenceIconCatalog.TryGetPackUri(
            TradeSearchInfluenceKind.Unknown,
            out _));
        Assert.False(PriceCheckerInfluenceIconCatalog.TryGetDisplayLabel(
            TradeSearchInfluenceKind.Unknown,
            out _));
    }

    [Theory]
    [InlineData("Fractured", "fractured.png", "Fractured")]
    [InlineData("Synthesised", "synthesised.png", "Synthesised")]
    [InlineData("Foulborn", "foulborn.png", "Foulborn")]
    [InlineData("Veiled", "veiled.png", "Veiled")]
    public void HeaderStatusCatalog_MapsRecognizedKindsToPackagedAssets(
        string kindName,
        string fileName,
        string displayLabel)
    {
        var kind = Enum.Parse<PriceCheckerHeaderStatusKind>(kindName);
        Assert.True(PriceCheckerHeaderIconCatalog.TryGetStatusAssetFileName(kind, out var mapped));
        Assert.Equal(fileName, mapped);
        Assert.True(PriceCheckerHeaderIconCatalog.TryGetStatusDisplayLabel(kind, out var label));
        Assert.Equal(displayLabel, label);
        Assert.True(File.Exists(Path.Combine(
            FindRepositoryRoot(),
            "PoEnhance.App",
            "Assets",
            "Influences",
            fileName)));
    }

    [Fact]
    public void Window_UpdateSearch_ShowsLeagueNameInHeaderWithoutLeagueLabel()
    {
        RunOnSta(() =>
        {
            var window = new PriceCheckerWindow
            {
                Width = PriceCheckerPlacementCalculator.UserPanelMinimumWidth,
                Height = 720,
            };
            window.UpdateSearch(new PriceCheckerSearchViewState
            {
                LeagueIdentifier = "Allflame",
                Message = "Search complete",
                Summary = "Showing 1 of 1",
            });
            window.Show();
            window.UpdateLayout();

            var leagueHeader = Assert.IsType<TextBlock>(window.FindName("LeagueHeaderText"));
            Assert.Equal("Allflame", leagueHeader.Text);
            Assert.DoesNotContain("League:", leagueHeader.Text, StringComparison.Ordinal);
            Assert.Null(window.FindName("LeagueStatusText"));
            Assert.Equal(
                "Search complete",
                Assert.IsType<TextBlock>(window.FindName("SearchStatusText")).Text);
            Assert.Equal(
                "Showing 1 of 1",
                Assert.IsType<TextBlock>(window.FindName("SearchSummaryText")).Text);
            window.Close();
        });
    }

    [Theory]
    [InlineData(576d)]
    [InlineData(720d)]
    public void Window_LeagueHeader_StartsNearPopupLeftEdgeIndependentOfContentPadding(double width)
    {
        RunOnSta(() =>
        {
            var window = new PriceCheckerWindow
            {
                Width = width,
                Height = 720,
                MinWidth = PriceCheckerPlacementCalculator.UserPanelMinimumWidth,
            };
            window.UpdateContent(new PriceCheckerWindowState(
                new TradeSearchDraft
                {
                    DisplayName = "Centered Rare Title",
                    ParsedBaseType = "Hubris Circlet",
                    Rarity = "Rare",
                    InfluenceCriteria = ImmutableArray.Create(
                        new TradeSearchInfluenceCriterion
                        {
                            Kind = TradeSearchInfluenceKind.Shaper,
                            Family = TradeSearchInfluenceFamily.Traditional,
                            ObservedLine = "Shaper Item",
                            DisplayLabel = "Shaper",
                            IsEnabled = true,
                        },
                        new TradeSearchInfluenceCriterion
                        {
                            Kind = TradeSearchInfluenceKind.Elder,
                            Family = TradeSearchInfluenceFamily.Traditional,
                            ObservedLine = "Elder Item",
                            DisplayLabel = "Elder",
                            IsEnabled = true,
                        }),
                },
                TradeSearchValidationResult.FromDiagnostics([]))
            {
                Presentation = new PriceCheckerItemPresentation { IsRarityEditable = true },
            });
            window.UpdateSearch(new PriceCheckerSearchViewState
            {
                LeagueIdentifier = "Allflame",
                Message = "ready",
                Summary = "0",
            });
            window.Show();
            window.UpdateLayout();

            var border = Assert.IsType<Border>(window.Content);
            var league = Assert.IsType<TextBlock>(window.FindName("LeagueHeaderText"));
            var title = Assert.IsType<TextBlock>(window.FindName("TitleDisplayNameText"));
            var contentGrid = Assert.IsType<Grid>(
                Assert.IsType<Grid>(border.Child).Children[1]);
            var pin = Assert.IsType<ToggleButton>(window.FindName("PinToggleButton"));

            Assert.Equal(HorizontalAlignment.Left, league.HorizontalAlignment);
            Assert.Equal(6d, league.Margin.Left);

            // Coordinate relative to the popup Border root (inside 1 DIP chrome), not Margin alone.
            var leagueLeft = league.TranslatePoint(new Point(0, 0), border).X;
            Assert.InRange(leagueLeft, 5.5d, 10.5d);

            // Must stay independent of Panel 1 / body content Margin="12".
            Assert.Equal(12d, contentGrid.Margin.Left);
            Assert.True(leagueLeft < contentGrid.Margin.Left);

            var titleLeft = title.TranslatePoint(new Point(0, 0), border).X;
            var leagueRight = league.TranslatePoint(new Point(league.ActualWidth, 0), border).X;
            Assert.True(leagueRight + 4d <= titleLeft);

            var pinLeft = pin.TranslatePoint(new Point(0, 0), border).X;
            Assert.True(leagueRight + 4d <= pinLeft);

            window.Close();
        });
    }

    [Fact]
    public void Window_LongLeagueName_EllipsizesWithoutCoveringCenteredTitleOrButtons()
    {
        RunOnSta(() =>
        {
            var window = new PriceCheckerWindow
            {
                Width = PriceCheckerPlacementCalculator.UserPanelMinimumWidth,
                Height = 720,
            };
            window.UpdateContent(new PriceCheckerWindowState(
                new TradeSearchDraft
                {
                    DisplayName = "Short Title",
                    ParsedBaseType = "Iron Ring",
                    Rarity = "Magic",
                },
                TradeSearchValidationResult.FromDiagnostics([]))
            {
                Presentation = new PriceCheckerItemPresentation { IsRarityEditable = true },
            });
            window.UpdateSearch(new PriceCheckerSearchViewState
            {
                LeagueIdentifier = "Very Long League Name That Must Ellipsize In Header",
                Message = "ready",
                Summary = "0",
            });
            window.Show();
            window.UpdateLayout();

            var border = Assert.IsType<Border>(window.Content);
            var league = Assert.IsType<TextBlock>(window.FindName("LeagueHeaderText"));
            var title = Assert.IsType<TextBlock>(window.FindName("TitleDisplayNameText"));
            var pin = Assert.IsType<ToggleButton>(window.FindName("PinToggleButton"));

            Assert.Equal(TextTrimming.CharacterEllipsis, league.TextTrimming);
            Assert.True(league.ActualWidth <= league.MaxWidth + 0.5d);
            Assert.InRange(league.TranslatePoint(new Point(0, 0), border).X, 5.5d, 10.5d);

            var leagueRight = league.TranslatePoint(new Point(league.ActualWidth, 0), border).X;
            Assert.True(leagueRight + 4d <= title.TranslatePoint(new Point(0, 0), border).X);
            Assert.True(leagueRight + 4d <= pin.TranslatePoint(new Point(0, 0), border).X);
            window.Close();
        });
    }

    [Fact]
    public void Window_InfluenceIcons_AlternateAroundTitleAndToggleWithoutWhiteOutline()
    {
        RunOnSta(() =>
        {
            var criteria = ImmutableArray.Create(
                new TradeSearchInfluenceCriterion
                {
                    Kind = TradeSearchInfluenceKind.Shaper,
                    Family = TradeSearchInfluenceFamily.Traditional,
                    ObservedLine = "Shaper Item",
                    DisplayLabel = "Shaper",
                    IsEnabled = true,
                },
                new TradeSearchInfluenceCriterion
                {
                    Kind = TradeSearchInfluenceKind.Elder,
                    Family = TradeSearchInfluenceFamily.Traditional,
                    ObservedLine = "Elder Item",
                    DisplayLabel = "Elder",
                    IsEnabled = true,
                },
                new TradeSearchInfluenceCriterion
                {
                    Kind = TradeSearchInfluenceKind.SearingExarch,
                    Family = TradeSearchInfluenceFamily.Eldritch,
                    ObservedLine = "Searing Exarch Item",
                    DisplayLabel = "Searing Exarch",
                    IsEnabled = false,
                },
                new TradeSearchInfluenceCriterion
                {
                    Kind = TradeSearchInfluenceKind.EaterOfWorlds,
                    Family = TradeSearchInfluenceFamily.Eldritch,
                    ObservedLine = "Eater of Worlds Item",
                    DisplayLabel = "Eater of Worlds",
                    IsEnabled = true,
                });

            var window = new PriceCheckerWindow
            {
                Width = PriceCheckerPlacementCalculator.UserPanelMinimumWidth,
                Height = 720,
            };
            PriceCheckerInfluenceActivationChangedEventArgs? changed = null;
            window.InfluenceActivationChanged += (_, args) => changed = args;
            window.UpdateContent(new PriceCheckerWindowState(
                new TradeSearchDraft
                {
                    DisplayName = "Influence Test",
                    ParsedBaseType = "Iron Ring",
                    Rarity = "Rare",
                    InfluenceCriteria = criteria,
                },
                TradeSearchValidationResult.FromDiagnostics([]))
            {
                Presentation = new PriceCheckerItemPresentation { IsRarityEditable = true },
            });
            window.Show();
            window.UpdateLayout();

            var left = Assert.IsType<StackPanel>(window.FindName("TitleInfluenceIconsLeft"));
            var right = Assert.IsType<StackPanel>(window.FindName("TitleInfluenceIconsRight"));
            Assert.Equal(2, left.Children.Count);
            Assert.Equal(2, right.Children.Count);

            var leftKinds = left.Children.OfType<Button>()
                .Select(button => Assert.IsType<TradeSearchInfluenceKind>(button.Tag))
                .ToArray();
            var rightKinds = right.Children.OfType<Button>()
                .Select(button => Assert.IsType<TradeSearchInfluenceKind>(button.Tag))
                .ToArray();
            Assert.Equal(
                [TradeSearchInfluenceKind.SearingExarch, TradeSearchInfluenceKind.Shaper],
                leftKinds);
            Assert.Equal(
                [TradeSearchInfluenceKind.Elder, TradeSearchInfluenceKind.EaterOfWorlds],
                rightKinds);

            var inactive = Assert.IsType<Button>(left.Children[0]);
            var active = Assert.IsType<Button>(left.Children[1]);
            Assert.Equal(0d, inactive.BorderThickness.Left);
            Assert.Equal(0d, active.BorderThickness.Left);
            Assert.Equal(
                "Searing Exarch",
                Assert.IsType<ToolTip>(inactive.ToolTip).Content);
            Assert.Equal(250, ToolTipService.GetInitialShowDelay(inactive));

            var inactiveImage = Assert.IsType<Image>(inactive.Content);
            var activeImage = Assert.IsType<Image>(active.Content);
            Assert.Equal(PriceCheckerHeaderIconCatalog.DisplayPixelSize, inactiveImage.Width);
            Assert.Equal(PriceCheckerHeaderIconCatalog.DisplayPixelSize, activeImage.Width);
            Assert.IsType<FormatConvertedBitmap>(inactiveImage.Source);
            Assert.IsType<BitmapImage>(activeImage.Source);
            Assert.Equal(PriceCheckerHeaderIconCatalog.InactiveOpacity, inactiveImage.Opacity);
            Assert.Equal(1.0, activeImage.Opacity);

            active.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, active));
            Assert.NotNull(changed);
            Assert.Equal(TradeSearchInfluenceKind.Shaper, changed!.Kind);
            Assert.False(changed.IsEnabled);
            window.Close();
        });
    }

    [Fact]
    public void HeaderIcons_FracturedAlone_MirrorsIconOnBothSides()
    {
        var draft = new TradeSearchDraft
        {
            DisplayName = "Dread Loop",
            ParsedBaseType = "Vermillion Ring",
            ItemStates = ["Fractured Item"],
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "fractured-mod",
                    OriginalText = "+10 to Strength",
                    IsFractured = true,
                },
            ],
        };

        var (left, right) = PriceCheckerHeaderIconCatalog.ResolveHeaderIcons(draft);

        Assert.Single(left);
        Assert.Single(right);
        Assert.Equal("fractured.png", left[0].AssetFileName);
        Assert.Equal("fractured.png", right[0].AssetFileName);
        Assert.False(left[0].IsInfluenceToggle);
        Assert.False(right[0].IsInfluenceToggle);
    }

    [Fact]
    public void HeaderIcons_InfluenceOverridesFracturedInHeader()
    {
        var draft = new TradeSearchDraft
        {
            DisplayName = "Agony Knuckle",
            ParsedBaseType = "Wyvernscale Gauntlets",
            ItemStates = ["Fractured Item"],
            InfluenceCriteria = ImmutableArray.Create(
                new TradeSearchInfluenceCriterion
                {
                    Kind = TradeSearchInfluenceKind.SearingExarch,
                    Family = TradeSearchInfluenceFamily.Eldritch,
                    ObservedLine = "Searing Exarch Item",
                    DisplayLabel = "Searing Exarch",
                    IsEnabled = true,
                },
                new TradeSearchInfluenceCriterion
                {
                    Kind = TradeSearchInfluenceKind.EaterOfWorlds,
                    Family = TradeSearchInfluenceFamily.Eldritch,
                    ObservedLine = "Eater of Worlds Item",
                    DisplayLabel = "Eater of Worlds",
                    IsEnabled = true,
                }),
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "fractured-suppress",
                    OriginalText = "+14% chance to Suppress Spell Damage",
                    IsFractured = true,
                },
            ],
        };

        var (left, right) = PriceCheckerHeaderIconCatalog.ResolveHeaderIcons(draft);

        Assert.Single(left);
        Assert.Single(right);
        Assert.Equal("searing-exarch.png", left[0].AssetFileName);
        Assert.Equal("eater-of-worlds.png", right[0].AssetFileName);
        Assert.DoesNotContain(left.Concat(right), entry => entry.AssetFileName == "fractured.png");
        Assert.True(PriceCheckerHeaderIconCatalog.IsFracturedItem(draft));
    }

    [Fact]
    public void HeaderIcons_VeiledOnlyWhileUnrevealed()
    {
        var veiledDraft = new TradeSearchDraft
        {
            DisplayName = "Veiled Test",
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "veiled-prefix",
                    OriginalText = "Veiled Prefix",
                    IsVeiled = true,
                },
            ],
        };
        var unveiledDraft = veiledDraft with
        {
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "unveiled-mod",
                    OriginalText = "Some unveiled mod",
                    IsVeiled = false,
                },
            ],
        };

        var (veiledLeft, veiledRight) = PriceCheckerHeaderIconCatalog.ResolveHeaderIcons(veiledDraft);
        var (unveiledLeft, unveiledRight) = PriceCheckerHeaderIconCatalog.ResolveHeaderIcons(unveiledDraft);

        Assert.Equal("veiled.png", Assert.Single(veiledLeft).AssetFileName);
        Assert.Equal("veiled.png", Assert.Single(veiledRight).AssetFileName);
        Assert.Empty(unveiledLeft);
        Assert.Empty(unveiledRight);
    }

    [Fact]
    public void Window_FracturedStatusIcon_IsInformationalNotToggle()
    {
        RunOnSta(() =>
        {
            var window = new PriceCheckerWindow
            {
                Width = PriceCheckerPlacementCalculator.UserPanelMinimumWidth,
                Height = 720,
            };
            PriceCheckerInfluenceActivationChangedEventArgs? changed = null;
            window.InfluenceActivationChanged += (_, args) => changed = args;
            window.UpdateContent(new PriceCheckerWindowState(
                new TradeSearchDraft
                {
                    DisplayName = "Dread Loop",
                    ParsedBaseType = "Vermillion Ring",
                    Rarity = "Rare",
                    ItemStates = ["Fractured Item"],
                    ModifierFilters =
                    [
                        new ResolvedSearchComponent
                        {
                            ComponentId = "fractured-mod",
                            OriginalText = "+10 to Strength",
                            IsFractured = true,
                        },
                    ],
                },
                TradeSearchValidationResult.FromDiagnostics([]))
            {
                Presentation = new PriceCheckerItemPresentation { IsRarityEditable = true },
            });
            window.Show();
            window.UpdateLayout();

            var left = Assert.IsType<StackPanel>(window.FindName("TitleInfluenceIconsLeft"));
            var right = Assert.IsType<StackPanel>(window.FindName("TitleInfluenceIconsRight"));
            var leftIcon = Assert.IsType<Border>(Assert.Single(left.Children));
            var rightIcon = Assert.IsType<Border>(Assert.Single(right.Children));
            Assert.IsType<Border>(rightIcon);
            Assert.IsNotType<Button>(leftIcon);
            Assert.Equal("fractured.png", leftIcon.Tag);
            Assert.Equal("Fractured", Assert.IsType<ToolTip>(leftIcon.ToolTip).Content);
            Assert.Equal(250, ToolTipService.GetInitialShowDelay(leftIcon));
            Assert.Equal(
                PriceCheckerHeaderIconCatalog.DisplayPixelSize,
                Assert.IsType<Image>(leftIcon.Child).Width);
            leftIcon.RaiseEvent(new MouseButtonEventArgs(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = leftIcon,
            });
            Assert.Null(changed);
            Assert.Same(
                window.FindName("HeaderRequestedFiltersPanel"),
                LogicalTreeHelper.GetParent(
                    Assert.IsType<Button>(window.FindName("FracturedStateButton"))));
            window.Close();
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
