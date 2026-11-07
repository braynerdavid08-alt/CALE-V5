using Cale.BuildingBlocks.Infrastructure.Security;
using Microsoft.Data.Sqlite;

// Local-only Phase 3 accounts. Never for production.
const string Password = "Fase3-Prueba-2026!";
var dbPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "Cale.Api", "cale-dev.db"));

if (!File.Exists(dbPath))
{
    Console.Error.WriteLine($"DB not found: {dbPath}");
    return 1;
}

Console.WriteLine($"Seeding Fase3 accounts into {dbPath}");

var hasher = new PasswordHasher();
var hash = hasher.Hash(Password);
var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff");

await using var conn = new SqliteConnection($"Data Source={dbPath}");
await conn.OpenAsync();

async Task ResetAsync(string email)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        UPDATE Usuarios
        SET PasswordHash = $hash,
            EmailConfirmado = 1,
            Activo = 1,
            DebeCambiarClave = 0
        WHERE Email = $email;
        """;
    cmd.Parameters.AddWithValue("$hash", hash);
    cmd.Parameters.AddWithValue("$email", email);
    var n = await cmd.ExecuteNonQueryAsync();
    Console.WriteLine(n > 0 ? $"  reset {email}" : $"  missing {email}");
}

await ResetAsync("admin@cale.local");
await ResetAsync("escuela@cale.local");
await ResetAsync("profesor@cale.local");
await ResetAsync("estudiante@cale.local");

long schoolId;
await using (var schoolCmd = conn.CreateCommand())
{
    schoolCmd.CommandText = "SELECT Id FROM Usuarios WHERE Email = 'escuela@cale.local' LIMIT 1;";
    schoolId = (long)(await schoolCmd.ExecuteScalarAsync() ?? 0L);
}

if (schoolId == 0)
{
    Console.Error.WriteLine("School demo missing.");
    return 2;
}

var extras = new (string Email, string Name)[]
{
    ("estudiante2.fase3@cale.local", "Estudiante Fase3 2"),
    ("estudiante3.fase3@cale.local", "Estudiante Fase3 3"),
    ("estudiante4.fase3@cale.local", "Estudiante Fase3 4"),
};

foreach (var (email, name) in extras)
{
    await using var find = conn.CreateCommand();
    find.CommandText = "SELECT Id FROM Usuarios WHERE Email = $email LIMIT 1;";
    find.Parameters.AddWithValue("$email", email);
    var existing = await find.ExecuteScalarAsync();

    if (existing is null)
    {
        await using var insert = conn.CreateCommand();
        insert.CommandText = """
            INSERT INTO Usuarios
              (Nombre, Email, PasswordHash, Rol, SchoolId, Activo, CreadoEn, UltimoAccesoEn,
               DebeCambiarClave, EmailConfirmado, EmailCodigoHash, EmailCodigoExpiraEn, EscuelaCreadoraId)
            VALUES
              ($name, $email, $hash, 'Student', $schoolId, 1, $now, NULL,
               0, 1, NULL, NULL, $schoolId);
            """;
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$email", email);
        insert.Parameters.AddWithValue("$hash", hash);
        insert.Parameters.AddWithValue("$schoolId", schoolId);
        insert.Parameters.AddWithValue("$now", now);
        await insert.ExecuteNonQueryAsync();
        Console.WriteLine($"  create {email}");
    }
    else
    {
        await ResetAsync(email);
    }
}

await using (var verifyCmd = conn.CreateCommand())
{
    verifyCmd.CommandText = "SELECT PasswordHash FROM Usuarios WHERE Email='profesor@cale.local'";
    var stored = (string)(await verifyCmd.ExecuteScalarAsync())!;
    Console.WriteLine($"  verify teacher hash => {hasher.Verify(Password, stored)}");
}

Console.WriteLine();
Console.WriteLine("Credenciales Fase 3 (solo local):");
Console.WriteLine($"  Password (todos): {Password}");
Console.WriteLine("  Teacher:  profesor@cale.local");
Console.WriteLine("  Students: estudiante@cale.local");
foreach (var (email, _) in extras)
{
    Console.WriteLine($"            {email}");
}

return 0;
