using PoEnhance.App.Infrastructure.Input;

namespace PoEnhance.App.Tests.Infrastructure.Input;

public sealed class KeyboardInputSenderTests
{
    [Fact]
    public void AdvancedItemDescriptionCopySequence_IsCtrlCWithNoAlt()
    {
        var sequence = KeyboardInputSender.BuildAdvancedItemDescriptionCopySequence();

        Assert.Equal(4, sequence.Count);
        Assert.Collection(
            sequence,
            stroke => AssertStroke(stroke, 0xA2, isKeyUp: false),
            stroke => AssertStroke(stroke, 0x43, isKeyUp: false),
            stroke => AssertStroke(stroke, 0x43, isKeyUp: true),
            stroke => AssertStroke(stroke, 0xA2, isKeyUp: true));
        Assert.DoesNotContain(sequence, stroke => stroke.VirtualKey == 0xA4);
        Assert.All(sequence, stroke => Assert.Null(stroke.UnicodeCharacter));
    }

    private static void AssertStroke(KeyboardInputStroke stroke, ushort virtualKey, bool isKeyUp)
    {
        Assert.Equal(virtualKey, stroke.VirtualKey);
        Assert.Equal(isKeyUp, stroke.IsKeyUp);
    }
}
