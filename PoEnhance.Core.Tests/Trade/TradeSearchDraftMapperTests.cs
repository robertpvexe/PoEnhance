using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchDraftMapperTests
{
    private readonly ItemTextParser parser = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Fact]
    public void CreateDraft_RareItemWithExactBaseResolution_PreservesParsedAndResolvedBaseFields()
    {
        var item = ParseSample("rare-onyx-amulet.txt");
        var baseRecord = Base("base.onyx-amulet", "Onyx Amulet", "Amulet");

        var result = mapper.CreateDraft(item, ExactBase(baseRecord));

        var draft = AssertSuccessfulDraft(result);
        Assert.Equal("Amulets", draft.ItemClass);
        Assert.Equal("Rare", draft.Rarity);
        Assert.Equal("Dusk Beads", draft.DisplayName);
        Assert.Equal("Onyx Amulet", draft.ParsedBaseType);
        Assert.Equal(30, draft.ItemLevel);
        Assert.Equal(ItemBaseResolutionStatus.Exact, draft.Base.Status);
        Assert.Equal("base.onyx-amulet", draft.Base.ResolvedBaseId);
        Assert.Equal("Onyx Amulet", draft.Base.ResolvedBaseName);
    }

    [Fact]
    public void CreateDraft_RareItemWithSeveralParsedModifiers_AddsModifierDrafts()
    {
        var item = ParseSample("advanced-rare-ring-with-implicit.txt");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.Equal(item.Modifiers.Count, draft.ModifierFilters.Count);
        Assert.Contains(draft.ModifierFilters, filter =>
            filter.OriginalText == "+55(50-59) to maximum Life" &&
            filter.ParsedKind == ParsedModifierKind.Prefix &&
            filter.ParsedModifierName == "Hale");
        Assert.Contains(draft.ModifierFilters, filter =>
            filter.OriginalText == "+12(11-13)% to all Elemental Resistances" &&
            filter.ParsedKind == ParsedModifierKind.Suffix &&
            filter.ParsedModifierName == "of the Rainbow");
    }

    [Fact]
    public void CreateDraft_ModifierFiltersAreUnselectedByDefault()
    {
        var item = ParseSample("advanced-rare-ring-with-implicit.txt");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.NotEmpty(draft.ModifierFilters);
        Assert.All(draft.ModifierFilters, filter => Assert.False(filter.IsSelected));
    }

    [Fact]
    public void CreateDraft_ExactModifierResolutionPreservesCatalogIdentity()
    {
        var item = ParseSample("advanced-rare-ring-with-implicit.txt");
        var haleIndex = FindModifierIndex(item, "Hale");
        var resolution = ModifierResolution(
            item,
            haleIndex,
            ModifierCandidateResolutionStatus.Exact,
            ModifierGenerationType.Prefix,
            Modifier("mod.prefix.hale", "Hale", ModifierGenerationType.Prefix));

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item, modifierResolutions: [resolution]));

        var filter = draft.ModifierFilters[haleIndex];
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, filter.ResolutionStatus);
        Assert.Equal(ModifierGenerationType.Prefix, filter.GenerationType);
        Assert.Equal("mod.prefix.hale", filter.ResolvedModifierId);
        Assert.Equal("Hale", filter.ResolvedModifierName);
    }

    [Fact]
    public void CreateDraft_ExactModifierResolutionPreservesTrustedLocality()
    {
        var item = ParseSample("advanced-rare-ring-with-implicit.txt");
        var haleIndex = FindModifierIndex(item, "Hale");
        var localResolution = ModifierResolution(
            item,
            haleIndex,
            ModifierCandidateResolutionStatus.Exact,
            ModifierGenerationType.Prefix,
            [Modifier("mod.prefix.hale", "Hale", ModifierGenerationType.Prefix)],
            ModifierLocality.Local);

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item, modifierResolutions: [localResolution]));

        Assert.Equal(ModifierLocality.Local, draft.ModifierFilters[haleIndex].Locality);
    }

    [Fact]
    public void CreateDraft_ExactModifierResolutionPreservesInternalStatEvidence()
    {
        var item = ParseSample("advanced-rare-ring-with-implicit.txt");
        var haleIndex = FindModifierIndex(item, "Hale");
        var resolution = ModifierResolution(
            item,
            haleIndex,
            ModifierCandidateResolutionStatus.Exact,
            ModifierGenerationType.Prefix,
            [Modifier(
                "mod.prefix.hale",
                "Hale",
                ModifierGenerationType.Prefix,
                "base_maximum_life")],
            ModifierLocality.Global);

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item, modifierResolutions: [resolution]));

        Assert.Equal(["base_maximum_life"], draft.ModifierFilters[haleIndex].ResolvedStatIds);
    }

    [Fact]
    public void CreateDraft_AmbiguousOrUnknownModifierDoesNotGuessCatalogIdentity()
    {
        var item = ParseSample("advanced-rare-ring-with-implicit.txt");
        var rainbowIndex = FindModifierIndex(item, "of the Rainbow");
        var resolution = ModifierResolution(
            item,
            rainbowIndex,
            ModifierCandidateResolutionStatus.Unknown,
            ModifierGenerationType.Suffix,
            Modifier("mod.suffix.rainbow.one", "of the Rainbow", ModifierGenerationType.Suffix),
            Modifier("mod.suffix.rainbow.two", "of the Rainbow", ModifierGenerationType.Suffix));

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item, modifierResolutions: [resolution]));

        var filter = draft.ModifierFilters[rainbowIndex];
        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, filter.ResolutionStatus);
        Assert.Equal(ModifierGenerationType.Suffix, filter.GenerationType);
        Assert.Null(filter.ResolvedModifierId);
        Assert.Null(filter.ResolvedModifierName);
    }

    [Fact]
    public void CreateDraft_MissingGameDataStillCreatesParserDerivedDraft()
    {
        var item = ParseSample("rare-onyx-amulet.txt");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.Equal("Dusk Beads", draft.DisplayName);
        Assert.Equal("Onyx Amulet", draft.ParsedBaseType);
        Assert.Null(draft.Base.Status);
        Assert.Null(draft.Base.ResolvedBaseId);
        Assert.Null(draft.Base.ResolvedBaseName);
    }

    [Fact]
    public void CreateDraft_UnknownBaseResolutionRemainsUnknown()
    {
        var item = ParseSample("rare-onyx-amulet.txt");
        var baseResolution = new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Unknown,
            Candidates =
            [
                Base("base.one", "Onyx Amulet", "Amulet"),
                Base("base.two", "Onyx Amulet", "Amulet"),
            ],
        };

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item, baseResolution));

        Assert.Equal(ItemBaseResolutionStatus.Unknown, draft.Base.Status);
        Assert.Null(draft.Base.ResolvedBaseId);
        Assert.Null(draft.Base.ResolvedBaseName);
    }

    [Fact]
    public void CreateDraft_DefaultListingPreferenceIsInstantBuyout()
    {
        var item = ParseSample("rare-onyx-amulet.txt");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.Equal(TradeListingMode.InstantBuyout, draft.ListingMode);
    }

    [Theory]
    [InlineData("Bows", "Ranger Bow", "Bow", "Bow")]
    [InlineData("Wands", "Blasting Wand", "Wand", "Wand")]
    [InlineData("Body Armours", "Titan Plate", "Body Armour", "Body Armour")]
    [InlineData("Rings", "Vermillion Ring", "Ring", "Ring")]
    public void CreateDraft_OrdinaryExactBaseDefaultsToCategoryCriterion(
        string itemClass,
        string baseName,
        string gameDataItemClass,
        string expectedCategory)
    {
        var item = ParseOrdinaryItem(itemClass, baseName);
        var baseRecord = Base($"base.{baseName.Replace(' ', '-').ToLowerInvariant()}", baseName, gameDataItemClass);

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item, ExactBase(baseRecord)));

        Assert.Equal(baseRecord.Id, draft.Base.Observed?.ExactBaseId);
        Assert.Equal(baseName, draft.Base.Observed?.ExactBaseName);
        Assert.Equal(expectedCategory, draft.Base.Observed?.Category);
        Assert.Equal(BaseSearchMode.Category, draft.Base.ActiveCriterion?.Mode);
        Assert.Equal(expectedCategory, draft.Base.ActiveCriterion?.Category);
        Assert.Equal(BaseSearchMode.ExactBase, draft.Base.AvailableCriteria.ExactBase?.Mode);
        Assert.Equal(baseName, draft.Base.AvailableCriteria.ExactBase?.ExactBaseName);
    }

    [Fact]
    public void CreateDraft_OneHybridSourceModifierCreatesIndependentComponents()
    {
        var item = parser.Parse("""
Item Class: Body Armours
Rarity: Rare
Dragon Shelter
Titan Plate
--------
Item Level: 84
--------
{ Prefix Modifier "Layered" (Tier: 5) - Defences }
25% increased Armour
11% increased Stun and Block Recovery
""");
        var resolution = ModifierResolution(
            item,
            modifierIndex: 0,
            ModifierCandidateResolutionStatus.Exact,
            ModifierGenerationType.Prefix,
            [Modifier(
                "mod.prefix.layered",
                "Layered",
                ModifierGenerationType.Prefix,
                "local_armour_+%",
                "base_stun_recovery_+%")],
            ModifierLocality.Unknown);

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item, modifierResolutions: [resolution]));

        Assert.Equal(2, draft.ModifierFilters.Count);
        Assert.All(draft.ModifierFilters, component =>
        {
            Assert.Equal(0, component.SourceModifierIndex);
            Assert.Equal("mod.prefix.layered", component.ResolvedModifierId);
        });
        Assert.Equal(["25% increased Armour", "11% increased Stun and Block Recovery"],
            draft.ModifierFilters.Select(component => component.OriginalText));
        Assert.Equal(["local_armour_+%"], draft.ModifierFilters[0].ResolvedStatIds);
        Assert.Equal(["base_stun_recovery_+%"], draft.ModifierFilters[1].ResolvedStatIds);
        Assert.NotEqual(draft.ModifierFilters[0].ComponentId, draft.ModifierFilters[1].ComponentId);
        Assert.Equal([0, 1], draft.ModifierFilters.Select(component => component.SourceLineIndex));
    }

    [Fact]
    public void CreateDraft_IdenticalEffectTemplatesFromDifferentSourcesDoNotOverwriteProvenance()
    {
        var item = parser.Parse("""
Item Class: One Hand Axes
Rarity: Rare
Test Item
Test Weapon
--------
Item Level: 84
--------
{ Prefix Modifier "Pure Source" (Tier: 7) - Damage, Physical, Attack }
52(50-64)% increased Physical Damage
{ Prefix Modifier "Hybrid Source" (Tier: 5) - Damage, Physical, Attack }
39(35-44)% increased Physical Damage
+93(73-97) to Accuracy Rating
""");
        var pureModifier = Modifier(
            "mod.pure-physical",
            "Pure Source",
            ModifierGenerationType.Prefix,
            "local_physical_damage_percent");
        var hybridModifier = Modifier(
            "mod.hybrid-physical-accuracy",
            "Hybrid Source",
            ModifierGenerationType.Prefix,
            "local_physical_damage_percent",
            "local_accuracy");
        var resolutions = new[]
        {
            ModifierResolution(
                item,
                modifierIndex: 0,
                ModifierCandidateResolutionStatus.Exact,
                ModifierGenerationType.Prefix,
                [pureModifier],
                ModifierLocality.Local),
            ModifierResolution(
                item,
                modifierIndex: 1,
                ModifierCandidateResolutionStatus.Exact,
                ModifierGenerationType.Prefix,
                [hybridModifier],
                ModifierLocality.Local),
        };

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item, modifierResolutions: resolutions));
        var physical = draft.ModifierFilters
            .Where(component => component.OriginalText.Contains("increased Physical Damage", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, physical.Length);
        Assert.Equal([0, 1], physical.Select(component => component.SourceModifierIndex));
        Assert.Equal([0, 0], physical.Select(component => component.SourceComponentIndex));
        Assert.Equal(2, physical.Select(component => component.ComponentId).Distinct().Count());
        Assert.Equal(1, physical.Select(component => component.CanonicalSignature).Distinct().Count());
        Assert.Equal(
            ["mod.pure-physical", "mod.hybrid-physical-accuracy"],
            physical.Select(component => component.ResolvedModifierId));
        Assert.All(physical, component =>
        {
            Assert.Equal(["local_physical_damage_percent"], component.ResolvedStatIds);
            Assert.Equal(ModifierLocality.Local, component.Locality);
            Assert.True(component.IsSearchable);
        });

        var accuracy = Assert.Single(draft.ModifierFilters, component =>
            component.OriginalText.Contains("Accuracy Rating", StringComparison.Ordinal));
        Assert.Equal(1, accuracy.SourceModifierIndex);
        Assert.Equal(1, accuracy.SourceComponentIndex);
        Assert.Equal("mod.hybrid-physical-accuracy", accuracy.ResolvedModifierId);
        Assert.Equal(["local_accuracy"], accuracy.ResolvedStatIds);
    }

    [Fact]
    public void CreateDraft_CanStoreInPersonListingPreferenceWithoutNetworkBehavior()
    {
        var item = ParseSample("rare-onyx-amulet.txt");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item, listingMode: TradeListingMode.InPerson));

        Assert.Equal(TradeListingMode.InPerson, draft.ListingMode);
    }

    [Fact]
    public void CreateDraft_PreservesTraditionalAndEldritchInfluencesSeparately()
    {
        var item = parser.Parse("""
Item Class: Body Armours
Rarity: Rare
Dragon Shelter
Astral Plate
--------
Shaper Item
Searing Exarch Item
Eater of Worlds Item
--------
Item Level: 84
--------
{ Prefix Modifier "Conqueror's" (Tier: 1) - Damage }
10% increased Damage
""");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.Equal(["Shaper Item"], draft.TraditionalInfluences);
        Assert.Equal(["Searing Exarch Item", "Eater of Worlds Item"], draft.EldritchInfluences);
    }

    [Fact]
    public void CreateDraft_PreservesItemStatesAndCorruptionForTradeValidation()
    {
        var item = parser.Parse("""
Item Class: Body Armours
Rarity: Rare
Dragon Shelter
Synthesised Astral Plate
--------
Synthesised Item
Corrupted
--------
Item Level: 84
""");

        var draft = AssertSuccessfulDraft(mapper.CreateDraft(item));

        Assert.Contains("Synthesised Item", draft.ItemStates);
        Assert.Contains("Corrupted", draft.ItemStates);
        Assert.True(draft.IsCorrupted);
    }

    [Fact]
    public void CreateDraft_RepeatedRenderingOfSameParsedResultIsPure()
    {
        var item = ParseSample("advanced-rare-ring-with-implicit.txt");
        var resolution = ModifierResolution(
            item,
            FindModifierIndex(item, "Hale"),
            ModifierCandidateResolutionStatus.Exact,
            ModifierGenerationType.Prefix,
            Modifier("mod.prefix.hale", "Hale", ModifierGenerationType.Prefix));

        var firstDraft = AssertSuccessfulDraft(mapper.CreateDraft(item, modifierResolutions: [resolution]));
        var secondDraft = AssertSuccessfulDraft(mapper.CreateDraft(item, modifierResolutions: [resolution]));

        Assert.Equal(firstDraft.ModifierFilters.Count, secondDraft.ModifierFilters.Count);
        Assert.Equal(
            firstDraft.ModifierFilters.Select(filter => filter.ResolvedModifierId),
            secondDraft.ModifierFilters.Select(filter => filter.ResolvedModifierId));
        Assert.All(secondDraft.ModifierFilters, filter => Assert.False(filter.IsSelected));
    }

    [Fact]
    public void CreateDraft_DoesNotMutateParsedItemOrResolutionObjects()
    {
        var item = ParseSample("advanced-rare-ring-with-implicit.txt");
        var originalModifierCount = item.Modifiers.Count;
        var originalInfluences = item.TraditionalInfluences.ToArray();
        var haleIndex = FindModifierIndex(item, "Hale");
        var candidate = Modifier("mod.prefix.hale", "Hale", ModifierGenerationType.Prefix);
        var resolution = ModifierResolution(
            item,
            haleIndex,
            ModifierCandidateResolutionStatus.Exact,
            ModifierGenerationType.Prefix,
            candidate);

        _ = mapper.CreateDraft(item, modifierResolutions: [resolution]);

        Assert.Equal(originalModifierCount, item.Modifiers.Count);
        Assert.Equal(originalInfluences, item.TraditionalInfluences);
        Assert.Same(item.Modifiers[haleIndex], resolution.ParsedModifier);
        Assert.Same(candidate, Assert.Single(resolution.Candidates));
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, resolution.Status);
    }

    [Fact]
    public void CreateDraft_UnsupportedInputReturnsDiagnosticAndDoesNotThrow()
    {
        var exception = Record.Exception(() => mapper.CreateDraft(null));

        Assert.Null(exception);
        var draftResult = mapper.CreateDraft(null);
        Assert.False(draftResult.IsSuccess);
        Assert.Null(draftResult.Draft);
        Assert.Equal(
            TradeSearchDraftDiagnosticCodes.UnsupportedInput,
            Assert.Single(draftResult.Diagnostics).Code);
    }

    [Fact]
    public void CoreAssembly_DoesNotReferenceWpfOrNetworkAssemblies()
    {
        var referencedNames = typeof(TradeSearchDraftMapper).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("PresentationCore", referencedNames);
        Assert.DoesNotContain("PresentationFramework", referencedNames);
        Assert.DoesNotContain("WindowsBase", referencedNames);
        Assert.DoesNotContain("System.Net.Http", referencedNames);
    }

    private ParsedItem ParseSample(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Items", fileName);
        return parser.Parse(File.ReadAllText(path));
    }

    private ParsedItem ParseOrdinaryItem(
        string itemClass,
        string baseName)
    {
        return parser.Parse($$"""
Item Class: {{itemClass}}
Rarity: Rare
Storm Shell
{{baseName}}
--------
Item Level: 84
""");
    }

    private static TradeSearchDraft AssertSuccessfulDraft(TradeSearchDraftResult result)
    {
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static int FindModifierIndex(ParsedItem item, string name)
    {
        return item.Modifiers
            .Select((modifier, modifierIndex) => new { modifier, modifierIndex })
            .Single(pair => pair.modifier.Name == name)
            .modifierIndex;
    }

    private static ItemBaseResolutionResult ExactBase(ItemBaseRecord itemBase)
    {
        return new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Exact,
            MatchedItemBase = itemBase,
            ResolvedBaseId = itemBase.Id,
            ResolvedBaseName = itemBase.Name,
            Candidates = [itemBase],
        };
    }

    private static ModifierCandidateResolutionResult ModifierResolution(
        ParsedItem item,
        int modifierIndex,
        ModifierCandidateResolutionStatus status,
        ModifierGenerationType generationType,
        params ModifierDefinition[] candidates)
    {
        return ModifierResolution(
            item,
            modifierIndex,
            status,
            generationType,
            candidates,
            ModifierLocality.Unknown);
    }

    private static ModifierCandidateResolutionResult ModifierResolution(
        ParsedItem item,
        int modifierIndex,
        ModifierCandidateResolutionStatus status,
        ModifierGenerationType generationType,
        IReadOnlyList<ModifierDefinition> candidates,
        ModifierLocality locality)
    {
        var modifier = item.Modifiers[modifierIndex];
        return new ModifierCandidateResolutionResult(
            modifierIndex,
            modifier,
            modifier.Name,
            modifier.Kind,
            generationType,
            status,
            candidates,
            [],
            Locality: locality);
    }

    private static ItemBaseRecord Base(string id, string name, string itemClass)
    {
        return new ItemBaseRecord
        {
            Id = id,
            Name = name,
            ItemClass = itemClass,
        };
    }

    private static ModifierDefinition Modifier(
        string id,
        string name,
        ModifierGenerationType generationType,
        params string[] statIds)
    {
        return new ModifierDefinition
        {
            Id = id,
            Name = name,
            GenerationType = generationType,
            Stats = statIds
                .Select((statId, index) => new ModifierStat
                {
                    Index = index,
                    StatId = statId,
                })
                .ToArray(),
        };
    }
}
