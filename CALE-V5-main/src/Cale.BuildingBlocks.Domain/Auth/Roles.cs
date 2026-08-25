namespace Cale.BuildingBlocks.Domain.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string School = "School";
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    public static readonly string[] All = [Admin, School, Teacher, Student];

    public static bool IsValid(string? role) =>
        role is Admin or School or Teacher or Student;

    public static string Normalize(string? role) => role switch
    {
        "Escuela" or School => School,
        "Profesor" or Teacher => Teacher,
        "Estudiante" or "Alumno" or Student => Student,
        "Administrador" or Admin => Admin,
        _ => Student
    };

    public static bool IsStaff(string? role)
    {
        var normalized = Normalize(role);
        return normalized is Admin or School or Teacher;
    }
}
