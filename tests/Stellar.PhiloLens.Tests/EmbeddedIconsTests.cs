using Stellar.PhiloLens.UI;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class EmbeddedIconsTests
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Fact]
    public void The_lens_icon_is_embedded_as_a_png()
    {
        var bytes = EmbeddedIcons.Load(EmbeddedIcons.Lens);

        Assert.NotNull(bytes);
        Assert.Equal(PngSignature, bytes![..PngSignature.Length]);
    }

    [Fact]
    public void A_missing_icon_loads_as_null()
    {
        Assert.Null(EmbeddedIcons.Load("missing.png"));
    }
}
