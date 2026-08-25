using System.Text.Json.Nodes;
using PoEnhance.GameData;

namespace PoEnhance.GameData.Tests;

public sealed class UniqueItemCatalogTests
{
    [Fact]
    public void Validate_SchemaTwoWithoutCatalog_IsInvalid()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        package = package with
        {
            Manifest = package.Manifest with { SchemaVersion = 2 },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.Contains(result.Errors, error =>
            error.Code == GameDataValidationErrorCodes.PackageUniqueItemsRequired);
    }

    [Fact]
    public void JsonRoundTrip_ValidCatalog_PreservesVersionBlockAndProvenance()
    {
        var package = CreatePackage();
        var json = GameDataPackageJson.Serialize(package);

        Assert.Contains("\"locality\": \"global\"", json, StringComparison.Ordinal);
        Assert.Contains("\"valueShape\": \"scalar\"", json, StringComparison.Ordinal);

        var roundTripped = Assert.IsType<GameDataPackage>(
            GameDataPackageJson.Deserialize(json));

        Assert.True(GameDataPackageValidator.Validate(roundTripped).IsValid);
        var identity = Assert.Single(Assert.IsType<UniqueItemCatalog>(roundTripped.UniqueItems).Items);
        Assert.Equal(UniqueItemKind.Replica, identity.Kind);
        var version = Assert.Single(identity.Versions);
        Assert.Equal(UniqueItemVersionRole.Historical, version.Role);
        var block = Assert.Single(version.ModifierBlocks);
        Assert.Equal(UniqueModifierSemanticLocality.Global,
            block.SourceSemanticFingerprint.Locality);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["mod.prefix.maximum-life.t5"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(["base_maximum_life"], block.MechanicalMapping.StatIds);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        Assert.Equal(["implicit-zero-stat-composition"], provenance.ResolutionReasons);
        Assert.Equal("copiedInstance", provenance.ValueAuthority);
        Assert.Equal("translation.maximum-life", Assert.Single(provenance.Translations).TranslationId);
        Assert.Equal(UniqueModifierSemanticLocality.Global,
            provenance.SourceSemanticFingerprint!.Locality);
        Assert.Equal(["base_maximum_life"],
            provenance.MatchedSemanticFingerprint!.OrderedStatIds);
        Assert.Equal("number", Assert.Single(provenance.MatchedSemanticFingerprint.Values).Unit);
        Assert.Equal(["pob:test"], block.SourceObservationIds);
    }

    [Fact]
    public void Validate_MechanicalProvenanceWithUnknownDefaultedStat_FailsClosed()
    {
        var package = CreatePackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var identity = Assert.Single(catalog.Items);
        var version = Assert.Single(identity.Versions);
        var block = Assert.Single(version.ModifierBlocks);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        var evidence = Assert.Single(provenance.Translations);
        package = package with
        {
            UniqueItems = catalog with
            {
                Items =
                [
                    identity with
                    {
                        Versions =
                        [
                            version with
                            {
                                ModifierBlocks =
                                [
                                    block with
                                    {
                                        MechanicalMapping = block.MechanicalMapping with
                                        {
                                            Provenance = provenance with
                                            {
                                                Translations =
                                                [
                                                    evidence with
                                                    {
                                                        DefaultedStatIds = ["not-in-vector"],
                                                    },
                                                ],
                                            },
                                        },
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.Contains(result.Errors, error =>
            error.Code == GameDataValidationErrorCodes.UniqueCatalogBlockInvalid);
    }

    [Fact]
    public void Validate_MechanicalFingerprintWithRepeatedOrderedStatPositions_IsValid()
    {
        var package = CreatePackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var identity = Assert.Single(catalog.Items);
        var version = Assert.Single(identity.Versions);
        var block = Assert.Single(version.ModifierBlocks);
        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            block.MechanicalMapping.Provenance);
        var fingerprint = Assert.IsType<UniqueModifierSemanticFingerprint>(
            provenance.MatchedSemanticFingerprint);
        package = package with
        {
            UniqueItems = catalog with
            {
                Items =
                [
                    identity with
                    {
                        Versions =
                        [
                            version with
                            {
                                ModifierBlocks =
                                [
                                    block with
                                    {
                                        MechanicalMapping = block.MechanicalMapping with
                                        {
                                            Provenance = provenance with
                                            {
                                                MatchedSemanticFingerprint = fingerprint with
                                                {
                                                    OrderedStatIds =
                                                    [
                                                        "base_maximum_life",
                                                        "base_maximum_life",
                                                    ],
                                                },
                                            },
                                        },
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        Assert.True(GameDataPackageValidator.Validate(package).IsValid);
    }

    [Fact]
    public void Validate_UnknownMechanicalReference_FailsClosed()
    {
        var package = CreatePackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var identity = Assert.Single(catalog.Items);
        var version = Assert.Single(identity.Versions);
        var block = Assert.Single(version.ModifierBlocks);
        package = package with
        {
            UniqueItems = catalog with
            {
                Items =
                [
                    identity with
                    {
                        Versions =
                        [
                            version with
                            {
                                ModifierBlocks =
                                [
                                    block with
                                    {
                                        MechanicalMapping = block.MechanicalMapping with
                                        {
                                            ModifierIds = ["mod.does-not-exist"],
                                        },
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.Contains(result.Errors, error =>
            error.Code == GameDataValidationErrorCodes.UniqueCatalogBlockInvalid);
    }

    [Fact]
    public void Validate_GeneratedCandidateWithoutPoolMembership_FailsClosed()
    {
        var package = CreatePackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var identity = Assert.Single(catalog.Items);
        var version = Assert.Single(identity.Versions);
        var block = Assert.Single(version.ModifierBlocks);
        package = package with
        {
            UniqueItems = catalog with
            {
                Items =
                [
                    identity with
                    {
                        Versions =
                        [
                            version with
                            {
                                GeneratedCandidateSelectionLimit = 1,
                                ModifierBlocks =
                                [
                                    block with
                                    {
                                        SourceSemantics =
                                            UniqueModifierSourceSemantics.GeneratedCandidate,
                                        CandidatePoolMembershipIds = [],
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.Contains(result.Errors, error =>
            error.Code == GameDataValidationErrorCodes.UniqueCatalogBlockInvalid);
    }

    [Fact]
    public void JsonRoundTrip_GeneratedCandidate_PreservesPoolMembershipAndSelectionLimit()
    {
        var package = CreatePackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var identity = Assert.Single(catalog.Items);
        var version = Assert.Single(identity.Versions);
        var block = Assert.Single(version.ModifierBlocks);
        package = package with
        {
            UniqueItems = catalog with
            {
                Items =
                [
                    identity with
                    {
                        Versions =
                        [
                            version with
                            {
                                GeneratedCandidateSelectionLimit = 2,
                                ModifierBlocks =
                                [
                                    block with
                                    {
                                        SourceSemantics =
                                            UniqueModifierSourceSemantics.GeneratedCandidate,
                                        CandidatePoolMembershipIds = ["pob-generated-candidate:test"],
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var roundTripped = Assert.IsType<GameDataPackage>(
            GameDataPackageJson.Deserialize(GameDataPackageJson.Serialize(package)));

        Assert.True(GameDataPackageValidator.Validate(roundTripped).IsValid);
        var retainedVersion = Assert.Single(Assert.Single(roundTripped.UniqueItems!.Items).Versions);
        Assert.Equal(2, retainedVersion.GeneratedCandidateSelectionLimit);
        var retainedBlock = Assert.Single(retainedVersion.ModifierBlocks);
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, retainedBlock.SourceSemantics);
        Assert.Equal(["pob-generated-candidate:test"], retainedBlock.CandidatePoolMembershipIds);
    }

    [Fact]
    public void JsonRoundTrip_OptionAxis_PreservesChoiceAndBlockProvenance()
    {
        var package = CreatePackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var identity = Assert.Single(catalog.Items);
        var version = Assert.Single(identity.Versions);
        var block = Assert.Single(version.ModifierBlocks);
        package = package with
        {
            UniqueItems = catalog with
            {
                Items =
                [
                    identity with
                    {
                        Versions =
                        [
                            version with
                            {
                                OptionAxes =
                                [
                                    new UniqueItemOptionAxis
                                    {
                                        Id = "pob-option-axis:test",
                                        SelectionLimit = 1,
                                        Choices =
                                        [
                                            new UniqueItemOptionChoice
                                            {
                                                Id = "pob-option-choice:test",
                                                SourceObservationIds = ["pob:test"],
                                            },
                                        ],
                                        SourceObservationIds = ["pob:test"],
                                    },
                                ],
                                ModifierBlocks =
                                [
                                    block with
                                    {
                                        OptionChoiceMemberships =
                                        [
                                            new UniqueModifierOptionChoiceMembership
                                            {
                                                OptionAxisId = "pob-option-axis:test",
                                                OptionChoiceId = "pob-option-choice:test",
                                                SourceObservationIds = ["pob:test"],
                                            },
                                        ],
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var roundTripped = Assert.IsType<GameDataPackage>(
            GameDataPackageJson.Deserialize(GameDataPackageJson.Serialize(package)));

        Assert.True(GameDataPackageValidator.Validate(roundTripped).IsValid);
        var retainedVersion = Assert.Single(Assert.Single(roundTripped.UniqueItems!.Items).Versions);
        var retainedAxis = Assert.Single(retainedVersion.OptionAxes);
        Assert.Equal("pob-option-axis:test", retainedAxis.Id);
        Assert.Equal("pob-option-choice:test", Assert.Single(retainedAxis.Choices).Id);
        var membership = Assert.Single(Assert.Single(retainedVersion.ModifierBlocks)
            .OptionChoiceMemberships);
        Assert.Equal(retainedAxis.Id, membership.OptionAxisId);
        Assert.Equal(retainedAxis.Choices[0].Id, membership.OptionChoiceId);
        Assert.Equal(["pob:test"], membership.SourceObservationIds);
    }

    [Fact]
    public void JsonDeserialize_SchemaThreeWithoutOptionAxisProperties_DefaultsToEmptyCollections()
    {
        var legacyJson = JsonNode.Parse(GameDataPackageJson.Serialize(CreatePackage()))!.AsObject();
        var version = legacyJson["uniqueItems"]!["items"]![0]!["versions"]![0]!.AsObject();
        var block = version["modifierBlocks"]![0]!.AsObject();
        Assert.True(version.Remove("optionAxes"));
        Assert.True(block.Remove("optionChoiceMemberships"));

        var deserialized = Assert.IsType<GameDataPackage>(
            GameDataPackageJson.Deserialize(legacyJson.ToJsonString()));

        Assert.True(GameDataPackageValidator.Validate(deserialized).IsValid);
        var retainedVersion = Assert.Single(Assert.Single(deserialized.UniqueItems!.Items).Versions);
        Assert.Empty(retainedVersion.OptionAxes);
        Assert.Empty(Assert.Single(retainedVersion.ModifierBlocks).OptionChoiceMemberships);
    }

    [Fact]
    public void Validate_OptionMembershipWithUnknownChoice_FailsClosed()
    {
        var package = CreatePackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var identity = Assert.Single(catalog.Items);
        var version = Assert.Single(identity.Versions);
        var block = Assert.Single(version.ModifierBlocks);
        package = package with
        {
            UniqueItems = catalog with
            {
                Items =
                [
                    identity with
                    {
                        Versions =
                        [
                            version with
                            {
                                OptionAxes =
                                [
                                    new UniqueItemOptionAxis
                                    {
                                        Id = "pob-option-axis:test",
                                        SelectionLimit = 1,
                                        Choices =
                                        [
                                            new UniqueItemOptionChoice
                                            {
                                                Id = "pob-option-choice:test",
                                                SourceObservationIds = ["pob:test"],
                                            },
                                        ],
                                        SourceObservationIds = ["pob:test"],
                                    },
                                ],
                                ModifierBlocks =
                                [
                                    block with
                                    {
                                        OptionChoiceMemberships =
                                        [
                                            new UniqueModifierOptionChoiceMembership
                                            {
                                                OptionAxisId = "pob-option-axis:test",
                                                OptionChoiceId = "pob-option-choice:missing",
                                                SourceObservationIds = ["pob:test"],
                                            },
                                        ],
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.Contains(result.Errors, error =>
            error.Code == GameDataValidationErrorCodes.UniqueCatalogBlockInvalid);
    }

    [Fact]
    public void JsonRoundTrip_SchemaThreeRelationship_PreservesDirectionalProvenance()
    {
        var package = CreateFoulbornPackage();

        var roundTripped = Assert.IsType<GameDataPackage>(
            GameDataPackageJson.Deserialize(GameDataPackageJson.Serialize(package)));

        Assert.True(GameDataPackageValidator.Validate(roundTripped).IsValid);
        var catalog = Assert.IsType<UniqueItemCatalog>(roundTripped.UniqueItems);
        var source = Assert.Single(catalog.FoulbornRelationshipSources);
        Assert.Equal("src/Data/ModFoulbornMap.jsonc", source.SourcePath);
        var relationship = Assert.Single(catalog.FoulbornModifierRelationships);
        Assert.Equal("mod.prefix.maximum-life.t5", relationship.NormalModifierId);
        Assert.Equal("mod.suffix.fire-resistance.t4", relationship.FoulbornModifierId);
        Assert.Equal(["unique-block:test"], relationship.NormalModifierBlockIds);
        Assert.Equal(UniqueItemVersionRole.Current, relationship.AppliesToRole);
        Assert.Equal(UniqueFoulbornModifierRelationshipStatus.Exact, relationship.Status);
        Assert.Equal(
            relationship.Id,
            Assert.Single(GameDataCatalog.FromPackage(roundTripped)
                .FindFoulbornRelationshipsByUniqueItemId(relationship.UniqueItemId)).Id);
    }

    [Fact]
    public void Validate_SchemaThreeWithoutRelationshipEvidence_FailsClosed()
    {
        var package = CreatePackage() with
        {
            Manifest = CreatePackage().Manifest with { SchemaVersion = 3 },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.Contains(result.Errors, error =>
            error.Code == GameDataValidationErrorCodes.PackageFoulbornRelationshipsRequired);
    }

    [Fact]
    public void Validate_ConflictingItemScopedRelationship_FailsClosed()
    {
        var package = CreateFoulbornPackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var relationship = Assert.Single(catalog.FoulbornModifierRelationships);
        package = package with
        {
            UniqueItems = catalog with
            {
                FoulbornModifierRelationships =
                [
                    relationship,
                    relationship with
                    {
                        Id = "foulborn-relationship:conflict",
                        FoulbornModifierId = "mod.implicit.gold-ring.item-rarity",
                    },
                ],
            },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.Contains(result.Errors, error =>
            error.Code == GameDataValidationErrorCodes.UniqueFoulbornRelationshipConflict);
    }

    [Fact]
    public void Validate_CollisionFreeFoulbornIdentityAlias_WithCompleteEvidence_IsValid()
    {
        var package = CreateFoulbornPackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var identity = Assert.Single(catalog.Items) with
        {
            CanonicalName = "Mjölner",
            CanonicalIdentityKey = "ordinary|mjolner",
        };
        var relationship = Assert.Single(catalog.FoulbornModifierRelationships) with
        {
            ItemName = "Mjolner",
            CanonicalItemName = "Mjölner",
            CanonicalIdentityKey = "ordinary|mjolner",
            IdentityNormalizationRule = "unicode-form-d-casefold-diacritic-punctuation-v1",
            IdentityLinkageEvidence = "Pinned source evidence resolves one identity.",
            CurrentHistoryDecisionReason = "The relationship applies only to explicit current observations.",
            UniqueItemId = identity.Id,
        };
        package = package with
        {
            UniqueItems = catalog with
            {
                Items = [identity],
                FoulbornModifierRelationships = [relationship],
            },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Validate_DistinctIdentitiesSharingCanonicalKey_FailsClosed()
    {
        var package = CreateFoulbornPackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var first = Assert.Single(catalog.Items) with { CanonicalIdentityKey = "ordinary|test item" };
        var second = first with
        {
            Id = "unique:test-collision",
            CanonicalName = "Tést Item",
            Versions = first.Versions.Select(version => version with { Id = "unique-version:test-collision" }).ToArray(),
        };
        package = package with
        {
            UniqueItems = catalog with { Items = [first, second] },
        };

        var result = GameDataPackageValidator.Validate(package);

        Assert.Contains(result.Errors, error =>
            error.Code == GameDataValidationErrorCodes.UniqueCatalogIdentityCollision);
    }

    private static GameDataPackage CreatePackage()
    {
        var package = GameDataPackageFixtures.CreateDevelopmentPackage();
        const string sourceObservationId = "pob:test";
        return package with
        {
            Manifest = package.Manifest with
            {
                SchemaVersion = 2,
                Sources =
                [
                    .. package.Manifest.Sources,
                    new GameDataPackageSource
                    {
                        SourceId = "path-of-building",
                        RetrievedAtUtc = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
                        SourceVersion = "v2.67.2",
                        SourceUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                    },
                ],
            },
            UniqueItems = new UniqueItemCatalog
            {
                SourceObservations =
                [
                    new UniqueCatalogSourceObservation
                    {
                        Id = sourceObservationId,
                        ManifestSourceId = "path-of-building",
                        RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                        Tag = "v2.67.2",
                        CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                        SourcePath = "Data/Uniques/test.lua",
                        IsGenerated = true,
                        ObservedKind = UniqueItemKind.Replica,
                        RawEntrySha256 = new string('a', 64),
                    },
                ],
                Items =
                [
                    new UniqueItemIdentity
                    {
                        Id = "unique:test",
                        CanonicalName = "Replica Test Item",
                        Kind = UniqueItemKind.Replica,
                        BaseTypeEvidence = ["Gold Ring"],
                        SourceObservationIds = [sourceObservationId],
                        Versions =
                        [
                            new UniqueItemVersionObservation
                            {
                                Id = "unique-version:test",
                                Label = "Pre 3.29.0",
                                Role = UniqueItemVersionRole.Historical,
                                BaseType = "Gold Ring",
                                SourceObservationIds = [sourceObservationId],
                                ModifierBlocks =
                                [
                                    new UniqueModifierBlock
                                    {
                                        Id = "unique-block:test",
                                        Kind = UniqueModifierBlockKind.Unique,
                                        Lines = ["+(50-59) to maximum Life"],
                                        CanonicalSignatures = ["+<number> to maximum Life"],
                                        SourceSemanticFingerprint = new UniqueModifierSemanticFingerprint
                                        {
                                            Locality = UniqueModifierSemanticLocality.Global,
                                            EvidenceMethods = ["pob-item-context-v1"],
                                        },
                                        SourceObservationIds = [sourceObservationId],
                                        MechanicalMapping = new UniqueModifierMechanicalMapping
                                        {
                                            Status = UniqueModifierMechanicalMappingStatus.Exact,
                                            ModifierIds = ["mod.prefix.maximum-life.t5"],
                                            StatIds = ["base_maximum_life"],
                                            Provenance = new UniqueModifierMechanicalProvenance
                                            {
                                                ResolutionReasons = ["implicit-zero-stat-composition"],
                                                SourceSemanticFingerprint = new UniqueModifierSemanticFingerprint
                                                {
                                                    Locality = UniqueModifierSemanticLocality.Global,
                                                    EvidenceMethods = ["pob-item-context-v1"],
                                                },
                                                MatchedSemanticFingerprint = new UniqueModifierSemanticFingerprint
                                                {
                                                    Locality = UniqueModifierSemanticLocality.Global,
                                                    OrderedStatIds = ["base_maximum_life"],
                                                    ValueShape = UniqueModifierSemanticValueShape.Scalar,
                                                    Values =
                                                    [
                                                        new UniqueModifierSemanticValue
                                                        {
                                                            Index = 0,
                                                            StatId = "base_maximum_life",
                                                            Format = "+#",
                                                            Unit = "number",
                                                        },
                                                    ],
                                                    EvidenceMethods = ["repoe-stat-vector-v1"],
                                                },
                                                Translations =
                                                [
                                                    new UniqueModifierTranslationEvidence
                                                    {
                                                        TranslationId = "translation.maximum-life",
                                                        StatIds = ["base_maximum_life"],
                                                        ModifierStatIndices = [0],
                                                        Conditions =
                                                        [
                                                            new StatTranslationCondition
                                                            {
                                                                Index = 0,
                                                            },
                                                        ],
                                                        ValueFormats = ["+#"],
                                                        FormatLines = ["{0} to maximum Life"],
                                                        IndexHandlers =
                                                        [
                                                            new StatTranslationIndexHandler { Index = 0 },
                                                        ],
                                                    },
                                                ],
                                                CatalogValuesUsedForSelection = true,
                                                ValueAuthority = "copiedInstance",
                                                SafetyRationale = "Test proof retains copied-instance values.",
                                            },
                                        },
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        };
    }

    internal static GameDataPackage CreateFoulbornPackage()
    {
        var package = CreatePackage();
        var catalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);
        var identity = Assert.Single(catalog.Items) with
        {
            CanonicalName = "Test Item",
            Kind = UniqueItemKind.Ordinary,
        };
        const string relationshipSourceId = "pob-foulborn-source:test";
        return package with
        {
            Manifest = package.Manifest with { SchemaVersion = 3 },
            UniqueItems = catalog with
            {
                Items = [identity],
                FoulbornRelationshipSources =
                [
                    new UniqueFoulbornRelationshipSourceObservation
                    {
                        Id = relationshipSourceId,
                        ManifestSourceId = "path-of-building",
                        RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                        Tag = "v2.67.2",
                        CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                        SourcePath = "src/Data/ModFoulbornMap.jsonc",
                        SourceFileSha256 = new string('d', 64),
                    },
                ],
                FoulbornModifierRelationships =
                [
                    new UniqueFoulbornModifierRelationship
                    {
                        Id = "foulborn-relationship:test",
                        ItemName = "Test Item",
                        UniqueItemId = identity.Id,
                        NormalModifierId = "mod.prefix.maximum-life.t5",
                        FoulbornModifierId = "mod.suffix.fire-resistance.t4",
                        NormalModifierBlockIds = ["unique-block:test"],
                        AppliesToRole = UniqueItemVersionRole.Current,
                        SourceObservationId = relationshipSourceId,
                        Status = UniqueFoulbornModifierRelationshipStatus.Exact,
                    },
                ],
            },
        };
    }
}
