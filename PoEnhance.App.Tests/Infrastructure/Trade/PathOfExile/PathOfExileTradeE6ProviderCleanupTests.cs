using System.Reflection;
using System.Text.Json;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeE6ProviderCleanupTests
{
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialCatalog = new(LoadOfficialCatalog);
    private static readonly MethodInfo InteractionReadyMethod =
        typeof(PriceCheckerSearchController).GetMethod(
            "IsModifierInteractionReady",
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(nameof(PriceCheckerSearchController), "IsModifierInteractionReady");

    [Fact]
    public void ResolveProviderComponents_LiveFixedNumericShape_PrefersGenericParametricAlternatives()
    {
        var service = CreateService();
        var catalog = OfficialCatalog.Value;
        var draft = new TradeSearchDraft
        {
            ItemClass = "Wands",
            Rarity = "Unique",
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    SourceModifierIndex = 0,
                    OriginalText = "Socketed Gems are Supported by Level 10 Spell Echo",
                    CanonicalSignature = "Socketed Gems are Supported by Level <number> Spell Echo",
                    ProviderSearchSignatures =
                    [
                        "Socketed Gems are Supported by Level <number> Spell Echo",
                        "Socketed Gems are Supported by Level 10 Spell Echo",
                    ],
                    ParsedKind = ParsedModifierKind.Unique,
                    UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    UniqueCatalogBlockIds = ["unique-block:support-echo"],
                    UniqueSourceObservationIds = ["observation:support-echo"],
                    ResolvedStatIds = ["support_spell_echo"],
                    IsSearchable = true,
                    SupportsValueBounds = false,
                    ValueBoundShape = ModifierBoundShape.PresenceOnly,
                    ObservedNumericValues = [10m],
                    CanonicalNumericValues = [10m],
                    FixedQueryValue = 10m,
                    StatMappingProof = ModifierStatMappingProofStatus.WholeVector,
                },
            ],
        };

        var resolved = service.ResolveProviderComponents(
            draft,
            catalog,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Test Wand",
                CanonicalType = "Test Base",
            });

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        Assert.Contains('#', component.ProviderStatText ?? string.Empty);
        Assert.True(component.ProviderStatAlternativeIds.Count > 1);
        Assert.DoesNotContain(component.ProviderStatAlternativeIds, statId =>
            catalog.TryGetById(statId, out var entry) && !entry.Text.Contains('#'));
        Assert.Equal(ModifierBoundShape.Scalar, component.ValueBoundShape);
        Assert.False(component.SupportsValueBounds);
        Assert.Null(component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.True(component.IsSearchable);
        Assert.True(IsInteractionReady(component));

        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(
            resolved with
            {
                ModifierFilters = [component with { IsSelected = true }],
            },
            catalog);
        Assert.True(mapping.IsSuccess);
        Assert.All(Assert.Single(mapping.Filters).Alternatives, alternative =>
        {
            Assert.Equal(10m, alternative.Minimum);
            Assert.Equal(10m, alternative.Maximum);
        });
    }

    [Fact]
    public void ResolveProviderComponents_LiveFixedNumericControlledDestruction_UsesGenericParametricAlternatives()
    {
        var service = CreateService();
        var catalog = OfficialCatalog.Value;
        var draft = new TradeSearchDraft
        {
            ItemClass = "Wands",
            Rarity = "Unique",
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    SourceModifierIndex = 0,
                    OriginalText = "Socketed Gems are Supported by Level 10 Controlled Destruction",
                    CanonicalSignature =
                        "Socketed Gems are Supported by Level <number> Controlled Destruction",
                    ProviderSearchSignatures =
                    [
                        "Socketed Gems are Supported by Level <number> Controlled Destruction",
                        "Socketed Gems are Supported by Level 10 Controlled Destruction",
                    ],
                    ParsedKind = ParsedModifierKind.Unique,
                    UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    UniqueCatalogBlockIds = ["unique-block:support-controlled-destruction"],
                    UniqueSourceObservationIds = ["observation:support-controlled-destruction"],
                    ResolvedStatIds = ["support_controlled_destruction"],
                    IsSearchable = true,
                    SupportsValueBounds = false,
                    ValueBoundShape = ModifierBoundShape.PresenceOnly,
                    ObservedNumericValues = [10m],
                    CanonicalNumericValues = [10m],
                    FixedQueryValue = 10m,
                    StatMappingProof = ModifierStatMappingProofStatus.WholeVector,
                },
            ],
        };

        var resolved = service.ResolveProviderComponents(
            draft,
            catalog,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Test Wand",
                CanonicalType = "Test Base",
            });

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        Assert.Contains('#', component.ProviderStatText ?? string.Empty);
        Assert.True(component.ProviderStatAlternativeIds.Count > 1);
        Assert.DoesNotContain(component.ProviderStatAlternativeIds, statId =>
            catalog.TryGetById(statId, out var entry) && !entry.Text.Contains('#'));
        Assert.Equal(ModifierBoundShape.Scalar, component.ValueBoundShape);
        Assert.False(component.SupportsValueBounds);
        Assert.Null(component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.True(component.IsSearchable);
        Assert.True(IsInteractionReady(component));
    }

    [Fact]
    public void ResolveProviderComponents_WithoutFixedNumericEvidence_StaysAmbiguousAmongMixedCandidates()
    {
        var service = CreateService();
        var catalog = OfficialCatalog.Value;
        var draft = new TradeSearchDraft
        {
            ItemClass = "Wands",
            Rarity = "Unique",
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    SourceModifierIndex = 0,
                    OriginalText = "Socketed Gems are Supported by Level 10 Spell Echo",
                    CanonicalSignature = "Socketed Gems are Supported by Level <number> Spell Echo",
                    ProviderSearchSignatures =
                    [
                        "Socketed Gems are Supported by Level <number> Spell Echo",
                    ],
                    ParsedKind = ParsedModifierKind.Unique,
                    UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    UniqueCatalogBlockIds = ["unique-block:support-echo"],
                    UniqueSourceObservationIds = ["observation:support-echo"],
                    ResolvedStatIds = ["support_spell_echo"],
                    IsSearchable = true,
                    SupportsValueBounds = true,
                    ValueBoundShape = ModifierBoundShape.Scalar,
                    CanonicalNumericValues = [10m],
                    StatMappingProof = ModifierStatMappingProofStatus.WholeVector,
                },
            ],
        };

        var resolved = service.ResolveProviderComponents(
            draft,
            catalog,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Test Wand",
                CanonicalType = "Test Base",
            });

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Ambiguous, component.ProviderResolutionStatus);
        Assert.False(component.IsSearchable);
    }

    [Fact]
    public void ResolveProviderComponents_GenericEquivalentSupportGemCandidates_RemainExactEquivalentSet()
    {
        var service = CreateService();
        var catalog = OfficialCatalog.Value;
        var draft = new TradeSearchDraft
        {
            ItemClass = "Wands",
            Rarity = "Unique",
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    SourceModifierIndex = 0,
                    OriginalText = "Socketed Gems are Supported by Level 10 Arcane Surge",
                    CanonicalSignature = "Socketed Gems are Supported by Level <number> Arcane Surge",
                    ProviderSearchSignatures =
                    [
                        "Socketed Gems are Supported by Level <number> Arcane Surge",
                        "Socketed Gems are Supported by Level 1 Arcane Surge",
                    ],
                    ParsedKind = ParsedModifierKind.Unique,
                    UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    UniqueCatalogBlockIds = ["unique-block:support-arcane-surge"],
                    UniqueSourceObservationIds = ["observation:support-arcane-surge"],
                    ResolvedStatIds = ["support_arcane_surge"],
                    IsSearchable = true,
                    SupportsValueBounds = false,
                    ValueBoundShape = ModifierBoundShape.PresenceOnly,
                    ObservedNumericValues = [10m],
                    CanonicalNumericValues = [10m],
                    FixedQueryValue = 10m,
                    StatMappingProof = ModifierStatMappingProofStatus.WholeVector,
                },
            ],
        };

        var resolved = service.ResolveProviderComponents(
            draft,
            catalog,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Test Wand",
                CanonicalType = "Test Base",
            });

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        Assert.True(component.ProviderStatAlternativeIds.Count > 1);
        Assert.True(component.IsSearchable);
        Assert.True(IsInteractionReady(component));
        Assert.False(component.SupportsValueBounds);
        Assert.Null(component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);

        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(
            resolved with
            {
                ModifierFilters = [component with { IsSelected = true }],
            },
            catalog);
        Assert.True(mapping.IsSuccess);
        Assert.All(Assert.Single(mapping.Filters).Alternatives, alternative =>
        {
            Assert.Equal(10m, alternative.Minimum);
            Assert.Equal(10m, alternative.Maximum);
        });
    }

    [Fact]
    public void ResolveProviderComponents_ProvenMultilineUniqueBlock_ExpandsIntoIndependentProviderRows()
    {
        var service = CreateService();
        var catalog = OfficialCatalog.Value;
        var sharedBlockId = "unique-block:atomic-fortification";
        var sharedObservations = new[] { "observation:atomic" };
        var draft = new TradeSearchDraft
        {
            ItemClass = "Amulets",
            Rarity = "Unique",
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    SourceModifierIndex = 0,
                    SourceLineIndex = 0,
                    SourceComponentIndex = 0,
                    OriginalText = "You do not inherently take less Damage for having Fortification",
                    CanonicalSignature =
                        "You do not inherently take less Damage for having Fortification",
                    ProviderSearchSignatures =
                    [
                        "You do not inherently take less Damage for having Fortification",
                    ],
                    ParsedKind = ParsedModifierKind.Unique,
                    UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    UniqueCatalogBlockIds = [sharedBlockId],
                    UniqueSourceObservationIds = sharedObservations,
                    ResolvedStatIds = ["should_use_alternate_fortify"],
                    IsSearchable = true,
                    ValueBoundShape = ModifierBoundShape.PresenceOnly,
                    SupportsValueBounds = false,
                    StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
                },
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:1",
                    SourceModifierIndex = 0,
                    SourceLineIndex = 1,
                    SourceComponentIndex = 1,
                    OriginalText = "+4% chance to Suppress Spell Damage per Fortification",
                    CanonicalSignature =
                        "+<number>% chance to Suppress Spell Damage per Fortification",
                    ProviderSearchSignatures =
                    [
                        "+<number>% chance to Suppress Spell Damage per Fortification",
                        "+4% chance to Suppress Spell Damage per Fortification",
                    ],
                    ParsedKind = ParsedModifierKind.Unique,
                    UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    UniqueCatalogBlockIds = [sharedBlockId],
                    UniqueSourceObservationIds = sharedObservations,
                    ResolvedStatIds = ["spell_suppression_chance_%_per_fortification"],
                    IsSearchable = true,
                    SupportsValueBounds = true,
                    ValueBoundShape = ModifierBoundShape.Scalar,
                    CanonicalNumericValues = [4m],
                    RequestedMinimum = 4m,
                    DefaultBoundDirection = ModifierBoundDirection.Minimum,
                    StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
                },
            ],
        };

        var resolved = service.ResolveProviderComponents(
            draft,
            catalog,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Test Unique",
                CanonicalType = "Jade Amulet",
            });

        Assert.Equal(2, resolved.ModifierFilters.Count);
        Assert.All(resolved.ModifierFilters, component =>
        {
            Assert.Equal(0, component.SourceModifierIndex);
            Assert.Equal(sharedBlockId, Assert.Single(component.UniqueCatalogBlockIds));
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
            Assert.True(component.IsSearchable);
            Assert.Empty(component.Contributors);
            Assert.Equal(SearchComponentContributorProjection.None, component.ContributorProjection);
            Assert.True(IsInteractionReady(component));
        });

        var presence = Assert.Single(
            resolved.ModifierFilters,
            component => component.SourceLineIndex == 0);
        Assert.Equal(ModifierBoundShape.PresenceOnly, presence.ValueBoundShape);
        Assert.False(presence.SupportsValueBounds);
        Assert.Null(presence.RequestedMinimum);
        Assert.Null(presence.RequestedMaximum);

        var suppress = Assert.Single(
            resolved.ModifierFilters,
            component => component.SourceLineIndex == 1);
        Assert.Equal(ModifierBoundShape.Scalar, suppress.ValueBoundShape);
        Assert.Equal(4m, suppress.RequestedMinimum);
    }

    [Fact]
    public void BeaconFixture_SourceVariantIdentityRemainsExplicitlyUnsupportedOutsideE6()
    {
        var draft = new TradeSearchDraft
        {
            ItemClass = "Boots",
            Rarity = "Unique",
            DisplayName = "Beacon of Madness",
            ParsedBaseType = "Two-Toned Boots (Armour/Energy Shield)",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "Metadata/Items/Armours/Boots/TwoTonedBootsArmourEnergyShield",
                ResolvedBaseName = "Two-Toned Boots (Armour/Energy Shield)",
            },
        };
        var catalog = new PathOfExileTradeItemCatalog(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "armour",
                GroupLabel = "Armour",
                Name = "Beacon of Madness",
                Type = "Two-Toned Boots",
                IsUnique = true,
            },
        ]);

        var result = new PathOfExileTradeItemIdentityMapper().Map(draft, catalog);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Identity);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueIdentity,
            diagnostic.Code);
    }

    [Fact]
    public void ResolveProviderComponents_AmbiguousProviderMatch_FailClosesSearchability()
    {
        var service = new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatProvider(CreateAmbiguousLifeCatalog()),
            new StaticItemProvider(new PathOfExileTradeItemCatalog([])),
            new PathOfExileTradeSelectedModifierMapper(),
            new PathOfExileTradeItemIdentityMapper(),
            new NoSearchClient(),
            new NoFetchClient());

        var draft = new TradeSearchDraft
        {
            ItemClass = "Body Armour",
            Rarity = "Rare",
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    SourceModifierIndex = 0,
                    SourceLineIndex = 0,
                    OriginalText = "+55 to maximum Life",
                    CanonicalSignature = "+<number> to maximum Life",
                    ParsedKind = ParsedModifierKind.Prefix,
                    GenerationType = ModifierGenerationType.Prefix,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    ResolvedStatIds = ["base_maximum_life"],
                    IsSearchable = true,
                    SupportsValueBounds = true,
                    ValueBoundShape = ModifierBoundShape.Scalar,
                    RequestedMinimum = 55m,
                },
            ],
        };

        var resolved = service.ResolveProviderComponents(draft, CreateAmbiguousLifeCatalog());

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Ambiguous, component.ProviderResolutionStatus);
        Assert.False(component.IsSearchable);
        Assert.False(IsInteractionReady(component));
    }

    [Fact]
    public void OfficialCatalog_FixedLiteralSupportGemStat_ProjectsPresenceOnlyWithoutNumericBounds()
    {
        var catalog = OfficialCatalog.Value;
        var providerStat = catalog.Entries.First(entry =>
            entry.Text.Contains("Spell Echo", StringComparison.OrdinalIgnoreCase) &&
            entry.Text.Contains("Supported", StringComparison.OrdinalIgnoreCase) &&
            entry.Text.Contains("10", StringComparison.Ordinal) &&
            !entry.Text.Contains('#') &&
            string.Equals(entry.Type, "explicit", StringComparison.OrdinalIgnoreCase));

        var component = new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            OriginalText = "Socketed Gems are Supported by Level 10 Spell Echo",
            CanonicalSignature = "Socketed Gems are Supported by Level <number> Spell Echo",
            ProviderSearchSignatures =
            [
                "Socketed Gems are Supported by Level <number> Spell Echo",
                "Socketed Gems are Supported by Level 10 Spell Echo",
            ],
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            UniqueCatalogBlockIds = ["unique-block:support-echo"],
            UniqueSourceObservationIds = ["observation:support-echo"],
            ResolvedStatIds = ["support_spell_echo"],
            IsSearchable = true,
            SupportsValueBounds = false,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            StatMappingProof = ModifierStatMappingProofStatus.WholeVector,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderStatId = providerStat.Id,
            ProviderStatText = providerStat.Text,
            ProviderStatAlternativeIds = [providerStat.Id],
            ProviderCandidateStatIds = [providerStat.Id],
        };

        var candidate = PathOfExileTradeStatCandidateClassifier.ToCandidate(providerStat);
        Assert.Contains('#', candidate.LookupTemplate);
        Assert.DoesNotContain('#', candidate.Text);
        Assert.True(PathOfExileTradeModifierBoundProjector.IsProvenFixedLiteralProviderCandidate(
            component,
            candidate));

        var projection = PathOfExileTradeModifierBoundProjector.ProjectBounds(component, candidate);

        Assert.True(projection.IsFaithful);
        Assert.Equal("ExactFixedLiteralPresence", projection.ProjectionKind);
        Assert.Equal(ModifierBoundShape.PresenceOnly, projection.ValueBoundShape);
        Assert.Null(projection.Minimum);
        Assert.Null(projection.Maximum);
    }

    [Fact]
    public void OfficialCatalog_ControlledDestructionPresence_UsesFixedPresenceBridgeWithoutNumericBounds()
    {
        var catalog = OfficialCatalog.Value;
        var providerStat = catalog.Entries.First(entry =>
            entry.Text.Contains("Controlled Destruction", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.Type, "explicit", StringComparison.OrdinalIgnoreCase));

        var component = new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = "Socketed Gems are Supported by Level 20 Controlled Destruction",
            CanonicalSignature =
                "Socketed Gems are Supported by Level <number> Controlled Destruction",
            ProviderSearchSignatures =
            [
                "Socketed Gems are Supported by Level 20 Controlled Destruction",
            ],
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            UniqueCatalogBlockIds = ["unique-block:controlled-destruction"],
            UniqueSourceObservationIds = ["observation:controlled-destruction"],
            ResolvedStatIds = ["support_controlled_destruction"],
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            SupportsValueBounds = false,
            ProviderFallbackNumericValues = [20m],
            StatMappingProof = ModifierStatMappingProofStatus.ProviderExact,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderStatId = providerStat.Id,
        };

        var candidate = PathOfExileTradeStatCandidateClassifier.ToCandidate(providerStat);
        var projection = PathOfExileTradeModifierBoundProjector.ProjectBounds(component, candidate);

        Assert.True(projection.IsFaithful);
        Assert.Equal(ModifierBoundShape.PresenceOnly, projection.ValueBoundShape);
        Assert.Null(projection.Minimum);
        Assert.Null(projection.Maximum);
    }

    [Fact]
    public void ResolveProviderComponents_MultilineUniqueBlock_RequiresCompleteCatalogRepresentation()
    {
        var service = CreateService();
        var text = string.Join(
            Environment.NewLine,
            "+10% chance to gain Fortification when you Stun an Enemy",
            "10% increased Area of Effect");
        var providerText = string.Join(
            Environment.NewLine,
            "#% chance to gain Fortification when you Stun an Enemy",
            "#% increased Area of Effect");
        var draft = new TradeSearchDraft
        {
            ItemClass = "Body Armour",
            Rarity = "Unique",
            DisplayName = "Willowgift",
            ParsedBaseType = "Festival Garb",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseName = "Festival Garb",
            },
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    SourceModifierIndex = 0,
                    SourceLineIndex = -1,
                    OriginalText = text,
                    CanonicalSignature = providerText,
                    ParsedKind = ParsedModifierKind.Unique,
                    UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    ResolvedStatIds = ["fortification_on_stun", "area_of_effect_+%"],
                    UniqueCatalogBlockIds = ["unique-block:willowgift"],
                    UniqueSourceObservationIds = ["observation:willowgift-a", "observation:willowgift-b"],
                    IsEquivalentSourceSet = true,
                    IsSearchable = true,
                    IsSelected = true,
                },
            ],
        };
        var completeCatalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.complete_willowgift", providerText, "explicit"),
            Stat("explicit.partial_fortification", "#% chance to gain Fortification when you Stun an Enemy", "explicit"),
        ]);

        var resolved = service.ResolveProviderComponents(
            draft,
            completeCatalog,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Willowgift",
                CanonicalType = "Festival Garb",
            });

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("explicit.complete_willowgift", component.ProviderStatId);
        Assert.True(component.IsSearchable);
        Assert.True(IsInteractionReady(component));

        var partialCatalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.partial_fortification", "#% chance to gain Fortification when you Stun an Enemy", "explicit"),
        ]);
        var partial = service.ResolveProviderComponents(
            draft,
            partialCatalog,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Willowgift",
                CanonicalType = "Festival Garb",
            });
        var unsupported = Assert.Single(partial.ModifierFilters);
        Assert.False(unsupported.IsSearchable);
        Assert.NotEqual(SearchComponentProviderResolutionStatus.Exact, unsupported.ProviderResolutionStatus);
    }

    [Fact]
    public void OfficialCatalog_NegativeFireResistanceScalar_ResolvesExactExplicitCandidate()
    {
        var service = CreateService();
        var catalog = OfficialCatalog.Value;
        var draft = new TradeSearchDraft
        {
            ItemClass = "Body Armour",
            Rarity = "Unique",
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    SourceModifierIndex = 0,
                    OriginalText = "-29(-30--20)% to Fire Resistance",
                    CanonicalSignature = "-<number>% to Fire Resistance",
                    ParsedKind = ParsedModifierKind.Unique,
                    UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    ResolvedStatIds = ["base_fire_damage_resistance_%"],
                    UniqueCatalogBlockIds = ["unique-block:fire-res"],
                    UniqueSourceObservationIds = ["observation:fire-res"],
                    IsSearchable = true,
                    SupportsValueBounds = true,
                    ValueBoundShape = ModifierBoundShape.Scalar,
                    ObservedNumericValues = [-29m],
                    CanonicalNumericValues = [-29m],
                    RequestedMinimum = -29m,
                    DefaultBoundDirection = ModifierBoundDirection.Minimum,
                },
            ],
        };

        var resolved = service.ResolveProviderComponents(
            draft,
            catalog,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Willowgift",
                CanonicalType = "Festival Garb",
            });

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.StartsWith("explicit.", component.ProviderStatId, StringComparison.Ordinal);
        Assert.True(component.IsSearchable);
        Assert.True(IsInteractionReady(component));
        var mapped = new PathOfExileTradeSelectedModifierMapper().Map(
            resolved with
            {
                ModifierFilters = [component with { IsSelected = true }],
            },
            catalog);
        Assert.True(mapped.IsSuccess);
        var filter = Assert.Single(mapped.Filters);
        Assert.Equal(-29m, filter.Minimum);
        Assert.Null(filter.Maximum);
    }

    [Fact]
    public void SelectedMapper_ExactRecoveredUniquePresence_SerializesWithoutNumericValueObject()
    {
        var mapper = new PathOfExileTradeSelectedModifierMapper();
        var component = new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            OriginalText = "You can apply an additional Curse",
            CanonicalSignature = "You can apply an additional Curse",
            ParsedKind = ParsedModifierKind.Unknown,
            UniqueOrigin = ParsedUniqueModifierOrigin.Unspecified,
            UsesIdentityBoundUniqueRecovery = true,
            RecoveredSourceKind = ParsedModifierKind.Unique,
            RecoveredSourceUniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            UniqueCatalogBlockIds = ["unique-block:curse"],
            UniqueSourceObservationIds = ["observation:curse"],
            ResolvedStatIds = ["number_of_additional_curses_allowed"],
            IsSearchable = true,
            IsSelected = true,
            StatMappingProof = ModifierStatMappingProofStatus.ProviderExact,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderStatId = "explicit.stat_30642521",
            ProviderCanonicalSignature = "You can apply an additional Curse",
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            SupportsValueBounds = false,
            ProviderFallbackNumericValues = [1m],
        };
        var draft = new TradeSearchDraft
        {
            Rarity = "Unique",
            DisplayName = "Windscream",
            ParsedBaseType = "Reinforced Greaves",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseName = "Reinforced Greaves",
            },
            ModifierFilters = [component],
        };

        var result = mapper.Map(
            draft,
            new PathOfExileTradeStatCatalog(
            [
                Stat(
                    "explicit.stat_30642521",
                    "You can apply # additional Curses",
                    "explicit"),
            ]));

        Assert.True(result.IsSuccess);
        var filter = Assert.Single(result.Filters);
        Assert.Equal("explicit.stat_30642521", filter.StatId);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);

        var query = new PathOfExileTradeQueryBuilder().Build(
            draft,
            TradeSearchValidationResult.FromDiagnostics([]),
            "Standard",
            result.Filters,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Windscream",
                CanonicalType = "Reinforced Greaves",
            });
        Assert.True(query.IsSuccess);
        using var document = JsonDocument.Parse(query.SerializedJson!);
        var serializedFilter = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.Equal("explicit.stat_30642521", serializedFilter.GetProperty("id").GetString());
        Assert.False(serializedFilter.TryGetProperty("value", out _));
    }

    private static PathOfExileTradePriceCheckService CreateService()
    {
        return new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatProvider(OfficialCatalog.Value),
            new StaticItemProvider(new PathOfExileTradeItemCatalog([])),
            new PathOfExileTradeSelectedModifierMapper(),
            new PathOfExileTradeItemIdentityMapper(),
            new NoSearchClient(),
            new NoFetchClient());
    }

    private static PathOfExileTradeStatCatalog CreateAmbiguousLifeCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.life_a", "+# to maximum Life", "explicit"),
            Stat("explicit.life_b", "+# to maximum Life", "explicit"),
        ]);
    }

    private static PathOfExileTradeStatEntry Stat(string id, string text, string type)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = 0,
            GroupId = type,
            GroupLabel = type,
            Id = id,
            Text = text,
            Type = type,
        };
    }

    private static bool IsInteractionReady(ResolvedSearchComponent component) =>
        (bool)(InteractionReadyMethod.Invoke(null, [component]) ?? false);

    private static PathOfExileTradeStatCatalog LoadOfficialCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        var result = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.IsType<PathOfExileTradeStatCatalog>(result.Catalog);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(relativeParts)}");
    }

    private sealed class StaticStatProvider(PathOfExileTradeStatCatalog catalog) : IPathOfExileTradeStatCatalogProvider
    {
        public bool TryGetCachedCatalog(out PathOfExileTradeStatCatalog cachedCatalog)
        {
            cachedCatalog = catalog;
            return true;
        }

        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
    }

    private sealed class StaticItemProvider(PathOfExileTradeItemCatalog catalog) : IPathOfExileTradeItemCatalogProvider
    {
        public bool TryGetCachedCatalog(out PathOfExileTradeItemCatalog cachedCatalog)
        {
            cachedCatalog = catalog;
            return true;
        }

        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeItemCatalogProviderResult.Success(catalog));
    }

    private sealed class NoSearchClient : IPathOfExileTradeSearchClient
    {
        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoFetchClient : IPathOfExileTradeFetchClient
    {
        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
