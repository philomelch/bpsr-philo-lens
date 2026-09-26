using System.Collections.Generic;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class InspectionServiceTests
{
    private const long PlayerId = (215381L << 16) | 640;
    private const int Marksman = 11;
    private const int VerdantOracle = 5;
    private const int LucyTransform = 14;

    private readonly FakeClassEvidenceSource _source = new();
    private readonly FakePlayerBuildSource _build = new();

    [Fact]
    public void Inspect_reads_the_player_right_away()
    {
        var service = new InspectionService(_source, _build);
        _source.Evidence[PlayerId] = LiveClass(Marksman);

        service.Inspect(PlayerId);

        Assert.Equal(new InspectedPlayer(PlayerId, new ClassSpec(Marksman, 0, SpecSource.None), _build.Build), service.Current);
    }

    [Fact]
    public void Refresh_keeps_the_last_value_while_reads_are_unsafe()
    {
        var service = new InspectionService(_source, _build);
        _source.Evidence[PlayerId] = LiveClass(Marksman);
        service.Inspect(PlayerId);

        _source.CanRead = false;
        _source.Evidence[PlayerId] = LiveClass(VerdantOracle);
        service.Refresh();

        Assert.Equal(Marksman, service.Current?.ClassSpec.ClassId);
    }

    [Fact]
    public void A_transform_keeps_showing_the_class_seen_before_it()
    {
        var service = new InspectionService(_source, _build);
        _source.Evidence[PlayerId] = LiveClass(Marksman);
        service.Inspect(PlayerId);

        _source.Evidence[PlayerId] = LiveClass(LucyTransform);
        service.Refresh();

        Assert.Equal(Marksman, service.Current?.ClassSpec.ClassId);
    }

    [Fact]
    public void Inspecting_another_player_does_not_show_the_previous_one()
    {
        const long otherId = (8194385L << 16) | 640;
        var service = new InspectionService(_source, _build);
        _source.Evidence[PlayerId] = LiveClass(Marksman);
        service.Inspect(PlayerId);

        _source.CanRead = false;
        service.Inspect(otherId);

        Assert.Null(service.Current);
    }

    [Fact]
    public void Refresh_carries_the_current_build()
    {
        var service = new InspectionService(_source, _build);
        _source.Evidence[PlayerId] = LiveClass(Marksman);
        service.Inspect(PlayerId);

        _build.Build = PlayerBuild.Empty with { AbilityScore = 32_000, SeasonStrength = 134 };
        service.Refresh();

        Assert.Equal(32_000, service.Current?.Build.AbilityScore);
        Assert.Equal(134, service.Current?.Build.SeasonStrength);
    }

    private static ClassEvidence LiveClass(int classId) => new(classId, 0, 0, 0, 0);

    private sealed class FakePlayerBuildSource : IPlayerBuildSource
    {
        public PlayerBuild Build { get; set; } = PlayerBuild.Empty;

        public PlayerBuild Read(long entityId) => Build;
    }

    private sealed class FakeClassEvidenceSource : IClassEvidenceSource
    {
        public bool CanRead { get; set; } = true;
        public Dictionary<long, ClassEvidence> Evidence { get; } = new();

        public ClassEvidence Read(long entityId) => Evidence.TryGetValue(entityId, out var evidence) ? evidence : default;
    }
}
