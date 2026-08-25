namespace Cale.BuildingBlocks.Domain.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    public static readonly string[] All = [Admin, Teacher, Student];

    public static bool IsValid(string? role) =>
        role is Admin or Teacher or Student;

    public static string Normalize(string? role) => role switch
    {
        "Profesor" => Teacher,
        "Estudiante" or "Alumno" => Student,
        Admin => Admin,
        Teacher => Teacher,
        Student => Student,
        _ => Student
    };
}
