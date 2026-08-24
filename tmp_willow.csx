using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;
var parser = new ItemTextParser();
var text = """
Item Class: Body Armours
Rarity: Unique
Willowgift
Festival Garb
--------
Item Level: 85
--------
{ Unique Modifier }
-29(-30--20)% to Fire Resistance
{ Unique Modifier }
You do not inherently take less Damage for having Fortification
+4% chance to Suppress Spell Damage per Fortification
""";
var parsed = parser.Parse(text);
var gd = GameDataCatalog.FromPackage(GameDataPackageLoader.LoadFromFileAsync("artifacts/poenhance-game-data.json").GetAwaiter().GetResult().Package!);
var resolver = new ParsedUniqueItemResolver();
var result = resolver.Resolve(parsed, gd);
Console.WriteLine($"Versions: {result.CompatibleVersions.Count}");
foreach (var b in result.ModifierBlocks) {
  Console.WriteLine($"Block idx={b.ParsedModifierIndex} resolved={b.IsResolved} code={b.DiagnosticCode} stats=[{string.Join(",", b.StatIds)}]");
}
