using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchDraftMapperBaseImplicitTests
{
    private readonly ItemTextParser parser = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Theory]
    [InlineData(
        BaseImplicitRecognitionStatus.CurrentExact,
        BaseImplicitSnapshotRole.CurrentCandidate,
        "current",
        "3.29.1.2.2")]
    [InlineData(
        BaseImplicitRecognitionStatus.HistoricalExact,
        BaseImplicitSnapshotRole.HistoricalObserved,
        "historical",
        "3.28.0.13")]
    public void CreateDraft_ExactBaseImplicitRecognitionProducesStructuredComponentAndPreservesProvenance(
        BaseImplicitRecognitionStatus recognitionStatus,
        BaseImplicitSnapshotRole snapshotRole,
        string commit,
        string version)
    {
        var item = ParseStaffBlock();
        var effect = Effect(
            "effect",
            "staff_block_%",
            "+#% Chance to Block Attack Damage while wielding a Staff",
            22m,
            new string('a', 64));
        var recognition = Recognition(recognitionStatus, snapshotRole, commit, version, effect);
        var resolution = Resolution(item, recognition);
        var effectCatalog = BaseImplicitMechanicalEffectCatalogFactory.Create(effect);
        var textMatch = new ModifierTextSignatureMatcher().Match(
            effect.Modifier!, effectCatalog, item.Modifiers[0].ValueLines);
        Assert.True(
            textMatch.Outcome == ModifierTextSignatureMatchOutcome.Match,
            $"Text match: {textMatch.Outcome}; {textMatch.ReasonCode}; {textMatch.Reason}");

        var result = mapper.CreateDraft(item, modifierResolutions: [resolution]);

        Assert.NotNull(result.Draft);
        var draft = result.Draft!;
        var component = Assert.Single(draft.ModifierFilters);
        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, resolution.Status);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.True(component.IsBaseImplicit);
        Assert.True(component.IsSearchable);
        Assert.Equal("old-base-implicit", component.ResolvedModifierId);
        Assert.Equal(["staff_block_%"], component.ResolvedStatIds);
        Assert.Equal(
            "+<number>% Chance to Block Attack Damage while wielding a Staff",
            component.ProviderCanonicalSignature);
        var provenance = Assert.IsType<SearchComponentBaseImplicitProvenance>(
            component.BaseImplicitProvenance);
        Assert.Equal(recognitionStatus, provenance.RecognitionStatus);
        Assert.Equal([new string('a', 64)], provenance.MechanicalSignatures);
        var source = Assert.Single(provenance.SourceSnapshots);
        Assert.Equal(snapshotRole, source.Role);
        Assert.Equal(commit, source.CommitSha);
        Assert.Equal(version, source.DataVersion);
        var retainedSource = Assert.Single(component.Sources);
        Assert.Equal(
            recognitionStatus,
            retainedSource.BaseImplicitProvenance?.RecognitionStatus);
        Assert.Equal(
            [new string('a', 64)],
            retainedSource.BaseImplicitProvenance?.MechanicalSignatures);
        var domain = Assert.Single(component.ProviderDomainEvidence);
        Assert.Equal("Implicit", domain.ProviderDomain);
        Assert.Equal(ModifierGenerationType.Implicit, domain.GenerationType);
        Assert.True(domain.IsSourceExact);
    }

    [Fact]
    public void CreateDraft_AmbiguousBaseImplicitRecognitionRemainsUnsearchableWithAmbiguousProvenance()
    {
        var item = ParseStaffBlock();
        var first = Effect(
            "first",
            "staff_block_%",
            "+#% Chance to Block Attack Damage while wielding a Staff",
            22m,
            new string('a', 64));
        var second = Effect(
            "second",
            "other_staff_block_%",
            "+#% Chance to Block Attack Damage while wielding a Staff",
            22m,
            new string('b', 64));
        var firstMatch = Match(first, BaseImplicitSnapshotRole.HistoricalObserved, "old-a", "3.28-a");
        var secondMatch = Match(second, BaseImplicitSnapshotRole.HistoricalObserved, "old-b", "3.28-b");
        var recognition = new BaseImplicitRecognitionResult(
            BaseImplicitRecognitionStatus.Ambiguous,
            [firstMatch, secondMatch],
            "base-implicit-history-ambiguous",
            "Two historical mechanics matched.");

        var result = mapper.CreateDraft(item, modifierResolutions: [Resolution(item, recognition)]);

        Assert.NotNull(result.Draft);
        var component = Assert.Single(result.Draft!.ModifierFilters);
        Assert.True(component.IsBaseImplicit);
        Assert.False(component.IsSearchable);
        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, component.ResolutionStatus);
        var provenance = Assert.IsType<SearchComponentBaseImplicitProvenance>(
            component.BaseImplicitProvenance);
        Assert.Equal(BaseImplicitRecognitionStatus.Ambiguous, provenance.RecognitionStatus);
        Assert.Equal(2, provenance.MechanicalSignatures.Count);
        Assert.Empty(component.ProviderDomainEvidence);
    }

    [Theory]
    [InlineData(BaseImplicitRecognitionStatus.CurrentExact, BaseImplicitSnapshotRole.CurrentCandidate, "Test Belt")]
    [InlineData(BaseImplicitRecognitionStatus.HistoricalExact, BaseImplicitSnapshotRole.HistoricalObserved, null)]
    public void CreateDraft_ExactBaseGuaranteeIsRetainedOnlyForCurrentRecognizedPresenceImplicit(
        BaseImplicitRecognitionStatus recognitionStatus,
        BaseImplicitSnapshotRole snapshotRole,
        string? expectedGuaranteedBase)
    {
        var item = parser.Parse("""
Item Class: Belts
Rarity: Rare
Test Belt
--------
Item Level: 84
--------
{ Implicit Modifier }
Has 1 Test Socket
""");
        var effect = PresenceEffect();
        var baseRecord = new ItemBaseRecord
        {
            Id = "base.test-belt",
            Name = "Test Belt",
            ItemClass = "Belts",
            Domain = "item",
            ImplicitModifierIds = [effect.Modifier!.Id!],
            Sources = [new GameDataSourceReference { SourceId = "test-source" }],
        };
        var catalog = GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "test",
                CreatedAtUtc = DateTimeOffset.UnixEpoch,
                Sources = [new GameDataPackageSource { SourceId = "test-source", RetrievedAtUtc = DateTimeOffset.UnixEpoch }],
            },
            ItemBases = [baseRecord],
            Modifiers = [effect.Modifier],
            Stats = effect.Stats,
            StatTranslations = effect.StatTranslations,
        });
        var baseResolution = new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Exact,
            MatchedItemBase = baseRecord,
            ResolvedBaseId = baseRecord.Id,
            ResolvedBaseName = baseRecord.Name,
            Candidates = [baseRecord],
        };
        var recognition = Recognition(
            recognitionStatus,
            snapshotRole,
            "source-commit",
            "source-version",
            effect);

        var result = mapper.CreateDraft(
            item,
            baseResolution,
            [Resolution(item, recognition)],
            catalog);

        Assert.NotNull(result.Draft);
        var component = Assert.Single(result.Draft!.ModifierFilters);
        Assert.False(component.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.PresenceOnly, component.ValueBoundShape);
        Assert.Equal(expectedGuaranteedBase, component.GuaranteedExactBaseName);
    }

    private ParsedItem ParseStaffBlock() => parser.Parse("""
Item Class: Warstaves
Rarity: Magic
Test Staff
Warstaff
--------
Item Level: 84
--------
{ Implicit Modifier }
+22% Chance to Block Attack Damage while wielding a Staff
""");

    private static ModifierCandidateResolutionResult Resolution(
        ParsedItem item,
        BaseImplicitRecognitionResult recognition) => new(
        ParsedModifierIndex: 0,
        ParsedModifier: item.Modifiers[0],
        ParsedModifierName: null,
        ParsedModifierKind: ParsedModifierKind.Implicit,
        GenerationType: ModifierGenerationType.Implicit,
        Status: ModifierCandidateResolutionStatus.Unknown,
        Candidates: [],
        Diagnostics: [],
        Locality: ModifierLocality.Unknown)
    {
        BaseImplicitRecognition = recognition,
    };

    private static BaseImplicitRecognitionResult Recognition(
        BaseImplicitRecognitionStatus status,
        BaseImplicitSnapshotRole role,
        string commit,
        string version,
        BaseImplicitMechanicalEffect effect) => new(
        status,
        [Match(effect, role, commit, version)],
        status == BaseImplicitRecognitionStatus.CurrentExact
            ? "base-implicit-current-exact"
            : "base-implicit-historical-exact",
        "Exact structured base-implicit mechanics matched.");

    private static BaseImplicitRecognitionMatch Match(
        BaseImplicitMechanicalEffect effect,
        BaseImplicitSnapshotRole role,
        string commit,
        string version)
    {
        var snapshot = new BaseImplicitSourceSnapshot
        {
            Id = $"{role}-snapshot",
            Role = role,
            ManifestSourceId = "test-source",
            CommitSha = commit,
            DataVersion = version,
        };
        return new BaseImplicitRecognitionMatch(
            new BaseImplicitObservation
            {
                CanonicalBaseId = "base.test",
                SourceSnapshotId = snapshot.Id,
                ImplicitModifierIds = [effect.SourceModifierId!],
                MechanicalEffectIds = [effect.Id],
            },
            effect,
            snapshot);
    }

    private static BaseImplicitMechanicalEffect Effect(
        string id,
        string statId,
        string format,
        decimal value,
        string mechanicalSignature)
    {
        var source = new GameDataSourceReference { SourceId = "test-source" };
        var modifier = new ModifierDefinition
        {
            Id = "old-base-implicit",
            GroupId = "old-base-implicit-group",
            Name = "Test base implicit",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "implicit",
            Domain = "item",
            Stats = [new ModifierStat { Index = 0, StatId = statId, MinValue = value, MaxValue = value }],
            Sources = [source],
        };
        return new BaseImplicitMechanicalEffect
        {
            Id = id,
            SourceSnapshotId = "test-snapshot",
            SourceModifierId = modifier.Id,
            IsResolved = true,
            MechanicalSignature = mechanicalSignature,
            Modifier = modifier,
            Stats = [new StatDefinition { Id = statId, IsLocal = false, Sources = [source] }],
            StatTranslations =
            [
                new StatTranslationDefinition
                {
                    Id = $"{id}-translation",
                    StatIds = [statId],
                    Language = "English",
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            ValueFormats = ["+#"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0, Handlers = [] }],
                            FormatLines = [format.Replace("+#", "{0}", StringComparison.Ordinal)],
                        },
                    ],
                    Sources = [source],
                },
            ],
        };
    }

    private static BaseImplicitMechanicalEffect PresenceEffect()
    {
        var source = new GameDataSourceReference { SourceId = "test-source" };
        var modifier = new ModifierDefinition
        {
            Id = "test-socket-implicit",
            GroupId = "test-socket-group",
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "item",
            Stats = [new ModifierStat { Index = 0, StatId = "test_socket_count", MinValue = 1m, MaxValue = 1m }],
            Sources = [source],
        };
        return new BaseImplicitMechanicalEffect
        {
            Id = "test-socket-effect",
            SourceSnapshotId = "test-snapshot",
            SourceModifierId = modifier.Id,
            IsResolved = true,
            MechanicalSignature = new string('c', 64),
            Modifier = modifier,
            Stats = [new StatDefinition { Id = "test_socket_count", IsLocal = true, Sources = [source] }],
            StatTranslations =
            [
                new StatTranslationDefinition
                {
                    Id = "test-socket-translation",
                    StatIds = ["test_socket_count"],
                    Language = "English",
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0, MinValue = 1m, MaxValue = 1m }],
                            ValueFormats = ["ignore"],
                            IndexHandlers = [new StatTranslationIndexHandler { Index = 0, Handlers = [] }],
                            FormatLines = ["Has 1 Test Socket"],
                        },
                    ],
                    Sources = [source],
                },
            ],
        };
    }
}
