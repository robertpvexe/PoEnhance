using System.Collections.ObjectModel;

namespace PoEnhance.Core.Items.GameData;

public sealed record ModifierTextSignature(IReadOnlyList<string> Lines)
{
    public static ModifierTextSignature Create(IEnumerable<string> lines)
    {
        return new ModifierTextSignature(new ReadOnlyCollection<string>(lines.ToArray()));
    }
}
