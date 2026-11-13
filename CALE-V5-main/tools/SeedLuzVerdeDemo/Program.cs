using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.BuildingBlocks.Infrastructure.Security;
using Cale.BuildingBlocks.Infrastructure.Time;
using Cale.Modules.Assessment.Infrastructure.Persistence;
using Cale.Modules.Catalog.Infrastructure.Persistence;
using Cale.Modules.Classroom.Infrastructure.Persistence;
using Cale.Modules.Engagement.Infrastructure.Persistence;
using Cale.Modules.GameShow.Infrastructure.Persistence;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Cale.Modules.LiveClassroom.Infrastructure.Persistence;
using Cale.Modules.Presentation.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeedLuzVerdeDemo;

static string? ArgValue(string[] args, string name)
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

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "CALE-V5-main.sln"))
            || File.Exists(Path.Combine(dir.FullName, "Cale.sln"))
            || Directory.Exists(Path.Combine(dir.FullName, "src", "Cale.Api")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return Directory.GetCurrentDirectory();
}

static IConfiguration BuildConfig(string repoRoot, string[] args, bool promoteProduction)
{
    var apiDir = Path.Combine(repoRoot, "src", "Cale.Api");
    var builder = new ConfigurationBuilder()
        .SetBasePath(apiDir)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Development.json", optional: true);

    // Promote may load Development.local.json (Render connection). Local seed never does.
    if (promoteProduction)
    {
        builder.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: false);
    }

    builder.AddEnvironmentVariables();

    var connOverride = ArgValue(args, "--connection")
        ?? Environment.GetEnvironmentVariable("CALE_DEMO_CONNECTION");
    if (!string.IsNullOrWhiteSpace(connOverride))
    {
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Cale"] = connOverride
        });
    }
    else if (!promoteProduction)
    {
        var demoDb = Path.Combine(apiDir, "cale-demo-luzverde.db");
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Cale"] = $"Data Source={demoDb}"
        });
    }

    // Tool never sends real email.
    builder.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Email:Enabled"] = "false"
    });

    return builder.Build();
}

var repoRoot = FindRepoRoot();
var promoteProduction = args.Contains("--promote-production", StringComparer.OrdinalIgnoreCase);
var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
var purgeOnly = args.Contains("--purge", StringComparer.OrdinalIgnoreCase);
var setOwnersPasswordOnly = args.Contains("--set-owners-password", StringComparer.OrdinalIgnoreCase)
    && !promoteProduction;
var seed = !setOwnersPasswordOnly
    && (args.Contains("--seed", StringComparer.OrdinalIgnoreCase)
        || promoteProduction
        || (!purgeOnly && !dryRun && args.Length == 0));

DemoRuntime.PromoteProduction = promoteProduction;
DemoRuntime.DryRun = dryRun;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h"))
{
    Console.WriteLine("""
        SeedLuzVerdeDemo — 3 CEAs DEMO en laboratorio local o en producción (cealuzverde.com)

        Laboratorio SQLite:
          $env:CALE_DEMO_LAB_OWNER_PASSWORD = "<clave-local>"
          dotnet run --project tools/SeedLuzVerdeDemo -- --seed
          dotnet run --project tools/SeedLuzVerdeDemo -- --purge
          dotnet run --project tools/SeedLuzVerdeDemo -- --set-owners-password
          # alternativa: --lab-owner-password "<clave-local>"
          # si no hay env/arg en --seed, se genera una clave y se guarda en el archivo local

        Producción (aditivo, solo registros DEMO):
          dotnet run --project tools/SeedLuzVerdeDemo -- --promote-production --dry-run --connection "<postgres>"
          dotnet run --project tools/SeedLuzVerdeDemo -- --promote-production --seed ^
            --confirm-host "<host>" --confirm-database "<db>" --confirm-backup YYYY-MM-DD ^
            --i-understand-additive-only --connection "<postgres>"

        Seguridad:
          - Sin --promote-production: bloquea Render/Production.
          - Con --promote-production: exige confirm-host/db/backup + additive-only.
          - No migra esquema en promote. No borra clientes reales.
          - Clave lab: env CALE_DEMO_LAB_OWNER_PASSWORD o --lab-owner-password (nunca hardcodeada).
          - Credenciales → docs/demo-luzverde-credenciales.local.md (gitignored).
        """);
    return 0;
}

if (!promoteProduction && (seed || setOwnersPasswordOnly))
{
    try
    {
        DemoRuntime.LabOwnerPassword = DemoCredentials.ResolveLabOwnerPassword(
            args,
            required: setOwnersPasswordOnly);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 4;
    }
}

var config = BuildConfig(repoRoot, args, promoteProduction);
string connection;
try
{
    connection = DatabaseConnection.Resolve(config);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"No se pudo resolver la cadena de conexión: {ex.Message}");
    return 2;
}

try
{
    DemoSafety.AssertSafeToWrite(
        config,
        connection,
        promoteProduction,
        ArgValue(args, "--confirm-host"),
        ArgValue(args, "--confirm-database"),
        ArgValue(args, "--confirm-backup"),
        args.Contains("--i-understand-additive-only", StringComparer.OrdinalIgnoreCase));
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("No se realizó ninguna escritura.");
    return 3;
}

Console.WriteLine(promoteProduction
    ? $"Promote producción OK (backup {DemoRuntime.ConfirmedBackupDate})."
    : "Entorno local seguro.");
Console.WriteLine($"DB: {DatabaseConnection.Detect(connection)}");
if (DatabaseConnection.Detect(connection) == DatabaseProviderKind.PostgreSql)
{
    Console.WriteLine($"Destino: {DatabaseConnection.Describe(connection)}");
}
else
{
    Console.WriteLine($"Destino: {connection}");
}

var services = new ServiceCollection();
var mappings = new MappingAssemblies(
    typeof(UserConfiguration).Assembly,
    typeof(BankConfiguration).Assembly,
    typeof(AttemptConfiguration).Assembly,
    typeof(GroupConfiguration).Assembly,
    typeof(NotificationConfiguration).Assembly,
    typeof(PresentationDeckConfiguration).Assembly,
    typeof(LiveSessionConfiguration).Assembly,
    typeof(GameShowSessionConfiguration).Assembly,
    typeof(TheoryTopicConfiguration).Assembly,
    typeof(Cale.Modules.Courses.Infrastructure.Persistence.CourseConfiguration).Assembly,
    typeof(Cale.Api.Services.Assistant.AssistantUsageDailyConfiguration).Assembly);

services.AddSingleton(mappings);
services.AddSingleton<IConfiguration>(config);
services.AddSingleton<IClock, SystemClock>();
services.AddSingleton<IPasswordHasher, PasswordHasher>();
services.AddCalePersistence(config, mappings);

await using var sp = services.BuildServiceProvider();
await using var scope = sp.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();

Console.WriteLine("Comprobando esquema…");

var canConnect = false;
try
{
    canConnect = await db.Database.CanConnectAsync();
}
catch
{
    canConnect = false;
}

if (!canConnect)
{
    Console.Error.WriteLine("ABORT: no hay conexión a la base de datos.");
    return 4;
}

var schemaReady = false;
try
{
    _ = await db.Set<Cale.Modules.Identity.Domain.User>()
        .AsNoTracking()
        .Select(u => u.Id)
        .Take(1)
        .ToListAsync();
    schemaReady = true;
    Console.WriteLine("Esquema existente compatible.");
}
catch (Exception ex)
{
    Console.WriteLine($"Esquema incompleto ({ex.GetType().Name}).");
}

if (!schemaReady)
{
    if (promoteProduction)
    {
        Console.Error.WriteLine(
            "ABORT promote: el esquema de producción no es compatible con el modelo. " +
            "No se ejecutan migraciones ni EnsureCreated en promote.");
        return 4;
    }

    if (DatabaseConnection.Detect(connection) != DatabaseProviderKind.Sqlite)
    {
        Console.Error.WriteLine("ABORT: destino no SQLite y esquema incompatible.");
        return 4;
    }

    try
    {
        if (args.Contains("--fresh", StringComparer.OrdinalIgnoreCase) || !canConnect)
        {
            if (canConnect)
            {
                await db.Database.EnsureDeletedAsync();
            }

            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            Console.Error.WriteLine("ABORT: usa --fresh para recrear SQLite local.");
            return 4;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ABORT: {ex.Message}");
        return 4;
    }
}

if (!promoteProduction)
{
    try
    {
        await FeatureSchema.EnsureAsync(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FeatureSchema aviso: {ex.Message}");
    }

    await UserCreatorSchemaGuard.EnsureAsync(db);
    await DemoSchemaPatch.EnsureAsync(db);
}
else
{
    // Soft column patches only — never EnsureDeleted / Migrate in production.
    await UserCreatorSchemaGuard.EnsureAsync(db);
    await DemoSchemaPatch.EnsureAsync(db);
}

Console.WriteLine();
Console.WriteLine("=== PREFLIGHT DEMO ===");
var preflight = await DemoPreflight.InspectAsync(db, CancellationToken.None);
DemoPreflight.Print(preflight);

if (preflight.BlockingNonDemoEmails.Count > 0)
{
    Console.Error.WriteLine("ABORT: colisión con cuentas reales. No se escribe nada.");
    return 6;
}

if (dryRun)
{
    Console.WriteLine();
    Console.WriteLine("DRY-RUN: sin escrituras. Revisa el preflight y confirma backup antes del promote real.");
    return 0;
}

if (setOwnersPasswordOnly)
{
    var labPassword = DemoCredentials.OwnerPasswordForMode();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var updated = 0;
    foreach (var school in DemoMarkers.Schools)
    {
        var user = await db.Set<Cale.Modules.Identity.Domain.User>()
            .FirstOrDefaultAsync(u => u.Email == school.OwnerEmail);
        if (user is null || !DemoMarkers.IsDemoEmail(user.Email))
        {
            Console.WriteLine($"Omitido: {school.OwnerEmail}");
            continue;
        }

        user.ChangePassword(hasher.Hash(labPassword));
        if (!user.EmailConfirmed)
        {
            user.MarkEmailConfirmed();
        }

        if (!user.IsActive)
        {
            user.Activate();
        }

        updated++;
        Console.WriteLine($"OK: {school.OwnerEmail}");
    }

    await db.SaveChangesAsync();
    Console.WriteLine(
        $"Propietarios lab actualizados: {updated}. Clave tomada de {DemoCredentials.LabOwnerPasswordEnv} o {DemoCredentials.LabOwnerPasswordArg}.");
    return 0;
}

if (purgeOnly || seed)
{
    Console.WriteLine("Purgando únicamente registros DEMO…");
    var purged = await DemoPurge.PurgeAsync(db, CancellationToken.None);
    Console.WriteLine($"Purge OK: {purged}");
}

if (!seed)
{
    Console.WriteLine("Listo (--purge).");
    return 0;
}

Console.WriteLine(promoteProduction
    ? "Generando 3 CEAs DEMO en producción (aditivo)…"
    : "Generando 3 CEAs DEMO en laboratorio…");
var seeder = new DemoSeeder(
    db,
    scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
    scope.ServiceProvider.GetRequiredService<IClock>());
var credentials = await seeder.SeedAsync(CancellationToken.None);

var connDesc = DatabaseConnection.Detect(connection) == DatabaseProviderKind.PostgreSql
    ? DatabaseConnection.Describe(connection)
    : connection;
var credPath = DemoCredentials.WriteLocalFile(repoRoot, credentials, connDesc);

Console.WriteLine();
Console.WriteLine("=== DEMO Luz Verde listo ===");
Console.WriteLine($"Escuelas: {credentials.Count}");
foreach (var c in credentials)
{
    Console.WriteLine(
        $"  {c.SchoolName}: owner={c.OwnerEmail}, instructors={c.Instructors.Count}, students={c.Students.Count}");
}

Console.WriteLine($"Credenciales: {credPath}");
if (!promoteProduction && DemoRuntime.LabOwnerPasswordWasGenerated)
{
    Console.WriteLine(
        $"Clave lab generada (no estaba {DemoCredentials.LabOwnerPasswordEnv} ni {DemoCredentials.LabOwnerPasswordArg}); ver archivo local.");
}
if (promoteProduction)
{
    Console.WriteLine("Login: https://www.cealuzverde.com con los propietarios DEMO.");
}

return 0;
