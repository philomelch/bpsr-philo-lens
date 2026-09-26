using Stellar.PhiloLens.Domain;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class ClassSpecResolverTests
{
    private const int VerdantOracle = 5;
    private const int Marksman = 11;
    private const int Smite = 50001;
    private const int Lifebind = 50002;
    private const int Falconry = 110002;
    private const int LucyTransform = 14;

    private static ClassEvidence Evidence(int live = 0, int party = 0, int profile = 0, int talentSpec = 0, int castSpec = 0) =>
        new(live, party, profile, talentSpec, castSpec);

    [Fact]
    public void Shows_the_talent_spec_when_it_belongs_to_the_class()
    {
        var result = ClassSpecResolver.Resolve(Evidence(live: VerdantOracle, talentSpec: Lifebind), lastKnownClassId: 0);

        Assert.Equal(new ClassSpec(VerdantOracle, Lifebind, SpecSource.Talents), result);
    }

    [Fact]
    public void A_talent_spec_wins_over_a_different_cast_spec()
    {
        var result = ClassSpecResolver.Resolve(Evidence(live: VerdantOracle, talentSpec: Lifebind, castSpec: Smite), lastKnownClassId: 0);

        Assert.Equal(new ClassSpec(VerdantOracle, Lifebind, SpecSource.Talents), result);
    }

    [Fact]
    public void Shows_the_cast_spec_when_no_talent_spec_is_known()
    {
        var result = ClassSpecResolver.Resolve(Evidence(live: VerdantOracle, castSpec: Smite), lastKnownClassId: 0);

        Assert.Equal(new ClassSpec(VerdantOracle, Smite, SpecSource.Casts), result);
    }

    [Fact]
    public void Falls_back_to_the_class_when_no_spec_was_seen()
    {
        var result = ClassSpecResolver.Resolve(Evidence(live: Marksman), lastKnownClassId: 0);

        Assert.Equal(new ClassSpec(Marksman, 0, SpecSource.None), result);
    }

    [Fact]
    public void Drops_a_talent_spec_held_over_from_a_previous_class()
    {
        var result = ClassSpecResolver.Resolve(Evidence(live: Marksman, talentSpec: Smite), lastKnownClassId: 0);

        Assert.Equal(new ClassSpec(Marksman, 0, SpecSource.None), result);
    }

    [Fact]
    public void Drops_a_cast_spec_left_over_from_a_previous_class()
    {
        var result = ClassSpecResolver.Resolve(Evidence(live: Marksman, castSpec: Smite), lastKnownClassId: 0);

        Assert.Equal(new ClassSpec(Marksman, 0, SpecSource.None), result);
    }

    [Fact]
    public void Ignores_a_transform_and_uses_the_next_real_class()
    {
        var result = ClassSpecResolver.Resolve(Evidence(live: LucyTransform, party: Marksman), lastKnownClassId: 0);

        Assert.Equal(Marksman, result.ClassId);
    }

    [Fact]
    public void Uses_the_last_known_class_while_every_source_reads_a_transform()
    {
        var result = ClassSpecResolver.Resolve(Evidence(live: LucyTransform, talentSpec: Falconry), lastKnownClassId: Marksman);

        Assert.Equal(new ClassSpec(Marksman, Falconry, SpecSource.Talents), result);
    }

    [Fact]
    public void Uses_the_profile_class_for_players_out_of_range()
    {
        var result = ClassSpecResolver.Resolve(Evidence(profile: VerdantOracle), lastKnownClassId: 0);

        Assert.Equal(VerdantOracle, result.ClassId);
    }

    [Fact]
    public void Derives_the_class_from_the_talent_spec_when_nothing_else_is_known()
    {
        var result = ClassSpecResolver.Resolve(Evidence(talentSpec: Falconry), lastKnownClassId: 0);

        Assert.Equal(new ClassSpec(Marksman, Falconry, SpecSource.Talents), result);
    }

    [Fact]
    public void Derives_the_class_from_the_cast_spec_when_nothing_else_is_known()
    {
        var result = ClassSpecResolver.Resolve(Evidence(castSpec: Falconry), lastKnownClassId: 0);

        Assert.Equal(new ClassSpec(Marksman, Falconry, SpecSource.Casts), result);
    }

    [Fact]
    public void Returns_unknown_when_no_source_has_a_real_class()
    {
        var result = ClassSpecResolver.Resolve(Evidence(live: LucyTransform), lastKnownClassId: 0);

        Assert.Equal(ClassSpec.Unknown, result);
    }
}
