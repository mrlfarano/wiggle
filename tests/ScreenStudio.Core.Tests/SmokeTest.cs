using ScreenStudio.Core.Math;

namespace ScreenStudio.Core.Tests;

public class SmokeTest
{
    [Fact]
    public void Vec2_Add_And_Length_Work()
    {
        var v = new Vec2(3, 4);
        Assert.Equal(5.0, v.Length, 3);
        Assert.Equal(new Vec2(4, 6), v + new Vec2(1, 2));
    }
}

