using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.Identity.Domain;
using NetArchTest.Rules;

namespace Cale.ArchitectureTests;

public class LayeringTests
{
    [Fact]
    public void BuildingBlocksDomain_Must_Not_Reference_EfCore()
    {
        var result = Types.InAssembly(typeof(Roles).Assembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            Format(result.FailingTypeNames));
    }

    [Fact]
    public void IdentityDomain_Must_Not_Reference_EfCore()
    {
        var result = Types.InNamespace("Cale.Modules.Identity.Domain")
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            Format(result.FailingTypeNames));
    }

    [Fact]
    public void IdentityApplication_Must_Not_Reference_EfCore()
    {
        var result = Types.InAssembly(typeof(User).Assembly)
            .That()
            .ResideInNamespaceStartingWith("Cale.Modules.Identity.Application")
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            Format(result.FailingTypeNames));
    }

    [Fact]
    public void IdentityDomain_Must_Not_Reference_AspNetCore()
    {
        var result = Types.InAssembly(typeof(User).Assembly)
            .That()
            .ResideInNamespace("Cale.Modules.Identity.Domain")
            .Should()
            .NotHaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            Format(result.FailingTypeNames));
    }

    private static string Format(IEnumerable<string>? names) =>
        names is null ? "architecture rule failed" : string.Join(", ", names);
}
