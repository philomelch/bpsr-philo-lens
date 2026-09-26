using Stellar.PhiloLens.Domain;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class ImagineNamesTests
{
    [Fact]
    public void Finds_the_label_every_name_shares()
    {
        var label = ImagineNames.CommonLabel(new[] { "Battle Imagine - Tina", "Battle Imagine - Tatta", "Battle Imagine - Airona" });

        Assert.Equal("Battle Imagine - ", label);
    }

    [Fact]
    public void Never_cuts_into_a_name_that_starts_the_same_way()
    {
        var label = ImagineNames.CommonLabel(new[] { "Battle Imagine - Tina", "Battle Imagine - Tatta" });

        Assert.Equal("Battle Imagine - ", label);
    }

    [Fact]
    public void Works_with_other_languages_and_separators()
    {
        var label = ImagineNames.CommonLabel(new[] { "バトルイマジン・ティナ", "バトルイマジン・タッタ" });

        Assert.Equal("バトルイマジン・", label);
    }

    [Fact]
    public void Has_no_label_when_names_share_none()
    {
        Assert.Equal(string.Empty, ImagineNames.CommonLabel(new[] { "Arcane! Divine Reliance", "Stunt! Kinetic Strike" }));
    }

    [Fact]
    public void Has_no_label_with_a_single_name()
    {
        Assert.Equal(string.Empty, ImagineNames.CommonLabel(new[] { "Battle Imagine - Tina" }));
    }

    [Fact]
    public void Removes_the_label_but_keeps_names_that_lack_it()
    {
        Assert.Equal("Tina", ImagineNames.WithoutLabel("Battle Imagine - Tina", "Battle Imagine - "));
        Assert.Equal("Arcane! Divine Reliance", ImagineNames.WithoutLabel("Arcane! Divine Reliance", "Battle Imagine - "));
    }
}
