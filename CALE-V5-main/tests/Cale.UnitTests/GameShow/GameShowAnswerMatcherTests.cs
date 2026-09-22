using Cale.Modules.GameShow.Application;
using Xunit;

namespace Cale.UnitTests.GameShow;

public sealed class GameShowAnswerMatcherTests
{
    [Theory]
    [InlineData("Frenos", "frenos", true)]
    [InlineData("Frenos", "FRENOS", true)]
    [InlineData("Frenos", "  frenos  ", true)]
    [InlineData("Frenos", "frenós", true)]
    [InlineData("Frenos", "aceite", false)]
    [InlineData("Luces", "revisar las luces", true)]
    [InlineData("Frenos", "el sistema de frenos del vehiculo esta fallando hoy", false)]
    [InlineData("Aceite", "motor", false)]
    public void Matches_normalized_variants(string canonical, string submitted, bool expected)
    {
        var ok = GameShowAnswerMatcher.Matches(submitted, canonical, "[]");
        Assert.Equal(expected, ok);
    }

    [Fact]
    public void Matches_aliases()
    {
        var json = GameShowAnswerMatcher.SerializeAliases(["llanta", "neumatico"]);
        Assert.True(GameShowAnswerMatcher.Matches("Neumático", "Llantas", json));
    }
}
