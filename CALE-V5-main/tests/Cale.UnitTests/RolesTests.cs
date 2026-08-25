using Cale.BuildingBlocks.Domain.Auth;

namespace Cale.UnitTests;

public class RolesTests
{
    [Theory]
    [InlineData("Alumno", Roles.Student)]
    [InlineData("Estudiante", Roles.Student)]
    [InlineData("Profesor", Roles.Teacher)]
    [InlineData("Escuela", Roles.School)]
    [InlineData("School", Roles.School)]
    [InlineData("Admin", Roles.Admin)]
    [InlineData("Teacher", Roles.Teacher)]
    [InlineData("Student", Roles.Student)]
    public void Normalize_LegacyRoles(string input, string expected)
    {
        Assert.Equal(expected, Roles.Normalize(input));
    }

    [Fact]
    public void IsValid_OnlyEnglishRoles()
    {
        Assert.True(Roles.IsValid(Roles.Admin));
        Assert.True(Roles.IsValid(Roles.School));
        Assert.False(Roles.IsValid("Profesor"));
    }

    [Fact]
    public void IsStaff_IncludesSchool()
    {
        Assert.True(Roles.IsStaff(Roles.School));
        Assert.False(Roles.IsStaff(Roles.Student));
    }
}
