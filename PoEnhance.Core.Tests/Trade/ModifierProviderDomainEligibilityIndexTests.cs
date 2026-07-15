using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class ModifierProviderDomainEligibilityIndexTests
{
    [Fact]
    public void AccuracyWeaponContext_UsesGenericFamiliesForExplicitCraftedFracturedImplicitAndEnchant()
    {
        var catalog = Catalog();
        var source = Assert.Single(catalog.FindModifiersById("accuracy.explicit.weapon"));
        var component = Component(source, catalog, "local_accuracy_rating", ModifierLocality.Local);
        var itemBase = Assert.Single(catalog.FindItemBasesById("base.weapon"));

        var evaluations = ModifierProviderDomainEligibilityIndex.For(catalog).Evaluate(
            component,
            source,
            ItemModifierEligibilityContext.ForItemBase(itemBase));
        var supported = evaluations
            .Where(evaluation => evaluation.Status == ModifierProviderDomainEligibilityStatus.Supported)
            .ToArray();

        Assert.Contains(supported, evaluation => evaluation.ProviderDomain == "Explicit");
        Assert.Contains(supported, evaluation =>
            evaluation.ProviderDomain == "Crafted" &&
            evaluation.ReasonCode == ModifierProviderDomainEligibilityReasonCodes.CraftedFamilySupported);
        Assert.Contains(supported, evaluation =>
            evaluation.ProviderDomain == "Fractured" &&
            evaluation.IsProjectedDomain &&
            evaluation.ReasonCode == ModifierProviderDomainEligibilityReasonCodes.FracturableExplicitFamilySupported);
        Assert.Contains(supported, evaluation =>
            evaluation.ProviderDomain == "Implicit" &&
            evaluation.Modifier.Id == "SynthesisImplicitAccuracyWeapon1");
        Assert.Contains(supported, evaluation => evaluation.ProviderDomain == "Enchant");
        Assert.DoesNotContain(supported, evaluation => evaluation.ProviderDomain == "Scourge");
    }

    [Fact]
    public void SynthesizedAccuracyImplicit_IsContextualRatherThanGloballyAllowedOrRejected()
    {
        var catalog = Catalog();
        var source = Assert.Single(catalog.FindModifiersById("accuracy.explicit.jewel"));
        var component = Component(source, catalog, "accuracy_rating", ModifierLocality.Global);
        var index = ModifierProviderDomainEligibilityIndex.For(catalog);
        var jewel = Assert.Single(catalog.FindItemBasesById("base.jewel"));
        var ring = Assert.Single(catalog.FindItemBasesById("base.ring"));

        var jewelResults = index.Evaluate(
            component,
            source,
            ItemModifierEligibilityContext.ForItemBase(jewel));
        var ringResults = index.Evaluate(
            component,
            source,
            ItemModifierEligibilityContext.ForItemBase(ring));

        Assert.Contains(jewelResults, evaluation =>
            evaluation.Status == ModifierProviderDomainEligibilityStatus.Supported &&
            evaluation.ProviderDomain == "Implicit" &&
            evaluation.Modifier.Id == "SynthesisImplicitAccuracyJewel1");
        Assert.Contains(ringResults, evaluation =>
            evaluation.Status == ModifierProviderDomainEligibilityStatus.Rejected &&
            evaluation.ProviderDomain == "Implicit" &&
            evaluation.Modifier.Id == "SynthesisImplicitAccuracyJewel1" &&
            evaluation.ReasonCode == ModifierProviderDomainEligibilityReasonCodes.ImplicitSourceContextMismatch);
        Assert.Contains(ringResults, evaluation =>
            evaluation.Status == ModifierProviderDomainEligibilityStatus.Supported &&
            evaluation.ProviderDomain == "Implicit" &&
            evaluation.Modifier.Id == "SynthesisImplicitFlatAccuracy1");
    }

    [Fact]
    public void FracturedProjection_DoesNotUseInfluenceOnlyExplicitEvidence()
    {
        var catalog = Catalog();
        var source = Assert.Single(catalog.FindModifiersById("accuracy.influence.weapon"));
        var component = Component(source, catalog, "local_influence_accuracy_rating", ModifierLocality.Local);
        var itemBase = Assert.Single(catalog.FindItemBasesById("base.influence.weapon"));

        var results = ModifierProviderDomainEligibilityIndex.For(catalog).Evaluate(
            component,
            source,
            ItemModifierEligibilityContext.Create(itemBase, ["Shaper Item"]));

        Assert.Contains(results, evaluation =>
            evaluation.Status == ModifierProviderDomainEligibilityStatus.Supported &&
            evaluation.ProviderDomain == "Explicit" &&
            evaluation.MatchedDynamicTag);
        Assert.DoesNotContain(results, evaluation =>
            evaluation.Status == ModifierProviderDomainEligibilityStatus.Supported &&
            evaluation.ProviderDomain == "Fractured");
    }

    private static ResolvedSearchComponent Component(
        ModifierDefinition source,
        GameDataCatalog catalog,
        string statId,
        ModifierLocality locality)
    {
        var stat = Assert.Single(source.Stats, stat => stat.StatId == statId);
        var identity = Assert.Single(ModifierBoundDefaults.FindTranslationIdentities(source, [stat], catalog));
        return new ResolvedSearchComponent
        {
            ComponentId = $"component:{source.Id}",
            OriginalText = "+100 to Accuracy Rating",
            CanonicalSignature = "+<number> to Accuracy Rating",
            ParsedKind = ParsedModifierKind.Suffix,
            GenerationType = source.GenerationType,
            Locality = locality,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = source.Id,
            ResolvedStatIds = [statId],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ValueBoundTranslationIdentity = identity,
        };
    }

    private static GameDataCatalog Catalog()
    {
        const string localAccuracy = "local_accuracy_rating";
        const string localInfluenceAccuracy = "local_influence_accuracy_rating";
        const string accuracy = "accuracy_rating";
        var modifiers = new[]
        {
            Modifier(
                "accuracy.explicit.weapon",
                ModifierGenerationType.Suffix,
                "suffix",
                "item",
                localAccuracy,
                [new ModifierSpawnWeight { Tag = "weapon", Weight = 1000 }, new ModifierSpawnWeight { Tag = "default", Weight = 0 }]),
            Modifier(
                "accuracy.crafted.weapon",
                ModifierGenerationType.Suffix,
                "suffix",
                "crafted",
                localAccuracy),
            Modifier(
                "SynthesisImplicitAccuracyWeapon1",
                ModifierGenerationType.Implicit,
                "unique",
                "item",
                localAccuracy),
            Modifier(
                "accuracy.enchant.weapon",
                ModifierGenerationType.Enchantment,
                "enchantment",
                "item",
                localAccuracy,
                [new ModifierSpawnWeight { Tag = "weapon", Weight = 100 }, new ModifierSpawnWeight { Tag = "default", Weight = 0 }]),
            Modifier(
                "accuracy.influence.weapon",
                ModifierGenerationType.Suffix,
                "suffix",
                "item",
                localInfluenceAccuracy,
                [new ModifierSpawnWeight { Tag = "axe_shaper", Weight = 1000 }, new ModifierSpawnWeight { Tag = "default", Weight = 0 }]),
            Modifier(
                "accuracy.explicit.jewel",
                ModifierGenerationType.Suffix,
                "suffix",
                "misc",
                accuracy,
                [new ModifierSpawnWeight { Tag = "jewel", Weight = 1000 }, new ModifierSpawnWeight { Tag = "default", Weight = 0 }]),
            Modifier(
                "accuracy.explicit.ring",
                ModifierGenerationType.Suffix,
                "suffix",
                "item",
                accuracy,
                [new ModifierSpawnWeight { Tag = "ring", Weight = 1000 }, new ModifierSpawnWeight { Tag = "default", Weight = 0 }]),
            Modifier(
                "SynthesisImplicitAccuracyJewel1",
                ModifierGenerationType.Implicit,
                "unique",
                "item",
                accuracy),
            Modifier(
                "SynthesisImplicitFlatAccuracy1",
                ModifierGenerationType.Implicit,
                "unique",
                "item",
                accuracy),
        };

        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = Manifest(),
            ItemBases =
            [
                Base("base.weapon", "One Hand Axe", "item", "weapon", "axe"),
                Base("base.influence.weapon", "One Hand Axe", "item", "weapon", "axe"),
                Base("base.jewel", "Jewel", "misc", "jewel"),
                Base("base.ring", "Ring", "item", "ring"),
            ],
            Modifiers = modifiers,
            Stats =
            [
                new StatDefinition { Id = localAccuracy, IsLocal = true },
                new StatDefinition { Id = localInfluenceAccuracy, IsLocal = true },
                new StatDefinition { Id = accuracy, IsLocal = false },
            ],
            StatTranslations =
            [
                Translation("translation.local-accuracy", localAccuracy),
                Translation("translation.local-influence-accuracy", localInfluenceAccuracy),
                Translation("translation.accuracy", accuracy),
            ],
        });
    }

    private static ModifierDefinition Modifier(
        string id,
        ModifierGenerationType generationType,
        string sourceGenerationType,
        string domain,
        string statId,
        IReadOnlyList<ModifierSpawnWeight>? spawnWeights = null)
    {
        return new ModifierDefinition
        {
            Id = id,
            GroupId = "accuracy",
            GenerationType = generationType,
            SourceGenerationType = sourceGenerationType,
            Domain = domain,
            Stats = [new ModifierStat { Index = 0, StatId = statId, MinValue = 1, MaxValue = 100 }],
            SpawnWeights = spawnWeights ?? [],
        };
    }

    private static ItemBaseRecord Base(
        string id,
        string itemClass,
        string domain,
        params string[] tags)
    {
        return new ItemBaseRecord
        {
            Id = id,
            Name = id,
            ItemClass = itemClass,
            Domain = domain,
            Tags = tags,
        };
    }

    private static StatTranslationDefinition Translation(string id, string statId)
    {
        return new StatTranslationDefinition
        {
            Id = id,
            StatIds = [statId],
            Variants =
            [
                new StatTranslationVariant
                {
                    Conditions = [new StatTranslationCondition { Index = 0 }],
                    ValueFormats = ["+#"],
                    IndexHandlers = [new StatTranslationIndexHandler { Index = 0, Handlers = [] }],
                    FormatLines = ["{0} to Accuracy Rating"],
                },
            ],
        };
    }

    private static GameDataPackageManifest Manifest()
    {
        return new GameDataPackageManifest
        {
            SchemaVersion = 1,
            DataVersion = "test",
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            League = "test",
            Patch = "test",
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "test",
                    RetrievedAtUtc = DateTimeOffset.UnixEpoch,
                    SourceVersion = "test",
                    SourceUri = "https://example.test",
                },
            ],
        };
    }
}
