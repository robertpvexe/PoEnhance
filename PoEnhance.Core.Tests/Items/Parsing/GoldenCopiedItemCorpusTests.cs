using System.Globalization;
using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.Core.Tests.Items.Parsing;

public sealed class GoldenCopiedItemCorpusTests
{
    private static readonly string[] ForbiddenModifierRows =
    [
        "(The Damage Types are Physical, Fire, Cold, Lightning, and Chaos)",
        "Our flesh longs to move as one.",
        "Place into an allocated Jewel Socket on the Passive Skill Tree. Right click to remove from the Socket.",
        "Unidentified",
        "Searing Exarch Item",
        "Eater of Worlds Item",
    ];

    private static readonly IReadOnlyDictionary<int, string[]> ExpectedPropertyLines = new Dictionary<int, string[]>
    {
        [0] =
        [
            "Evasion Rating: 828",
            "Energy Shield: 166",
            "Requirements:",
            "Level: 84",
            "Dex: 173",
            "Int: 173 (unmet)",
            "Sockets: G-G-B-B G",
        ],
        [1] =
        [
            "Physical Damage: 38-114",
            "Elemental Damage: 26-57 (augmented)",
            "Critical Strike Chance: 5.00%",
            "Attacks per Second: 1.20",
            "Weapon Range: 1.1 metres",
            "Requirements:",
            "Level: 61",
            "Str: 167",
            "Dex: 57",
            "Sockets: R",
        ],
        [2] =
        [
            "Physical Damage: 56-117",
            "Elemental Damage: 70-139 (augmented), 46-81 (augmented), 9-155 (augmented)",
            "Critical Strike Chance: 6.00%",
            "Attacks per Second: 1.30",
            "Requirements:",
            "Level: 65",
            "Dex: 212 (unmet)",
            "Sockets: W-B G-W-R-G",
        ],
        [3] =
        [
            "Physical Damage: 61-160 (augmented)",
            "Critical Strike Chance: 5.00%",
            "Attacks per Second: 1.53 (augmented)",
            "Weapon Range: 1.1 metres",
            "Requirements:",
            "Level: 61",
            "Str: 167",
            "Dex: 57",
            "Sockets: R-R-G",
        ],
        [4] =
        [
            "Physical Damage: 21-39",
            "Elemental Damage: 52-89 (augmented)",
            "Critical Strike Chance: 8.50%",
            "Attacks per Second: 1.60",
            "Requirements:",
            "Level: 41",
            "Int: 131",
            "Sockets: B-B-B",
        ],
        [5] = [],
        [6] = [],
        [7] =
        [
            "Requirements:",
            "Level: 59",
        ],
        [8] =
        [
            "Armour: 1705 (augmented)",
            "Requirements:",
            "Level: 66",
            "Str: 177",
            "Sockets: R-R-R-R R",
        ],
        [9] =
        [
            "Quality: +20% (augmented)",
            "Energy Shield: 57 (augmented)",
            "Requirements:",
            "Level: 67",
            "Int: 94",
            "Sockets: R-G G-B",
        ],
        [10] =
        [
            "Requirements:",
            "Level: 33",
            "Sockets: A",
        ],
        [11] =
        [
            "Quality: +20% (augmented)",
            "Armour: 2330 (augmented)",
            "Evasion Rating: 2349 (augmented)",
            "Requirements:",
            "Level: 78",
            "Str: 151",
            "Dex: 151",
            "Int: 68",
            "Sockets: R-R-G-R-B-G",
        ],
        [12] =
        [
            "Requirements:",
            "Level: 65",
        ],
        [13] =
        [
            "Chance to Block: 24%",
            "Evasion Rating: 429 (augmented)",
            "Energy Shield: 124 (augmented)",
            "Requirements:",
            "Level: 70",
            "Dex: 85",
            "Int: 85",
            "Sockets: B-B-G",
        ],
        [14] = [],
    };

    private readonly ItemTextParser parser = new();

    public static IEnumerable<object[]> ExpectedFixtures()
    {
        return ExpectedFixture.All.Select(fixture => new object[] { fixture });
    }

    [Fact]
    public void LoadItems_RealCorpusSplitsOnlyAtBlankLineBeforeItemClass()
    {
        var items = CopiedItemCorpus.LoadItems();

        Assert.Equal(15, items.Count);
        Assert.All(items, item => Assert.StartsWith("Item Class:", item, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ExpectedFixtures))]
    public void Parse_RealAdvancedCopiedItemCorpus_MatchesHandAuthoredExpectations(ExpectedFixture expected)
    {
        var itemText = CopiedItemCorpus.LoadItems()[expected.Index];

        var result = parser.Parse(itemText);

        Assert.Equal(expected.InputFormat, result.InputFormat);
        Assert.Equal(expected.ItemClass, result.ItemClass);
        Assert.Equal(expected.Rarity, result.Rarity);
        Assert.Equal(expected.Name, result.Name);
        Assert.Equal(expected.BaseType, result.BaseType);
        Assert.Equal(expected.Name ?? expected.BaseType, result.DisplayName);
        Assert.Equal(expected.IsUnidentified, result.ItemStates.Contains("Unidentified"));
        Assert.Equal(expected.ItemLevel, result.ItemLevel);
        Assert.Equal(expected.Modifiers.Count, result.Modifiers.Count);
        Assert.Equal(expected.LogicalEffectCount, result.Modifiers.Sum(modifier => modifier.Effects.Count));
        Assert.Equal(expected.DescriptionLines, result.DescriptionLines);
        Assert.Equal(expected.FlavourTextLines, result.FlavourTextLines);
        Assert.Equal(expected.EldritchInfluences, result.EldritchInfluences);
        Assert.Equal(ExpectedPropertyLines[expected.Index], result.PropertyLines);

        for (var index = 0; index < expected.Modifiers.Count; index++)
        {
            AssertModifier(expected.Modifiers[index], result.Modifiers[index], index);
        }

        AssertNoSemanticLeakage(result);
    }

    [Fact]
    public void Parse_GoldenCorpus_RarityAndNameComeOnlyFromHeaderNotModifierNames()
    {
        var fixtures = ParseAll();
        var magicAxe = fixtures[1];
        var rareAxe = fixtures[3];

        Assert.Equal("Magic", magicAxe.Rarity);
        Assert.Equal("Flaming Reaver Axe of the Marksman", magicAxe.Name);
        Assert.Null(magicAxe.BaseType);
        Assert.Equal("Rare", rareAxe.Rarity);
        Assert.Equal("Morbid Bite", rareAxe.Name);
        Assert.Equal("Reaver Axe", rareAxe.BaseType);
        Assert.DoesNotContain("of Celebration", rareAxe.DisplayName);
    }

    [Fact]
    public void Parse_GoldenCorpus_HybridSourceModifiersRetainAllEffectsUnderOneSourceModifier()
    {
        var fixtures = ParseAll();

        AssertHybrid(fixtures[8], "Crocodile's", ["+139(97-144) to Armour", "+37(34-38) to maximum Life"]);
        AssertHybrid(fixtures[9], "Boggart's", ["25(21-26)% increased Energy Shield", "10(10-11)% increased Stun and Block Recovery"]);
        AssertHybrid(fixtures[11], "Adaptable", ["+226(221-300) to Armour", "+234(221-300) to Evasion Rating"]);
        AssertHybrid(fixtures[11], "Elephant's", ["37(33-38)% increased Armour and Evasion", "15(14-15)% increased Stun and Block Recovery"]);
        AssertHybrid(fixtures[13], "Prior's", ["+15(11-15) to maximum Energy Shield", "+27(24-28) to maximum Life"]);
        AssertHybrid(fixtures[13], "Wasp's", ["28(27-32)% increased Evasion and Energy Shield", "13(12-13)% increased Stun and Block Recovery"]);
        AssertHybrid(fixtures[13], "Cherub's", ["+59(49-85) to Evasion Rating", "+26(23-28) to maximum Energy Shield"]);
    }

    [Fact]
    public void Parse_GoldenCorpus_RemindersBelongToEffectsAndNeverBecomeEffects()
    {
        var fixtures = ParseAll();

        AssertReminder(
            fixtures[4],
            "of Zapping",
            "28(25-30)% chance to Shock",
            "(Shock increases Damage taken by up to 50%, depending on the amount of Lightning Damage in the hit, for 2 seconds)");
        AssertReminder(
            fixtures[7],
            null,
            "Cannot roll Modifiers of Non-Physical Damage Types",
            "(The Damage Types are Physical, Fire, Cold, Lightning, and Chaos)");
        AssertReminder(
            fixtures[10],
            null,
            "Has 1 Abyssal Socket",
            "(Only Abyss Jewels can be Socketed in Abyssal Sockets)");
        AssertReminder(
            fixtures[10],
            "of the Boxer",
            "10(10-11)% reduced Enemy Stun Threshold",
            "(The Stun Threshold determines how much Damage can Stun something)");
        AssertReminder(
            fixtures[13],
            null,
            "+5% chance to Suppress Spell Damage",
            "(40% of Damage from Suppressed Hits and Ailments they inflict is prevented)");
        AssertReminder(
            fixtures[14],
            null,
            "+2% to maximum Cold Resistance",
            "(Maximum Resistances cannot be raised above 90%)");
        AssertReminder(
            fixtures[14],
            null,
            "Cannot roll Modifiers of Non-Cold Damage Types",
            "(The Damage Types are Physical, Fire, Cold, Lightning, and Chaos)");

        Assert.All(fixtures.SelectMany(item => item.Modifiers), modifier =>
            Assert.DoesNotContain(modifier.ValueLines, IsReminderText));
    }

    [Fact]
    public void Parse_GoldenCorpus_UnscalableValueIsEffectMetadataNotText()
    {
        var fixtures = ParseAll();
        var unscalableEffects = fixtures
            .SelectMany(item => item.Modifiers)
            .SelectMany(modifier => modifier.Effects)
            .Where(effect => effect.HasUnscalableValue)
            .Select(effect => effect.Text)
            .ToArray();

        Assert.Equal(
            [
                "Cannot roll Caster Modifiers",
                "Cannot roll Modifiers of Non-Physical Damage Types",
                "Has 1 Abyssal Socket",
                "Cannot roll Modifiers of Non-Cold Damage Types",
            ],
            unscalableEffects);
        Assert.DoesNotContain(ParseAll().SelectMany(item => item.ModifierLines), line =>
            line.Contains("Unscalable Value", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_GoldenCorpus_IsCultureInvariantForCopiedNumericText()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            var polish = ParseAll().Select(CreateComparableSnapshot).ToArray();

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            var invariant = ParseAll().Select(CreateComparableSnapshot).ToArray();

            Assert.Equal(invariant, polish);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Parse_GoldenCorpus_LfAndCrLfFormsProduceEquivalentStructures()
    {
        foreach (var item in CopiedItemCorpus.LoadItems())
        {
            var lf = item.Replace("\r\n", "\n", StringComparison.Ordinal);
            var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);

            Assert.Equal(
                CreateComparableSnapshot(parser.Parse(lf)),
                CreateComparableSnapshot(parser.Parse(crlf)));
        }
    }

    [Fact]
    public void Parse_GoldenCorpus_UsesOnlyRawTextWithoutGameDataOrTradeCatalogInputs()
    {
        var items = CopiedItemCorpus.LoadItems();

        var parsed = items.Select(item => parser.Parse(item)).ToArray();

        Assert.Equal(15, parsed.Length);
        Assert.Equal(ParsedItemInputFormat.Normal, parsed[0].InputFormat);
        Assert.All(parsed.Skip(1), item => Assert.Equal(ParsedItemInputFormat.Advanced, item.InputFormat));
    }

    private ParsedItem[] ParseAll()
    {
        return CopiedItemCorpus.LoadItems()
            .Select(item => parser.Parse(item))
            .ToArray();
    }

    private static void AssertModifier(ExpectedModifier expected, ParsedModifier actual, int sourceModifierIndex)
    {
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.IsCrafted, actual.IsCrafted);
        Assert.Equal(expected.Effects.Select(effect => effect.Text), actual.ValueLines);
        Assert.Equal(expected.Effects.Count, actual.Effects.Count);

        for (var index = 0; index < expected.Effects.Count; index++)
        {
            var expectedEffect = expected.Effects[index];
            var actualEffect = actual.Effects[index];
            Assert.Equal(expectedEffect.Text, actualEffect.Text);
            Assert.Equal(expectedEffect.ReminderLines, actualEffect.ReminderLines);
            Assert.Equal(expectedEffect.HasUnscalableValue, actualEffect.HasUnscalableValue);
        }

        Assert.Equal(
            expected.Effects.Select(effect => effect.Text),
            actual.ValueLines);
        Assert.Equal(
            expected.Effects.SelectMany(effect => effect.ReminderLines),
            actual.ReminderLines);
        Assert.Equal($"source modifier {sourceModifierIndex}", $"source modifier {sourceModifierIndex}");
    }

    private static void AssertNoSemanticLeakage(ParsedItem result)
    {
        var effectTexts = result.Modifiers.SelectMany(modifier => modifier.ValueLines).ToArray();
        foreach (var forbiddenRow in ForbiddenModifierRows)
        {
            Assert.DoesNotContain(forbiddenRow, effectTexts);
            Assert.DoesNotContain(forbiddenRow, result.ModifierLines);
        }

        Assert.DoesNotContain(effectTexts, line =>
            line.Contains("Unscalable Value", StringComparison.Ordinal) ||
            IsReminderText(line));
        Assert.DoesNotContain(result.Modifiers, modifier =>
            modifier.ValueLines.Any(line => result.PropertyLines.Contains(line)));
    }

    private static void AssertHybrid(ParsedItem item, string modifierName, IReadOnlyList<string> expectedEffects)
    {
        var modifier = Assert.Single(item.Modifiers, modifier => modifier.Name == modifierName);
        Assert.Equal(expectedEffects, modifier.ValueLines);
        Assert.Equal(expectedEffects.Count, modifier.Effects.Count);
    }

    private static void AssertReminder(
        ParsedItem item,
        string? modifierName,
        string effectText,
        string reminderText)
    {
        var modifier = modifierName is null
            ? item.Modifiers.First(modifier => modifier.Effects.Any(effect => effect.Text == effectText))
            : Assert.Single(item.Modifiers, modifier => modifier.Name == modifierName);
        var effect = Assert.Single(modifier.Effects, effect => effect.Text == effectText);
        Assert.Equal([reminderText], effect.ReminderLines);
        Assert.DoesNotContain(reminderText, modifier.ValueLines);
    }

    private static bool IsReminderText(string line)
    {
        return line.Length >= 2 && line[0] == '(' && line[^1] == ')';
    }

    private static string CreateComparableSnapshot(ParsedItem item)
    {
        return string.Join(
            "\u001f",
            item.ItemClass,
            item.Rarity,
            item.Name,
            item.BaseType,
            string.Join("\u001e", item.ItemStates),
            string.Join("\u001e", item.PropertyLines),
            string.Join("\u001e", item.DescriptionLines),
            string.Join("\u001e", item.FlavourTextLines),
            string.Join(
                "\u001e",
                item.Modifiers.Select(modifier => string.Join(
                    "\u001d",
                    modifier.Kind,
                    modifier.Name,
                    modifier.IsCrafted,
                    string.Join(
                        "\u001c",
                        modifier.Effects.Select(effect => string.Join(
                            "\u001b",
                            effect.Text,
                            effect.HasUnscalableValue,
                            string.Join("\u001a", effect.ReminderLines))))))));
    }

    public sealed record ExpectedFixture(
        int Index,
        string ItemClass,
        string Rarity,
        string? Name,
        string? BaseType,
        int? ItemLevel,
        bool IsUnidentified,
        IReadOnlyList<ExpectedModifier> Modifiers,
        IReadOnlyList<string> DescriptionLines,
        IReadOnlyList<string> FlavourTextLines,
        IReadOnlyList<string> EldritchInfluences)
    {
        public ParsedItemInputFormat InputFormat => Modifiers.Count == 0
            ? ParsedItemInputFormat.Normal
            : ParsedItemInputFormat.Advanced;

        public int LogicalEffectCount => Modifiers.Sum(modifier => modifier.Effects.Count);

        public static IReadOnlyList<ExpectedFixture> All { get; } =
        [
            new(0, "Body Armours", "Normal", null, "Necrotic Armour", 86, false, [], [], [], []),
            new(1, "One Hand Axes", "Magic", "Flaming Reaver Axe of the Marksman", null, 83, false,
            [
                Modifier(ParsedModifierKind.Prefix, "Flaming", Effect("Adds 26(24-33) to 57(49-57) Fire Damage")),
                Modifier(ParsedModifierKind.Suffix, "of the Marksman", Effect("+388(326-455) to Accuracy Rating")),
            ], [], [], []),
            new(2, "Bows", "Rare", "Golem Fletch", "Ranger Bow", 85, false,
            [
                Modifier(ParsedModifierKind.Prefix, "Freezing", Effect("Adds 46(41-55) to 81(81-95) Cold Damage")),
                Modifier(ParsedModifierKind.Prefix, "Scorching", Effect("Adds 70(63-85) to 139(128-148) Fire Damage")),
                Modifier(ParsedModifierKind.Prefix, "Sparking", Effect("Adds 9(8-10) to 155(148-173) Lightning Damage")),
                Modifier(ParsedModifierKind.Suffix, "of the Wind", Effect("+53(51-55) to Dexterity")),
            ], [], [], []),
            new(3, "One Hand Axes", "Rare", "Morbid Bite", "Reaver Axe", 85, false,
            [
                Modifier(ParsedModifierKind.Prefix, "Flaring", Effect("Adds 23(22-29) to 46(45-52) Physical Damage")),
                Modifier(ParsedModifierKind.Suffix, "of Celebration", Effect("27(26-27)% increased Attack Speed")),
            ], [], [], []),
            new(4, "Wands", "Rare", "Wrath Cry", "Blasting Wand", 85, false,
            [
                Modifier(ParsedModifierKind.Implicit, null, Effect("Cannot roll Caster Modifiers", unscalable: true)),
                Modifier(ParsedModifierKind.Prefix, "Shocking", Effect("Adds 9(2-9) to 110(109-115) Lightning Damage to Spells")),
                Modifier(ParsedModifierKind.Prefix, "Glaciated", Effect("Adds 52(41-57) to 89(83-97) Cold Damage")),
                Modifier(
                    ParsedModifierKind.Suffix,
                    "of Zapping",
                    Effect(
                        "28(25-30)% chance to Shock",
                        "(Shock increases Damage taken by up to 50%, depending on the amount of Lightning Damage in the hit, for 2 seconds)")),
                Modifier(ParsedModifierKind.Suffix, "of Electricity", Effect("16(16-18)% increased Lightning Damage")),
                Modifier(ParsedModifierKind.Suffix, "of Victory", Effect("Gain 17(12-18) Life per Enemy Killed")),
            ], [], [], []),
            new(5, "Jewels", "Rare", "Sol Hope", "Viridian Jewel", 84, false,
            [
                Modifier(ParsedModifierKind.Prefix, "Thwarting", Effect("+2(2-3)% Chance to Block Spell Damage while holding a Shield")),
                Modifier(ParsedModifierKind.Suffix, "of Venom", Effect("17(16-20)% increased Damage with Poison")),
                Modifier(ParsedModifierKind.Suffix, "of Runes", Effect("Totems gain +10(6-10)% to all Elemental Resistances")),
            ], ["Place into an allocated Jewel Socket on the Passive Skill Tree. Right click to remove from the Socket."], [], []),
            new(6, "Jewels", "Magic", "Trapping Cobalt Jewel of Berserking", null, 84, false,
            [
                Modifier(ParsedModifierKind.Prefix, "Trapping", Effect("15(14-16)% increased Trap Damage")),
                Modifier(ParsedModifierKind.Suffix, "of Berserking", Effect("5(3-5)% increased Attack Speed")),
            ], ["Place into an allocated Jewel Socket on the Passive Skill Tree. Right click to remove from the Socket."], [], []),
            new(7, "Rings", "Rare", "Eagle Spiral", "Organic Ring", 85, false,
            [
                Modifier(
                    ParsedModifierKind.Implicit,
                    null,
                    Effect("3% additional Physical Damage Reduction"),
                    Effect(
                        "Cannot roll Modifiers of Non-Physical Damage Types",
                        "(The Damage Types are Physical, Fire, Cold, Lightning, and Chaos)",
                        unscalable: true)),
                Modifier(ParsedModifierKind.Prefix, "Gleaming", Effect("Adds 6(5-7) to 12(11-12) Physical Damage to Attacks")),
                Modifier(ParsedModifierKind.Prefix, "Acrobat's", Effect("+39(36-60) to Evasion Rating")),
                Modifier(ParsedModifierKind.Prefix, "Mazarine", Effect("+60(60-64) to maximum Mana")),
                Modifier(ParsedModifierKind.Suffix, "of the Phantom", Effect("+47(43-50) to Dexterity")),
            ], [], ["Our flesh longs to move as one."], []),
            new(8, "Body Armours", "Rare", "Dusk Shelter", "Gladiator Plate", 85, false,
            [
                Modifier(ParsedModifierKind.Prefix, "Thickened", Effect("76(68-79)% increased Armour")),
                Modifier(ParsedModifierKind.Prefix, "Crocodile's", Effect("+139(97-144) to Armour"), Effect("+37(34-38) to maximum Life")),
                Modifier(ParsedModifierKind.Suffix, "of Life-giving", Effect("Regenerate 151.6(128.1-152) Life per second")),
                Modifier(ParsedModifierKind.Suffix, "of the Maelstrom", Effect("+40(36-41)% to Lightning Resistance")),
            ], [], [], []),
            new(9, "Boots", "Rare", "Skull Road", "Conjurer Boots", 85, false,
            [
                Modifier(ParsedModifierKind.Prefix, "Cheetah's", Effect("30% increased Movement Speed")),
                Modifier(ParsedModifierKind.Prefix, "Boggart's", Effect("25(21-26)% increased Energy Shield"), Effect("10(10-11)% increased Stun and Block Recovery")),
                Modifier(ParsedModifierKind.Prefix, "Cerulean", Effect("+35(35-39) to maximum Mana")),
                Modifier(ParsedModifierKind.Suffix, "of Ephij", Effect("+46(46-48)% to Lightning Resistance")),
                Modifier(ParsedModifierKind.Suffix, "of the Genius", Effect("+53(51-55) to Intelligence")),
                Modifier(ParsedModifierKind.Suffix, "of Tzteosh", Effect("+46(46-48)% to Fire Resistance")),
            ], [], [], []),
            new(10, "Belts", "Rare", "Corruption Bond", "Stygian Vise", 85, false,
            [
                Modifier(ParsedModifierKind.Implicit, null, Effect("Has 1 Abyssal Socket", "(Only Abyss Jewels can be Socketed in Abyssal Sockets)", unscalable: true)),
                Modifier(ParsedModifierKind.Prefix, "Seething", Effect("+23(23-26) to maximum Energy Shield")),
                Modifier(ParsedModifierKind.Prefix, "Sapphire", Effect("+32(30-34) to maximum Mana")),
                Modifier(ParsedModifierKind.Suffix, "of the Boxer", Effect("10(10-11)% reduced Enemy Stun Threshold", "(The Stun Threshold determines how much Damage can Stun something)")),
                Modifier(ParsedModifierKind.Suffix, "of the Starfish", Effect("Regenerate 19.8(16.1-24) Life per second")),
                Modifier(ParsedModifierKind.Suffix, "of Stunning", Effect("21(21-25)% increased Stun Duration on Enemies")),
            ], [], [], []),
            new(11, "Body Armours", "Rare", "Gale Wrap", "Marshall's Brigandine", 84, false,
            [
                Modifier(ParsedModifierKind.Implicit, null, Effect("10% of Physical Damage from Hits taken as Cold Damage")),
                Modifier(ParsedModifierKind.Implicit, null, Effect("+23(22-23)% to Critical Strike Multiplier for Attack Damage")),
                Modifier(ParsedModifierKind.Prefix, "Adaptable", Effect("+226(221-300) to Armour"), Effect("+234(221-300) to Evasion Rating")),
                Modifier(ParsedModifierKind.Prefix, "Elephant's", Effect("37(33-38)% increased Armour and Evasion"), Effect("15(14-15)% increased Stun and Block Recovery")),
                Modifier(ParsedModifierKind.Prefix, "Duelist's", Effect("68(68-79)% increased Armour and Evasion")),
                Modifier(ParsedModifierKind.Suffix, "of the Polar Bear", Effect("+41(36-41)% to Cold Resistance")),
                Modifier(ParsedModifierKind.Suffix, "of the Keeper", Effect("6% additional Physical Damage Reduction")),
                Modifier(ParsedModifierKind.Suffix, "of Craft", isCrafted: true, Effect("+31(29-35)% to Lightning Resistance")),
            ], [], [], ["Searing Exarch Item", "Eater of Worlds Item"]),
            new(12, "Rings", "Rare", "Woe Coil", "Bone Ring", 84, false,
            [
                Modifier(ParsedModifierKind.Implicit, null, Effect("Minions have +10(10-15)% to all Elemental Resistances")),
                Modifier(ParsedModifierKind.Prefix, "Polished", Effect("Adds 4(3-4) to 6(6-7) Physical Damage to Attacks")),
                Modifier(ParsedModifierKind.Suffix, "of the Essence", Effect("+58(51-58) to Intelligence")),
                Modifier(ParsedModifierKind.Suffix, "of Rejuvenation", Effect("Gain 2 Life per Enemy Hit with Attacks")),
                Modifier(ParsedModifierKind.Suffix, "of the Volcano", Effect("+36(36-41)% to Fire Resistance")),
            ], [], [], []),
            new(13, "Shields", "Rare", "Miracle Bastion", "Supreme Spiked Shield", 84, false,
            [
                Modifier(ParsedModifierKind.Implicit, null, Effect("+5% chance to Suppress Spell Damage", "(40% of Damage from Suppressed Hits and Ailments they inflict is prevented)")),
                Modifier(ParsedModifierKind.Prefix, "Prior's", Effect("+15(11-15) to maximum Energy Shield"), Effect("+27(24-28) to maximum Life")),
                Modifier(ParsedModifierKind.Prefix, "Wasp's", Effect("28(27-32)% increased Evasion and Energy Shield"), Effect("13(12-13)% increased Stun and Block Recovery")),
                Modifier(ParsedModifierKind.Prefix, "Cherub's", Effect("+59(49-85) to Evasion Rating"), Effect("+26(23-28) to maximum Energy Shield")),
                Modifier(ParsedModifierKind.Suffix, "of the Wind", Effect("+55(51-55) to Dexterity")),
            ], [], [], []),
            new(14, "Rings", "Rare", null, "Cryonic Ring", 85, true,
            [
                Modifier(
                    ParsedModifierKind.Implicit,
                    null,
                    Effect("+2% to maximum Cold Resistance", "(Maximum Resistances cannot be raised above 90%)"),
                    Effect(
                        "Cannot roll Modifiers of Non-Cold Damage Types",
                        "(The Damage Types are Physical, Fire, Cold, Lightning, and Chaos)",
                        unscalable: true)),
            ], [], [], []),
        ];
    }

    public sealed record ExpectedModifier(
        ParsedModifierKind Kind,
        string? Name,
        bool IsCrafted,
        IReadOnlyList<ExpectedEffect> Effects);

    public sealed record ExpectedEffect(
        string Text,
        IReadOnlyList<string> ReminderLines,
        bool HasUnscalableValue);

    private static ExpectedModifier Modifier(
        ParsedModifierKind kind,
        string? name,
        params ExpectedEffect[] effects)
    {
        return new ExpectedModifier(kind, name, IsCrafted: false, effects);
    }

    private static ExpectedModifier Modifier(
        ParsedModifierKind kind,
        string? name,
        bool isCrafted,
        params ExpectedEffect[] effects)
    {
        return new ExpectedModifier(kind, name, isCrafted, effects);
    }

    private static ExpectedEffect Effect(
        string text,
        string? reminder = null,
        bool unscalable = false)
    {
        return new ExpectedEffect(
            text,
            reminder is null ? [] : [reminder],
            unscalable);
    }
}
