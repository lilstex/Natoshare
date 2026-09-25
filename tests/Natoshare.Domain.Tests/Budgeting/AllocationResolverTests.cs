using FluentAssertions;
using Natoshare.Domain.Budgeting;
using Xunit;

namespace Natoshare.Domain.Tests.Budgeting;

public class AllocationResolverTests
{
    private static AllocationConfigVersion Version(int year, int month, DateTimeOffset? supersededAt = null) => new()
    {
        Id = Guid.NewGuid(),
        EffectiveFromMonth = new DateOnly(year, month, 1),
        CreatedAt = DateTimeOffset.UtcNow,
        SupersededAt = supersededAt,
    };

    [Fact]
    public void Returns_null_when_there_are_no_versions_yet()
    {
        var result = AllocationResolver.ResolveActiveVersion([], new DateOnly(2026, 9, 15));

        result.Should().BeNull();
    }

    [Fact]
    public void Picks_the_version_that_started_closest_to_but_not_after_the_month()
    {
        var january = Version(2026, 1);
        var june = Version(2026, 6);
        var versions = new[] { january, june };

        var result = AllocationResolver.ResolveActiveVersion(versions, new DateOnly(2026, 9, 1));

        result.Should().Be(june);
    }

    [Fact]
    public void Does_not_pick_a_version_that_only_starts_later()
    {
        var future = Version(2027, 1);

        var result = AllocationResolver.ResolveActiveVersion([future], new DateOnly(2026, 9, 1));

        result.Should().BeNull();
    }

    [Fact]
    public void Ignores_a_superseded_version_even_if_it_would_otherwise_win()
    {
        var supersededJune = Version(2026, 6, supersededAt: DateTimeOffset.UtcNow);
        var january = Version(2026, 1);

        var result = AllocationResolver.ResolveActiveVersion([january, supersededJune], new DateOnly(2026, 9, 1));

        result.Should().Be(january);
    }

    [Fact]
    public void A_version_applies_on_its_own_first_month_too()
    {
        var september = Version(2026, 9);

        var result = AllocationResolver.ResolveActiveVersion([september], new DateOnly(2026, 9, 1));

        result.Should().Be(september);
    }
}
