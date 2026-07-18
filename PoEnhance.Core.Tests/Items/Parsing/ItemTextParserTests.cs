using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Tests.Items.Parsing;

public sealed class ItemTextParserTests
{
    private readonly ItemTextParser _parser = new();

    [Fact]
    public void Parse_LeatherBeltSample_ExtractsExpectedFieldsAndPreservesRawText()
    {
        var rawText = ReadSample("unique-leather-belt.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal(ParsedItemInputFormat.Normal, result.InputFormat);
        Assert.Equal("Belts", result.ItemClass);
        Assert.Equal("Unique", result.Rarity);
        Assert.Equal("Screams of the Desiccated", result.Name);
        Assert.Equal("Leather Belt", result.BaseType);
        Assert.Null(result.ItemTypeDescriptor);
        Assert.Empty(result.ItemStates);
        Assert.Empty(result.NoteLines);
        Assert.Empty(result.TraditionalInfluences);
        Assert.Empty(result.EldritchInfluences);
        Assert.False(result.IsCorrupted);
        Assert.Equal(85, result.ItemLevel);
        Assert.Contains("Requirements:", result.PropertyLines);
        Assert.Contains("Level: 56", result.PropertyLines);
        Assert.Contains("+35 to maximum Life (implicit)", result.ModifierLines);
        Assert.Contains("You have Diamond Shrine Buff while affected by no Flasks", result.ModifierLines);
        Assert.Contains("+32 to Intelligence", result.ModifierLines);
        Assert.Contains("+26% to Chaos Resistance", result.ModifierLines);
        Assert.Contains(result.Modifiers, modifier =>
            modifier.Text == "+35 to maximum Life (implicit)"
            && modifier.ValueLines.SequenceEqual(["+35 to maximum Life (implicit)"])
            && modifier.Kind == ParsedModifierKind.Implicit
            && modifier.RawMetadataLine is null
            && modifier.Rank is null
            && !modifier.IsCrafted
            && !modifier.IsFractured
            && !modifier.IsVeiled);
        Assert.Contains(result.Modifiers, modifier =>
            modifier.Text == "+32 to Intelligence"
            && modifier.ValueLines.SequenceEqual(["+32 to Intelligence"])
            && modifier.Kind == ParsedModifierKind.Unknown
            && modifier.RawMetadataLine is null
            && modifier.Rank is null
            && !modifier.IsCrafted
            && !modifier.IsFractured
            && !modifier.IsVeiled);
        Assert.Contains("\"I staggered on, dying of thirst... I thought I found respite,", result.FlavourTextLines);
        Assert.DoesNotContain("\"I staggered on, dying of thirst... I thought I found respite,", result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_AdvancedBodyArmourSample_GroupsMultiLineMetadataModifiersAndPreservesRawText()
    {
        var rawText = ReadSample("advanced-rare-body-armour.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal(ParsedItemInputFormat.Advanced, result.InputFormat);
        Assert.Equal("Body Armours", result.ItemClass);
        Assert.Equal("Rare", result.Rarity);
        Assert.Equal("Rage Hide", result.Name);
        Assert.Equal("War Plate", result.BaseType);
        Assert.Null(result.ItemTypeDescriptor);
        Assert.Empty(result.ItemStates);
        Assert.Empty(result.NoteLines);
        Assert.Equal(30, result.ItemLevel);
        Assert.Contains("Armour: 422 (augmented)", result.PropertyLines);
        Assert.DoesNotContain(result.PropertyLines, line => line.StartsWith("{ Prefix Modifier", StringComparison.Ordinal));
        Assert.Equal(3, result.Modifiers.Count);

        var journeymans = Assert.Single(result.Modifiers, modifier => modifier.Name == "Journeyman's");
        Assert.Equal(
            [
                "21(20-24)% increased Physical Damage",
                "+28(21-46) to Accuracy Rating",
            ],
            journeymans.ValueLines);
        Assert.Equal(
            $"21(20-24)% increased Physical Damage{Environment.NewLine}+28(21-46) to Accuracy Rating",
            journeymans.Text);
        Assert.Equal("{ Prefix Modifier \"Journeyman's\" (Tier: 7) — Damage, Physical, Attack }", journeymans.RawMetadataLine);
        Assert.Equal(ParsedModifierKind.Prefix, journeymans.Kind);
        Assert.Equal(7, journeymans.Tier);
        Assert.Null(journeymans.Rank);
        Assert.False(journeymans.IsCrafted);
        Assert.Equal("Damage, Physical, Attack", journeymans.CategoryText);

        var light = Assert.Single(result.Modifiers, modifier => modifier.Name == "of Light");
        Assert.Equal(
            [
                "12(12-15)% increased Global Accuracy Rating",
                "10% increased Light Radius",
            ],
            light.ValueLines);
        Assert.Equal(ParsedModifierKind.Suffix, light.Kind);
        Assert.Equal(2, light.Tier);
        Assert.Null(light.Rank);
        Assert.False(light.IsCrafted);
        Assert.Equal("Attack", light.CategoryText);

        var upgraded = Assert.Single(result.Modifiers, modifier => modifier.Name == "Upgraded");
        Assert.Equal(
            [
                "10(8-12)% increased Physical Damage",
                "+15(10-20) to Accuracy Rating",
            ],
            upgraded.ValueLines);
        Assert.Equal("{ Master Crafted Prefix Modifier \"Upgraded\" (Rank: 1) — Damage, Physical, Attack }", upgraded.RawMetadataLine);
        Assert.Equal(ParsedModifierKind.Prefix, upgraded.Kind);
        Assert.Null(upgraded.Tier);
        Assert.Equal(1, upgraded.Rank);
        Assert.True(upgraded.IsCrafted);
        Assert.Equal("Damage, Physical, Attack", upgraded.CategoryText);
        Assert.Equal(2, result.PrefixModifiers.Count);
        Assert.Single(result.SuffixModifiers);
        Assert.Empty(result.ImplicitModifiers);
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);
    }

    [Fact]
    public void Parse_EldritchInfluencedItem_ClassifiesInfluenceLinesSeparately()
    {
        var rawText = ReadSample("eldritch-influenced-boots.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Empty(result.TraditionalInfluences);
        Assert.Equal(["Searing Exarch Item", "Eater of Worlds Item"], result.EldritchInfluences);
        Assert.DoesNotContain("Searing Exarch Item", result.ModifierLines);
        Assert.DoesNotContain("Eater of Worlds Item", result.ModifierLines);
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Searing Exarch Item"));
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Eater of Worlds Item"));
        Assert.DoesNotContain("Searing Exarch Item", result.UnclassifiedLines);
        Assert.Contains(result.ExplicitModifiersWithUnknownKind, modifier => modifier.Text == "+35 to maximum Life");
    }

    [Fact]
    public void Parse_EldritchImplicitMetadata_RetainsDistinctOriginAndTier()
    {
        var result = _parser.Parse("""
Item Class: Body Armours
Rarity: Rare
Gale Wrap
Marshall's Brigandine
--------
Item Level: 84
--------
{ Eater of Worlds Implicit Modifier (Lesser) - Physical, Elemental, Cold }
10% of Physical Damage from Hits taken as Cold Damage
{ Searing Exarch Implicit Modifier (Greater) }
+23(22-23)% to Critical Strike Multiplier for Attack Damage
--------
Searing Exarch Item
Eater of Worlds Item
""");

        Assert.Collection(
            result.ImplicitModifiers,
            eater =>
            {
                Assert.Equal(ParsedImplicitModifierOrigin.EaterOfWorlds, eater.ImplicitOrigin);
                Assert.Equal(ParsedEldritchImplicitTier.Lesser, eater.EldritchTier);
            },
            exarch =>
            {
                Assert.Equal(ParsedImplicitModifierOrigin.SearingExarch, exarch.ImplicitOrigin);
                Assert.Equal(ParsedEldritchImplicitTier.Greater, exarch.EldritchTier);
            });
    }

    [Fact]
    public void Parse_SynthesisedItem_AssignsGenericImplicitFromConfirmedItemState()
    {
        var result = _parser.Parse("""
Item Class: Helmets
Rarity: Rare
Synthesised Item
Gale Dome
Synthesised Reaver Helmet
--------
Item Level: 84
--------
{ Implicit Modifier }
+24(22-25) to maximum Energy Shield
""");

        var implicitModifier = Assert.Single(result.ImplicitModifiers);
        Assert.Equal(ParsedImplicitModifierOrigin.Synthesis, implicitModifier.ImplicitOrigin);
        Assert.Null(implicitModifier.EldritchTier);
    }

    [Fact]
    public void Parse_AdvancedItem_ClassifiesModifierBucketsFromMetadata()
    {
        var rawText = ReadSample("advanced-rare-ring-with-implicit.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal(ParsedItemInputFormat.Advanced, result.InputFormat);
        Assert.Single(result.ImplicitModifiers);
        Assert.Equal(2, result.PrefixModifiers.Count);
        Assert.Equal(2, result.SuffixModifiers.Count);
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);

        var implicitModifier = Assert.Single(result.ImplicitModifiers);
        Assert.Equal("Ruby and Sapphire", implicitModifier.Name);
        Assert.Equal("+12% to Fire and Cold Resistances (implicit)", implicitModifier.Text);

        var fracturedSuffix = Assert.Single(result.SuffixModifiers, modifier => modifier.Name == "of the Order");
        Assert.True(fracturedSuffix.IsFractured);
        Assert.False(fracturedSuffix.IsVeiled);
        Assert.Equal(ParsedModifierKind.Suffix, fracturedSuffix.Kind);

        var veiledPrefix = Assert.Single(result.PrefixModifiers, modifier => modifier.Name == "Chosen");
        Assert.True(veiledPrefix.IsVeiled);
        Assert.True(veiledPrefix.IsNamedUnveiled);
        Assert.False(veiledPrefix.IsUnrevealedVeiledPlaceholder);
        Assert.Equal(ParsedVeiledModifierState.NamedUnveiled, veiledPrefix.VeiledState);
        Assert.Equal(1, veiledPrefix.Rank);
        Assert.Equal(ParsedModifierKind.Prefix, veiledPrefix.Kind);
    }

    [Fact]
    public void Parse_RarePoleaxeSample_ExtractsItemTypeDescriptorWithoutChangingBaseType()
    {
        var rawText = ReadSample("rare-poleaxe.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal(ParsedItemInputFormat.Normal, result.InputFormat);
        Assert.Equal("Two Hand Axes", result.ItemClass);
        Assert.Equal("Rare", result.Rarity);
        Assert.Equal("Dread Edge", result.Name);
        Assert.Equal("Poleaxe", result.BaseType);
        Assert.Equal("Two Handed Axe", result.ItemTypeDescriptor);
        Assert.Empty(result.ItemStates);
        Assert.Empty(result.NoteLines);
        Assert.Equal(30, result.ItemLevel);
        Assert.Contains("Physical Damage: 55-103 (augmented)", result.PropertyLines);
        Assert.Contains("Critical Strike Chance: 5.00%", result.PropertyLines);
        Assert.DoesNotContain("Two Handed Axe", result.UnclassifiedLines);
        Assert.Empty(result.PrefixModifiers);
        Assert.Empty(result.SuffixModifiers);
        Assert.Contains(result.ExplicitModifiersWithUnknownKind, modifier => modifier.Text == "Adds 5 to 10 Physical Damage");
    }

    [Fact]
    public void Parse_AmuletSample_LeavesItemTypeDescriptorEmpty()
    {
        var rawText = ReadSample("rare-onyx-amulet.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("Amulets", result.ItemClass);
        Assert.Equal("Dusk Beads", result.Name);
        Assert.Equal("Onyx Amulet", result.BaseType);
        Assert.Null(result.ItemTypeDescriptor);
        Assert.Contains("Requirements:", result.PropertyLines);
        Assert.DoesNotContain("Onyx Amulet", result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_UnidentifiedRareItem_TreatsSingleHeaderLineAsBaseType()
    {
        var rawText = ReadSample("unidentified-rare-poleaxe.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("Two Hand Axes", result.ItemClass);
        Assert.Equal("Rare", result.Rarity);
        Assert.Null(result.Name);
        Assert.Equal("Poleaxe", result.BaseType);
        Assert.Equal("Two Handed Axe", result.ItemTypeDescriptor);
        Assert.Contains("Unidentified", result.ItemStates);
        Assert.False(result.IsCorrupted);
        Assert.Contains("Physical Damage: 32-60", result.PropertyLines);
        Assert.DoesNotContain("Unidentified", result.PropertyLines);
        Assert.DoesNotContain("Unidentified", result.ModifierLines);
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Unidentified"));
        Assert.DoesNotContain("Unidentified", result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_UnidentifiedMagicItem_TreatsSingleHeaderLineAsBaseType()
    {
        var rawText = ReadSample("unidentified-magic-bow.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("Bows", result.ItemClass);
        Assert.Equal("Magic", result.Rarity);
        Assert.Null(result.Name);
        Assert.Equal("Grove Bow", result.BaseType);
        Assert.Equal("Bow", result.ItemTypeDescriptor);
        Assert.Contains("Unidentified", result.ItemStates);
        Assert.False(result.IsCorrupted);
        Assert.DoesNotContain("Unidentified", result.PropertyLines);
        Assert.DoesNotContain("Unidentified", result.ModifierLines);
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Unidentified"));
        Assert.DoesNotContain("Unidentified", result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_CorruptedAndNoteLines_ClassifiesThemSeparately()
    {
        var rawText = ReadSample("corrupted-noted-amulet.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("Dusk Beads", result.Name);
        Assert.Equal("Onyx Amulet", result.BaseType);
        Assert.Contains("Corrupted", result.ItemStates);
        Assert.True(result.IsCorrupted);
        Assert.Contains("Note: ~price 1 chaos", result.NoteLines);
        Assert.Equal("~price 1 chaos", result.ListingNote);
        Assert.DoesNotContain("Corrupted", result.PropertyLines);
        Assert.DoesNotContain("Corrupted", result.ModifierLines);
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Corrupted"));
        Assert.DoesNotContain("Corrupted", result.UnclassifiedLines);
        Assert.DoesNotContain("Note: ~price 1 chaos", result.PropertyLines);
        Assert.DoesNotContain("Note: ~price 1 chaos", result.ModifierLines);
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Note: ~price 1 chaos"));
        Assert.DoesNotContain("Note: ~price 1 chaos", result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_ItemStateFlagsAreCanonicalForMirroredAndIdentification()
    {
        var mirrored = _parser.Parse("""
Item Class: Rings
Rarity: Rare
Mirror Band
Iron Ring
--------
Item Level: 84
--------
Mirrored
""");
        var unidentified = _parser.Parse(ReadSample("unidentified-rare-poleaxe.txt"));

        Assert.True(mirrored.IsMirrored);
        Assert.True(mirrored.IsIdentified);
        Assert.False(unidentified.IsMirrored);
        Assert.False(unidentified.IsIdentified);
    }

    [Fact]
    public void Parse_MalformedAdvancedMetadata_KeepsMetadataWithVisibleModifier()
    {
        var rawText = ReadSample("malformed-advanced-metadata.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal(ParsedItemInputFormat.Advanced, result.InputFormat);
        Assert.DoesNotContain(result.PropertyLines, line => line.StartsWith("{ Mystery Modifier", StringComparison.Ordinal));

        var modifier = Assert.Single(result.Modifiers);
        Assert.Equal("+7 to maximum Life", modifier.Text);
        Assert.Equal(["+7 to maximum Life"], modifier.ValueLines);
        Assert.Equal("{ Mystery Modifier \"Bent\" (Tier: nope) — Odd Metadata }", modifier.RawMetadataLine);
        Assert.Equal(ParsedModifierKind.Unknown, modifier.Kind);
        Assert.Equal("Bent", modifier.Name);
        Assert.Null(modifier.Tier);
        Assert.Null(modifier.Rank);
        Assert.False(modifier.IsCrafted);
        Assert.False(modifier.IsFractured);
        Assert.False(modifier.IsVeiled);
        Assert.Equal("Odd Metadata", modifier.CategoryText);
    }

    [Fact]
    public void Parse_StandaloneVeiledSuffix_ClassifiesAsVeiledSuffixModifier()
    {
        var rawText = ReadSample("veiled-suffix-amulet.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Empty(result.PrefixModifiers);
        Assert.Empty(result.ImplicitModifiers);
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);

        var modifier = Assert.Single(result.SuffixModifiers);
        Assert.Equal("Veiled Suffix", modifier.Text);
        Assert.Equal(["Veiled Suffix"], modifier.ValueLines);
        Assert.Equal(ParsedModifierKind.Suffix, modifier.Kind);
        Assert.True(modifier.IsVeiled);
        Assert.Null(modifier.Name);
        Assert.Null(modifier.RawMetadataLine);
    }

    [Fact]
    public void Parse_AdvancedVeiledSuffix_ConsumesMetadataWithoutDuplication()
    {
        const string rawText = """
Item Class: Amulets
Rarity: Rare
Viper Beads
Onyx Amulet
--------
Item Level: 84
--------
{ Suffix Modifier "of the Veil" }
Veiled Suffix
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Empty(result.PrefixModifiers);
        Assert.Empty(result.ImplicitModifiers);
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);
        Assert.Empty(result.UnclassifiedLines);

        var modifier = Assert.Single(result.SuffixModifiers);
        Assert.Equal("Veiled Suffix", modifier.Text);
        Assert.Equal(["Veiled Suffix"], modifier.ValueLines);
        Assert.Equal(ParsedModifierKind.Suffix, modifier.Kind);
        Assert.Equal("of the Veil", modifier.Name);
        Assert.Equal("{ Suffix Modifier \"of the Veil\" }", modifier.RawMetadataLine);
        Assert.True(modifier.IsVeiled);
    }

    [Fact]
    public void Parse_AdvancedVeiledPrefix_ConsumesMetadataWithoutDuplication()
    {
        const string rawText = """
Item Class: Amulets
Rarity: Rare
Viper Beads
Onyx Amulet
--------
Item Level: 84
--------
{ Prefix Modifier "Chosen" }
Veiled Prefix
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Empty(result.SuffixModifiers);
        Assert.Empty(result.ImplicitModifiers);
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);
        Assert.Empty(result.UnclassifiedLines);

        var modifier = Assert.Single(result.PrefixModifiers);
        Assert.Equal("Veiled Prefix", modifier.Text);
        Assert.Equal(["Veiled Prefix"], modifier.ValueLines);
        Assert.Equal(ParsedModifierKind.Prefix, modifier.Kind);
        Assert.Equal("Chosen", modifier.Name);
        Assert.Equal("{ Prefix Modifier \"Chosen\" }", modifier.RawMetadataLine);
        Assert.True(modifier.IsVeiled);
        Assert.True(modifier.IsUnrevealedVeiledPlaceholder);
        Assert.False(modifier.IsNamedUnveiled);
        Assert.Equal(ParsedVeiledModifierState.UnrevealedPlaceholder, modifier.VeiledState);
    }

    [Fact]
    public void Parse_Zf2080KrakenTorc_RetainsAnointAndVeiledSuffixWithoutGuessingIdentity()
    {
        const string rawText = """
Item Class: Amulets
Rarity: Rare
Kraken Torc
Coral Amulet
--------
Item Level: 85
--------
Allocates Blast Waves (enchant)
--------
{ Implicit Modifier — Life }
Regenerate 4(2-4) Life per second
--------
{ Suffix Modifier "of the Veil" }
Veiled Suffix
""";

        var result = _parser.Parse(rawText);

        var anoint = Assert.Single(result.Enchantments);
        Assert.True(anoint.IsAnoint);
        Assert.Equal("Allocates Blast Waves (enchant)", anoint.Text);
        var veiled = Assert.Single(result.Modifiers, modifier => modifier.IsVeiled);
        Assert.Equal(ParsedModifierKind.Suffix, veiled.Kind);
        Assert.Equal("Veiled Suffix", veiled.Text);
        Assert.Equal("of the Veil", veiled.Name);
        Assert.False(veiled.IsFractured);
        Assert.True(veiled.IsUnrevealedVeiledPlaceholder);
        Assert.False(veiled.IsNamedUnveiled);
    }

    [Fact]
    public void Parse_Zf2049PainRoad_RetainsEldritchAndFracturedProvenanceSeparately()
    {
        const string rawText = """
Item Class: Boots
Rarity: Rare
Pain Road
Crusader Boots
--------
Item Level: 86
--------
{ Searing Exarch Implicit Modifier (Lesser) — Elemental, Lightning, Ailment }
35(33-35)% chance to Avoid being Shocked
{ Eater of Worlds Implicit Modifier (Lesser) — Elemental, Fire, Cold, Lightning, Ailment }
17(15-17)% chance to Avoid Elemental Ailments
--------
{ Fractured Prefix Modifier "Robust" (Tier: 4) — Life }
+84(70-84) to maximum Life
Searing Exarch Item
Eater of Worlds Item
--------
Fractured Item
""";

        var result = _parser.Parse(rawText);

        Assert.Contains("Fractured Item", result.ItemStates);
        Assert.Equal(["Searing Exarch Item", "Eater of Worlds Item"], result.EldritchInfluences);
        var fractured = Assert.Single(result.Modifiers, modifier => modifier.IsFractured);
        Assert.Equal(ParsedModifierKind.Prefix, fractured.Kind);
        Assert.Equal("Robust", fractured.Name);
        Assert.False(fractured.IsVeiled);
        Assert.Equal(2, result.Modifiers.Count(modifier =>
            modifier.Kind == ParsedModifierKind.Implicit));
    }

    [Fact]
    public void Parse_OrbOfScouring_TreatsSingleCurrencyHeaderAsBaseType()
    {
        const string rawText = """
Item Class: Stackable Currency
Rarity: Currency
Orb of Scouring
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("Stackable Currency", result.ItemClass);
        Assert.Equal("Currency", result.Rarity);
        Assert.Null(result.Name);
        Assert.Equal("Orb of Scouring", result.BaseType);
        Assert.Equal("Orb of Scouring", result.DisplayName);
        Assert.Empty(result.DescriptionLines);
        Assert.DoesNotContain("Currency", result.UnclassifiedLines);
        Assert.DoesNotContain("Orb of Scouring", result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_CurrencyInstructions_ClassifiesDescriptionLines()
    {
        const string rawText = """
Item Class: Stackable Currency
Rarity: Currency
Orb of Scouring
--------
Stack Size: 1/40
--------
Removes all modifiers from an item
Right click this item then left click on a magic or rare item to apply it.
Shift click to unstack.
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal(
            [
                "Removes all modifiers from an item",
                "Right click this item then left click on a magic or rare item to apply it.",
                "Shift click to unstack.",
            ],
            result.DescriptionLines);
        Assert.Empty(result.Modifiers);
        Assert.Empty(result.ModifierLines);
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);
        Assert.Empty(result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_Incubator_ClassifiesEffectAndUsageTextAsDescriptionLines()
    {
        const string rawText = """
Item Class: Incubators
Rarity: Currency
Kalguuran Incubator
--------
Adds an incubated Expedition item to an equippable item
Item drops after killing 6 552 monsters
Right click this item then left click an item to apply it. The Incubated item drops after killing a specific number of monsters.
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("Incubators", result.ItemClass);
        Assert.Equal("Currency", result.Rarity);
        Assert.Null(result.Name);
        Assert.Equal("Kalguuran Incubator", result.BaseType);
        Assert.Equal("Kalguuran Incubator", result.DisplayName);
        Assert.Equal(
            [
                "Adds an incubated Expedition item to an equippable item",
                "Item drops after killing 6 552 monsters",
                "Right click this item then left click an item to apply it. The Incubated item drops after killing a specific number of monsters.",
            ],
            result.DescriptionLines);
        Assert.Empty(result.Modifiers);
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);
        Assert.Empty(result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_Wombgift_ClassifiesMechanicTextAndRequirement()
    {
        const string rawText = """
Item Class: Stackable Currency
Rarity: Currency
Wombgift
--------
Requires 1239 Hiveblood
--------
Can grow into an Armour or Jewellery item on the Genesis Tree
Place this item into an allocated equipment item womb on the Genesis Tree. Right click to retrieve from the Genesis Tree.
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Null(result.Name);
        Assert.Equal("Wombgift", result.BaseType);
        Assert.Contains("Requires 1239 Hiveblood", result.PropertyLines);
        Assert.Equal(
            [
                "Can grow into an Armour or Jewellery item on the Genesis Tree",
                "Place this item into an allocated equipment item womb on the Genesis Tree. Right click to retrieve from the Genesis Tree.",
            ],
            result.DescriptionLines);
        Assert.Empty(result.Modifiers);
        Assert.Empty(result.ModifierLines);
        Assert.DoesNotContain("Requires 1239 Hiveblood", result.DescriptionLines);
        Assert.DoesNotContain("Requires 1239 Hiveblood", result.UnclassifiedLines);
        Assert.DoesNotContain("Can grow into an Armour or Jewellery item on the Genesis Tree", result.UnclassifiedLines);
        Assert.DoesNotContain(
            "Place this item into an allocated equipment item womb on the Genesis Tree. Right click to retrieve from the Genesis Tree.",
            result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_UtilityFlask_ClassifiesFlaskPropertiesAndPreservesAffixes()
    {
        const string rawText = """
Item Class: Utility Flasks
Rarity: Magic
Chemist's Granite Flask of the Armadillo
--------
Item Level: 84
--------
Lasts 6.90 Seconds
Consumes 30 of 60 Charges on use
Currently has 60 Charges
+1500 to Armour
+5% to maximum Fire Resistance
+40% to Fire Resistance
Used when Charges reach full (enchant)
--------
{ Prefix Modifier "Chemist's" (Tier: 3) - Flask }
25(23-25)% reduced Charges per use
{ Suffix Modifier "of the Armadillo" (Tier: 2) - Defences }
60(56-60)% increased Armour during Effect
--------
Right click to drink. Can only hold charges while in belt. Refills as you kill monsters.
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("Chemist's Granite Flask of the Armadillo", result.Name);
        Assert.Null(result.BaseType);
        Assert.Null(result.ItemTypeDescriptor);
        Assert.Contains("Lasts 6.90 Seconds", result.PropertyLines);
        Assert.Contains("Consumes 30 of 60 Charges on use", result.PropertyLines);
        Assert.Contains("Currently has 60 Charges", result.PropertyLines);
        Assert.Contains("+1500 to Armour", result.PropertyLines);
        Assert.Contains("+5% to maximum Fire Resistance", result.PropertyLines);
        Assert.Contains("+40% to Fire Resistance", result.PropertyLines);
        Assert.Equal(["Right click to drink. Can only hold charges while in belt. Refills as you kill monsters."], result.DescriptionLines);

        var enchantment = Assert.Single(result.Enchantments);
        Assert.Equal("Used when Charges reach full (enchant)", enchantment.Text);
        Assert.False(enchantment.IsAnoint);

        var prefix = Assert.Single(result.PrefixModifiers);
        Assert.Equal("Chemist's", prefix.Name);
        Assert.Equal("25(23-25)% reduced Charges per use", prefix.Text);

        var suffix = Assert.Single(result.SuffixModifiers);
        Assert.Equal("of the Armadillo", suffix.Name);
        Assert.Equal("60(56-60)% increased Armour during Effect", suffix.Text);

        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Lasts 6.90 Seconds"));
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Consumes 30 of 60 Charges on use"));
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Currently has 60 Charges"));
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("+1500 to Armour"));
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("+5% to maximum Fire Resistance"));
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("+40% to Fire Resistance"));
        Assert.Empty(result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_LifeFlask_ClassifiesPreItemLevelFlaskStatsAsProperties()
    {
        const string rawText = """
Item Class: Life Flasks
Rarity: Magic
Seething Divine Life Flask of Sealing
--------
Recovers 1152 Life over 1.50 Seconds
Consumes 15 of 45 Charges on use
Currently has 45 Charges
--------
Item Level: 84
--------
{ Prefix Modifier "Seething" (Tier: 1) - Flask }
66% reduced Amount Recovered
Instant Recovery
{ Suffix Modifier "of Sealing" (Tier: 1) - Bleeding, Flask }
Grants Immunity to Bleeding for 17 seconds if used while Bleeding
Grants Immunity to Corrupted Blood for 17 seconds if used while affected by Corrupted Blood
--------
Right click to drink. Can only hold charges while in belt. Refills as you kill monsters.
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("Life Flasks", result.ItemClass);
        Assert.Equal("Seething Divine Life Flask of Sealing", result.Name);
        Assert.Null(result.BaseType);
        Assert.Null(result.ItemTypeDescriptor);
        Assert.Contains("Recovers 1152 Life over 1.50 Seconds", result.PropertyLines);
        Assert.Contains("Consumes 15 of 45 Charges on use", result.PropertyLines);
        Assert.Contains("Currently has 45 Charges", result.PropertyLines);
        Assert.Equal(["Right click to drink. Can only hold charges while in belt. Refills as you kill monsters."], result.DescriptionLines);

        var prefix = Assert.Single(result.PrefixModifiers);
        Assert.Equal("Seething", prefix.Name);
        Assert.Equal(
            [
                "66% reduced Amount Recovered",
                "Instant Recovery",
            ],
            prefix.ValueLines);

        var suffix = Assert.Single(result.SuffixModifiers);
        Assert.Equal("of Sealing", suffix.Name);
        Assert.Equal(
            [
                "Grants Immunity to Bleeding for 17 seconds if used while Bleeding",
                "Grants Immunity to Corrupted Blood for 17 seconds if used while affected by Corrupted Blood",
            ],
            suffix.ValueLines);

        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Recovers 1152 Life over 1.50 Seconds"));
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Consumes 15 of 45 Charges on use"));
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Currently has 45 Charges"));
        Assert.Empty(result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_CoinOfSkill_ClassifiesDescriptionAroundFlavourText()
    {
        const string rawText = """
Item Class: Stackable Currency
Rarity: Currency
Coin of Skill
--------
Corrupts a level 20 Skill Gem, Imbuing it with a random Dexterity Support effect
--------
"Strong and slow, clever and weak. I fear neither.
I wish for the skill to overcome them both."
--------
Right click this item then left click a level 20 Skill Gem to corrupt it. The added Support effect will always be valid for the Skill Gem. Corrupted items cannot be modified again.
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Null(result.Name);
        Assert.Equal("Coin of Skill", result.BaseType);
        Assert.Equal(
            [
                "Corrupts a level 20 Skill Gem, Imbuing it with a random Dexterity Support effect",
                "Right click this item then left click a level 20 Skill Gem to corrupt it. The added Support effect will always be valid for the Skill Gem. Corrupted items cannot be modified again.",
            ],
            result.DescriptionLines);
        Assert.Equal(
            [
                "\"Strong and slow, clever and weak. I fear neither.",
                "I wish for the skill to overcome them both.\"",
            ],
            result.FlavourTextLines);
        Assert.Empty(result.Modifiers);
        Assert.Empty(result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_JewelSocketInstructions_ClassifiesDescriptionLines()
    {
        const string rawText = """
Item Class: Jewels
Rarity: Rare
Foe Spark
Crimson Jewel
--------
Item Level: 84
--------
Place into an allocated Jewel Socket on the Passive Skill Tree. Right click to remove from the Socket.
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal(
            ["Place into an allocated Jewel Socket on the Passive Skill Tree. Right click to remove from the Socket."],
            result.DescriptionLines);
        Assert.Empty(result.Modifiers);
        Assert.Empty(result.ModifierLines);
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);
        Assert.Empty(result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_UniqueJewelWithFlavourAndSocketInstruction_DoesNotMixDescriptionIntoFlavour()
    {
        const string rawText = """
Item Class: Jewels
Rarity: Unique
Voices
Large Cluster Jewel
--------
Item Level: 84
--------
Only a madman would ignore a god's instructions.
Place into an allocated Large Jewel Socket on the Passive Skill Tree. Right click to remove from the Socket.
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal(
            ["Only a madman would ignore a god's instructions."],
            result.FlavourTextLines);
        Assert.Equal(
            ["Place into an allocated Large Jewel Socket on the Passive Skill Tree. Right click to remove from the Socket."],
            result.DescriptionLines);
        Assert.Empty(result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_BlightAnointment_ClassifiesAllocatesEnchantAsAnoint()
    {
        const string rawText = """
Item Class: Amulets
Rarity: Rare
Rune Pendant
Amber Amulet
--------
Item Level: 84
--------
Allocates Wandslinger (enchant)
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);

        var enchantment = Assert.Single(result.Enchantments);
        Assert.Equal("Allocates Wandslinger (enchant)", enchantment.Text);
        Assert.True(enchantment.IsAnoint);
        Assert.Empty(result.ImplicitModifiers);
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);
        Assert.DoesNotContain("Allocates Wandslinger (enchant)", result.ModifierLines);
        Assert.DoesNotContain("Allocates Wandslinger (enchant)", result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_AdvancedUniqueItem_ClassifiesUniqueModifiersAndFlavourText()
    {
        const string rawText = """
Item Class: Bows
Rarity: Unique
The Tempest
Long Bow
--------
Item Level: 84
--------
{ Unique Modifier "Storm Song" - Elemental, Lightning }
Adds 1 to 85 Lightning Damage
25% increased Attack Speed
--------
Born from the marriage of ice and sky,
the aurora evokes both awe and power.
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);

        var uniqueModifier = Assert.Single(result.UniqueModifiers);
        Assert.Equal("Storm Song", uniqueModifier.Name);
        Assert.Equal(ParsedModifierKind.Unique, uniqueModifier.Kind);
        Assert.Equal(ParsedUniqueModifierOrigin.Ordinary, uniqueModifier.UniqueOrigin);
        Assert.Equal("{ Unique Modifier \"Storm Song\" - Elemental, Lightning }", uniqueModifier.RawMetadataLine);
        Assert.Equal("Elemental, Lightning", uniqueModifier.CategoryText);
        Assert.Equal(
            [
                "Adds 1 to 85 Lightning Damage",
                "25% increased Attack Speed",
            ],
            uniqueModifier.ValueLines);
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);
        Assert.Equal(
            [
                "Born from the marriage of ice and sky,",
                "the aurora evokes both awe and power.",
            ],
            result.FlavourTextLines);
        Assert.Empty(result.DescriptionLines);
        Assert.Empty(result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_FoulbornMidnightBargain_PreservesOrdinaryAndFoulbornUniqueOriginsAndGrouping()
    {
        const string rawText = """
Item Class: Wands
Rarity: Unique
Foulborn Midnight Bargain
Calling Wand
--------
Item Level: 83
--------
{ Unique Modifier — Minion }
+1 to maximum number of Raised Zombies
+1 to maximum number of Spectres
+1 to maximum number of Skeletons
{ Foulborn Unique Modifier — Life, Defences, Energy Shield, Minion }
Lose 0.5% Life and Energy Shield per Second per Minion
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(2, result.UniqueModifiers.Count);
        Assert.Equal(ParsedUniqueModifierOrigin.Ordinary, result.UniqueModifiers[0].UniqueOrigin);
        Assert.Equal(
            [
                "+1 to maximum number of Raised Zombies",
                "+1 to maximum number of Spectres",
                "+1 to maximum number of Skeletons",
            ],
            result.UniqueModifiers[0].ValueLines);
        Assert.Equal(ParsedUniqueModifierOrigin.Foulborn, result.UniqueModifiers[1].UniqueOrigin);
        Assert.Equal(
            "Lose 0.5% Life and Energy Shield per Second per Minion",
            Assert.Single(result.UniqueModifiers[1].ValueLines));
    }

    [Fact]
    public void Parse_ListingNote_StoresListingNoteSeparately()
    {
        const string rawText = """
Item Class: Rings
Rarity: Rare
Storm Spiral
Two-Stone Ring
--------
Item Level: 82
--------
Note: ~b/o 1 divine
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("~b/o 1 divine", result.ListingNote);
        Assert.Contains("Note: ~b/o 1 divine", result.NoteLines);
        Assert.DoesNotContain("Note: ~b/o 1 divine", result.ModifierLines);
        Assert.Empty(result.DescriptionLines);
        Assert.DoesNotContain(result.Modifiers, modifier => modifier.ValueLines.Contains("Note: ~b/o 1 divine"));
        Assert.Empty(result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_ListingNote_StoresValueWithoutNotePrefixForPresentation()
    {
        const string rawText = """
Item Class: Rings
Rarity: Rare
Storm Spiral
Two-Stone Ring
--------
Item Level: 82
--------
Note: ~b/o 1 chaos
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("~b/o 1 chaos", result.ListingNote);
        Assert.False(result.ListingNote?.StartsWith("Note:", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Note: Note:", result.NoteLines);
        Assert.Empty(result.DescriptionLines);
        Assert.Empty(result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_CogworkRingStyleItem_PreservesMoreThanThreeSuffixes()
    {
        const string rawText = """
Item Class: Rings
Rarity: Rare
Havoc Loop
Cogwork Ring
--------
Item Level: 84
--------
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+55(50-59) to maximum Life
{ Prefix Modifier "Glinting" (Tier: 2) - Defences }
+120(101-120) to Evasion Rating
{ Suffix Modifier "of the Rainbow" (Tier: 4) - Elemental, Resistance }
+12(11-13)% to all Elemental Resistances
{ Suffix Modifier "of the Order" (Tier: 4) - Caster }
+17% to Global Critical Strike Multiplier
{ Suffix Modifier "of the Fox" (Tier: 3) - Attribute }
+30(28-32) to Dexterity
{ Suffix Modifier "of the Lightning" (Tier: 2) - Elemental, Lightning, Resistance }
+42(36-42)% to Lightning Resistance
""";

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal(2, result.PrefixModifiers.Count);
        Assert.Equal(4, result.SuffixModifiers.Count);
        Assert.Equal(6, result.Modifiers.Count);
        Assert.Contains(result.PrefixModifiers, modifier => modifier.Name == "Hale");
        Assert.Contains(result.PrefixModifiers, modifier => modifier.Name == "Glinting");
        Assert.Contains(result.SuffixModifiers, modifier => modifier.Name == "of the Rainbow");
        Assert.Contains(result.SuffixModifiers, modifier => modifier.Name == "of the Order");
        Assert.Contains(result.SuffixModifiers, modifier => modifier.Name == "of the Fox");
        Assert.Contains(result.SuffixModifiers, modifier => modifier.Name == "of the Lightning");
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);
        Assert.Empty(result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_NormalRaritySingleHeaderLine_TreatsHeaderAsBaseTypeAndUsesDisplayNameFallback()
    {
        var rawText = ReadSample("normal-full-wyrmscale.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal("Body Armours", result.ItemClass);
        Assert.Equal("Normal", result.Rarity);
        Assert.Null(result.Name);
        Assert.Equal("Full Wyrmscale", result.BaseType);
        Assert.Equal("Full Wyrmscale", result.DisplayName);
        Assert.DoesNotContain("Full Wyrmscale", result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_IncompleteSample_DoesNotThrowAndKeepsUnclassifiedLines()
    {
        var rawText = ReadSample("incomplete-item.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal(ParsedItemInputFormat.Normal, result.InputFormat);
        Assert.Equal("Rare", result.Rarity);
        Assert.Equal("Half Remembered Item", result.Name);
        Assert.Null(result.BaseType);
        Assert.Null(result.ItemLevel);
        Assert.Empty(result.ItemStates);
        Assert.Empty(result.NoteLines);
        Assert.Contains("Unexpected loose line", result.UnclassifiedLines);
    }

    [Fact]
    public void Parse_WhitespaceOnlySample_PreservesRawTextAndReturnsEmptyResult()
    {
        var rawText = ReadSample("whitespace-only.txt");

        var result = _parser.Parse(rawText);

        Assert.Equal(rawText, result.RawText);
        Assert.Equal(ParsedItemInputFormat.Unknown, result.InputFormat);
        Assert.Null(result.ItemClass);
        Assert.Null(result.Rarity);
        Assert.Null(result.Name);
        Assert.Null(result.BaseType);
        Assert.Null(result.ItemLevel);
        Assert.Empty(result.ItemStates);
        Assert.Empty(result.NoteLines);
        Assert.Null(result.ListingNote);
        Assert.Empty(result.TraditionalInfluences);
        Assert.Empty(result.EldritchInfluences);
        Assert.False(result.IsCorrupted);
        Assert.Empty(result.PropertyLines);
        Assert.Empty(result.Modifiers);
        Assert.Empty(result.ImplicitModifiers);
        Assert.Empty(result.PrefixModifiers);
        Assert.Empty(result.SuffixModifiers);
        Assert.Empty(result.UniqueModifiers);
        Assert.Empty(result.ExplicitModifiersWithUnknownKind);
        Assert.Empty(result.ModifierLines);
        Assert.Empty(result.FlavourTextLines);
        Assert.Empty(result.Enchantments);
        Assert.Empty(result.DescriptionLines);
        Assert.Empty(result.UnclassifiedLines);
    }

    private static string ReadSample(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Items", fileName));
    }
}
