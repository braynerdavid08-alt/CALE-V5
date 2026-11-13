using System.Security.Cryptography;
using System.Text;

namespace SeedLuzVerdeDemo;

internal sealed class DemoCredentialSet
{
    public required string OwnerEmail { get; init; }
    public required string OwnerPassword { get; init; }
    public required string SchoolName { get; init; }
    public required List<DemoUserCredential> Instructors { get; init; }
    public required List<DemoUserCredential> Students { get; init; }
}

internal sealed class DemoUserCredential
{
    public required int Scenario { get; init; }
    public required string Role { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public string? Document { get; init; }
    public string? License { get; init; }
    public string? Notes { get; init; }
}

internal static class DemoCredentials
{
    /// <summary>Env var for the SQLite lab owner password (never commit real values).</summary>
    public const string LabOwnerPasswordEnv = "CALE_DEMO_LAB_OWNER_PASSWORD";

    public const string LabOwnerPasswordArg = "--lab-owner-password";

    /// <summary>
    /// Resolves lab owner password: CLI → env → optional generated secret.
    /// <paramref name="required"/> forces CLI/env (used by --set-owners-password).
    /// </summary>
    public static string ResolveLabOwnerPassword(string[] args, bool required)
    {
        var fromArg = ReadArg(args, LabOwnerPasswordArg);
        if (!string.IsNullOrWhiteSpace(fromArg))
        {
            DemoRuntime.LabOwnerPasswordWasGenerated = false;
            return fromArg.Trim();
        }

        var fromEnv = Environment.GetEnvironmentVariable(LabOwnerPasswordEnv);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            DemoRuntime.LabOwnerPasswordWasGenerated = false;
            return fromEnv.Trim();
        }

        if (required)
        {
            throw new InvalidOperationException(
                $"Falta la clave de laboratorio. Define {LabOwnerPasswordEnv} o pasa {LabOwnerPasswordArg} <valor>.");
        }

        DemoRuntime.LabOwnerPasswordWasGenerated = true;
        return GeneratePassword(20);
    }

    public static string OwnerPasswordForMode()
    {
        if (DemoRuntime.PromoteProduction)
        {
            return GeneratePassword(20);
        }

        if (string.IsNullOrWhiteSpace(DemoRuntime.LabOwnerPassword))
        {
            throw new InvalidOperationException(
                $"Clave de laboratorio no resuelta. Define {LabOwnerPasswordEnv} o {LabOwnerPasswordArg}.");
        }

        return DemoRuntime.LabOwnerPassword;
    }

    private static string? ReadArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    public static string GeneratePassword(int length = 16)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(chars);
    }

    public static string WriteLocalFile(
        string repoRoot,
        IReadOnlyList<DemoCredentialSet> sets,
        string connectionDescription)
    {
        var path = Path.Combine(repoRoot, "docs", "demo-luzverde-credenciales.local.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var sb = new StringBuilder();
        sb.AppendLine("# Credenciales DEMO Luz Verde (LOCAL — no versionar)");
        sb.AppendLine();
        sb.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (hora local)");
        sb.AppendLine($"Base de datos: `{connectionDescription}`");
        sb.AppendLine(DemoRuntime.PromoteProduction
            ? "Objetivo: **producción** (https://www.cealuzverde.com)"
            : "Objetivo: laboratorio local SQLite");
        if (DemoRuntime.PromoteProduction)
        {
            sb.AppendLine($"Host confirmado: `{DemoRuntime.ConfirmedHost}`");
            sb.AppendLine($"Database confirmada: `{DemoRuntime.ConfirmedDatabase}`");
            sb.AppendLine($"Backup declarado: `{DemoRuntime.ConfirmedBackupDate}`");
        }

        sb.AppendLine();
        sb.AppendLine("## Uso");
        sb.AppendLine();
        sb.AppendLine("1. Inicia sesión en https://www.cealuzverde.com (o API apuntando a la misma DB).");
        sb.AppendLine("2. Usa un propietario (`School`) de la tabla inferior.");
        sb.AppendLine("3. Regenerar solo DEMO: `dotnet run --project tools/SeedLuzVerdeDemo -- --promote-production ...`");
        sb.AppendLine("4. Limpiar solo DEMO: `--purge` con las mismas confirmaciones de promote.");
        sb.AppendLine();
        sb.AppendLine("Las escuelas DEMO **no** tienen `PaymentAdminStatus=Confirmed` ni ficha comercial publicada.");
        sb.AppendLine("El acceso operativo se otorga con **Courtesy**, sin directorio público.");
        sb.AppendLine();

        foreach (var set in sets)
        {
            sb.AppendLine($"## {set.SchoolName}");
            sb.AppendLine();
            sb.AppendLine("| Rol | Nombre | Correo | Contraseña |");
            sb.AppendLine("|---|---|---|---|");
            sb.AppendLine($"| Propietario | — | `{set.OwnerEmail}` | `{set.OwnerPassword}` |");
            foreach (var i in set.Instructors)
            {
                sb.AppendLine($"| Instructor | {i.Name} | `{i.Email}` | `{i.Password}` |");
            }

            sb.AppendLine();
            sb.AppendLine("### Estudiantes (escenarios 1–14)");
            sb.AppendLine();
            sb.AppendLine("| # | Escenario | Nombre | Documento | Categoría | Correo | Contraseña |");
            sb.AppendLine("|---:|---|---|---|---|---|---|");
            foreach (var s in set.Students.OrderBy(x => x.Scenario))
            {
                sb.AppendLine(
                    $"| {s.Scenario} | {s.Notes} | {s.Name} | {s.Document} | {s.License} | `{s.Email}` | `{s.Password}` |");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Notas de modelado");
        sb.AppendLine();
        sb.AppendLine("- El examen práctico no tiene estado aprobado/reprobado en el dominio; escenario 13 = `Attended`, 14 = `NoShow` + reprogramación.");
        sb.AppendLine("- El examen teórico distingue aprobado/reprobado vía `Attempt.Passed`.");
        sb.AppendLine("- Correos `demo.*@cealuzverde.com` — marcados DEMO; seed confirma email sin envíos reales desde la herramienta.");
        sb.AppendLine();

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }
}
