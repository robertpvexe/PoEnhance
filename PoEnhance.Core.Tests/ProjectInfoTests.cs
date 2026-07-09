using PoEnhance.Core;

namespace PoEnhance.Core.Tests;

public class ProjectInfoTests
{
    [Fact]
    public void Name_ReturnsPoEnhance()
    {
        Assert.Equal("PoEnhance", ProjectInfo.Name);
    }
}
