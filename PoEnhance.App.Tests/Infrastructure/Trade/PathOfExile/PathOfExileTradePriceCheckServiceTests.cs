using System.Net;
using System.Reflection;
using System.Text.Json;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradePriceCheckServiceTests
{
    private const string League = "Mercenaries";
    private const string AlberonsWarpathText = """
Item Class: Boots
Rarity: Unique
Alberon's Warpath
Soldier Boots
--------
Armour: 304 (augmented)
Energy Shield: 19
--------
Requirements:
Level: 49
Str: 47
Int: 47
--------
Sockets: B
--------
Item Level: 85
--------
{ Unique Modifier — Defences, Armour }
+208(180-220) to Armour
{ Unique Modifier — Attribute }
16(15-18)% increased Strength
{ Unique Modifier — Damage, Chaos, Attack }
Adds 1 to 80 Chaos Damage to Attacks
{ Unique Modifier — Chaos, Resistance }
+18(13-19)% to Chaos Resistance
{ Unique Modifier — Speed }
25% increased Movement Speed
{ Unique Modifier }
Summoned Skeleton Warriors are Permanent and Follow you
Summon Skeletons cannot Summon more than 1 Skeleton Warrior — Unscalable Value
--------
Alberon walked among the accursed,
and they welcomed him.
""";

    [Fact]
    public void ResolveProviderComponents_AdvancedLiteralPresence_UsesExactIdentityWithoutValueProjection()
    {
        const string statsJson = """
        {
          "result": [
            {
              "id": "explicit",
              "label": "Explicit",
              "entries": [
                {
                  "id": "explicit.stat_literal_presence",
                  "text": "1 Added Passive Skill is Test Notable",
                  "type": "explicit"
                }
              ]
            }
          ]
        }
        """;
        var parse = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(statsJson);
        Assert.True(parse.IsSuccess);
        var catalog = Assert.IsType<PathOfExileTradeStatCatalog>(parse.Catalog);
        var entry = Assert.Single(catalog.Entries);
        var candidate = PathOfExileTradeStatCandidateClassifier.ToCandidate(entry);
        Assert.Empty(entry.OptionMetadata);
        Assert.Equal(0, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(entry.Text));
        Assert.Equal(1, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(
            candidate.NormalizedTemplate));

        var parsed = new ItemTextParser().Parse("""
        Item Class: Jewels
        Rarity: Rare
        Test Shine
        Medium Cluster Jewel
        --------
        Item Level: 84
        --------
        { Prefix Modifier "Test Prefix" (Tier: 1) }
        1 Added Passive Skill is Test Notable — Unscalable Value
        """);
        var mapped = new TradeSearchDraftMapper().CreateDraft(parsed);
        Assert.True(mapped.IsSuccess);
        var mappedComponent = Assert.Single(Assert.IsType<TradeSearchDraft>(mapped.Draft).ModifierFilters);
        var liveComponent = mappedComponent with
        {
            GenerationType = ModifierGenerationType.Prefix,
            Sources =
            [
                new SearchComponentSourceProvenance
                {
                    ComponentId = mappedComponent.ComponentId,
                    SourceModifierIndex = mappedComponent.SourceModifierIndex,
                    SourceLineIndex = mappedComponent.SourceLineIndex,
                    SourceComponentIndex = mappedComponent.SourceComponentIndex,
                    OriginalText = mappedComponent.OriginalText,
                    CanonicalSignature = mappedComponent.CanonicalSignature,
                    ParsedKind = mappedComponent.ParsedKind,
                    GenerationType = ModifierGenerationType.Prefix,
                    ProviderDomain = "Explicit",
                    ValueBoundShape = mappedComponent.ValueBoundShape,
                },
            ],
        };
        Assert.Equal(ParsedModifierKind.Prefix, liveComponent.ParsedKind);
        Assert.Equal(ModifierBoundShape.PresenceOnly, liveComponent.ValueBoundShape);
        Assert.False(liveComponent.SupportsValueBounds);
        Assert.Null(liveComponent.RequestedMinimum);
        Assert.Null(liveComponent.RequestedMaximum);
        Assert.Empty(liveComponent.ProviderFallbackNumericValues);

        var fixture = ServiceFixture.Create();
        var firstDraft = fixture.Service.ResolveProviderComponents(
            Draft() with { ModifierFilters = [liveComponent] },
            catalog);
        var first = Assert.Single(firstDraft.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, first.ProviderResolutionStatus);
        Assert.Equal(ModifierStatMappingProofStatus.ProviderExact, first.StatMappingProof);
        Assert.Equal(entry.Id, first.ProviderStatId);
        Assert.Equal(ModifierBoundShape.PresenceOnly, first.ValueBoundShape);
        Assert.True(first.IsSearchable);
        Assert.False(first.SupportsValueBounds);
        Assert.Null(first.RequestedMinimum);
        Assert.Null(first.RequestedMaximum);

        var secondDraft = fixture.Service.ResolveProviderComponents(firstDraft, catalog);
        var second = Assert.Single(secondDraft.ModifierFilters);
        Assert.Equal(first.ProviderResolutionStatus, second.ProviderResolutionStatus);
        Assert.Equal(first.StatMappingProof, second.StatMappingProof);
        Assert.Equal(first.ProviderStatId, second.ProviderStatId);
        Assert.Equal(first.ProviderStatText, second.ProviderStatText);
        Assert.Equal(first.SelectedFilterVariantIdentity, second.SelectedFilterVariantIdentity);
        Assert.Equal(first.ValueBoundShape, second.ValueBoundShape);
        Assert.Equal(first.SupportsValueBounds, second.SupportsValueBounds);
        Assert.Equal(first.RequestedMinimum, second.RequestedMinimum);
        Assert.Equal(first.RequestedMaximum, second.RequestedMaximum);
        Assert.Equal(first.IsSearchable, second.IsSearchable);

        var selectedDraft = secondDraft with
        {
            ModifierFilters = [second with { IsSelected = true }],
        };
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(selectedDraft, catalog);
        Assert.True(mapping.IsSuccess);
        var providerFilter = Assert.Single(mapping.Filters);
        Assert.Equal(entry.Id, providerFilter.StatId);
        Assert.Null(providerFilter.Minimum);
        Assert.Null(providerFilter.Maximum);

        var build = new PathOfExileTradeQueryBuilder().Build(
            selectedDraft,
            new TradeSearchDraftValidator().Validate(selectedDraft),
            League,
            mapping.Filters);
        Assert.True(build.IsSuccess);
        using var document = JsonDocument.Parse(build.SerializedJson!);
        var statFilter = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")
            .EnumerateArray()
            .SelectMany(group => group.GetProperty("filters").EnumerateArray()));
        Assert.Equal(entry.Id, statFilter.GetProperty("id").GetString());
        Assert.False(statFilter.TryGetProperty("value", out _));
    }

    [Theory]
    [InlineData("raw-placeholder")]
    [InlineData("missing-identity")]
    [InlineData("ambiguous-identity")]
    [InlineData("wrong-kind")]
    [InlineData("candidate-fractured")]
    [InlineData("candidate-veiled")]
    [InlineData("fractured")]
    [InlineData("veiled")]
    [InlineData("unveiled")]
    [InlineData("minimum")]
    [InlineData("maximum")]
    [InlineData("supports-value-bounds")]
    [InlineData("hidden-numeric-values")]
    [InlineData("proofless")]
    [InlineData("option-metadata")]
    [InlineData("generation-mismatch")]
    [InlineData("source-domain-mismatch")]
    [InlineData("selected-identity-mismatch")]
    [InlineData("requested-identity-mismatch")]
    [InlineData("requested-kind-mismatch")]
    public void ResolveProviderComponents_AdvancedLiteralPresenceGuard_UnsafeShapeFallsThrough(
        string scenario)
    {
        const string text = "1 Added Passive Skill is Test Notable";
        var component = StructuredLiteralPresenceComponent(text);
        IReadOnlyList<PathOfExileTradeStatEntry> entries =
        [
            Stat("explicit.stat_literal_presence", text, "explicit"),
        ];

        switch (scenario)
        {
            case "raw-placeholder":
                entries = [Stat("explicit.stat_literal_presence", "# Added Passive Skill is Test Notable", "explicit")];
                break;
            case "missing-identity":
                entries = [];
                break;
            case "ambiguous-identity":
                entries =
                [
                    Stat("explicit.stat_literal_presence.one", text, "explicit"),
                    Stat("explicit.stat_literal_presence.two", text, "explicit") with { ProviderOrder = 1 },
                ];
                break;
            case "wrong-kind":
                entries = [Stat("implicit.stat_literal_presence", text, "implicit")];
                break;
            case "candidate-fractured":
                entries = [Stat("fractured.stat_literal_presence", text, "fractured")];
                break;
            case "candidate-veiled":
                entries = [Stat("veiled.stat_literal_presence", text, "veiled")];
                break;
            case "fractured":
                component = component with
                {
                    IsFractured = true,
                    Sources = component.Sources.Select(source => source with { IsFractured = true }).ToArray(),
                };
                break;
            case "veiled":
                component = component with
                {
                    IsVeiled = true,
                    Sources = component.Sources.Select(source => source with { IsVeiled = true }).ToArray(),
                };
                break;
            case "unveiled":
                component = component with
                {
                    IsUnveiled = true,
                    Sources = component.Sources.Select(source => source with { IsUnveiled = true }).ToArray(),
                };
                break;
            case "minimum":
                component = component with { RequestedMinimum = 1m };
                break;
            case "maximum":
                component = component with { RequestedMaximum = 1m };
                break;
            case "supports-value-bounds":
                component = component with { SupportsValueBounds = true };
                break;
            case "hidden-numeric-values":
                component = component with
                {
                    ObservedNumericValues = [1m],
                    CanonicalNumericValues = [1m],
                    ProviderFallbackNumericValues = [1m],
                };
                break;
            case "proofless":
                component = component with
                {
                    SourceModifierIndex = -1,
                    SourceLineIndex = -1,
                    Sources = [],
                };
                break;
            case "option-metadata":
                entries =
                [
                    Stat("explicit.stat_literal_presence", text, "explicit") with
                    {
                        OptionMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["option"] = "1",
                        },
                    },
                ];
                break;
            case "generation-mismatch":
                component = component with
                {
                    GenerationType = ModifierGenerationType.Suffix,
                    Sources = component.Sources.Select(source => source with
                    {
                        GenerationType = ModifierGenerationType.Suffix,
                    }).ToArray(),
                };
                break;
            case "source-domain-mismatch":
                component = component with
                {
                    Sources = component.Sources.Select(source => source with
                    {
                        ProviderDomain = "Crafted",
                    }).ToArray(),
                };
                break;
            case "selected-identity-mismatch":
                component = component with
                {
                    SelectedFilterVariantIdentity = PathOfExileTradeProviderIdentity.Create(
                        "explicit.stat_other"),
                };
                break;
            case "requested-identity-mismatch":
                component = component with
                {
                    RequestedFilterVariantIdentity = PathOfExileTradeProviderIdentity.Create(
                        "explicit.stat_other"),
                };
                break;
            case "requested-kind-mismatch":
                component = component with { RequestedFilterVariantKind = "crafted" };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        var resolved = Assert.Single(ServiceFixture.Create().Service.ResolveProviderComponents(
            Draft() with { ModifierFilters = [component] },
            new PathOfExileTradeStatCatalog(entries)).ModifierFilters);

        Assert.DoesNotContain(
            resolved.ProviderResolutionStatus,
            new[]
            {
                SearchComponentProviderResolutionStatus.Exact,
                SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            });
        Assert.Null(resolved.ProviderStatId);
        Assert.Empty(resolved.FilterVariants);
    }

    [Fact]
    public void ResolveProviderComponents_StructuredAdvancedNegativeScalarUsesExactProviderPresenceWhenGameDataValueProjectionIsUnavailable()
    {
        var fixture = ServiceFixture.Create();
        var draft = Draft() with
        {
            ModifierFilters =
            [
                SpecialComponent(
                    "Non-Channelling Skills have -7(-7--6) to Total Mana Cost",
                    "Non-Channelling Skills have -<number> to Total Mana Cost") with
                {
                    ResolutionStatus = ModifierCandidateResolutionStatus.Unknown,
                    ResolvedModifierId = null,
                    ResolvedStatIds = [],
                    IsCrafted = true,
                    IsSearchable = false,
                    NotSearchableReason = "The source modifier did not resolve to one exact GameData modifier.",
                    ValueBoundShape = ModifierBoundShape.Unsupported,
                    IsSelected = true,
                },
            ],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat(
                "crafted.stat_mana_cost",
                "Non-Channelling Skills have +# to Total Mana Cost",
                "crafted"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(draft, catalog);

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal(ModifierStatMappingProofStatus.ProviderExact, component.StatMappingProof);
        Assert.Equal("crafted.stat_mana_cost", component.ProviderStatId);
        Assert.True(component.IsSearchable);
        Assert.False(component.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.PresenceOnly, component.ValueBoundShape);
        Assert.Null(component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.True(new PathOfExileTradeSelectedModifierMapper().Map(resolved, catalog).IsSuccess);
    }

    [Fact]
    public void ResolveProviderComponents_ExactProviderOwnedPresenceIsIdempotent()
    {
        var fixture = ServiceFixture.Create();
        var source = new SearchComponentSourceProvenance
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            OriginalText = "Non-Channelling Skills have -7(-7--6) to Total Mana Cost",
            CanonicalSignature = "Non-Channelling Skills have -<number> to Total Mana Cost",
            ParsedKind = ParsedModifierKind.Suffix,
            ProviderDomain = "Crafted",
            IsCrafted = true,
            ResolvedModifierId = "mod.special.test",
            ResolvedStatIds = ["stat.special.test"],
            ValueBoundShape = ModifierBoundShape.Unsupported,
        };
        var draft = Draft() with
        {
            ModifierFilters =
            [
                SpecialComponent(source.OriginalText, source.CanonicalSignature) with
                {
                    ResolutionStatus = ModifierCandidateResolutionStatus.Unknown,
                    ResolvedModifierId = null,
                    ResolvedStatIds = [],
                    IsCrafted = true,
                    IsSearchable = false,
                    ValueBoundShape = ModifierBoundShape.Unsupported,
                    Sources = [source],
                },
            ],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat(
                "crafted.stat_mana_cost",
                "Non-Channelling Skills have +# to Total Mana Cost",
                "crafted"),
        ]);

        var first = Assert.Single(fixture.Service.ResolveProviderComponents(draft, catalog).ModifierFilters);
        var second = Assert.Single(fixture.Service.ResolveProviderComponents(
            draft with { ModifierFilters = [first] },
            catalog).ModifierFilters);

        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, second.ProviderResolutionStatus);
        Assert.Equal(first.ProviderStatId, second.ProviderStatId);
        Assert.Equal(ModifierStatMappingProofStatus.ProviderExact, second.StatMappingProof);
        Assert.Equal(first.SelectedFilterVariantIdentity, second.SelectedFilterVariantIdentity);
        Assert.Equal(first.RequestedFilterVariantIdentity, second.RequestedFilterVariantIdentity);
        Assert.Equal(first.RequestedFilterVariantKind, second.RequestedFilterVariantKind);
        Assert.Equal(
            Assert.Single(first.FilterVariants).ProviderKind,
            Assert.Single(second.FilterVariants).ProviderKind);
        Assert.Equal(
            Assert.Single(first.Sources).ProviderDomain,
            Assert.Single(second.Sources).ProviderDomain);
        Assert.Equal(ModifierBoundShape.PresenceOnly, second.ValueBoundShape);
        Assert.False(second.SupportsValueBounds);
        Assert.Null(second.RequestedMinimum);
        Assert.Null(second.RequestedMaximum);
        Assert.True(second.IsSearchable);
        Assert.Equal(first.ProviderDiagnosticCode, second.ProviderDiagnosticCode);
        Assert.Equal(first.ProviderDiagnosticMessage, second.ProviderDiagnosticMessage);
    }

    [Fact]
    public void ResolveProviderComponents_UnresolvedPresenceLikeTextWithoutExactProofStaysUnsupported()
    {
        var fixture = ServiceFixture.Create();
        var draft = Draft() with
        {
            ModifierFilters =
            [
                SpecialComponent(
                    "1 Added Passive Skill is Test Notable",
                    "<number> Added Passive Skill is Test Notable") with
                {
                    SourceModifierIndex = -1,
                    SourceLineIndex = -1,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Unknown,
                    ResolvedModifierId = null,
                    ResolvedStatIds = [],
                    IsSearchable = false,
                    ValueBoundShape = ModifierBoundShape.Unsupported,
                },
            ],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat(
                "explicit.stat_presence_like",
                "1 Added Passive Skill is Test Notable",
                "explicit"),
        ]);

        var resolved = Assert.Single(fixture.Service.ResolveProviderComponents(draft, catalog).ModifierFilters);

        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, resolved.ProviderResolutionStatus);
        Assert.NotEqual(ModifierStatMappingProofStatus.ProviderExact, resolved.StatMappingProof);
        Assert.Null(resolved.ProviderStatId);
        Assert.False(resolved.IsSearchable);
    }

    [Fact]
    public void ResolveProviderComponents_ExactProviderOwnedPresenceDoesNotRetainMissingIdentity()
    {
        var fixture = ServiceFixture.Create();
        var draft = Draft() with
        {
            ModifierFilters =
            [
                SpecialComponent(
                    "1 Added Passive Skill is Test Notable",
                    "<number> Added Passive Skill is Test Notable") with
                {
                    ResolutionStatus = ModifierCandidateResolutionStatus.Unknown,
                    ResolvedModifierId = null,
                    ResolvedStatIds = [],
                    IsSearchable = false,
                    ValueBoundShape = ModifierBoundShape.Unsupported,
                },
            ],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat(
                "explicit.stat_presence_like",
                "1 Added Passive Skill is Test Notable",
                "explicit"),
        ]);
        var first = Assert.Single(fixture.Service.ResolveProviderComponents(draft, catalog).ModifierFilters);

        var second = Assert.Single(fixture.Service.ResolveProviderComponents(
            draft with { ModifierFilters = [first] },
            new PathOfExileTradeStatCatalog([])).ModifierFilters);

        Assert.NotEqual(SearchComponentProviderResolutionStatus.Exact, second.ProviderResolutionStatus);
        Assert.Null(second.ProviderStatId);
        Assert.NotEqual(first.ProviderStatId, second.ProviderStatId);
    }

    [Fact]
    public void ResolveProviderComponents_ExactProviderOwnedPresenceDoesNotRetainChangedVariantRequest()
    {
        var fixture = ServiceFixture.Create();
        var draft = Draft() with
        {
            ModifierFilters =
            [
                SpecialComponent(
                    "1 Added Passive Skill is Test Notable",
                    "<number> Added Passive Skill is Test Notable") with
                {
                    ResolutionStatus = ModifierCandidateResolutionStatus.Unknown,
                    ResolvedModifierId = null,
                    ResolvedStatIds = [],
                    IsSearchable = false,
                    ValueBoundShape = ModifierBoundShape.Unsupported,
                },
            ],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat(
                "explicit.stat_presence_like",
                "1 Added Passive Skill is Test Notable",
                "explicit"),
        ]);
        var first = Assert.Single(fixture.Service.ResolveProviderComponents(draft, catalog).ModifierFilters);
        var requestedIdentity = PathOfExileTradeProviderIdentity.Create("explicit.stat.other");

        var second = Assert.Single(fixture.Service.ResolveProviderComponents(
            draft with
            {
                ModifierFilters =
                [
                    first with
                    {
                        SelectedFilterVariantIdentity = requestedIdentity,
                        RequestedFilterVariantIdentity = requestedIdentity,
                        RequestedFilterVariantKind = "explicit",
                    },
                ],
            },
            catalog).ModifierFilters);

        Assert.NotEqual(SearchComponentProviderResolutionStatus.Exact, second.ProviderResolutionStatus);
        Assert.Null(second.ProviderStatId);
        Assert.Equal(requestedIdentity, second.SelectedFilterVariantIdentity);
    }

    [Fact]
    public void ResolveProviderComponents_SelectedOrdinaryUniqueScalar_UsesExplicitDomainAndMapperPreservesIt()
    {
        var fixture = ServiceFixture.Create();
        var draft = UniqueDraft() with
        {
            ModifierFilters = [UniqueComponent("+69 to maximum Life", "+<number> to maximum Life") with
            {
                GenerationType = ModifierGenerationType.Implicit,
                SupportsValueBounds = true,
                ValueBoundShape = ModifierBoundShape.Scalar,
                ObservedNumericValues = [69m],
                CanonicalNumericValues = [69m],
                RequestedMinimum = 69m,
                IsSelected = true,
                ProviderDomainEvidence =
                [
                    new SearchComponentProviderDomainEvidence
                    {
                        ProviderDomain = "Implicit",
                        ModifierId = "unique.mod.life",
                        GenerationType = ModifierGenerationType.Implicit,
                        Locality = ModifierLocality.Global,
                        IsSourceExact = true,
                        EvidenceStrength = 1000,
                        ApplicabilityReason = "Exact source-generation evidence.",
                    },
                ],
            }],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
            Stat("implicit.stat_life", "+# to maximum Life", "implicit"),
            Stat("pseudo.total_life", "+# to maximum Life", "pseudo"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.No));

        var component = Assert.Single(resolved.ModifierFilters);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(resolved, catalog);
        Assert.True(
            component.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact,
            $"{component.ProviderResolutionStatus}: {component.ProviderDiagnosticCode} {component.ProviderDiagnosticMessage}");
        Assert.Equal(ModifierStatMappingProofStatus.ProviderExact, component.StatMappingProof);
        Assert.Equal("explicit.stat_life", component.ProviderStatId);
        Assert.True(component.IsSearchable);
        Assert.True(component.SupportsValueBounds);
        Assert.Equal(69m, component.RequestedMinimum);
        Assert.True(component.IsSelected);
        Assert.DoesNotContain(component.FilterVariants, variant =>
            string.Equals(variant.ProviderKind, "implicit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(component.FilterVariants, variant =>
            string.Equals(variant.ProviderKind, "pseudo", StringComparison.OrdinalIgnoreCase));
        Assert.True(mapping.IsSuccess);
        Assert.Equal("explicit.stat_life", Assert.Single(mapping.Filters).StatId);
    }

    [Fact]
    public void ResolveProviderComponents_SelectedOrdinaryUniquePresence_PreservesSelectionWithoutBounds()
    {
        var fixture = ServiceFixture.Create();
        var draft = UniqueDraft() with
        {
            ModifierFilters =
            [
                UniqueComponent(
                    "Gain Arcane Surge when you use a Movement Skill",
                    "Gain Arcane Surge when you use a Movement Skill") with
                {
                    IsSelected = true,
                },
            ],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat(
                "explicit.stat_arcane_surge",
                "Gain Arcane Surge when you use a Movement Skill",
                "explicit"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.No));

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.True(
            component.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact,
            $"{component.ProviderResolutionStatus}: {component.ProviderDiagnosticCode} {component.ProviderDiagnosticMessage}");
        Assert.Equal("explicit.stat_arcane_surge", component.ProviderStatId);
        Assert.True(component.IsSearchable);
        Assert.True(component.IsSelected);
        Assert.Equal(ModifierBoundShape.PresenceOnly, component.ValueBoundShape);
        Assert.False(component.SupportsValueBounds);
        Assert.Null(component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
    }

    [Fact]
    public void ResolveProviderComponents_FoulbornPresence_RequiresFoulbornIdentityAndNeverBorrowsOrdinaryProof()
    {
        var fixture = ServiceFixture.Create();
        var component = UniqueComponent(
            "Test Foulborn presence",
            "Test Foulborn presence",
            ParsedUniqueModifierOrigin.Foulborn) with
        {
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "foulborn.modifier.test",
            ResolvedStatIds = ["foulborn_stat"],
            UniqueFoulbornRelationshipIds = ["foulborn-relationship:test"],
            UniqueNormalCounterpartModifierIds = ["normal.modifier.test"],
            UniqueSourceObservationIds = ["pob-foulborn-source:test"],
            IsSearchable = true,
        };
        var draft = UniqueDraft("Foulborn Moonbender's Wing") with
        {
            ModifierFilters = [component],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.foulborn_presence", "Test Foulborn presence", "explicit"),
        ]);

        var ordinaryIdentity = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.No));
        var foulbornIdentity = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.Yes));

        var unsupported = Assert.Single(ordinaryIdentity.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, unsupported.ProviderResolutionStatus);
        Assert.False(unsupported.IsSearchable);
        Assert.False(unsupported.IsSelected);

        var exact = Assert.Single(foulbornIdentity.ModifierFilters);
        Assert.True(
            exact.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact,
            $"{exact.ProviderResolutionStatus}: {exact.ProviderDiagnosticCode} {exact.ProviderDiagnosticMessage}");
        Assert.Equal(ModifierStatMappingProofStatus.ProviderExact, exact.StatMappingProof);
        Assert.Equal("explicit.foulborn_presence", exact.ProviderStatId);
        Assert.Equal(["foulborn-relationship:test"], exact.UniqueFoulbornRelationshipIds);
        Assert.Equal(["normal.modifier.test"], exact.UniqueNormalCounterpartModifierIds);
        Assert.False(exact.SupportsValueBounds);
        Assert.Null(exact.RequestedMinimum);
        Assert.Null(exact.RequestedMaximum);
        Assert.False(exact.IsSelected);
    }

    [Fact]
    public void ResolveProviderComponents_AuthoritativeFoulbornMechanicsBlocker_IsNotReenabledByTextFallback()
    {
        var fixture = ServiceFixture.Create();
        var component = UniqueComponent(
            "Test Foulborn replacement",
            "Test Foulborn replacement",
            ParsedUniqueModifierOrigin.Foulborn) with
        {
            UniqueResolutionDiagnosticCode = "FOULBORN_REPLACEMENT_MECHANICS_UNAVAILABLE",
            IsSearchable = false,
        };
        var draft = UniqueDraft("Foulborn Test Unique") with
        {
            ModifierFilters = [component],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.foulborn_replacement", "Test Foulborn replacement", "explicit"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.Yes));

        var blocked = Assert.Single(resolved.ModifierFilters);
        Assert.False(blocked.IsSearchable);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, blocked.ProviderResolutionStatus);
        Assert.Null(blocked.ProviderStatId);
        Assert.Empty(blocked.ProviderStatAlternativeIds);
        Assert.Equal(
            "FOULBORN_REPLACEMENT_MECHANICS_UNAVAILABLE",
            blocked.UniqueResolutionDiagnosticCode);
    }

    [Fact]
    public void ResolveProviderComponents_CatalogProvenUniqueEquivalentSet_RemainsOneSearchableRow()
    {
        var fixture = ServiceFixture.Create();
        var component = UniqueComponent("Unique effect is active", "Unique effect is active") with
        {
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedStatIds = ["unique_effect_stat"],
            ResolvedStatLocalities = [ModifierLocality.Global],
            Locality = ModifierLocality.Global,
            UniqueCatalogBlockIds = ["unique-block:test"],
            UniqueSourceObservationIds = ["pob-observation:test"],
            IsSearchable = true,
        };
        var draft = UniqueDraft() with { ModifierFilters = [component] };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.unique.one", "Unique effect is active", "explicit"),
            Stat("explicit.unique.two", "Unique effect is active", "explicit"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.No));

        var row = Assert.Single(resolved.ModifierFilters);
        Assert.True(row.IsSearchable);
        Assert.Equal(SearchComponentProviderResolutionStatus.ExactEquivalentSet, row.ProviderResolutionStatus);
        Assert.Null(row.ProviderStatId);
        Assert.Equal(
            ["explicit.unique.one", "explicit.unique.two"],
            row.ProviderStatAlternativeIds);
        Assert.Single(row.FilterVariants);

        var controllerTimeResolution = fixture.Service.ResolveProviderComponents(
            resolved,
            catalog);

        var retained = Assert.Single(controllerTimeResolution.ModifierFilters);
        Assert.True(retained.IsSearchable);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            retained.ProviderResolutionStatus);
        Assert.Equal(
            ["explicit.unique.one", "explicit.unique.two"],
            retained.ProviderStatAlternativeIds);
        Assert.Single(retained.FilterVariants);
        Assert.Null(retained.ProviderDiagnosticCode);
    }

    [Fact]
    public void ResolveProviderComponents_UnselectedMultiLineUniqueBlockRemainsUnselectedAndUnsupported()
    {
        var fixture = ServiceFixture.Create();
        var draft = UniqueDraft("Foulborn Midnight Bargain", "Calling Wand") with
        {
            ModifierFilters =
            [
                UniqueComponent("+1 to maximum number of Raised Zombies", "+<number> to maximum number of Raised Zombies") with
                {
                    SourceModifierIndex = 0,
                    SourceLineIndex = 0,
                },
                UniqueComponent("+1 to maximum number of Spectres", "+<number> to maximum number of Spectres") with
                {
                    SourceModifierIndex = 0,
                    SourceLineIndex = 1,
                },
            ],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.zombies", "+# to maximum number of Raised Zombies", "explicit"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.Yes));

        Assert.Equal(2, resolved.ModifierFilters.Count);
        Assert.All(resolved.ModifierFilters, component =>
        {
            Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, component.ProviderResolutionStatus);
            Assert.Equal(
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.UniqueMultiLinePartialRepresentation,
                component.ProviderDiagnosticCode);
            Assert.False(component.IsSearchable);
            Assert.False(component.IsSelected);
            Assert.Null(component.ProviderStatId);
        });
    }

    [Fact]
    public void ResolveProviderComponents_SelectedMultiLineUniqueBlockPreservesIntentBoundsAndProvenance()
    {
        var fixture = ServiceFixture.Create();
        var first = UniqueComponent(
            "+1 to maximum number of Raised Zombies",
            "+<number> to maximum number of Raised Zombies") with
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            IsSelected = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            RequestedMinimum = 1m,
            RequestedMaximum = 2m,
            SelectedFilterVariantIdentity = "variant.explicit",
            FilterVariants =
            [
                new SearchFilterVariant
                {
                    Identity = "variant.explicit",
                    Label = "Explicit",
                    Description = "+# to maximum number of Raised Zombies",
                    ProviderKind = "explicit",
                    SupportsValueBounds = true,
                },
            ],
            Sources = [UniqueSource(
                "modifier:0:0",
                sourceModifierIndex: 0,
                sourceLineIndex: 0,
                "+1 to maximum number of Raised Zombies")],
        };
        var second = UniqueComponent(
            "+1 to maximum number of Spectres",
            "+<number> to maximum number of Spectres") with
        {
            ComponentId = "modifier:0:1",
            SourceModifierIndex = 0,
            SourceLineIndex = 1,
            IsSelected = true,
            Sources = [UniqueSource(
                "modifier:0:1",
                sourceModifierIndex: 0,
                sourceLineIndex: 1,
                "+1 to maximum number of Spectres")],
        };
        var draft = UniqueDraft("Foulborn Midnight Bargain", "Calling Wand") with
        {
            ModifierFilters = [first, second],
        };

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            EmptyStatCatalog(),
            UniqueIdentity(TradeTriState.Yes));

        Assert.Equal(2, resolved.ModifierFilters.Count);
        Assert.All(resolved.ModifierFilters, component =>
        {
            Assert.True(component.IsSelected);
            Assert.False(component.IsSearchable);
            Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, component.ProviderResolutionStatus);
            Assert.Equal(
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.UniqueMultiLinePartialRepresentation,
                component.ProviderDiagnosticCode);
            Assert.Null(component.ProviderStatId);
            Assert.Empty(component.FilterVariants);
        });

        var resolvedFirst = resolved.ModifierFilters[0];
        Assert.Equal(1m, resolvedFirst.RequestedMinimum);
        Assert.Equal(2m, resolvedFirst.RequestedMaximum);
        Assert.Equal("variant.explicit", resolvedFirst.SelectedFilterVariantIdentity);

        Assert.Collection(
            resolved.ModifierFilters,
            component => AssertUniqueSource(component, first),
            component => AssertUniqueSource(component, second));

        var validation = new TradeSearchDraftValidator().Validate(resolved);
        var unresolved = validation.Diagnostics
            .Where(diagnostic =>
                diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierVariantUnresolved &&
                diagnostic.Message.Contains(
                    "Multi-line Unique modifier blocks remain unsupported",
                    StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, unresolved.Length);
    }

    [Fact]
    public void ResolveProviderComponents_OneCatalogBackedMultiLineBlock_UsesOnlyCompleteProviderRepresentation()
    {
        var fixture = ServiceFixture.Create();
        var text = string.Join(Environment.NewLine,
        [
            "+1 to maximum number of Raised Zombies",
            "+1 to maximum number of Spectres",
        ]);
        var providerText = string.Join(Environment.NewLine,
        [
            "+# to maximum number of Raised Zombies",
            "+# to maximum number of Spectres",
        ]);
        var draft = UniqueDraft("Foulborn Midnight Bargain", "Calling Wand") with
        {
            ModifierFilters =
            [
                UniqueComponent(text, providerText) with
                {
                    SourceModifierIndex = 0,
                    SourceLineIndex = -1,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    ResolvedStatIds = ["zombie_stat", "spectre_stat"],
                    UniqueCatalogBlockIds = ["unique-block:minions"],
                    UniqueSourceObservationIds = ["pob:first", "pob:second"],
                    IsEquivalentSourceSet = true,
                    IsSearchable = true,
                    IsSelected = true,
                },
            ],
        };
        var completeCatalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.complete_minion_block", providerText, "explicit"),
            Stat("explicit.partial_zombies", "+# to maximum number of Raised Zombies", "explicit"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            completeCatalog,
            UniqueIdentity(TradeTriState.Yes));

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.True(component.IsSelected);
        Assert.True(component.IsSearchable);
        Assert.True(
            component.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact,
            $"{component.ProviderResolutionStatus}: {component.ProviderDiagnosticCode} {component.ProviderDiagnosticMessage}");
        Assert.Equal("explicit.complete_minion_block", component.ProviderStatId);
        Assert.DoesNotContain("explicit.partial_zombies", component.ProviderStatAlternativeIds);
        Assert.Equal(2, component.UniqueSourceObservationIds.Count);

        var partialOnly = fixture.Service.ResolveProviderComponents(
            draft,
            new PathOfExileTradeStatCatalog(
            [
                Stat("explicit.partial_zombies", "+# to maximum number of Raised Zombies", "explicit"),
            ]),
            UniqueIdentity(TradeTriState.Yes));
        var unsupported = Assert.Single(partialOnly.ModifierFilters);
        Assert.True(unsupported.IsSelected);
        Assert.False(unsupported.IsSearchable);
        Assert.NotEqual(SearchComponentProviderResolutionStatus.Exact,
            unsupported.ProviderResolutionStatus);
        Assert.Null(unsupported.ProviderStatId);
    }

    [Fact]
    public void ResolveProviderComponents_UniqueWithOnlyBroadPseudoCandidateRemainsUnsupported()
    {
        var fixture = ServiceFixture.Create();
        var draft = UniqueDraft() with
        {
            ModifierFilters = [UniqueComponent("+69 to maximum Life", "+<number> to maximum Life")],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("pseudo.total_life", "+# to maximum Life", "pseudo"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            UniqueIdentity(TradeTriState.No));

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.NotFound, component.ProviderResolutionStatus);
        Assert.False(component.IsSearchable);
        Assert.False(component.IsSelected);
        Assert.Null(component.ProviderStatId);
    }

    [Fact]
    public void ResolveProviderComponents_FracturedModifier_ExposesSafeVariantsAndDefaultsToFractured()
    {
        var fixture = ServiceFixture.Create();
        var draft = Draft() with
        {
            ItemStates = ["Fractured Item"],
            ModifierFilters =
            [
                SpecialComponent("+84 to maximum Life", "+<number> to maximum Life") with
                {
                    IsFractured = true,
                    Locality = ModifierLocality.Global,
                    SupportsValueBounds = true,
                    ValueBoundShape = ModifierBoundShape.Scalar,
                    CanonicalNumericValues = [84m],
                    ValueBoundTranslationHandlers = [[]],
                    ValueBoundTranslationIdentity = "test-life",
                    RequestedMinimum = 84m,
                    ProviderDomainEvidence =
                    [
                        new SearchComponentProviderDomainEvidence
                        {
                            ProviderDomain = "Fractured",
                            ModifierId = "mod.special.test",
                            GenerationType = ModifierGenerationType.Suffix,
                            Locality = ModifierLocality.Global,
                            IsSourceExact = true,
                            ItemBaseId = "base.fixture",
                            ItemClass = "Ring",
                            ApplicabilityReason = "Exact Fractured source fixture.",
                        },
                        new SearchComponentProviderDomainEvidence
                        {
                            ProviderDomain = "Explicit",
                            ModifierId = "mod.ordinary.test",
                            GenerationType = ModifierGenerationType.Suffix,
                            Locality = ModifierLocality.Global,
                            IsProjectedDomain = true,
                            ItemBaseId = "base.fixture",
                            ItemClass = "Ring",
                            ApplicabilityReason = "An independently eligible ordinary family fixture.",
                        },
                    ],
                },
            ],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
            Stat("fractured.stat_life", "+# to maximum Life", "fractured"),
            Stat("pseudo.total_life", "+# to maximum Life", "pseudo"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(draft, catalog);

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("fractured.stat_life", component.ProviderStatId);
        Assert.Equal(3, component.FilterVariants.Count);
        Assert.Contains(component.FilterVariants, variant => variant.ProviderKind == "fractured");
        Assert.Contains(component.FilterVariants, variant => variant.ProviderKind == "explicit");
        Assert.Contains(component.FilterVariants, variant => variant.ProviderKind == "pseudo");
        Assert.Equal(
            "fractured",
            Assert.Single(component.FilterVariants, variant =>
                variant.Identity == component.SelectedFilterVariantIdentity).ProviderKind,
            ignoreCase: true);
        Assert.Equal(component.SelectedFilterVariantIdentity, component.RequestedFilterVariantIdentity);
        Assert.Equal("fractured", component.RequestedFilterVariantKind);
        Assert.Null(component.ProviderDiagnosticMessage);
        Assert.True(component.SupportsValueBounds);
        Assert.Equal(84m, component.RequestedMinimum);
        Assert.False(component.IsSelected);
    }

    [Fact]
    public void ResolveProviderComponents_FracturedModifier_UsesStructuredApproximationWhenEveryGuardExists()
    {
        var fixture = ServiceFixture.Create();
        var draft = SafeFracturedBaseDraft() with
        {
            ModifierFilters =
            [
                SpecialComponent("+84 to maximum Life", "+<number> to maximum Life") with
                {
                    IsFractured = true,
                    Locality = ModifierLocality.Global,
                    SupportsValueBounds = true,
                    ValueBoundShape = ModifierBoundShape.Scalar,
                    CanonicalNumericValues = [84m],
                    ValueBoundTranslationHandlers = [[]],
                    ValueBoundTranslationIdentity = "test-life",
                    RequestedMinimum = 84m,
                    IsSelected = true,
                },
            ],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
            Stat("pseudo.total_life", "+# to maximum Life", "pseudo"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            filterCatalog: FracturedStateFilterCatalog());

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Approximate, component.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_life", component.ProviderStatId);
        Assert.Equal(3, component.FilterVariants.Count);
        Assert.Contains(component.FilterVariants, variant =>
            variant.Identity == PathOfExileTradeModifierVariantResolver.FracturedRequestIdentity &&
            variant.ProviderKind == "fractured");
        Assert.Contains(component.FilterVariants, variant => variant.ProviderKind == "pseudo");
        Assert.Equal("explicit", Assert.Single(component.FilterVariants, variant =>
            variant.Identity == component.SelectedFilterVariantIdentity).ProviderKind);
        Assert.Equal(
            PathOfExileTradeModifierVariantResolver.FracturedRequestIdentity,
            component.RequestedFilterVariantIdentity);
        Assert.Equal("fractured", component.RequestedFilterVariantKind);
        Assert.Equal(
            PathOfExileTradeModifierVariantResolver.FracturedApproximationMessage,
            component.ProviderDiagnosticMessage);
        Assert.Equal(84m, component.RequestedMinimum);
        Assert.True(component.IsFractured);
        Assert.True(component.IsSelected);
        Assert.Equal(BaseSearchMode.ExactBase, resolved.Base.ActiveCriterion?.Mode);
        Assert.Equal("Titan Plate", resolved.Base.ActiveCriterion?.ExactBaseName);
        Assert.True(resolved.Base.IsExactBaseForcedByFracturedApproximation);
        Assert.True(resolved.Base.IsFracturedStateForcedByFracturedApproximation);
        Assert.Equal(TradeTriState.Yes, resolved.ItemStateCriteria.Fractured);
    }

    [Theory]
    [InlineData("explicit")]
    [InlineData("pseudo")]
    public void ResolveProviderComponents_FracturedSourceManualVariantControlsResolution(
        string requestedKind)
    {
        var fixture = ServiceFixture.Create();
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
            Stat("pseudo.total_life", "+# to maximum Life", "pseudo"),
        ]);
        var initialDraft = SafeFracturedBaseDraft() with
        {
            ModifierFilters = [FracturedLifeComponent(isSelected: true)],
        };
        var approximate = fixture.Service.ResolveProviderComponents(
            initialDraft,
            catalog,
            filterCatalog: FracturedStateFilterCatalog());
        var initial = Assert.Single(approximate.ModifierFilters);
        var requested = Assert.Single(initial.FilterVariants, option =>
            option.ProviderKind == requestedKind);

        var manuallyRequested = fixture.Service.ResolveProviderComponents(
            approximate with
            {
                ModifierFilters =
                [
                    initial with
                    {
                        RequestedFilterVariantIdentity = requested.Identity,
                        RequestedFilterVariantKind = requested.ProviderKind,
                    },
                ],
            },
            catalog,
            filterCatalog: FracturedStateFilterCatalog());

        var resolved = Assert.Single(manuallyRequested.ModifierFilters);
        Assert.True(resolved.IsFractured);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, resolved.ProviderResolutionStatus);
        Assert.Equal(requested.Identity, resolved.RequestedFilterVariantIdentity);
        Assert.Equal(requestedKind, resolved.RequestedFilterVariantKind);
        Assert.Equal(requested.Identity, resolved.SelectedFilterVariantIdentity);
        Assert.StartsWith($"{requestedKind}.", resolved.ProviderStatId, StringComparison.Ordinal);
        Assert.Null(resolved.ProviderDiagnosticMessage);
        Assert.Equal(BaseSearchMode.Category, manuallyRequested.Base.ActiveCriterion?.Mode);
        Assert.Equal(TradeTriState.Any, manuallyRequested.ItemStateCriteria.Fractured);
    }

    [Fact]
    public void ResolveProviderComponents_ManualExplicitCanReturnToGuardedFracturedRequest()
    {
        var fixture = ServiceFixture.Create();
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
        ]);
        var initialDraft = SafeFracturedBaseDraft() with
        {
            ModifierFilters = [FracturedLifeComponent(isSelected: true)],
        };
        var approximate = fixture.Service.ResolveProviderComponents(
            initialDraft,
            catalog,
            filterCatalog: FracturedStateFilterCatalog());
        var approximateComponent = Assert.Single(approximate.ModifierFilters);
        var explicitOption = Assert.Single(approximateComponent.FilterVariants, option =>
            option.ProviderKind == "explicit");
        var manualExplicit = fixture.Service.ResolveProviderComponents(
            approximate with
            {
                ModifierFilters =
                [
                    approximateComponent with
                    {
                        RequestedFilterVariantIdentity = explicitOption.Identity,
                        RequestedFilterVariantKind = "explicit",
                    },
                ],
            },
            catalog,
            filterCatalog: FracturedStateFilterCatalog());
        var explicitComponent = Assert.Single(manualExplicit.ModifierFilters);

        var fracturedAgain = fixture.Service.ResolveProviderComponents(
            manualExplicit with
            {
                ModifierFilters =
                [
                    explicitComponent with
                    {
                        RequestedFilterVariantIdentity =
                            PathOfExileTradeModifierVariantResolver.FracturedRequestIdentity,
                        RequestedFilterVariantKind = "fractured",
                    },
                ],
            },
            catalog,
            filterCatalog: FracturedStateFilterCatalog());

        var resolved = Assert.Single(fracturedAgain.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Approximate, resolved.ProviderResolutionStatus);
        Assert.Equal("fractured", resolved.RequestedFilterVariantKind);
        Assert.Equal(BaseSearchMode.ExactBase, fracturedAgain.Base.ActiveCriterion?.Mode);
        Assert.Equal(TradeTriState.Yes, fracturedAgain.ItemStateCriteria.Fractured);
        Assert.Equal(
            PathOfExileTradeModifierVariantResolver.FracturedApproximationMessage,
            resolved.ProviderDiagnosticMessage);
    }

    [Fact]
    public void ResolveProviderComponents_FracturedModifierWithoutSafeVariantRemainsUnsupported()
    {
        var fixture = ServiceFixture.Create();
        var draft = Draft() with
        {
            ModifierFilters =
            [
                SpecialComponent("+84 to maximum Life", "+<number> to maximum Life") with
                {
                    IsFractured = true,
                },
            ],
        };

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            new PathOfExileTradeStatCatalog(
            [
                Stat("explicit.unrelated", "Adds # to # Fire Damage", "explicit"),
            ]));

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, component.ProviderResolutionStatus);
        Assert.Empty(component.FilterVariants);
        Assert.Null(component.ProviderStatId);
        Assert.False(component.IsSelected);
    }

    [Fact]
    public void ResolveProviderComponents_FracturedCatalogChangesRecomputeExactAndApproximate()
    {
        var fixture = ServiceFixture.Create();
        var draft = SafeFracturedBaseDraft() with
        {
            ModifierFilters =
            [
                FracturedLifeComponent(isSelected: true),
            ],
        };
        var explicitOnly = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
        ]);
        var withFractured = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
            Stat("fractured.stat_life", "+# to maximum Life", "fractured"),
        ]);
        var filterCatalog = FracturedStateFilterCatalog();

        var approximate = fixture.Service.ResolveProviderComponents(
            draft,
            explicitOnly,
            filterCatalog: filterCatalog);
        var exact = fixture.Service.ResolveProviderComponents(
            approximate,
            withFractured,
            filterCatalog: filterCatalog);
        var approximateAgain = fixture.Service.ResolveProviderComponents(
            exact,
            explicitOnly,
            filterCatalog: filterCatalog);

        Assert.Equal(
            SearchComponentProviderResolutionStatus.Approximate,
            Assert.Single(approximate.ModifierFilters).ProviderResolutionStatus);
        var exactComponent = Assert.Single(exact.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, exactComponent.ProviderResolutionStatus);
        Assert.Equal("fractured.stat_life", exactComponent.ProviderStatId);
        Assert.Null(exactComponent.ProviderDiagnosticMessage);
        Assert.Equal(BaseSearchMode.Category, exact.Base.ActiveCriterion?.Mode);
        Assert.False(exact.Base.IsExactBaseForcedByFracturedApproximation);
        Assert.False(exact.Base.IsFracturedStateForcedByFracturedApproximation);
        Assert.Equal(TradeTriState.Any, exact.ItemStateCriteria.Fractured);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.Approximate,
            Assert.Single(approximateAgain.ModifierFilters).ProviderResolutionStatus);
        Assert.Equal(BaseSearchMode.ExactBase, approximateAgain.Base.ActiveCriterion?.Mode);
        Assert.Equal(TradeTriState.Yes, approximateAgain.ItemStateCriteria.Fractured);
    }

    [Fact]
    public void ResolveProviderComponents_ManualVariantDisappearancePreservesRequestAndBecomesUnresolved()
    {
        var fixture = ServiceFixture.Create();
        var draft = SafeFracturedBaseDraft() with
        {
            ModifierFilters = [FracturedLifeComponent(isSelected: true)],
        };
        var initialCatalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
            Stat("fractured.stat_life", "+# to maximum Life", "fractured"),
            Stat("pseudo.total_life", "+# to maximum Life", "pseudo"),
        ]);
        var refreshedCatalog = new PathOfExileTradeStatCatalog(
        [
            Stat("fractured.stat_life", "+# to maximum Life", "fractured"),
            Stat("pseudo.total_life", "+# to maximum Life", "pseudo"),
        ]);
        var initial = fixture.Service.ResolveProviderComponents(
            draft,
            initialCatalog,
            filterCatalog: FracturedStateFilterCatalog());
        var initialComponent = Assert.Single(initial.ModifierFilters);
        var explicitOption = Assert.Single(initialComponent.FilterVariants, option =>
            option.ProviderKind == "explicit");
        var manuallySelected = fixture.Service.ResolveProviderComponents(
            initial with
            {
                ModifierFilters =
                [
                    initialComponent with
                    {
                        RequestedFilterVariantIdentity = explicitOption.Identity,
                        RequestedFilterVariantKind = "explicit",
                    },
                ],
            },
            initialCatalog,
            filterCatalog: FracturedStateFilterCatalog());

        var refreshed = fixture.Service.ResolveProviderComponents(
            manuallySelected,
            refreshedCatalog,
            filterCatalog: FracturedStateFilterCatalog());
        var restored = fixture.Service.ResolveProviderComponents(
            refreshed,
            initialCatalog,
            filterCatalog: FracturedStateFilterCatalog());

        var component = Assert.Single(refreshed.ModifierFilters);
        Assert.True(component.IsFractured);
        Assert.Equal(SearchComponentProviderResolutionStatus.NotFound, component.ProviderResolutionStatus);
        Assert.Equal(explicitOption.Identity, component.RequestedFilterVariantIdentity);
        Assert.Equal("explicit", component.RequestedFilterVariantKind);
        Assert.Null(component.ProviderStatId);
        Assert.DoesNotContain(component.FilterVariants, option => option.ProviderKind == "explicit");
        Assert.Contains(component.FilterVariants, option => option.ProviderKind == "fractured");
        Assert.Contains(component.FilterVariants, option => option.ProviderKind == "pseudo");
        var restoredComponent = Assert.Single(restored.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, restoredComponent.ProviderResolutionStatus);
        Assert.True(restoredComponent.IsSearchable);
        Assert.Equal(explicitOption.Identity, restoredComponent.RequestedFilterVariantIdentity);
        Assert.Equal("explicit.stat_life", restoredComponent.ProviderStatId);
    }

    [Fact]
    public void ResolveProviderComponents_EquivalentAlternativeKindIsOfferedAsOneLogicalOption()
    {
        var fixture = ServiceFixture.Create();
        var draft = SafeFracturedBaseDraft() with
        {
            ModifierFilters = [FracturedLifeComponent(isSelected: false)],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life_a", "+# to maximum Life", "explicit"),
            Stat("explicit.stat_life_b", "+# to maximum Life", "explicit"),
            Stat("fractured.stat_life", "+# to maximum Life", "fractured"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            filterCatalog: FracturedStateFilterCatalog());

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Contains(component.FilterVariants, option => option.ProviderKind == "fractured");
        var explicitOption = Assert.Single(component.FilterVariants, option =>
            option.ProviderKind == "explicit");
        Assert.Equal(2, explicitOption.ProviderAlternativeCount);
    }

    [Fact]
    public void ResolveProviderComponents_FracturedApproximationDoesNotMutateUserFracturedState()
    {
        var fixture = ServiceFixture.Create();
        var component = FracturedLifeComponent(isSelected: true);
        var draft = SafeFracturedBaseDraft() with
        {
            ItemStateCriteria = new TradeItemStateCriteria
            {
                Fractured = TradeTriState.Yes,
            },
            ModifierFilters = [component],
        };
        var explicitOnly = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
        ]);
        var withFractured = new PathOfExileTradeStatCatalog(
        [
            Stat("fractured.stat_life", "+# to maximum Life", "fractured"),
        ]);

        var approximate = fixture.Service.ResolveProviderComponents(
            draft,
            explicitOnly,
            filterCatalog: FracturedStateFilterCatalog());
        var exact = fixture.Service.ResolveProviderComponents(
            approximate,
            withFractured,
            filterCatalog: FracturedStateFilterCatalog());
        var incompatibleNo = fixture.Service.ResolveProviderComponents(
            draft with
            {
                ItemStateCriteria = new TradeItemStateCriteria
                {
                    Fractured = TradeTriState.No,
                },
            },
            explicitOnly,
            filterCatalog: FracturedStateFilterCatalog());

        Assert.Equal(TradeTriState.Yes, approximate.ItemStateCriteria.Fractured);
        Assert.False(approximate.Base.IsFracturedStateForcedByFracturedApproximation);
        Assert.Equal(TradeTriState.Yes, exact.ItemStateCriteria.Fractured);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.Unsupported,
            Assert.Single(incompatibleNo.ModifierFilters).ProviderResolutionStatus);
        Assert.Equal(TradeTriState.No, incompatibleNo.ItemStateCriteria.Fractured);
    }

    [Fact]
    public void ResolveProviderComponents_FracturedApproximationUsesEquivalentExplicitSet()
    {
        var fixture = ServiceFixture.Create();
        var draft = SafeFracturedBaseDraft() with
        {
            ModifierFilters = [FracturedLifeComponent(isSelected: true)],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life_a", "+# to maximum Life", "explicit"),
            Stat("explicit.stat_life_b", "+# to maximum Life", "explicit"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            filterCatalog: FracturedStateFilterCatalog());

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Approximate, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        Assert.Equal(
            ["explicit.stat_life_a", "explicit.stat_life_b"],
            component.ProviderStatAlternativeIds);
        Assert.Equal(BaseSearchMode.ExactBase, resolved.Base.ActiveCriterion?.Mode);
        Assert.Equal(TradeTriState.Yes, resolved.ItemStateCriteria.Fractured);
    }

    [Fact]
    public void ResolveProviderComponents_FracturedApproximationWithLocalityConflictIsUnsupported()
    {
        var fixture = ServiceFixture.Create();
        var draft = SafeFracturedBaseDraft() with
        {
            ModifierFilters =
            [
                FracturedLifeComponent(isSelected: true) with
                {
                    Locality = ModifierLocality.Local,
                },
            ],
        };

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            new PathOfExileTradeStatCatalog(
            [
                Stat("explicit.stat_life", "+# to maximum Life (Global)", "explicit"),
            ]),
            filterCatalog: FracturedStateFilterCatalog());

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
    }

    [Fact]
    public void ResolveProviderComponents_FracturedApproximationWithoutSafeBaseOrStateFilterIsUnsupported()
    {
        var fixture = ServiceFixture.Create();
        var unsafeBase = Draft() with
        {
            ModifierFilters = [FracturedLifeComponent(isSelected: true)],
        };
        var safeBase = SafeFracturedBaseDraft() with
        {
            ModifierFilters = [FracturedLifeComponent(isSelected: true)],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
        ]);

        var unresolvedBase = fixture.Service.ResolveProviderComponents(
            unsafeBase,
            catalog,
            filterCatalog: FracturedStateFilterCatalog());
        var missingStateFilter = fixture.Service.ResolveProviderComponents(
            safeBase,
            catalog,
            filterCatalog: new PathOfExileTradeFilterCatalog([]));

        Assert.Equal(
            SearchComponentProviderResolutionStatus.Unsupported,
            Assert.Single(unresolvedBase.ModifierFilters).ProviderResolutionStatus);
        Assert.Contains(
            "canonical base",
            Assert.Single(unresolvedBase.ModifierFilters).ProviderDiagnosticMessage,
            StringComparison.Ordinal);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.Unsupported,
            Assert.Single(missingStateFilter.ModifierFilters).ProviderResolutionStatus);
        Assert.Contains(
            "incompatible",
            Assert.Single(missingStateFilter.ModifierFilters).ProviderDiagnosticMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveProviderComponents_UnrelatedCatalogDiagnosticDoesNotBlockFracturedApproximation()
    {
        var fixture = ServiceFixture.Create();
        var draft = SafeFracturedBaseDraft() with
        {
            ModifierFilters = [FracturedLifeComponent(isSelected: true)],
        };
        var catalog = new PathOfExileTradeStatCatalog(
            [Stat("explicit.stat_life", "+# to maximum Life", "explicit")],
            [
                new PathOfExileTradeQueryDiagnostic(
                    PathOfExileTradeStatsDiagnosticCodes.MalformedEntry,
                    "A provider entry was omitted."),
            ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            filterCatalog: FracturedStateFilterCatalog());

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Approximate, component.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_life", component.ProviderStatId);
    }

    [Fact]
    public void ResolveProviderComponents_RelevantCatalogDiagnosticBlocksFracturedApproximation()
    {
        var fixture = ServiceFixture.Create();
        var draft = SafeFracturedBaseDraft() with
        {
            ModifierFilters = [FracturedLifeComponent(isSelected: true)],
        };
        var catalog = new PathOfExileTradeStatCatalog(
            [Stat("explicit.stat_life", "+# to maximum Life", "explicit")],
            [
                new PathOfExileTradeQueryDiagnostic(
                    PathOfExileTradeStatsDiagnosticCodes.DuplicateStatId,
                    "The required provider identity is duplicated.")
                {
                    ProviderStatId = "explicit.stat_life",
                },
            ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            catalog,
            filterCatalog: FracturedStateFilterCatalog());

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, component.ProviderResolutionStatus);
        Assert.Contains("diagnostic", component.ProviderDiagnosticMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveProviderComponents_VeiledPrefixAndSuffixUseTheGeneralPresenceIdentity()
    {
        var fixture = ServiceFixture.Create();
        var suffix = SpecialComponent("Veiled Suffix", "Veiled Suffix") with
        {
            IsVeiled = true,
            ResolutionStatus = ModifierCandidateResolutionStatus.Unknown,
            ResolvedModifierId = null,
            ResolvedStatIds = [],
            IsSearchable = false,
        };
        var draft = Draft() with
        {
            ModifierFilters =
            [
                suffix,
                suffix with
                {
                    ComponentId = "modifier:1:0",
                    SourceModifierIndex = 1,
                    OriginalText = "Veiled Prefix",
                    CanonicalSignature = "Veiled Prefix",
                    ParsedKind = ParsedModifierKind.Prefix,
                },
            ],
        };

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            new PathOfExileTradeStatCatalog(
            [
                Stat("explicit.veiled_text", "Veiled Suffix", "explicit"),
                Stat("veiled.general", "veiled", "veiled"),
                Stat("veiled.named", "Member's Veiled", "veiled"),
            ]));

        Assert.Equal(2, resolved.ModifierFilters.Count);
        Assert.All(resolved.ModifierFilters, component =>
        {
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
            Assert.Equal(ModifierStatMappingProofStatus.ProviderExact, component.StatMappingProof);
            Assert.Equal("veiled.general", component.ProviderStatId);
            Assert.True(component.IsSearchable);
            Assert.False(component.IsSelected);
            Assert.False(component.SupportsValueBounds);
            Assert.Null(component.RequestedMinimum);
            Assert.Null(component.RequestedMaximum);
            Assert.Equal("veiled", Assert.Single(component.FilterVariants).ProviderKind);
        });
        Assert.Equal(ParsedModifierKind.Suffix, resolved.ModifierFilters[0].ParsedKind);
        Assert.Equal(ParsedModifierKind.Prefix, resolved.ModifierFilters[1].ParsedKind);
    }

    [Fact]
    public void ResolveProviderComponents_VeiledPlaceholderWithoutGeneralPresenceRemainsUnsupported()
    {
        var fixture = ServiceFixture.Create();
        var draft = Draft() with
        {
            ModifierFilters =
            [
                SpecialComponent("Veiled Suffix", "Veiled Suffix") with
                {
                    IsVeiled = true,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Unknown,
                    ResolvedModifierId = null,
                    ResolvedStatIds = [],
                    IsSearchable = false,
                },
            ],
        };

        var resolved = fixture.Service.ResolveProviderComponents(
            draft,
            new PathOfExileTradeStatCatalog(
            [
                Stat("veiled.named", "Member's Veiled", "veiled"),
            ]));

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, component.ProviderResolutionStatus);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable,
            component.ProviderDiagnosticCode);
        Assert.Null(component.ProviderStatId);
        Assert.False(component.IsSearchable);
        Assert.False(component.IsSelected);
    }

    [Fact]
    public void ResolveProviderComponents_NamedUnveiledElementalLinesUseIndependentExplicitStatsAndBounds()
    {
        var fixture = ServiceFixture.Create();
        ResolvedSearchComponent Line(
            int index,
            string text,
            string signature,
            decimal minimum,
            decimal maximum) => SpecialComponent(text, signature) with
        {
            ComponentId = $"modifier:1:{index}",
            SourceModifierIndex = 1,
            SourceLineIndex = index,
            SourceComponentIndex = index,
            ParsedKind = ParsedModifierKind.Prefix,
            ParsedModifierName = "Chosen",
            IsUnveiled = true,
            IsVeiled = false,
            ProviderCanonicalSignature = signature,
            ResolvedStatIds = [$"stat.chosen.{index}.minimum", $"stat.chosen.{index}.maximum"],
            StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
            Locality = ModifierLocality.Global,
            ValueBoundShape = ModifierBoundShape.ArithmeticMeanRange,
            ObservedNumericValues = [minimum, maximum],
            CanonicalNumericValues = [minimum, maximum],
            OriginalSourceRollRanges =
            [
                new ModifierSourceRollRange(index == 0 ? 14m : 14m, 16m),
                new ModifierSourceRollRange(20m, 22m),
            ],
        };
        var draft = Draft() with
        {
            ModifierFilters =
            [
                Line(0, "Adds 16(14-16) to 21(20-22) Cold Damage", "Adds <number> to <number> Cold Damage", 16m, 21m),
                Line(1, "Adds 15(14-16) to 20(20-22) Lightning Damage", "Adds <number> to <number> Lightning Damage", 15m, 20m),
            ],
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.chosen.cold", "Adds # to # Cold Damage", "explicit"),
            Stat("explicit.chosen.lightning", "Adds # to # Lightning Damage", "explicit"),
            Stat("veiled.general", "Veiled", "veiled"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(draft, catalog);

        Assert.Equal(2, resolved.ModifierFilters.Count);
        Assert.Equal(
            ["explicit.chosen.cold", "explicit.chosen.lightning"],
            resolved.ModifierFilters.Select(component => component.ProviderStatId));
        Assert.Equal([18.5m, 17.5m], resolved.ModifierFilters.Select(component => component.RequestedMinimum));
        Assert.All(resolved.ModifierFilters, component =>
        {
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
            Assert.True(component.SupportsValueBounds, component.ValueBoundsUnsupportedReason);
            Assert.True(component.IsUnveiled);
            Assert.False(component.IsVeiled);
            Assert.Equal("explicit", Assert.Single(component.FilterVariants, variant =>
                variant.Identity == component.SelectedFilterVariantIdentity).ProviderKind);
            Assert.DoesNotContain(component.FilterVariants, variant =>
                string.Equals(variant.ProviderKind, "veiled", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Theory]
    [InlineData("-215(100-114) to maximum Life", "+<number> to maximum Life", "+# to maximum Life", -215)]
    [InlineData("52(-25--28)% reduced Rarity of Items found", "<number>% increased Rarity of Items found", "#% increased Rarity of Items found", -52)]
    [InlineData("-29(13-15)% of Damage taken Recouped as Life", "<number>% of Damage taken Recouped as Life", "#% of Damage taken Recouped as Life", -29)]
    public void ResolveProviderComponents_TransformedSignedScalarUsesProviderSignatureAndMaximumBound(
        string originalText,
        string providerSignature,
        string providerText,
        int providerValue)
    {
        var fixture = ServiceFixture.Create();
        var component = SpecialComponent(originalText, originalText) with
        {
            ParsedKind = ParsedModifierKind.Prefix,
            ProviderCanonicalSignature = providerSignature,
            StatMappingProof = ModifierStatMappingProofStatus.ProvenExact,
            Locality = ModifierLocality.Global,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [Math.Abs(providerValue)],
            CanonicalNumericValues = [providerValue],
            DefaultBoundDirection = ModifierBoundDirection.Maximum,
            RequestedMaximum = providerValue,
        };
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.transformed", providerText, "explicit"),
            Stat("pseudo.transformed", providerText, "pseudo"),
        ]);

        var resolved = Assert.Single(fixture.Service.ResolveProviderComponents(
            Draft() with { ModifierFilters = [component] },
            catalog).ModifierFilters);

        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, resolved.ProviderResolutionStatus);
        Assert.Equal("explicit.transformed", resolved.ProviderStatId);
        Assert.Equal(providerValue, resolved.RequestedMaximum);
        Assert.Null(resolved.RequestedMinimum);
        Assert.DoesNotContain(resolved.FilterVariants, variant =>
            string.Equals(variant.ProviderKind, "pseudo", StringComparison.OrdinalIgnoreCase) &&
            variant.Identity == resolved.SelectedFilterVariantIdentity);
    }

    [Fact]
    public async Task CheckAsync_ValidDraftBuildsSearchFetchesFirstBatchAndReturnsOrderedOffers()
    {
        var fixture = ServiceFixture.Create();
        var ids = Enumerable.Range(1, 12).Select(index => $"id-{index}").ToArray();
        fixture.SearchClient.Enqueue(SearchSuccess(ids, total: 12, inexact: true));
        fixture.FetchClient.Enqueue(FetchSuccess(ids.Take(10).Select(Offer).ToArray()));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Completed, result.Stage);
        Assert.Equal("query-1", result.SearchQueryId);
        Assert.Equal(ids, result.ResultIds);
        Assert.Equal(ids.Take(10), result.FetchedResultIds);
        Assert.Equal(12, result.ProviderTotal);
        Assert.True(result.Inexact);
        Assert.Equal(ids.Take(10), result.Offers.Select(offer => offer.Id));
        Assert.Empty(result.Diagnostics);
        Assert.Single(fixture.QueryBuilder.Calls);
        Assert.Empty(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.ItemCatalogProvider.Calls);
        Assert.Empty(fixture.SelectedModifierMapper.Calls);
        Assert.Empty(fixture.ItemIdentityMapper.Calls);
        Assert.Single(fixture.SearchClient.Calls);
        Assert.Single(fixture.FetchClient.Calls);
        Assert.Equal(ids.Take(10), fixture.FetchClient.Calls[0].ResultIds);
        Assert.Equal("query-1", fixture.FetchClient.Calls[0].QueryId);
    }

    [Fact]
    public async Task CheckAsync_InitialFetchUsesOnlyTheMeasuredVisibleCapacity()
    {
        var fixture = ServiceFixture.Create();
        var ids = Enumerable.Range(1, 12).Select(index => $"id-{index}").ToArray();
        fixture.SearchClient.Enqueue(SearchSuccess(ids, total: 12));
        fixture.FetchClient.Enqueue(FetchSuccess(ids.Take(6).Select(Offer).ToArray()));

        var result = await fixture.Service.CheckAsync(
            Draft(),
            ValidationSuccess(),
            League,
            initialFetchResultCount: 6);

        Assert.True(result.IsSuccess);
        Assert.Equal(ids.Take(6), result.FetchedResultIds);
        Assert.Equal(ids.Take(6), result.Offers.Select(offer => offer.Id));
        Assert.Equal(ids.Take(6), Assert.Single(fixture.FetchClient.Calls).ResultIds);
    }

    [Fact]
    public async Task FetchMoreAsync_FetchesOnlyRequestedNextBatchWithoutRepeatingSearchAndReturnsProviderOrder()
    {
        var fixture = ServiceFixture.Create();
        var nextIds = Enumerable.Range(11, 10).Select(index => $"id-{index}").ToArray();
        fixture.FetchClient.Enqueue(FetchSuccess(nextIds.Reverse().Select(Offer).ToArray()));

        var result = await fixture.Service.FetchMoreAsync("query-1", nextIds);

        Assert.True(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Completed, result.Stage);
        Assert.Equal("query-1", result.SearchQueryId);
        Assert.Equal(nextIds, result.FetchedResultIds);
        Assert.Equal(nextIds, result.Offers.Select(offer => offer.Id));
        Assert.Empty(fixture.SearchClient.Calls);
        var fetch = Assert.Single(fixture.FetchClient.Calls);
        Assert.Equal("query-1", fetch.QueryId);
        Assert.Equal(nextIds, fetch.ResultIds);
    }

    [Fact]
    public async Task FetchMoreAsync_FetchFailureReturnsStructuredDiagnosticsWithoutRepeatingSearch()
    {
        var fixture = ServiceFixture.Create();
        fixture.FetchClient.Enqueue(new PathOfExileTradeFetchExecutionResult
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Fetch failed."),
            ],
        });

        var result = await fixture.Service.FetchMoreAsync("query-1", ["id-11"]);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Fetch, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.FetchFailed, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Single(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_ZeroSearchResultsIsSuccessfulAndDoesNotFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Completed, result.Stage);
        Assert.Equal("query-1", result.SearchQueryId);
        Assert.Equal(0, result.ProviderTotal);
        Assert.NotNull(result.Offers);
        Assert.Empty(result.Offers);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_QueryBuildFailureReturnsStructuredFailureAndSendsNoHttp()
    {
        var fixture = ServiceFixture.Create();
        fixture.QueryBuilder.Result = PathOfExileTradeQueryBuildResult.Failure(
            new PathOfExileTradeQueryDiagnostic("LOCAL_INVALID", "Local validation failed."));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.QueryBuild, result.Stage);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.QueryBuildFailed, diagnostic.Code);
        Assert.Equal("LOCAL_INVALID", diagnostic.SourceCode);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierLoadsCatalogMapsAndPassesProviderFiltersToQueryBuilder()
    {
        var fixture = ServiceFixture.Create();
        var catalog = Catalog();
        var providerFilters = new[] { ProviderFilter(0, "explicit.stat_life") };
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success(providerFilters);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(SelectedDraft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.ItemCatalogProvider.Calls);
        var mappingCall = Assert.Single(fixture.SelectedModifierMapper.Calls);
        var resolvedComponent = Assert.Single(mappingCall.Draft!.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, resolvedComponent.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_life", resolvedComponent.ProviderStatId);
        Assert.Equal("+# to maximum Life", resolvedComponent.ProviderStatText);
        Assert.Same(providerFilters, Assert.Single(fixture.QueryBuilder.Calls).SelectedModifierFilters);
        Assert.Single(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_ExactBaseSelectedBaseImplicitMarksComponentBaseGuaranteedBeforeMapping()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(ImplicitCatalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success([]);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(
            BaseImplicitDraft(BaseSearchMode.ExactBase),
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        var mappingCall = Assert.Single(fixture.SelectedModifierMapper.Calls);
        var component = Assert.Single(mappingCall.Draft!.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.BaseGuaranteed, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
    }

    [Fact]
    public async Task CheckAsync_CategorySelectedBaseImplicitResolvesProviderStatBeforeMapping()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(ImplicitCatalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success(
            [
                ProviderFilter(0, "implicit.stat_4082780964"),
            ]);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(
            BaseImplicitDraft(BaseSearchMode.Category),
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        var mappingCall = Assert.Single(fixture.SelectedModifierMapper.Calls);
        var component = Assert.Single(mappingCall.Draft!.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("implicit.stat_4082780964", component.ProviderStatId);
    }

    [Theory]
    [InlineData(BaseImplicitRecognitionStatus.CurrentExact, BaseImplicitSnapshotRole.CurrentCandidate)]
    [InlineData(BaseImplicitRecognitionStatus.HistoricalExact, BaseImplicitSnapshotRole.HistoricalObserved)]
    public void ResolveProviderComponents_RecognizedBaseImplicitMapsOnlyToExactImplicitDomainAndRetainsProvenance(
        BaseImplicitRecognitionStatus recognitionStatus,
        BaseImplicitSnapshotRole snapshotRole)
    {
        var fixture = ServiceFixture.Create();
        var draft = RecognizedBaseImplicitDraft(recognitionStatus, snapshotRole);

        var resolved = fixture.Service.ResolveProviderComponents(draft, ImplicitCatalog());

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("implicit.stat_4082780964", component.ProviderStatId);
        Assert.Equal(recognitionStatus, component.BaseImplicitProvenance?.RecognitionStatus);
        var source = Assert.Single(component.BaseImplicitProvenance!.SourceSnapshots);
        Assert.Equal(snapshotRole, source.Role);
        Assert.Equal("source-commit", source.CommitSha);
        Assert.Equal("source-version", source.DataVersion);
    }

    [Fact]
    public void ResolveProviderComponents_CorruptedImplicitUsesExactGameDataAndImplicitProviderDomain()
    {
        var fixture = ServiceFixture.Create();
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.maximum-life", "+# to maximum Life", "explicit"),
            Stat("implicit.maximum-life", "+# to maximum Life", "implicit"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            CorruptedImplicitDraft(exactGameData: true, selected: true),
            catalog);
        var component = Assert.Single(resolved.ModifierFilters);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(resolved, catalog);

        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("implicit.maximum-life", component.ProviderStatId);
        Assert.Equal(ParsedImplicitModifierOrigin.Corrupted, component.ImplicitOrigin);
        Assert.Equal(ModifierGenerationType.Corrupted, component.GenerationType);
        Assert.True(mapping.IsSuccess);
        Assert.Equal("implicit.maximum-life", Assert.Single(mapping.Filters).StatId);
    }

    [Fact]
    public void ResolveProviderComponents_CorruptedImplicitWithoutExactGameDataIsExplicitlyUnsupported()
    {
        var fixture = ServiceFixture.Create();
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("implicit.maximum-life", "+# to maximum Life", "implicit"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            CorruptedImplicitDraft(exactGameData: false, selected: true),
            catalog);
        var component = Assert.Single(resolved.ModifierFilters);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(resolved, catalog);

        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        Assert.False(mapping.IsSuccess);
        Assert.Empty(mapping.Filters);
        Assert.Single(mapping.Diagnostics);
        Assert.True(component.IsSelected);
    }

    [Fact]
    public void ResolveProviderComponents_CorruptedImplicitRejectsCrossDomainLookalike()
    {
        var fixture = ServiceFixture.Create();
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("explicit.maximum-life", "+# to maximum Life", "explicit"),
        ]);

        var resolved = fixture.Service.ResolveProviderComponents(
            CorruptedImplicitDraft(exactGameData: true, selected: true),
            catalog);
        var component = Assert.Single(resolved.ModifierFilters);

        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        Assert.Empty(component.ProviderStatAlternativeIds);
    }

    [Fact]
    public void ResolveProviderComponents_HistoricalBaseImplicitIsNotRewrittenAsGuaranteedByExactBase()
    {
        var fixture = ServiceFixture.Create();
        var draft = RecognizedBaseImplicitDraft(
            BaseImplicitRecognitionStatus.HistoricalExact,
            BaseImplicitSnapshotRole.HistoricalObserved,
            BaseSearchMode.ExactBase);

        var resolved = fixture.Service.ResolveProviderComponents(draft, ImplicitCatalog());

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("implicit.stat_4082780964", component.ProviderStatId);
        Assert.Equal(
            BaseImplicitRecognitionStatus.HistoricalExact,
            component.BaseImplicitProvenance?.RecognitionStatus);
    }

    [Theory]
    [InlineData("explicit")]
    [InlineData("crafted")]
    [InlineData("pseudo")]
    [InlineData("corrupted")]
    [InlineData("enchant")]
    [InlineData("fractured")]
    [InlineData("rune")]
    [InlineData("scourge")]
    public void ResolveProviderComponents_HistoricalBaseImplicitRejectsCrossDomainLookalike(string providerKind)
    {
        var fixture = ServiceFixture.Create();
        var catalog = BaseImplicitCatalog(($"{providerKind}.test", providerKind, providerKind));

        var resolved = fixture.Service.ResolveProviderComponents(
            RecognizedBaseImplicitDraft(
                BaseImplicitRecognitionStatus.HistoricalExact,
                BaseImplicitSnapshotRole.HistoricalObserved),
            catalog);

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        Assert.Empty(component.ProviderStatAlternativeIds);
    }

    [Fact]
    public void ResolveProviderComponents_RecognizedBaseImplicitWithoutProviderCandidateIsUnsupported()
    {
        var fixture = ServiceFixture.Create();

        var resolved = fixture.Service.ResolveProviderComponents(
            RecognizedBaseImplicitDraft(
                BaseImplicitRecognitionStatus.HistoricalExact,
                BaseImplicitSnapshotRole.HistoricalObserved),
            EmptyStatCatalog());

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Unsupported, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
    }

    [Fact]
    public void ResolveProviderComponents_NonEquivalentImplicitCandidatesAreAmbiguous()
    {
        var fixture = ServiceFixture.Create();
        var catalog = BaseImplicitCatalog(
            ("implicit.test_a", "implicit-a", "implicit"),
            ("implicit.test_b", "implicit-b", "implicit"));

        var resolved = fixture.Service.ResolveProviderComponents(
            RecognizedBaseImplicitDraft(
                BaseImplicitRecognitionStatus.HistoricalExact,
                BaseImplicitSnapshotRole.HistoricalObserved),
            catalog);

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Ambiguous, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        Assert.Equal(["implicit.test_a", "implicit.test_b"], component.ProviderCandidateStatIds);
    }

    [Fact]
    public void ResolveProviderComponents_EquivalentImplicitCandidatesAreProvenAsASetThenUseExistingCanonicalVariant()
    {
        var fixture = ServiceFixture.Create();
        var catalog = BaseImplicitCatalog(
            ("implicit.test_a", "implicit", "implicit"),
            ("implicit.test_b", "implicit", "implicit"));

        var draft = RecognizedBaseImplicitDraft(
            BaseImplicitRecognitionStatus.HistoricalExact,
            BaseImplicitSnapshotRole.HistoricalObserved);
        var match = new PathOfExileTradeStatMatcher().Match(draft.ModifierFilters[0], catalog);
        var resolved = fixture.Service.ResolveProviderComponents(draft, catalog);

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(PathOfExileTradeStatMatchStatus.ExactEquivalentSet, match.Status);
        Assert.Equal(["implicit.test_a", "implicit.test_b"],
            match.ExactEquivalentCandidates.Select(candidate => candidate.StatId));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("implicit.test_a", component.ProviderStatId);
        Assert.Equal(["implicit.test_a"], component.ProviderStatAlternativeIds);
        var variant = Assert.Single(component.FilterVariants);
        Assert.Equal("implicit", variant.ProviderKind);
    }

    [Fact]
    public void ResolveProviderComponents_AmbiguousHistoricalRecognitionDoesNotReachPermissiveProviderMatch()
    {
        var fixture = ServiceFixture.Create();
        var draft = RecognizedBaseImplicitDraft(
            BaseImplicitRecognitionStatus.HistoricalExact,
            BaseImplicitSnapshotRole.HistoricalObserved);
        draft = draft with
        {
            ModifierFilters =
            [
                draft.ModifierFilters[0] with
                {
                    IsSearchable = false,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Unknown,
                    ResolvedModifierId = null,
                    ResolvedStatIds = [],
                    ProviderDomainEvidence = [],
                    BaseImplicitProvenance = draft.ModifierFilters[0].BaseImplicitProvenance! with
                    {
                        RecognitionStatus = BaseImplicitRecognitionStatus.Ambiguous,
                        MechanicalSignatures = [new string('a', 64), new string('b', 64)],
                        Diagnostic = "Two historical mechanics matched.",
                    },
                },
            ],
        };

        var resolved = fixture.Service.ResolveProviderComponents(draft, ImplicitCatalog());

        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Ambiguous, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        Assert.Equal("Two historical mechanics matched.", component.ProviderDiagnosticMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedUnsupportedOrAmbiguousHistoricalBaseImplicitBlocksMappingWithoutSilentOmission(bool ambiguous)
    {
        var fixture = ServiceFixture.Create();
        var catalog = ambiguous
            ? ImplicitCatalog()
            : BaseImplicitCatalog(("explicit.test", "explicit", "explicit"));
        var draft = RecognizedBaseImplicitDraft(
            BaseImplicitRecognitionStatus.HistoricalExact,
            BaseImplicitSnapshotRole.HistoricalObserved);
        if (ambiguous)
        {
            draft = draft with
            {
                ModifierFilters =
                [
                    draft.ModifierFilters[0] with
                    {
                        BaseImplicitProvenance = draft.ModifierFilters[0].BaseImplicitProvenance! with
                        {
                            RecognitionStatus = BaseImplicitRecognitionStatus.Ambiguous,
                            MechanicalSignatures = [new string('a', 64), new string('b', 64)],
                        },
                    },
                ],
            };
        }

        var resolved = fixture.Service.ResolveProviderComponents(draft, catalog);
        var validation = new TradeSearchDraftValidator().Validate(resolved);
        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(resolved, catalog);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, diagnostic =>
            diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierVariantUnresolved &&
            diagnostic.Severity == TradeSearchValidationSeverity.Error);
        Assert.False(mapping.IsSuccess);
        Assert.Empty(mapping.Filters);
        Assert.Empty(fixture.SearchClient.Calls);
        var component = Assert.Single(resolved.ModifierFilters);
        Assert.Equal(
            ambiguous
                ? SearchComponentProviderResolutionStatus.Ambiguous
                : SearchComponentProviderResolutionStatus.Unsupported,
            component.ProviderResolutionStatus);
        Assert.True(component.IsSelected);
    }

    [Fact]
    public async Task CheckAsync_CategorySelectedBaseImplicitWithoutProviderStatActivatesExactBaseBeforeMapping()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(EmptyStatCatalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success([]);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(
            StygianViseBaseImplicitDraft(),
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        var mappingCall = Assert.Single(fixture.SelectedModifierMapper.Calls);
        Assert.Equal(BaseSearchMode.ExactBase, mappingCall.Draft!.Base.ActiveCriterion?.Mode);
        Assert.Equal("Stygian Vise", mappingCall.Draft.Base.ActiveCriterion?.ExactBaseName);
        var component = Assert.Single(mappingCall.Draft.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.BaseGuaranteed, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderStatId);
        var queryCall = Assert.Single(fixture.QueryBuilder.Calls);
        Assert.Equal(BaseSearchMode.ExactBase, queryCall.Draft!.Base.ActiveCriterion?.Mode);
        Assert.Equal(BaseSearchMode.ExactBase, result.EffectiveDraft?.Base.ActiveCriterion?.Mode);
        Assert.Equal("Stygian Vise", result.EffectiveDraft?.Base.ActiveCriterion?.ExactBaseName);
        Assert.Empty(queryCall.SelectedModifierFilters!);
    }

    [Fact]
    public void ResolveEffectiveDraft_SelectedDeterministicBaseImplicitActivatesAvailableExactBaseWithoutHttp()
    {
        var fixture = ServiceFixture.Create();

        var result = fixture.Service.ResolveEffectiveDraft(StygianViseBaseImplicitDraft());

        Assert.Equal(BaseSearchMode.ExactBase, result.Base.ActiveCriterion?.Mode);
        Assert.Equal("Stygian Vise", result.Base.ActiveCriterion?.ExactBaseName);
        Assert.Empty(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_UniqueLoadsItemCatalogMapsIdentityAndPassesProviderIdentityToQueryBuilder()
    {
        var fixture = ServiceFixture.Create();
        var catalog = ItemCatalog();
        var identity = new PathOfExileTradeItemIdentity
        {
            CanonicalName = "Moonbender's Wing",
            CanonicalType = "Tomahawk",
            Foulborn = TradeTriState.No,
        };
        fixture.ItemCatalogProvider.Enqueue(PathOfExileTradeItemCatalogProviderResult.Success(catalog));
        fixture.ItemIdentityMapper.Result = PathOfExileTradeItemIdentityMappingResult.Success(identity);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var draft = UniqueDraft();
        var result = await fixture.Service.CheckAsync(draft, ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        var catalogCall = Assert.Single(fixture.ItemCatalogProvider.Calls);
        Assert.False(catalogCall.CancellationToken.IsCancellationRequested);
        var identityCall = Assert.Single(fixture.ItemIdentityMapper.Calls);
        Assert.Same(draft, identityCall.Draft);
        Assert.Same(catalog, identityCall.Catalog);
        Assert.Same(identity, Assert.Single(fixture.QueryBuilder.Calls).ProviderItemIdentity);
        Assert.Empty(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.SelectedModifierMapper.Calls);
        Assert.Single(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_UniqueItemCatalogFailurePreventsIdentityQueryBuildSearchAndFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.ItemCatalogProvider.Enqueue(new PathOfExileTradeItemCatalogProviderResult
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Items failed."),
            ],
        });

        var result = await fixture.Service.CheckAsync(UniqueDraft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.CatalogLoad, result.Stage);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed, diagnostic.Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NetworkFailure, diagnostic.SourceCode);
        Assert.Single(fixture.ItemCatalogProvider.Calls);
        Assert.Empty(fixture.ItemIdentityMapper.Calls);
        Assert.Empty(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.SelectedModifierMapper.Calls);
        Assert.Empty(fixture.QueryBuilder.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_UniqueIdentityFailureReturnsQueryBuildFailureAndSendsNoSearch()
    {
        var fixture = ServiceFixture.Create();
        fixture.ItemIdentityMapper.Result =
            PathOfExileTradeItemIdentityMappingResult.Failure(
                new PathOfExileTradeItemIdentityMappingDiagnostic(
                    PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueDisplayVariant,
                    "Unsupported variant."));

        var result = await fixture.Service.CheckAsync(UniqueDraft("Foulborn Not Real"), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.QueryBuild, result.Stage);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.QueryBuildFailed, diagnostic.Code);
        Assert.Equal(
            PathOfExileTradeItemIdentityMappingDiagnosticCodes.UnsupportedUniqueDisplayVariant,
            diagnostic.SourceCode);
        Assert.Single(fixture.ItemCatalogProvider.Calls);
        Assert.Single(fixture.ItemIdentityMapper.Calls);
        Assert.Empty(fixture.QueryBuilder.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_UniqueSelectedModifierPassesItemIdentityAndProviderStatFilter()
    {
        var fixture = ServiceFixture.Create();
        var providerFilters = new[] { ProviderFilter(0, "explicit.stat_life") };
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(Catalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success(providerFilters);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(
            UniqueDraft() with
            {
                ModifierFilters = SelectedDraft().ModifierFilters,
            },
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.ItemCatalogProvider.Calls);
        Assert.Single(fixture.ItemIdentityMapper.Calls);
        Assert.Single(fixture.CatalogProvider.Calls);
        Assert.Single(fixture.SelectedModifierMapper.Calls);
        var queryCall = Assert.Single(fixture.QueryBuilder.Calls);
        Assert.NotNull(queryCall.ProviderItemIdentity);
        Assert.Same(providerFilters, queryCall.SelectedModifierFilters);
        Assert.Single(fixture.SearchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_AlberonsUnsupportedMultiLineBlockUnselected_AllowsExactIdentityOnlySearch()
    {
        var draft = AlberonsWarpathDraft();
        var skeletonComponents = AlberonsSkeletonComponents(draft);
        var skeletonComponent = Assert.Single(skeletonComponents);
        Assert.False(skeletonComponent.IsSelected);
        Assert.Contains("Summoned Skeleton Warriors are Permanent", skeletonComponent.OriginalText);
        Assert.Contains("Summon Skeletons cannot Summon", skeletonComponent.OriginalText);
        Assert.Single(skeletonComponents.Select(component => component.SourceModifierIndex).Distinct());

        var statCatalogProvider = new FakeCatalogProvider();
        var itemCatalogProvider = new FakeItemCatalogProvider();
        itemCatalogProvider.Enqueue(PathOfExileTradeItemCatalogProviderResult.Success(AlberonsItemCatalog()));
        var searchClient = new FakeSearchClient();
        searchClient.Enqueue(SearchSuccess([], total: 0));
        var fetchClient = new FakeFetchClient();
        var service = CreateProductionUniqueService(
            statCatalogProvider,
            itemCatalogProvider,
            searchClient,
            fetchClient);

        var result = await service.CheckAsync(
            draft,
            new TradeSearchDraftValidator().Validate(draft),
            League);

        Assert.True(
            result.IsSuccess,
            string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}/{diagnostic.SourceCode}: {diagnostic.Message}")));
        Assert.Equal(PathOfExileTradePriceCheckStage.Completed, result.Stage);
        Assert.Empty(statCatalogProvider.Calls);
        var search = Assert.Single(searchClient.Calls);
        Assert.Equal("Alberon's Warpath", search.Request?.Query.Name);
        Assert.Equal("Soldier Boots", search.Request?.Query.Type);
        Assert.Empty(Assert.Single(search.Request!.Query.Stats).Filters);
        Assert.Empty(fetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_AlberonsUnsupportedMultiLineBlockSelected_BlocksBeforeSearchWithoutLosingCoverage()
    {
        var draft = AlberonsWarpathDraft();
        var skeletonSourceIndex = Assert
            .Single(AlberonsSkeletonComponents(draft)
                .Select(component => component.SourceModifierIndex)
                .Distinct());
        var selectedDraft = draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select(component => component.SourceModifierIndex == skeletonSourceIndex
                    ? component with { IsSelected = true }
                    : component)
                .ToArray(),
        };
        Assert.True(Assert.Single(AlberonsSkeletonComponents(selectedDraft)).IsSelected);

        var statCatalogProvider = new FakeCatalogProvider();
        statCatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(EmptyStatCatalog()));
        var itemCatalogProvider = new FakeItemCatalogProvider();
        itemCatalogProvider.Enqueue(PathOfExileTradeItemCatalogProviderResult.Success(AlberonsItemCatalog()));
        var searchClient = new FakeSearchClient();
        var fetchClient = new FakeFetchClient();
        var service = CreateProductionUniqueService(
            statCatalogProvider,
            itemCatalogProvider,
            searchClient,
            fetchClient);

        var result = await service.CheckAsync(
            selectedDraft,
            new TradeSearchDraftValidator().Validate(selectedDraft),
            League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.QueryBuild, result.Stage);
        Assert.Empty(searchClient.Calls);
        Assert.Empty(fetchClient.Calls);
        var effectiveDraft = Assert.IsType<TradeSearchDraft>(result.EffectiveDraft);
        var effectiveSkeletonComponents = AlberonsSkeletonComponents(effectiveDraft);
        var effectiveSkeletonComponent = Assert.Single(effectiveSkeletonComponents);
        Assert.True(effectiveSkeletonComponent.IsSelected);
        Assert.False(effectiveSkeletonComponent.IsSearchable);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.Unsupported,
            effectiveSkeletonComponent.ProviderResolutionStatus);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
            effectiveSkeletonComponent.ProviderDiagnosticCode);

        var validation = new TradeSearchDraftValidator().Validate(effectiveDraft);
        var unresolved = validation.Diagnostics
            .Where(diagnostic =>
                diagnostic.Code == TradeSearchValidationDiagnosticCodes.SelectedModifierVariantUnresolved)
            .ToArray();
        Assert.Single(unresolved);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.SourceCode == PathOfExileTradeQueryDiagnosticCodes.LocallyInvalidDraft);

        var coverageBuild = new PathOfExileTradeQueryBuilder().Build(
            effectiveDraft,
            ValidationSuccess(),
            League,
            selectedModifierFilters: [],
            providerItemIdentity: AlberonsIdentity());
        Assert.False(coverageBuild.IsSuccess);
        Assert.Contains(coverageBuild.Diagnostics, diagnostic =>
            diagnostic.Code == PathOfExileTradeQueryDiagnosticCodes.SelectedModifiersMissingProviderMapping);
    }

    [Fact]
    public async Task CheckAsync_StagedCandidateDataDerivedFoulbornRaw_UsesUnderlyingIdentityVariantAndSelectedFilter()
    {
        var candidatePath = Environment.GetEnvironmentVariable("POENHANCE_UNIQUE_CANDIDATE");
        if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
        {
            return;
        }

        var load = await GameDataPackageLoader.LoadFromFileAsync(candidatePath);
        var package = Assert.IsType<GameDataPackage>(load.Package);
        var gameDataCatalog = GameDataCatalog.FromPackage(package);
        var uniqueCatalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        TradeSearchDraft? draft = null;
        UniqueItemIdentity? selectedIdentity = null;
        string? selectedBaseType = null;
        foreach (var identity in uniqueCatalog.Items.Where(item => item.Kind == UniqueItemKind.Ordinary))
        foreach (var version in identity.Versions.Where(itemVersion =>
            itemVersion.Role == UniqueItemVersionRole.Current))
        foreach (var block in version.ModifierBlocks.Where(candidateBlock =>
            candidateBlock.Kind == UniqueModifierBlockKind.Unique &&
            candidateBlock.Lines.Count == 1 &&
            candidateBlock.MechanicalMapping.Status == UniqueModifierMechanicalMappingStatus.Exact))
        {
            var observedLine = System.Text.RegularExpressions.Regex.Replace(
                block.Lines[0],
                @"(?<sign>[+-]?)\(\s*(?<minimum>[+-]?\d+(?:[\.,]\d+)?)\s*-\s*(?<maximum>[+-]?\d+(?:[\.,]\d+)?)\s*\)",
                match => $"{match.Groups["sign"].Value}{match.Groups["minimum"].Value}" +
                    $"({match.Groups["minimum"].Value}-{match.Groups["maximum"].Value})");
            var parsed = new ItemTextParser().Parse(string.Join(Environment.NewLine,
            [
                "Item Class: Test Items",
                "Rarity: Unique",
                $"Foulborn {identity.CanonicalName}",
                version.BaseType!,
                "--------",
                "Item Level: 80",
                "--------",
                "{ Unique Modifier }",
                observedLine,
            ]));
            var mapped = new TradeSearchDraftMapper().CreateDraft(
                parsed,
                modifierResolutions: [],
                gameDataCatalog: gameDataCatalog).Draft;
            if (mapped?.ModifierFilters is [{ IsSearchable: true, SupportsValueBounds: true }])
            {
                draft = mapped;
                selectedIdentity = identity;
                selectedBaseType = version.BaseType;
                break;
            }
        }

        var rawDraft = Assert.IsType<TradeSearchDraft>(draft);
        var rawComponent = Assert.Single(rawDraft.ModifierFilters);
        Assert.True(rawDraft.UniqueItemResolution?.IsFoulborn);
        Assert.Equal(TradeTriState.Yes, rawDraft.ItemVariantCriteria.Foulborn);
        Assert.NotEmpty(rawComponent.UniqueCatalogBlockIds);
        Assert.NotEmpty(rawComponent.UniqueSourceObservationIds);
        var selectedDraft = rawDraft with
        {
            ModifierFilters = [rawComponent with { IsSelected = true }],
        };
        var providerTemplate = PathOfExileTradeStatTemplateNormalizer
            .NormalizeModifierText(rawComponent.OriginalText)
            .NormalizedTemplate;
        var statCatalogProvider = new FakeCatalogProvider();
        statCatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(
            new PathOfExileTradeStatCatalog(
            [
                Stat("explicit.unique_candidate", providerTemplate, "explicit"),
            ])));
        var itemCatalogProvider = new FakeItemCatalogProvider();
        itemCatalogProvider.Enqueue(PathOfExileTradeItemCatalogProviderResult.Success(
            new PathOfExileTradeItemCatalog(
            [
                new PathOfExileTradeItemEntry
                {
                    ProviderOrder = 0,
                    GroupId = "candidate",
                    GroupLabel = "Candidate",
                    Name = selectedIdentity!.CanonicalName,
                    Type = selectedBaseType!,
                    IsUnique = true,
                },
            ])));
        var searchClient = new FakeSearchClient();
        searchClient.Enqueue(SearchSuccess([], total: 0));
        var service = CreateProductionUniqueService(
            statCatalogProvider,
            itemCatalogProvider,
            searchClient,
            new FakeFetchClient());

        var result = await service.CheckAsync(
            selectedDraft,
            new TradeSearchDraftValidator().Validate(selectedDraft),
            League);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine,
            result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        var effectiveComponent = Assert.Single(
            Assert.IsType<TradeSearchDraft>(result.EffectiveDraft).ModifierFilters);
        Assert.True(effectiveComponent.IsSelected);
        Assert.True(effectiveComponent.IsSearchable);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact,
            effectiveComponent.ProviderResolutionStatus);
        Assert.Equal("explicit.unique_candidate", effectiveComponent.ProviderStatId);
        var search = Assert.Single(searchClient.Calls);
        Assert.Equal(selectedIdentity.CanonicalName, search.Request?.Query.Name);
        Assert.Equal(selectedBaseType, search.Request?.Query.Type);
        Assert.Single(Assert.Single(search.Request!.Query.Stats).Filters);
        var miscFilters = Assert.IsType<PathOfExileTradeSearchFilterGroup>(
            search.Request.Query.Filters["misc_filters"]);
        Assert.Equal(
            "true",
            Assert.IsType<PathOfExileTradeSearchOptionFilter>(
                miscFilters.Filters["mutated"]).Option);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierWarningValidationStillLoadsCatalogAndMaps()
    {
        var fixture = ServiceFixture.Create();
        var providerFilters = new[] { ProviderFilter(0, "explicit.stat_life") };
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(Catalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success(providerFilters);
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        var result = await fixture.Service.CheckAsync(
            SelectedDraft(),
            TradeSearchValidationResult.FromDiagnostics(
            [
                new TradeSearchValidationDiagnostic(
                    TradeSearchValidationDiagnosticCodes.SelectedModifierUnresolved,
                    TradeSearchValidationSeverity.Warning,
                    "Local modifier did not resolve.",
                    ModifierFilterIndex: 0),
            ]),
            League);

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.CatalogProvider.Calls);
        Assert.Single(fixture.SelectedModifierMapper.Calls);
        Assert.Same(providerFilters, Assert.Single(fixture.QueryBuilder.Calls).SelectedModifierFilters);
        Assert.Single(fixture.SearchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_AdvancedRangeSelectedModifierMapsAndExecutesSearchAndFetch()
    {
        var queryBuilder = new FakeQueryBuilder
        {
            Result = PathOfExileTradeQueryBuildResult.Success(
                League,
                SearchRequest(),
                "{}",
                "Titan Plate",
                ItemBaseResolutionStatus.Exact),
        };
        var catalogProvider = new FakeCatalogProvider();
        catalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(Catalog()));
        var searchClient = new FakeSearchClient();
        searchClient.Enqueue(SearchSuccess(["id-1"], total: 1));
        var fetchClient = new FakeFetchClient();
        fetchClient.Enqueue(FetchSuccess([Offer("id-1")]));
        var service = new PathOfExileTradePriceCheckService(
            queryBuilder,
            new PathOfExileTradeStatMatcher(),
            catalogProvider,
            new FakeItemCatalogProvider(),
            new PathOfExileTradeSelectedModifierMapper(),
            new FakeItemIdentityMapper(),
            searchClient,
            fetchClient);

        var result = await service.CheckAsync(
            SelectedDraft("+101(100-114) to maximum Life"),
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        var queryBuildCall = Assert.Single(queryBuilder.Calls);
        var providerFilter = Assert.Single(queryBuildCall.SelectedModifierFilters ?? []);
        Assert.Equal("explicit.stat_life", providerFilter.StatId);
        Assert.Equal("+# to maximum Life", providerFilter.NormalizedItemTemplate);
        Assert.Empty(providerFilter.ExtractedNumericValues);
        Assert.Single(searchClient.Calls);
        Assert.Single(fetchClient.Calls);
    }

    [Theory]
    [InlineData(0, "explicit.physical")]
    [InlineData(1, "explicit.physical")]
    [InlineData(2, "explicit.accuracy.local")]
    public async Task CheckAsync_EachDuplicateEffectComponentResolvesFromItsOwnProvenance(
        int selectedIndex,
        string expectedProviderStatId)
    {
        var queryBuilder = SuccessfulQueryBuilder();
        var catalogProvider = new FakeCatalogProvider();
        catalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(DuplicateEffectCatalog()));
        var searchClient = new FakeSearchClient();
        searchClient.Enqueue(SearchSuccess(["id-1"], total: 1));
        var fetchClient = new FakeFetchClient();
        fetchClient.Enqueue(FetchSuccess([Offer("id-1")]));
        var service = new PathOfExileTradePriceCheckService(
            queryBuilder,
            new PathOfExileTradeStatMatcher(),
            catalogProvider,
            new FakeItemCatalogProvider(),
            new PathOfExileTradeSelectedModifierMapper(),
            new FakeItemIdentityMapper(),
            searchClient,
            fetchClient);

        var result = await service.CheckAsync(
            DuplicateEffectDraft(selectedIndex),
            ValidationSuccess(),
            League);

        Assert.True(result.IsSuccess);
        var effectiveDraft = Assert.IsType<TradeSearchDraft>(result.EffectiveDraft);
        Assert.Equal(3, effectiveDraft.ModifierFilters.Count);
        Assert.True(effectiveDraft.ModifierFilters[selectedIndex].IsSelected);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.Exact,
            effectiveDraft.ModifierFilters[selectedIndex].ProviderResolutionStatus);
        Assert.Equal(expectedProviderStatId, effectiveDraft.ModifierFilters[selectedIndex].ProviderStatId);
        var providerFilter = Assert.Single(Assert.Single(queryBuilder.Calls).SelectedModifierFilters ?? []);
        Assert.Equal(expectedProviderStatId, providerFilter.StatId);
        Assert.Equal([selectedIndex], providerFilter.SourceIndexes);
    }

    [Fact]
    public async Task CheckAsync_TwoSelectedUnaggregatedNumericSourcesSharingStatFailInsteadOfDuplicatingFilter()
    {
        var queryBuilder = SuccessfulQueryBuilder();
        var catalogProvider = new FakeCatalogProvider();
        catalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(DuplicateEffectCatalog()));
        var searchClient = new FakeSearchClient();
        searchClient.Enqueue(SearchSuccess(["id-1"], total: 1));
        var fetchClient = new FakeFetchClient();
        fetchClient.Enqueue(FetchSuccess([Offer("id-1")]));
        var service = new PathOfExileTradePriceCheckService(
            queryBuilder,
            new PathOfExileTradeStatMatcher(),
            catalogProvider,
            new FakeItemCatalogProvider(),
            new PathOfExileTradeSelectedModifierMapper(),
            new FakeItemIdentityMapper(),
            searchClient,
            fetchClient);

        var result = await service.CheckAsync(
            DuplicateEffectDraft(0, 1),
            ValidationSuccess(),
            League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.ModifierMapping, result.Stage);
        Assert.Empty(queryBuilder.Calls);
        Assert.Empty(searchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierCatalogFailurePreventsMappingQueryBuildSearchAndFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(new PathOfExileTradeStatCatalogProviderResult
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Stats failed."),
            ],
        });

        var result = await fixture.Service.CheckAsync(SelectedDraft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.CatalogLoad, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed, Assert.Single(result.Diagnostics).Code);
        Assert.Single(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.SelectedModifierMapper.Calls);
        Assert.Empty(fixture.QueryBuilder.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierCatalogFailurePreservesUnderlyingDiagnostics()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(new PathOfExileTradeStatCatalogProviderResult
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.ResponseTooLarge,
                    "Stats response exceeded the configured bound.",
                    HttpStatusCode.OK),
            ],
            ParserDiagnostics =
            [
                new PathOfExileTradeQueryDiagnostic(
                    PathOfExileTradeStatsDiagnosticCodes.MissingResultCollection,
                    "Missing result collection."),
            ],
        });

        var result = await fixture.Service.CheckAsync(SelectedDraft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.CatalogLoad, result.Stage);
        Assert.Equal(
            [
                PathOfExileTradeHttpDiagnosticCodes.ResponseTooLarge,
                PathOfExileTradeStatsDiagnosticCodes.MissingResultCollection,
            ],
            result.Diagnostics.Select(diagnostic => diagnostic.SourceCode));
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.CatalogLoadFailed, diagnostic.Code);
            Assert.Equal(PathOfExileTradePriceCheckStage.CatalogLoad, diagnostic.Stage);
        });
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierMappingFailurePreventsQueryBuildSearchAndFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(Catalog()));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Failure(
            [
                new PathOfExileTradeSelectedModifierMappingDiagnostic(
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous,
                    "Ambiguous modifier.",
                    SourceIndex: 0),
            ]);

        var result = await fixture.Service.CheckAsync(SelectedDraft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.ModifierMapping, result.Stage);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.SelectedModifierMappingFailed, diagnostic.Code);
        Assert.Equal(PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous, diagnostic.SourceCode);
        Assert.Single(fixture.CatalogProvider.Calls);
        Assert.Single(fixture.SelectedModifierMapper.Calls);
        Assert.Empty(fixture.QueryBuilder.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierLocalValidationFailureDoesNotLoadCatalog()
    {
        var fixture = ServiceFixture.Create();
        fixture.QueryBuilder.Result = PathOfExileTradeQueryBuildResult.Failure(
            new PathOfExileTradeQueryDiagnostic("LOCAL_INVALID", "Local validation failed."));

        var result = await fixture.Service.CheckAsync(
            SelectedDraft(),
            TradeSearchValidationResult.FromDiagnostics(
            [
                new TradeSearchValidationDiagnostic(
                    "LOCAL_INVALID",
                    TradeSearchValidationSeverity.Error,
                    "Local validation failed."),
            ]),
            League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.QueryBuild, result.Stage);
        Assert.Empty(fixture.CatalogProvider.Calls);
        Assert.Empty(fixture.SelectedModifierMapper.Calls);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Theory]
    [InlineData(false, false, PathOfExileTradePriceCheckDiagnosticCodes.SearchFailed)]
    [InlineData(true, false, PathOfExileTradePriceCheckDiagnosticCodes.SearchCancelled)]
    [InlineData(false, true, PathOfExileTradePriceCheckDiagnosticCodes.SearchTimeout)]
    public async Task CheckAsync_SearchFailureReturnsFailureAndDoesNotFetch(
        bool isCancelled,
        bool isTimeout,
        string expectedCode)
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(new PathOfExileTradeSearchExecutionResult
        {
            IsSuccess = false,
            IsCancelled = isCancelled,
            IsTimeout = isTimeout,
            HttpStatusCode = HttpStatusCode.BadGateway,
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus,
                    "Search failed.",
                    HttpStatusCode.BadGateway),
            ],
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Search, result.Stage);
        Assert.Equal(isCancelled, result.IsCancelled);
        Assert.Equal(isTimeout, result.IsTimeout);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus, result.Diagnostics[0].SourceCode);
        Assert.Equal(HttpStatusCode.BadGateway, result.Diagnostics[0].HttpStatusCode);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_MissingSearchQueryIdReturnsFailureAndDoesNotFetch()
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], queryId: " "));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Search, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.MissingSearchQueryId, result.Diagnostics[0].Code);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Theory]
    [InlineData(false, false, PathOfExileTradePriceCheckDiagnosticCodes.FetchFailed)]
    [InlineData(true, false, PathOfExileTradePriceCheckDiagnosticCodes.FetchCancelled)]
    [InlineData(false, true, PathOfExileTradePriceCheckDiagnosticCodes.FetchTimeout)]
    public async Task CheckAsync_FetchFailureReturnsFailureAfterOneFetch(
        bool isCancelled,
        bool isTimeout,
        string expectedCode)
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], total: 1));
        fixture.FetchClient.Enqueue(new PathOfExileTradeFetchExecutionResult
        {
            IsSuccess = false,
            IsCancelled = isCancelled,
            IsTimeout = isTimeout,
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                    "Fetch failed."),
            ],
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradePriceCheckStage.Fetch, result.Stage);
        Assert.Equal(isCancelled, result.IsCancelled);
        Assert.Equal(isTimeout, result.IsTimeout);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NetworkFailure, result.Diagnostics[0].SourceCode);
        Assert.Single(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_PreCancelledTokenBuildsNoSearchOrFetch()
    {
        var fixture = ServiceFixture.Create();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League, cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.Equal(PathOfExileTradePriceCheckStage.Search, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.SearchCancelled, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(fixture.SearchClient.Calls);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_CancelledBeforeFetchDoesNotFetch()
    {
        var fixture = ServiceFixture.Create();
        using var cancellation = new CancellationTokenSource();
        fixture.SearchClient.AfterSearch = () => cancellation.Cancel();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], total: 1));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League, cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.Equal(PathOfExileTradePriceCheckStage.Fetch, result.Stage);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.FetchCancelled, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(fixture.FetchClient.Calls);
    }

    [Fact]
    public async Task CheckAsync_PreservesSeparateSearchAndFetchRateLimitSnapshots()
    {
        var fixture = ServiceFixture.Create();
        var searchRateLimit = RateLimit("trade-search");
        var fetchRateLimit = RateLimit("trade-fetch");
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], total: 1) with
        {
            RateLimitSnapshot = searchRateLimit,
        });
        fixture.FetchClient.Enqueue(FetchSuccess([Offer("id-1")]) with
        {
            RateLimitSnapshot = fetchRateLimit,
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Same(searchRateLimit, result.SearchRateLimitSnapshot);
        Assert.Same(fetchRateLimit, result.FetchRateLimitSnapshot);
    }

    [Fact]
    public async Task CheckAsync_SelectedModifierPreservesSeparateCatalogSearchAndFetchRateLimitSnapshots()
    {
        var fixture = ServiceFixture.Create();
        var catalogRateLimit = RateLimit("trade-stats");
        var searchRateLimit = RateLimit("trade-search");
        var fetchRateLimit = RateLimit("trade-fetch");
        fixture.CatalogProvider.Enqueue(PathOfExileTradeStatCatalogProviderResult.Success(
            Catalog(),
            rateLimitSnapshot: catalogRateLimit));
        fixture.SelectedModifierMapper.Result =
            PathOfExileTradeSelectedModifierMappingResult.Success([ProviderFilter(0, "explicit.stat_life")]);
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1"], total: 1) with
        {
            RateLimitSnapshot = searchRateLimit,
        });
        fixture.FetchClient.Enqueue(FetchSuccess([Offer("id-1")]) with
        {
            RateLimitSnapshot = fetchRateLimit,
        });

        var result = await fixture.Service.CheckAsync(SelectedDraft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Same(catalogRateLimit, result.CatalogRateLimitSnapshot);
        Assert.Same(searchRateLimit, result.SearchRateLimitSnapshot);
        Assert.Same(fetchRateLimit, result.FetchRateLimitSnapshot);
    }

    [Fact]
    public async Task CheckAsync_PreservesPartialFetchDiagnosticsWhileRemainingSuccessful()
    {
        var fixture = ServiceFixture.Create();
        fixture.SearchClient.Enqueue(SearchSuccess(["id-1", "bad"], total: 2));
        fixture.FetchClient.Enqueue(FetchSuccess([Offer("id-1")]) with
        {
            Diagnostics =
            [
                new PathOfExileTradeHttpDiagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.MalformedOffer,
                    "Offer could not be parsed.",
                    ResultIndex: 1),
            ],
        });

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradePriceCheckDiagnosticCodes.FetchDiagnostic, diagnostic.Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.MalformedOffer, diagnostic.SourceCode);
        Assert.Equal(1, diagnostic.ResultIndex);
    }

    [Fact]
    public async Task CheckAsync_DoesNotRetrySearchFetchOrRequestAdditionalBatches()
    {
        var fixture = ServiceFixture.Create();
        var ids = Enumerable.Range(1, 25).Select(index => $"id-{index}").ToArray();
        fixture.SearchClient.Enqueue(SearchSuccess(ids, total: 25));
        fixture.FetchClient.Enqueue(FetchSuccess(ids.Take(10).Select(Offer).ToArray()));

        var result = await fixture.Service.CheckAsync(Draft(), ValidationSuccess(), League);

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.SearchClient.Calls);
        Assert.Single(fixture.FetchClient.Calls);
        var fetchedIds = Assert.IsAssignableFrom<IReadOnlyList<string?>>(fixture.FetchClient.Calls[0].ResultIds);
        Assert.Equal(10, fetchedIds.Count);
        Assert.Empty(fixture.SearchClient.PendingResults);
        Assert.Empty(fixture.FetchClient.PendingResults);
    }

    [Fact]
    public async Task CheckAsync_RevalidatesEffectiveDraftAndPassesLeagueToQueryBuilder()
    {
        var fixture = ServiceFixture.Create();
        var draft = Draft();
        var validation = ValidationSuccess();
        fixture.SearchClient.Enqueue(SearchSuccess([], total: 0));

        await fixture.Service.CheckAsync(draft, validation, League);

        var call = Assert.Single(fixture.QueryBuilder.Calls);
        Assert.Same(draft, call.Draft);
        Assert.NotSame(validation, call.ValidationResult);
        Assert.NotNull(call.ValidationResult);
        Assert.True(call.ValidationResult!.IsValid);
        Assert.Equal(League, call.LeagueIdentifier);
    }

    [Fact]
    public void PriceCheckService_DoesNotConstructHttpClientOrDependOnUi()
    {
        var dependencyTypes = ReferencedMemberTypes(typeof(PathOfExileTradePriceCheckService)).ToArray();

        Assert.DoesNotContain(dependencyTypes, type => type == typeof(HttpClient));
        Assert.DoesNotContain(dependencyTypes, type => Contains(type, "PriceChecker"));
        Assert.DoesNotContain(dependencyTypes, type => Contains(type, "Wpf"));
    }

    [Fact]
    public void PriceCheckerWpfCodeBehind_DoesNotInvokeTradeServicesOrClients()
    {
        var wpfCodeBehindTypes = new[]
        {
            typeof(PriceCheckerWindow),
            typeof(PriceCheckerWindowFactory),
        };

        Assert.DoesNotContain(wpfCodeBehindTypes.SelectMany(ReferencedMemberTypes), type =>
            Contains(type, "PathOfExileTradePriceCheckService") ||
            Contains(type, "PathOfExileTradeSearchClient") ||
            Contains(type, "PathOfExileTradeFetchClient"));
    }

    [Fact]
    public void CoreAssembly_GainsNoProviderSpecificDependency()
    {
        var coreAssembly = typeof(TradeSearchDraft).Assembly;

        Assert.DoesNotContain(coreAssembly.GetTypes(), type => Contains(type, "PathOfExileTrade"));
        Assert.DoesNotContain(coreAssembly.GetReferencedAssemblies(), assembly =>
            string.Equals(assembly.Name, "PoEnhance.App", StringComparison.Ordinal));
    }

    [Fact]
    public void PriceCheckService_DoesNotIntroduceCurrencyPublicStashCacheQueueSchedulerOrWaitTypes()
    {
        var providerTypes = typeof(PathOfExileTradePriceCheckService).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Infrastructure.Trade.PathOfExile")
            .Where(type => !type.IsNested && !type.Name.StartsWith("<", StringComparison.Ordinal))
            .Where(type => type.Name.Contains("PriceCheck", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.DoesNotContain(providerTypes, type =>
            Contains(type, "Currency") ||
            Contains(type, "PublicStash") ||
            Contains(type, "Cache") ||
            Contains(type, "Queue") ||
            Contains(type, "Scheduler") ||
            Contains(type, "Wait"));
        Assert.DoesNotContain(
            typeof(PathOfExileTradePriceCheckService).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name.Contains("Retry", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Batch", StringComparison.OrdinalIgnoreCase));
    }

    private static TradeSearchDraft Draft()
    {
        return new TradeSearchDraft
        {
            ItemClass = "Body Armours",
            Rarity = "Rare",
            DisplayName = "Armoured Shell",
            ParsedBaseType = "Titan Plate",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.titan-plate",
                ResolvedBaseName = "Titan Plate",
            },
        };
    }

    private static TradeSearchDraft SafeFracturedBaseDraft()
    {
        var category = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.Category,
            Category = "Body Armour",
        };
        var exactBase = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.ExactBase,
            Category = "Body Armour",
            ExactBaseName = "Titan Plate",
        };
        return Draft() with
        {
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.titan-plate",
                ResolvedBaseName = "Titan Plate",
                Category = "Body Armour",
                Observed = new ObservedBaseIdentity
                {
                    Status = ItemBaseResolutionStatus.Exact,
                    ExactBaseId = "base.titan-plate",
                    ExactBaseName = "Titan Plate",
                    Category = "Body Armour",
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = category,
                    ExactBase = exactBase,
                },
                ActiveCriterion = category,
            },
        };
    }

    private static PathOfExileTradeFilterCatalog FracturedStateFilterCatalog()
    {
        return new PathOfExileTradeFilterCatalog(
            [],
            optionFilterDefinitions:
            [
                new PathOfExileTradeOptionFilterDefinition
                {
                    GroupProviderOrder = 0,
                    ProviderOrder = 0,
                    GroupId = "misc_filters",
                    GroupTitle = "Miscellaneous",
                    FilterId = "fractured_item",
                    Text = "Fractured Item",
                    Options =
                    [
                        new PathOfExileTradeOptionDefinition { Id = null, Text = "Any" },
                        new PathOfExileTradeOptionDefinition { Id = "true", Text = "Yes" },
                        new PathOfExileTradeOptionDefinition { Id = "false", Text = "No" },
                    ],
                },
            ]);
    }

    private static TradeSearchDraft SelectedDraft(string originalText = "+55 to maximum Life")
    {
        var displayedValue = decimal.Parse(
            System.Text.RegularExpressions.Regex.Match(originalText, @"[+-]?\d+(?:\.\d+)?").Value,
            System.Globalization.CultureInfo.InvariantCulture);
        return Draft() with
        {
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    OriginalText = originalText,
                    CanonicalSignature = "+# to maximum Life",
                    ParsedKind = PoEnhance.Core.Items.Parsing.ParsedModifierKind.Prefix,
                    Locality = ModifierLocality.Global,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    ResolvedModifierId = "mod.life",
                    ResolvedStatIds = ["base_maximum_life"],
                    IsSearchable = true,
                    SupportsValueBounds = true,
                    ValueBoundShape = ModifierBoundShape.Scalar,
                    ObservedNumericValues = [displayedValue],
                    CanonicalNumericValues = [displayedValue],
                    RequestedMinimum = displayedValue,
                    IsSelected = true,
                },
            ],
        };
    }

    private static TradeSearchDraft DuplicateEffectDraft(params int[] selectedIndexes)
    {
        var selected = selectedIndexes.ToHashSet();
        return Draft() with
        {
            ItemClass = "One Hand Axes",
            ParsedBaseType = "Test Weapon",
            ModifierFilters =
            [
                DuplicateEffectComponent(
                    "modifier:0:0",
                    sourceModifierIndex: 0,
                    sourceComponentIndex: 0,
                    "52% increased Physical Damage",
                    "<number>% increased Physical Damage",
                    "mod.pure-physical",
                    "local_physical_damage_percent",
                    52m,
                    selected.Contains(0)),
                DuplicateEffectComponent(
                    "modifier:1:0",
                    sourceModifierIndex: 1,
                    sourceComponentIndex: 0,
                    "39% increased Physical Damage",
                    "<number>% increased Physical Damage",
                    "mod.hybrid-physical-accuracy",
                    "local_physical_damage_percent",
                    39m,
                    selected.Contains(1)),
                DuplicateEffectComponent(
                    "modifier:1:1",
                    sourceModifierIndex: 1,
                    sourceComponentIndex: 1,
                    "+93 to Accuracy Rating",
                    "+<number> to Accuracy Rating",
                    "mod.hybrid-physical-accuracy",
                    "local_accuracy",
                    93m,
                    selected.Contains(2)),
            ],
        };
    }

    private static ResolvedSearchComponent DuplicateEffectComponent(
        string componentId,
        int sourceModifierIndex,
        int sourceComponentIndex,
        string originalText,
        string canonicalSignature,
        string modifierId,
        string statId,
        decimal value,
        bool isSelected)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = componentId,
            SourceModifierIndex = sourceModifierIndex,
            SourceComponentIndex = sourceComponentIndex,
            OriginalText = originalText,
            CanonicalSignature = canonicalSignature,
            ParsedKind = PoEnhance.Core.Items.Parsing.ParsedModifierKind.Prefix,
            GenerationType = PoEnhance.GameData.ModifierGenerationType.Prefix,
            Locality = ModifierLocality.Local,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = modifierId,
            ResolvedStatIds = [statId],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [value],
            CanonicalNumericValues = [value],
            RequestedMinimum = value,
            IsSelected = isSelected,
        };
    }

    private static TradeSearchDraft BaseImplicitDraft(BaseSearchMode activeMode)
    {
        var category = "Wand";
        var exactBaseName = "Blasting Wand";
        var categoryCriterion = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.Category,
            Category = category,
        };
        var exactBaseCriterion = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.ExactBase,
            Category = category,
            ExactBaseName = exactBaseName,
        };

        return new TradeSearchDraft
        {
            ItemClass = "Wands",
            Rarity = "Rare",
            DisplayName = "Glyph Needle",
            ParsedBaseType = exactBaseName,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.blasting-wand",
                ResolvedBaseName = exactBaseName,
                Category = category,
                Observed = new ObservedBaseIdentity
                {
                    Status = ItemBaseResolutionStatus.Exact,
                    ExactBaseId = "base.blasting-wand",
                    ExactBaseName = exactBaseName,
                    Category = category,
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = categoryCriterion,
                    ExactBase = exactBaseCriterion,
                },
                ActiveCriterion = activeMode == BaseSearchMode.ExactBase
                    ? exactBaseCriterion
                    : categoryCriterion,
            },
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "base-implicit:0:mod.implicit.caster",
                    SourceModifierIndex = -1,
                    SourceComponentIndex = 0,
                    OriginalText = "Cannot roll Caster Modifiers",
                    CanonicalSignature = "Cannot roll Caster Modifiers",
                    ParsedKind = PoEnhance.Core.Items.Parsing.ParsedModifierKind.Implicit,
                    GenerationType = ModifierGenerationType.Implicit,
                    Locality = ModifierLocality.Global,
                    IsBaseImplicit = true,
                    GuaranteedExactBaseName = exactBaseName,
                    ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
                    ResolvedModifierId = "mod.implicit.caster",
                    ResolvedStatIds = ["kinetic_wand_implicit_cannot_roll_caster_modifiers"],
                    IsSearchable = true,
                    IsSelected = true,
                },
            ],
        };
    }

    private static TradeSearchDraft StygianViseBaseImplicitDraft()
    {
        var draft = BaseImplicitDraft(BaseSearchMode.Category);
        var category = "Belt";
        var exactBaseName = "Stygian Vise";
        var categoryCriterion = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.Category,
            Category = category,
        };
        var exactBaseCriterion = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.ExactBase,
            Category = category,
            ExactBaseName = exactBaseName,
        };

        return draft with
        {
            ItemClass = "Belts",
            DisplayName = "Corruption Bond",
            ParsedBaseType = exactBaseName,
            Base = draft.Base with
            {
                ResolvedBaseId = "base.stygian-vise",
                ResolvedBaseName = exactBaseName,
                Category = category,
                Observed = new ObservedBaseIdentity
                {
                    Status = ItemBaseResolutionStatus.Exact,
                    ExactBaseId = "base.stygian-vise",
                    ExactBaseName = exactBaseName,
                    Category = category,
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = categoryCriterion,
                    ExactBase = exactBaseCriterion,
                },
                ActiveCriterion = categoryCriterion,
            },
            ModifierFilters =
            [
                draft.ModifierFilters[0] with
                {
                    ComponentId = "base-implicit:0:StygianBeltImplicit1",
                    OriginalText = "Has 1 Abyssal Socket",
                    CanonicalSignature = "Has # Abyssal Socket",
                    GuaranteedExactBaseName = exactBaseName,
                    ResolvedModifierId = "StygianBeltImplicit1",
                    ResolvedStatIds = ["local_has_X_abyss_sockets"],
                },
            ],
        };
    }

    private static TradeSearchDraft RecognizedBaseImplicitDraft(
        BaseImplicitRecognitionStatus recognitionStatus,
        BaseImplicitSnapshotRole snapshotRole,
        BaseSearchMode activeMode = BaseSearchMode.Category)
    {
        var draft = BaseImplicitDraft(activeMode);
        var component = draft.ModifierFilters[0];
        return draft with
        {
            ModifierFilters =
            [
                component with
                {
                    ProviderCanonicalSignature = "Cannot roll Caster Modifiers",
                    BaseImplicitProvenance = new SearchComponentBaseImplicitProvenance
                    {
                        RecognitionStatus = recognitionStatus,
                        MechanicalSignatures = [new string('a', 64)],
                        SourceSnapshots =
                        [
                            new SearchComponentBaseImplicitSourceSnapshot
                            {
                                SnapshotId = "source-snapshot",
                                Role = snapshotRole,
                                CommitSha = "source-commit",
                                DataVersion = "source-version",
                            },
                        ],
                        DiagnosticCode = recognitionStatus == BaseImplicitRecognitionStatus.CurrentExact
                            ? "base-implicit-current-exact"
                            : "base-implicit-historical-exact",
                    },
                    ProviderDomainEvidence =
                    [
                        new SearchComponentProviderDomainEvidence
                        {
                            ProviderDomain = "Implicit",
                            ModifierId = component.ResolvedModifierId!,
                            GenerationType = ModifierGenerationType.Implicit,
                            Locality = ModifierLocality.Global,
                            IsSourceExact = true,
                            EvidenceStrength = 1000,
                            ApplicabilityReason = "Exact recognized base-implicit mechanics.",
                        },
                    ],
                },
            ],
        };
    }

    private static TradeSearchDraft CorruptedImplicitDraft(bool exactGameData, bool selected)
    {
        return Draft() with
        {
            IsCorrupted = true,
            ItemStateCriteria = new TradeItemStateCriteria
            {
                Corrupted = TradeTriState.Yes,
            },
            ModifierFilters =
            [
                new ResolvedSearchComponent
                {
                    ComponentId = "modifier:0:0",
                    SourceModifierIndex = 0,
                    SourceLineIndex = 0,
                    SourceComponentIndex = 0,
                    OriginalText = "+10 to maximum Life",
                    CanonicalSignature = "+<number> to maximum Life",
                    ProviderCanonicalSignature = "+<number> to maximum Life",
                    ParsedKind = ParsedModifierKind.Implicit,
                    ImplicitOrigin = ParsedImplicitModifierOrigin.Corrupted,
                    GenerationType = exactGameData ? ModifierGenerationType.Corrupted : null,
                    ResolutionStatus = exactGameData
                        ? ModifierCandidateResolutionStatus.Exact
                        : ModifierCandidateResolutionStatus.Unknown,
                    ResolvedModifierId = exactGameData ? "mod.corrupted.maximum-life" : null,
                    ResolvedStatIds = exactGameData ? ["maximum_life"] : [],
                    StatMappingProof = exactGameData
                        ? ModifierStatMappingProofStatus.WholeVector
                        : ModifierStatMappingProofStatus.Unknown,
                    Locality = ModifierLocality.Global,
                    IsSearchable = exactGameData,
                    IsSelected = selected,
                    SupportsValueBounds = true,
                    ValueBoundShape = ModifierBoundShape.Scalar,
                    ObservedNumericValues = [10m],
                    CanonicalNumericValues = [10m],
                    ValueBoundTranslationHandlers = [[]],
                    ValueBoundTranslationIdentity = "corrupted-maximum-life",
                    RequestedMinimum = 10m,
                },
            ],
        };
    }

    private static TradeSearchDraft UniqueDraft(
        string displayName = "Moonbender's Wing",
        string baseType = "Tomahawk")
    {
        return Draft() with
        {
            ItemClass = "One Hand Axes",
            Rarity = "Unique",
            DisplayName = displayName,
            ParsedBaseType = baseType,
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.tomahawk",
                ResolvedBaseName = baseType,
            },
        };
    }

    private static TradeSearchDraft AlberonsWarpathDraft()
    {
        var parsed = new ItemTextParser().Parse(AlberonsWarpathText);
        Assert.Equal("Alberon's Warpath", parsed.DisplayName);
        Assert.Equal("Soldier Boots", parsed.BaseType);
        Assert.Equal("Unique", parsed.Rarity);
        var skeletonSource = Assert.Single(parsed.UniqueModifiers, modifier =>
            modifier.ValueLines.Any(line => line.Contains(
                "Summoned Skeleton Warriors are Permanent",
                StringComparison.Ordinal)));
        Assert.Equal(2, skeletonSource.ValueLines.Count);

        var result = new TradeSearchDraftMapper().CreateDraft(parsed);
        Assert.True(result.IsSuccess);
        var mappedDraft = Assert.IsType<TradeSearchDraft>(result.Draft);
        var draft = mappedDraft with
        {
            RequestedItemFilters =
            [
                .. mappedDraft.RequestedItemFilters.Select(filter => filter with
                {
                    IsActive = false,
                    RequestedMinimum = null,
                }),
            ],
        };
        Assert.Equal("Alberon's Warpath", draft.DisplayName);
        Assert.Equal("Soldier Boots", draft.ParsedBaseType);
        Assert.Equal("Unique", draft.Rarity);
        return draft;
    }

    private static IReadOnlyList<ResolvedSearchComponent> AlberonsSkeletonComponents(TradeSearchDraft draft)
    {
        var first = Assert.Single(draft.ModifierFilters, component =>
            component.OriginalText.Contains(
                "Summoned Skeleton Warriors are Permanent",
                StringComparison.Ordinal));
        return draft.ModifierFilters
            .Where(component => component.SourceModifierIndex == first.SourceModifierIndex)
            .OrderBy(component => component.SourceLineIndex)
            .ToArray();
    }

    private static PathOfExileTradeItemIdentity AlberonsIdentity()
    {
        return new PathOfExileTradeItemIdentity
        {
            CanonicalName = "Alberon's Warpath",
            CanonicalType = "Soldier Boots",
            Foulborn = TradeTriState.No,
        };
    }

    private static PathOfExileTradeItemCatalog AlberonsItemCatalog()
    {
        return new PathOfExileTradeItemCatalog(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "armour",
                GroupLabel = "Armour",
                Name = "Alberon's Warpath",
                Type = "Soldier Boots",
                IsUnique = true,
            },
        ]);
    }

    private static PathOfExileTradePriceCheckService CreateProductionUniqueService(
        FakeCatalogProvider statCatalogProvider,
        FakeItemCatalogProvider itemCatalogProvider,
        FakeSearchClient searchClient,
        FakeFetchClient fetchClient)
    {
        return new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            statCatalogProvider,
            itemCatalogProvider,
            new PathOfExileTradeSelectedModifierMapper(),
            new PathOfExileTradeItemIdentityMapper(),
            searchClient,
            fetchClient,
            new StaticFilterCatalogProvider(
                PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog()));
    }

    private static ResolvedSearchComponent UniqueComponent(
        string originalText,
        string canonicalSignature,
        ParsedUniqueModifierOrigin origin = ParsedUniqueModifierOrigin.Ordinary)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            SourceComponentIndex = 0,
            OriginalText = originalText,
            CanonicalSignature = canonicalSignature,
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = origin,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            IsSelected = false,
        };
    }

    private static SearchComponentSourceProvenance UniqueSource(
        string componentId,
        int sourceModifierIndex,
        int sourceLineIndex,
        string originalText)
    {
        return new SearchComponentSourceProvenance
        {
            ComponentId = componentId,
            SourceModifierIndex = sourceModifierIndex,
            SourceLineIndex = sourceLineIndex,
            OriginalText = originalText,
            CanonicalSignature = PathOfExileTradeStatTemplateNormalizer
                .NormalizeModifierText(originalText)
                .NormalizedTemplate,
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            StatMappingProof = ModifierStatMappingProofStatus.ProviderExact,
            ProviderDomain = "Unique",
            ProviderIdentity = "unsafe.partial.identity",
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
        };
    }

    private static void AssertUniqueSource(
        ResolvedSearchComponent actual,
        ResolvedSearchComponent expected)
    {
        Assert.Equal(expected.ComponentId, actual.ComponentId);
        Assert.Equal(expected.SourceModifierIndex, actual.SourceModifierIndex);
        Assert.Equal(expected.SourceLineIndex, actual.SourceLineIndex);
        Assert.Equal(expected.SourceComponentIndex, actual.SourceComponentIndex);
        Assert.Equal(expected.OriginalText, actual.OriginalText);
        Assert.Equal(expected.ParsedKind, actual.ParsedKind);
        Assert.Equal(expected.UniqueOrigin, actual.UniqueOrigin);

        var expectedSource = Assert.Single(expected.Sources);
        var actualSource = Assert.Single(actual.Sources);
        Assert.Equal(expectedSource.ComponentId, actualSource.ComponentId);
        Assert.Equal(expectedSource.SourceModifierIndex, actualSource.SourceModifierIndex);
        Assert.Equal(expectedSource.SourceLineIndex, actualSource.SourceLineIndex);
        Assert.Equal(expectedSource.SourceComponentIndex, actualSource.SourceComponentIndex);
        Assert.Equal(expectedSource.OriginalText, actualSource.OriginalText);
        Assert.Equal(expectedSource.CanonicalSignature, actualSource.CanonicalSignature);
        Assert.Equal(expectedSource.ParsedKind, actualSource.ParsedKind);
        Assert.Equal(expectedSource.UniqueOrigin, actualSource.UniqueOrigin);
        Assert.Equal(expectedSource.ProviderDomain, actualSource.ProviderDomain);
        Assert.Equal(expectedSource.StatMappingProof, actualSource.StatMappingProof);
        Assert.Null(actualSource.ProviderIdentity);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.Unsupported,
            actualSource.ProviderResolutionStatus);
    }

    private static ResolvedSearchComponent SpecialComponent(
        string originalText,
        string canonicalSignature)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            SourceComponentIndex = 0,
            OriginalText = originalText,
            CanonicalSignature = canonicalSignature,
            ParsedKind = ParsedModifierKind.Suffix,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "mod.special.test",
            ResolvedStatIds = ["stat.special.test"],
            IsSearchable = true,
            IsSelected = false,
        };
    }

    private static ResolvedSearchComponent StructuredLiteralPresenceComponent(string originalText)
    {
        var canonicalSignature = PathOfExileTradeStatTemplateNormalizer
            .NormalizeModifierText(originalText)
            .NormalizedTemplate
            .Replace("#", "<number>", StringComparison.Ordinal);
        var source = new SearchComponentSourceProvenance
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            SourceComponentIndex = 0,
            OriginalText = originalText,
            CanonicalSignature = canonicalSignature,
            ParsedKind = ParsedModifierKind.Prefix,
            GenerationType = ModifierGenerationType.Prefix,
            ProviderDomain = "Explicit",
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
        };
        return new ResolvedSearchComponent
        {
            ComponentId = source.ComponentId,
            SourceModifierIndex = source.SourceModifierIndex,
            SourceLineIndex = source.SourceLineIndex,
            SourceComponentIndex = source.SourceComponentIndex,
            OriginalText = originalText,
            CanonicalSignature = canonicalSignature,
            ParsedKind = source.ParsedKind,
            GenerationType = source.GenerationType,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            SupportsValueBounds = false,
            RequestedMinimum = null,
            RequestedMaximum = null,
            Sources = [source],
        };
    }

    private static ResolvedSearchComponent FracturedLifeComponent(bool isSelected)
    {
        return SpecialComponent("+84 to maximum Life", "+<number> to maximum Life") with
        {
            IsFractured = true,
            Locality = ModifierLocality.Global,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [84m],
            CanonicalNumericValues = [84m],
            ValueBoundTranslationHandlers = [[]],
            ValueBoundTranslationIdentity = "test-life",
            RequestedMinimum = 84m,
            IsSelected = isSelected,
            ProviderDomainEvidence =
            [
                new SearchComponentProviderDomainEvidence
                {
                    ProviderDomain = "Fractured",
                    ModifierId = "mod.special.test",
                    GenerationType = ModifierGenerationType.Suffix,
                    Locality = ModifierLocality.Global,
                    IsSourceExact = true,
                    ItemBaseId = "base.titan-plate",
                    ItemClass = "Body Armour",
                    ApplicabilityReason = "Exact Fractured source fixture.",
                },
                new SearchComponentProviderDomainEvidence
                {
                    ProviderDomain = "Explicit",
                    ModifierId = "mod.explicit.test",
                    GenerationType = ModifierGenerationType.Suffix,
                    Locality = ModifierLocality.Global,
                    IsProjectedDomain = true,
                    ItemBaseId = "base.titan-plate",
                    ItemClass = "Body Armour",
                    ApplicabilityReason = "Compatible ordinary provider fixture.",
                },
            ],
        };
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

    private static PathOfExileTradeItemIdentity UniqueIdentity(TradeTriState foulborn)
    {
        return new PathOfExileTradeItemIdentity
        {
            CanonicalName = "Moonbender's Wing",
            CanonicalType = "Tomahawk",
            Foulborn = foulborn,
        };
    }

    private static TradeSearchValidationResult ValidationSuccess()
    {
        return TradeSearchValidationResult.FromDiagnostics([]);
    }

    private static PathOfExileTradeSearchRequest SearchRequest()
    {
        return new PathOfExileTradeSearchRequest
        {
            Query = new PathOfExileTradeSearchQuery
            {
                Status = new PathOfExileTradeSearchStatus
                {
                    Option = "online",
                },
                Type = "Titan Plate",
            },
            Sort = new PathOfExileTradeSearchSort(),
        };
    }

    private static PathOfExileTradeSearchExecutionResult SearchSuccess(
        IReadOnlyList<string> ids,
        int total = 1,
        bool? inexact = null,
        string queryId = "query-1")
    {
        return new PathOfExileTradeSearchExecutionResult
        {
            IsSuccess = true,
            Response = new PathOfExileTradeSearchResponse
            {
                Id = queryId,
                Result = ids,
                Total = total,
                Inexact = inexact,
            },
        };
    }

    private static PathOfExileTradeFetchExecutionResult FetchSuccess(
        IReadOnlyList<PathOfExileTradeFetchedOffer> offers)
    {
        return new PathOfExileTradeFetchExecutionResult
        {
            IsSuccess = true,
            Response = new PathOfExileTradeFetchResponse
            {
                Result = offers,
            },
        };
    }

    private static PathOfExileTradeFetchedOffer Offer(string id)
    {
        return new PathOfExileTradeFetchedOffer
        {
            Id = id,
            Item = new PathOfExileTradeFetchedItem(),
            Listing = new PathOfExileTradeListing(),
        };
    }

    private static PathOfExileTradeRateLimitSnapshot RateLimit(string policy)
    {
        return new PathOfExileTradeRateLimitSnapshot
        {
            Policy = policy,
            Rules =
            [
                new PathOfExileTradeRateLimitRule
                {
                    RuleName = "Ip",
                    MaximumRequestCount = 30,
                    IntervalSeconds = 60,
                    TimeoutSeconds = 0,
                    CurrentRequestCount = 2,
                    CurrentTimeoutSeconds = 0,
                },
            ],
        };
    }

    private static PathOfExileTradeStatCatalog Catalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 0,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = "explicit.stat_life",
                Text = "+# to maximum Life",
                Type = "explicit",
            },
        ]);
    }

    private static PathOfExileTradeStatCatalog DuplicateEffectCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 0,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = "explicit.physical",
                Text = "#% increased Physical Damage",
                Type = "explicit",
            },
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 1,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = "explicit.accuracy.global",
                Text = "+# to Accuracy Rating",
                Type = "explicit",
            },
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 2,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = "explicit.accuracy.local",
                Text = "+# to Accuracy Rating (Local)",
                Type = "explicit",
            },
        ]);
    }

    private static FakeQueryBuilder SuccessfulQueryBuilder()
    {
        return new FakeQueryBuilder
        {
            Result = PathOfExileTradeQueryBuildResult.Success(
                League,
                SearchRequest(),
                "{}",
                "Test Weapon",
                ItemBaseResolutionStatus.Exact),
        };
    }

    private static PathOfExileTradeStatCatalog ImplicitCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 0,
                GroupId = "implicit",
                GroupLabel = "Implicit",
                Id = "implicit.stat_4082780964",
                Text = "Cannot roll Caster Modifiers",
                Type = "implicit",
            },
        ]);
    }

    private static PathOfExileTradeStatCatalog BaseImplicitCatalog(
        params (string Id, string GroupId, string Type)[] entries)
    {
        return new PathOfExileTradeStatCatalog(entries.Select((entry, index) =>
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = index,
                GroupId = entry.GroupId,
                GroupLabel = entry.GroupId,
                Id = entry.Id,
                Text = "Cannot roll Caster Modifiers",
                Type = entry.Type,
            }));
    }

    private static PathOfExileTradeStatCatalog EmptyStatCatalog()
    {
        return new PathOfExileTradeStatCatalog([]);
    }

    private static PathOfExileTradeItemCatalog ItemCatalog()
    {
        return new PathOfExileTradeItemCatalog(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "weapon",
                GroupLabel = "Weapons",
                Name = "Moonbender's Wing",
                Type = "Tomahawk",
                IsUnique = true,
            },
        ]);
    }

    private static PathOfExileTradeSelectedModifierFilter ProviderFilter(
        int sourceIndex,
        string statId)
    {
        return new PathOfExileTradeSelectedModifierFilter
        {
            SourceIndex = sourceIndex,
            StatId = statId,
            OriginalText = "+55 to maximum Life",
            NormalizedItemTemplate = "+# to maximum Life",
            ExtractedNumericValues = [55m],
        };
    }

    private static IEnumerable<Type> ReferencedMemberTypes(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        return type.GetConstructors(flags).SelectMany(constructor =>
                constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(type.GetFields(flags).Select(field => field.FieldType))
            .Concat(type.GetProperties(flags).Select(property => property.PropertyType))
            .Concat(type.GetMethods(flags).Select(method => method.ReturnType))
            .Concat(type.GetMethods(flags).SelectMany(method =>
                method.GetParameters().Select(parameter => parameter.ParameterType)));
    }

    private static bool Contains(Type type, string value)
    {
        return type.FullName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    }

    private sealed record QueryBuildCall(
        TradeSearchDraft? Draft,
        TradeSearchValidationResult? ValidationResult,
        string? LeagueIdentifier,
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? SelectedModifierFilters,
        PathOfExileTradeItemIdentity? ProviderItemIdentity,
        PathOfExileTradeFilterCatalog? ProviderFilterCatalog,
        IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter>? SelectedItemPropertyFilters);

    private sealed record CatalogCall(CancellationToken CancellationToken);

    private sealed record ItemIdentityMappingCall(
        TradeSearchDraft? Draft,
        PathOfExileTradeItemCatalog? Catalog);

    private sealed record MappingCall(TradeSearchDraft? Draft);

    private sealed record SearchCall(
        PathOfExileTradeSearchRequest? Request,
        string? LeagueIdentifier,
        CancellationToken CancellationToken);

    private sealed record FetchCall(
        string? QueryId,
        IReadOnlyList<string?>? ResultIds,
        CancellationToken CancellationToken);

    private sealed class ServiceFixture
    {
        private ServiceFixture(
            FakeQueryBuilder queryBuilder,
            FakeCatalogProvider catalogProvider,
            FakeItemCatalogProvider itemCatalogProvider,
            FakeSelectedModifierMapper selectedModifierMapper,
            FakeItemIdentityMapper itemIdentityMapper,
            FakeSearchClient searchClient,
            FakeFetchClient fetchClient)
        {
            QueryBuilder = queryBuilder;
            CatalogProvider = catalogProvider;
            ItemCatalogProvider = itemCatalogProvider;
            SelectedModifierMapper = selectedModifierMapper;
            ItemIdentityMapper = itemIdentityMapper;
            SearchClient = searchClient;
            FetchClient = fetchClient;
            Service = new PathOfExileTradePriceCheckService(
                queryBuilder,
                new PathOfExileTradeStatMatcher(),
                catalogProvider,
                itemCatalogProvider,
                selectedModifierMapper,
                itemIdentityMapper,
                searchClient,
                fetchClient);
        }

        public PathOfExileTradePriceCheckService Service { get; }

        public FakeQueryBuilder QueryBuilder { get; }

        public FakeCatalogProvider CatalogProvider { get; }

        public FakeItemCatalogProvider ItemCatalogProvider { get; }

        public FakeSelectedModifierMapper SelectedModifierMapper { get; }

        public FakeItemIdentityMapper ItemIdentityMapper { get; }

        public FakeSearchClient SearchClient { get; }

        public FakeFetchClient FetchClient { get; }

        public static ServiceFixture Create()
        {
            var queryBuilder = new FakeQueryBuilder
            {
                Result = PathOfExileTradeQueryBuildResult.Success(
                    League,
                    SearchRequest(),
                    "{}",
                    "Titan Plate",
                    ItemBaseResolutionStatus.Exact),
            };

            return new ServiceFixture(
                queryBuilder,
                new FakeCatalogProvider(),
                new FakeItemCatalogProvider(),
                new FakeSelectedModifierMapper(),
                new FakeItemIdentityMapper(),
                new FakeSearchClient(),
                new FakeFetchClient());
        }
    }

    private sealed class FakeQueryBuilder : IPathOfExileTradeQueryBuilder
    {
        public PathOfExileTradeQueryBuildResult Result { get; set; } =
            PathOfExileTradeQueryBuildResult.Failure();

        public List<QueryBuildCall> Calls { get; } = [];

        public PathOfExileTradeQueryBuildResult Build(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier,
            IReadOnlyList<PathOfExileTradeSelectedModifierFilter>? selectedModifierFilters = null,
            PathOfExileTradeItemIdentity? providerItemIdentity = null,
            PathOfExileTradeFilterCatalog? providerFilterCatalog = null,
            IReadOnlyList<PathOfExileTradeSelectedItemPropertyFilter>? selectedItemPropertyFilters = null)
        {
            Calls.Add(new QueryBuildCall(
                draft,
                validationResult,
                leagueIdentifier,
                selectedModifierFilters,
                providerItemIdentity,
                providerFilterCatalog,
                selectedItemPropertyFilters));
            return Result;
        }
    }

    private sealed class FakeCatalogProvider : IPathOfExileTradeStatCatalogProvider
    {
        public Queue<PathOfExileTradeStatCatalogProviderResult> PendingResults { get; } = [];

        public List<CatalogCall> Calls { get; } = [];

        public void Enqueue(PathOfExileTradeStatCatalogProviderResult result)
        {
            PendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new CatalogCall(cancellationToken));
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new PathOfExileTradeStatCatalogProviderResult
                {
                    IsCancelled = true,
                    Diagnostics =
                    [
                        new PathOfExileTradeHttpDiagnostic(
                            PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
                            "Cancelled."),
                    ],
                });
            }

            return Task.FromResult(PendingResults.Count == 0
                ? PathOfExileTradeStatCatalogProviderResult.Success(Catalog())
                : PendingResults.Dequeue());
        }
    }

    private sealed class StaticFilterCatalogProvider : IPathOfExileTradeFilterCatalogProvider
    {
        private readonly PathOfExileTradeFilterCatalog catalog;

        public StaticFilterCatalogProvider(PathOfExileTradeFilterCatalog catalog)
        {
            this.catalog = catalog;
        }

        public bool TryGetCachedCatalog(out PathOfExileTradeFilterCatalog cachedCatalog)
        {
            cachedCatalog = catalog;
            return true;
        }

        public Task<PathOfExileTradeFilterCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PathOfExileTradeFilterCatalogProviderResult.Success(catalog));
        }
    }

    private sealed class FakeItemCatalogProvider : IPathOfExileTradeItemCatalogProvider
    {
        public Queue<PathOfExileTradeItemCatalogProviderResult> PendingResults { get; } = [];

        public List<CatalogCall> Calls { get; } = [];

        public void Enqueue(PathOfExileTradeItemCatalogProviderResult result)
        {
            PendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new CatalogCall(cancellationToken));
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new PathOfExileTradeItemCatalogProviderResult
                {
                    IsCancelled = true,
                    Diagnostics =
                    [
                        new PathOfExileTradeHttpDiagnostic(
                            PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
                            "Cancelled."),
                    ],
                });
            }

            return Task.FromResult(PendingResults.Count == 0
                ? PathOfExileTradeItemCatalogProviderResult.Success(ItemCatalog())
                : PendingResults.Dequeue());
        }
    }

    private sealed class FakeSelectedModifierMapper : IPathOfExileTradeSelectedModifierMapper
    {
        public List<MappingCall> Calls { get; } = [];

        public PathOfExileTradeSelectedModifierMappingResult Result { get; set; } =
            PathOfExileTradeSelectedModifierMappingResult.Success([ProviderFilter(0, "explicit.stat_life")]);

        public PathOfExileTradeSelectedModifierMappingResult Map(
            TradeSearchDraft? draft,
            PathOfExileTradeStatCatalog? catalog = null)
        {
            Calls.Add(new MappingCall(draft));
            return Result;
        }
    }

    private sealed class FakeItemIdentityMapper : IPathOfExileTradeItemIdentityMapper
    {
        public List<ItemIdentityMappingCall> Calls { get; } = [];

        public PathOfExileTradeItemIdentityMappingResult Result { get; set; } =
            PathOfExileTradeItemIdentityMappingResult.Success(new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Moonbender's Wing",
                CanonicalType = "Tomahawk",
                Foulborn = TradeTriState.No,
            });

        public PathOfExileTradeItemIdentityMappingResult Map(
            TradeSearchDraft? draft,
            PathOfExileTradeItemCatalog? catalog)
        {
            Calls.Add(new ItemIdentityMappingCall(draft, catalog));
            return Result;
        }
    }

    private sealed class FakeSearchClient : IPathOfExileTradeSearchClient
    {
        public Queue<PathOfExileTradeSearchExecutionResult> PendingResults { get; } = [];

        public List<SearchCall> Calls { get; } = [];

        public Action? AfterSearch { get; set; }

        public void Enqueue(PathOfExileTradeSearchExecutionResult result)
        {
            PendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new SearchCall(request, leagueIdentifier, cancellationToken));
            if (PendingResults.Count == 0)
            {
                throw new InvalidOperationException("No fake Search result was configured.");
            }

            var result = PendingResults.Dequeue();
            AfterSearch?.Invoke();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeFetchClient : IPathOfExileTradeFetchClient
    {
        public Queue<PathOfExileTradeFetchExecutionResult> PendingResults { get; } = [];

        public List<FetchCall> Calls { get; } = [];

        public void Enqueue(PathOfExileTradeFetchExecutionResult result)
        {
            PendingResults.Enqueue(result);
        }

        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new FetchCall(queryId, resultIds, cancellationToken));
            if (PendingResults.Count == 0)
            {
                throw new InvalidOperationException("No fake Fetch result was configured.");
            }

            return Task.FromResult(PendingResults.Dequeue());
        }
    }
}
