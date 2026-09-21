using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cale.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActividadesGrupo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    AutorId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Instrucciones = table.Column<string>(type: "text", nullable: true),
                    FechaPublicacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaLimite = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PuntajeMaximo = table.Column<decimal>(type: "numeric", nullable: true),
                    AdjuntoUrl = table.Column<string>(type: "text", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActividadesGrupo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprenticePaymentAbonos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    StudentUserId = table.Column<int>(type: "integer", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Notes = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprenticePaymentAbonos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AvisosGrupo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    AutorId = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Contenido = table.Column<string>(type: "text", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisosGrupo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bancos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    SeedInicialCompletado = table.Column<bool>(type: "boolean", nullable: false),
                    DistribucionRespuestasInicialAplicada = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoPorId = table.Column<int>(type: "integer", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bancos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bloques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bloques", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogMediaBlobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogMediaBlobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnrollmentAuthorizationEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    StudentUserId = table.Column<int>(type: "integer", nullable: false),
                    AuthorizationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentAuthorizationEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntregasActividad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActividadId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    ContenidoTexto = table.Column<string>(type: "text", nullable: true),
                    ArchivoUrl = table.Column<string>(type: "text", nullable: true),
                    EntregadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Calificacion = table.Column<decimal>(type: "numeric", nullable: true),
                    ComentarioDocente = table.Column<string>(type: "text", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntregasActividad", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Examenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    BancoId = table.Column<int>(type: "integer", nullable: true),
                    NumeroPreguntas = table.Column<int>(type: "integer", nullable: false),
                    TiempoMinutos = table.Column<int>(type: "integer", nullable: false),
                    IntentosPermitidos = table.Column<int>(type: "integer", nullable: false),
                    Aleatorio = table.Column<bool>(type: "boolean", nullable: false),
                    Publicado = table.Column<bool>(type: "boolean", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoPorId = table.Column<int>(type: "integer", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaCierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Examenes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExamenesGrupos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamenId = table.Column<int>(type: "integer", nullable: false),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaCierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamenesGrupos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExamenesPreguntas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamenId = table.Column<int>(type: "integer", nullable: false),
                    PreguntaId = table.Column<int>(type: "integer", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamenesPreguntas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Grupos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ProfesorId = table.Column<int>(type: "integer", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    FechaInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grupos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GruposUsuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposUsuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomepageAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    Area = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StatKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PreviousValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomepageAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomepageSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HeroBadge = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    HeroTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeroTitleHighlight = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeroDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    HeroCtaPrimaryLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HeroCtaPrimaryPath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeroCtaSecondaryLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HeroVideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HeroImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HeroImageUrlMobile = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HeroImageAlt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeroImageEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    HeroVisible = table.Column<bool>(type: "boolean", nullable: false),
                    BenefitsJson = table.Column<string>(type: "text", nullable: false),
                    StepsJson = table.Column<string>(type: "text", nullable: false),
                    StepsSectionTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StepsSectionSubtitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SchoolsSectionVisible = table.Column<bool>(type: "boolean", nullable: false),
                    InstructorsSectionVisible = table.Column<bool>(type: "boolean", nullable: false),
                    StatsSectionVisible = table.Column<bool>(type: "boolean", nullable: false),
                    BenefitsSectionVisible = table.Column<bool>(type: "boolean", nullable: false),
                    StepsSectionVisible = table.Column<bool>(type: "boolean", nullable: false),
                    SeoTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SeoDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AboutHtml = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    BlogIntro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomepageSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomepageStatSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SubLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Icon = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ManualValue = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LastComputedValue = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LastComputedDisplay = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Visible = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    LastComputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomepageStatSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Intentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    BancoId = table.Column<int>(type: "integer", nullable: false),
                    ExamenId = table.Column<int>(type: "integer", nullable: true),
                    Modo = table.Column<string>(type: "text", nullable: false),
                    TotalPreguntas = table.Column<int>(type: "integer", nullable: false),
                    Aciertos = table.Column<int>(type: "integer", nullable: false),
                    Porcentaje = table.Column<decimal>(type: "numeric", nullable: false),
                    Aprobado = table.Column<bool>(type: "boolean", nullable: false),
                    TiempoSegundos = table.Column<int>(type: "integer", nullable: false),
                    InicioEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Intentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntentosPreguntas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntentoId = table.Column<int>(type: "integer", nullable: false),
                    PreguntaId = table.Column<int>(type: "integer", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentosPreguntas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveDoubts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    VoteCount = table.Column<int>(type: "integer", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveDoubts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HostUserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JoinCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BankId = table.Column<int>(type: "integer", nullable: false),
                    ConfigJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CurrentQuestionIndex = table.Column<int>(type: "integer", nullable: false),
                    RevealCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    QuestionOpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QuestionClosesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaterialesGrupo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    AutorId = table.Column<int>(type: "integer", nullable: false),
                    Modulo = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: true),
                    ContenidoTexto = table.Column<string>(type: "text", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialesGrupo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MembershipEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PlanPriceCop = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mensaje = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Leida = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeidaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GrupoId = table.Column<int>(type: "integer", nullable: true),
                    RelatedEntity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    RelatedId = table.Column<int>(type: "integer", nullable: true),
                    Link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Prioridad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Archivada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AcademicEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MembershipEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AdminEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SystemEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "PracticalVehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Plate = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticalVehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Preguntas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreadoPorId = table.Column<int>(type: "integer", nullable: true),
                    BancoId = table.Column<int>(type: "integer", nullable: false),
                    BloqueId = table.Column<int>(type: "integer", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Materia = table.Column<string>(type: "text", nullable: true),
                    Tema = table.Column<string>(type: "text", nullable: true),
                    Subtema = table.Column<string>(type: "text", nullable: true),
                    Dificultad = table.Column<string>(type: "text", nullable: true),
                    ImagenUrl = table.Column<string>(type: "text", nullable: true),
                    Fuente = table.Column<string>(type: "text", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Explicacion = table.Column<string>(type: "text", nullable: true),
                    PorQueIncorrectas = table.Column<string>(type: "text", nullable: true),
                    Pista = table.Column<string>(type: "text", nullable: true),
                    Activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preguntas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PresentacionDiapositivas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PresentationId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    BackgroundJson = table.Column<string>(type: "text", nullable: false),
                    ElementsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresentacionDiapositivas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Presentaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<int>(type: "integer", nullable: false),
                    SchoolId = table.Column<int>(type: "integer", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SlideCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presentaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PresentationMediaBlobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresentationMediaBlobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RespuestasIntento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntentoId = table.Column<int>(type: "integer", nullable: false),
                    PreguntaId = table.Column<int>(type: "integer", nullable: false),
                    OpcionId = table.Column<int>(type: "integer", nullable: true),
                    EsCorrecta = table.Column<bool>(type: "boolean", nullable: false),
                    PreguntaTextoSnapshot = table.Column<string>(type: "text", nullable: true),
                    OpcionSeleccionadaSnapshot = table.Column<string>(type: "text", nullable: true),
                    OpcionCorrectaSnapshot = table.Column<string>(type: "text", nullable: true),
                    TipoPreguntaSnapshot = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespuestasIntento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolApprenticeProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    StudentUserId = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    DocumentNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EnrollmentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EnrollmentMonth = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OrderNumber = table.Column<int>(type: "integer", nullable: true),
                    ScheduleSlot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AmountDue = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric", nullable: false),
                    BalanceDue = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BalancePaymentAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    AccountsReceivable = table.Column<decimal>(type: "numeric", nullable: false),
                    BalancePaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BalancePaymentMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BalanceReceiptNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    EnrollmentPin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RuntRegistered = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnrolled = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolApprenticeProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolJoinRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeacherUserId = table.Column<int>(type: "integer", nullable: false),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolJoinRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LegalName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    TaxId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BillingEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Department = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlanPriceCop = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedPlanCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SubscriptionStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RenewalStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PaymentProofUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProofSubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastDecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MembershipStartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MembershipEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TeachersMaxOverride = table.Column<int>(type: "integer", nullable: true),
                    StudentsMaxOverride = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolStudentEnrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    StudentUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttendanceDayType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LicenseCategories = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TheoryExamAuthorized = table.Column<bool>(type: "boolean", nullable: false),
                    TheoryExamAuthorizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PracticalAuthorized = table.Column<bool>(type: "boolean", nullable: false),
                    PracticalAuthorizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolStudentEnrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentDailyCheckIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentUserId = table.Column<int>(type: "integer", nullable: false),
                    CheckInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentDailyCheckIns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TheoryClassrooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Identifier = table.Column<string>(type: "text", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheoryClassrooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TheoryExamAppointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    ExamDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SlotTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    StudentUserId = table.Column<int>(type: "integer", nullable: true),
                    StudentLabel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NoShow = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheoryExamAppointments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TheoryTopics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheoryTopics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TheoryTrainingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    DefaultDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinCancelHours = table.Column<int>(type: "integer", nullable: false),
                    ReservationCloseMinutesBefore = table.Column<int>(type: "integer", nullable: false),
                    RequiredTheoryHours = table.Column<int>(type: "integer", nullable: false),
                    RequiredWorkshopHours = table.Column<int>(type: "integer", nullable: false),
                    TheoryExamId = table.Column<int>(type: "integer", nullable: true),
                    WeekdaysEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SaturdayEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyReservationOpen = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyClassReminder24h = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyClassReminder1h = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyExamReminder24h = table.Column<bool>(type: "boolean", nullable: false),
                    MaxWeekdayClassesPerDay = table.Column<int>(type: "integer", nullable: false),
                    MaxSaturdayClassesPerDay = table.Column<int>(type: "integer", nullable: false),
                    MaxDailyTheoryMinutes = table.Column<int>(type: "integer", nullable: false),
                    WeekdayReservationOpenDaysBefore = table.Column<int>(type: "integer", nullable: false),
                    SaturdayReservationOpenDaysBefore = table.Column<int>(type: "integer", nullable: false),
                    StudentBookingWindowStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    StudentBookingWindowEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    LicenseCategoryPoliciesJson = table.Column<string>(type: "text", nullable: true),
                    SavedBookingPresetsJson = table.Column<string>(type: "text", nullable: true),
                    HiddenBookingPresetKeysJson = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheoryTrainingSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Rol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SchoolId = table.Column<int>(type: "integer", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UltimoAccesoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DebeCambiarClave = table.Column<bool>(type: "boolean", nullable: false),
                    EmailConfirmado = table.Column<bool>(type: "boolean", nullable: false),
                    EmailCodigoHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EmailCodigoExpiraEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Valoraciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    BancoId = table.Column<int>(type: "integer", nullable: true),
                    IntentoId = table.Column<int>(type: "integer", nullable: true),
                    Estrellas = table.Column<int>(type: "integer", nullable: false),
                    Comentario = table.Column<string>(type: "text", nullable: true),
                    Critica = table.Column<string>(type: "text", nullable: true),
                    Revisada = table.Column<bool>(type: "boolean", nullable: false),
                    Oculta = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Valoraciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveDoubtVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DoubtId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveDoubtVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveDoubtVotes_LiveDoubts_DoubtId",
                        column: x => x.DoubtId,
                        principalTable: "LiveDoubts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiveParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ParticipantToken = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsConnected = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveParticipants_LiveSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "LiveSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiveSessionQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Difficulty = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsSurprise = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveSessionQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveSessionQuestions_LiveSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "LiveSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticalLessonSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    InstructorUserId = table.Column<int>(type: "integer", nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticalLessonSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticalLessonSessions_PracticalVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "PracticalVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpcionesPregunta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PreguntaId = table.Column<int>(type: "integer", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    EsCorrecta = table.Column<bool>(type: "boolean", nullable: false),
                    ImagenUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpcionesPregunta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpcionesPregunta_Preguntas_PreguntaId",
                        column: x => x.PreguntaId,
                        principalTable: "Preguntas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TheoryClassSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolUserId = table.Column<int>(type: "integer", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false),
                    InstructorUserId = table.Column<int>(type: "integer", nullable: true),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReservationOpenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReservationCloseAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CancelledByUserId = table.Column<int>(type: "integer", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheoryClassSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TheoryClassSessions_TheoryClassrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "TheoryClassrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TheoryClassSessions_TheoryTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "TheoryTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiveAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionQuestionId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    OptionId = table.Column<int>(type: "integer", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    AnsweredAtMs = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveAnswers_LiveSessionQuestions_SessionQuestionId",
                        column: x => x.SessionQuestionId,
                        principalTable: "LiveSessionQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticalLessonReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LessonSessionId = table.Column<int>(type: "integer", nullable: false),
                    StudentUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticalLessonReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticalLessonReservations_PracticalLessonSessions_LessonS~",
                        column: x => x.LessonSessionId,
                        principalTable: "PracticalLessonSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TheoryAttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassSessionId = table.Column<int>(type: "integer", nullable: false),
                    StudentUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MarkedByUserId = table.Column<int>(type: "integer", nullable: true),
                    MarkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheoryAttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TheoryAttendanceRecords_TheoryClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "TheoryClassSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TheoryClassReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassSessionId = table.Column<int>(type: "integer", nullable: false),
                    StudentUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheoryClassReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TheoryClassReservations_TheoryClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "TheoryClassSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprenticePaymentAbonos_SchoolUserId_StudentUserId_PaymentD~",
                table: "ApprenticePaymentAbonos",
                columns: new[] { "SchoolUserId", "StudentUserId", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Bancos_CreadoPorId",
                table: "Bancos",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogMediaBlobs_CreatedAt",
                table: "CatalogMediaBlobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogMediaBlobs_OwnerId",
                table: "CatalogMediaBlobs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentAuthorizationEvents_SchoolUserId_StudentUserId_Cr~",
                table: "EnrollmentAuthorizationEvents",
                columns: new[] { "SchoolUserId", "StudentUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EntregasActividad_ActividadId_UsuarioId",
                table: "EntregasActividad",
                columns: new[] { "ActividadId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamenesGrupos_ExamenId_GrupoId",
                table: "ExamenesGrupos",
                columns: new[] { "ExamenId", "GrupoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grupos_Codigo",
                table: "Grupos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomepageAudits_CreatedAt",
                table: "HomepageAudits",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_HomepageStatSettings_Key",
                table: "HomepageStatSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Intentos_OpenExam",
                table: "Intentos",
                columns: new[] { "UsuarioId", "ExamenId" },
                unique: true,
                filter: "\"FinEn\" IS NULL AND \"ExamenId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LiveAnswers_ParticipantId",
                table: "LiveAnswers",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveAnswers_SessionQuestionId_ParticipantId",
                table: "LiveAnswers",
                columns: new[] { "SessionQuestionId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveDoubts_SessionId",
                table: "LiveDoubts",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveDoubtVotes_DoubtId_ParticipantId",
                table: "LiveDoubtVotes",
                columns: new[] { "DoubtId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveParticipants_ParticipantToken",
                table: "LiveParticipants",
                column: "ParticipantToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveParticipants_SessionId",
                table: "LiveParticipants",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveSessionQuestions_SessionId_SortOrder",
                table: "LiveSessionQuestions",
                columns: new[] { "SessionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveSessions_HostUserId",
                table: "LiveSessions",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveSessions_JoinCode",
                table: "LiveSessions",
                column: "JoinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipEvents_CreatedAt",
                table: "MembershipEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipEvents_EventType",
                table: "MembershipEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipEvents_SchoolUserId",
                table: "MembershipEvents",
                column: "SchoolUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_UsuarioId_CreadoEn",
                table: "Notificaciones",
                columns: new[] { "UsuarioId", "CreadoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_UsuarioId_DedupeKey",
                table: "Notificaciones",
                columns: new[] { "UsuarioId", "DedupeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_UsuarioId_Leida_Archivada",
                table: "Notificaciones",
                columns: new[] { "UsuarioId", "Leida", "Archivada" });

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesPregunta_PreguntaId",
                table: "OpcionesPregunta",
                column: "PreguntaId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticalLessonReservations_LessonSessionId_StudentUserId",
                table: "PracticalLessonReservations",
                columns: new[] { "LessonSessionId", "StudentUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticalLessonSessions_SchoolUserId_SessionDate",
                table: "PracticalLessonSessions",
                columns: new[] { "SchoolUserId", "SessionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PracticalLessonSessions_VehicleId",
                table: "PracticalLessonSessions",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticalVehicles_SchoolUserId",
                table: "PracticalVehicles",
                column: "SchoolUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PresentacionDiapositivas_PresentationId_Position",
                table: "PresentacionDiapositivas",
                columns: new[] { "PresentationId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_Presentaciones_OwnerId",
                table: "Presentaciones",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Presentaciones_UpdatedAt",
                table: "Presentaciones",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PresentationMediaBlobs_CreatedAt",
                table: "PresentationMediaBlobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PresentationMediaBlobs_OwnerId",
                table: "PresentationMediaBlobs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasIntento_Attempt_Question",
                table: "RespuestasIntento",
                columns: new[] { "IntentoId", "PreguntaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApprenticeProfiles_SchoolUserId_DocumentNumber",
                table: "SchoolApprenticeProfiles",
                columns: new[] { "SchoolUserId", "DocumentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApprenticeProfiles_SchoolUserId_StudentUserId",
                table: "SchoolApprenticeProfiles",
                columns: new[] { "SchoolUserId", "StudentUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolJoinRequests_SchoolUserId_Status",
                table: "SchoolJoinRequests",
                columns: new[] { "SchoolUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolJoinRequests_TeacherUserId_Status",
                table: "SchoolJoinRequests",
                columns: new[] { "TeacherUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolProfiles_SubscriptionStatus",
                table: "SchoolProfiles",
                column: "SubscriptionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolProfiles_TaxId",
                table: "SchoolProfiles",
                column: "TaxId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolProfiles_UserId",
                table: "SchoolProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudentEnrollments_SchoolUserId_StudentUserId",
                table: "SchoolStudentEnrollments",
                columns: new[] { "SchoolUserId", "StudentUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudentEnrollments_StudentUserId",
                table: "SchoolStudentEnrollments",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDailyCheckIns_StudentUserId_CheckInDate",
                table: "StudentDailyCheckIns",
                columns: new[] { "StudentUserId", "CheckInDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TheoryAttendanceRecords_ClassSessionId_StudentUserId",
                table: "TheoryAttendanceRecords",
                columns: new[] { "ClassSessionId", "StudentUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TheoryClassReservations_ClassSessionId_StudentUserId",
                table: "TheoryClassReservations",
                columns: new[] { "ClassSessionId", "StudentUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TheoryClassReservations_StudentUserId",
                table: "TheoryClassReservations",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TheoryClassrooms_SchoolUserId",
                table: "TheoryClassrooms",
                column: "SchoolUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TheoryClassSessions_ClassroomId",
                table: "TheoryClassSessions",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_TheoryClassSessions_ReservationCloseAt",
                table: "TheoryClassSessions",
                column: "ReservationCloseAt");

            migrationBuilder.CreateIndex(
                name: "IX_TheoryClassSessions_ReservationOpenAt",
                table: "TheoryClassSessions",
                column: "ReservationOpenAt");

            migrationBuilder.CreateIndex(
                name: "IX_TheoryClassSessions_SchoolUserId_SessionDate",
                table: "TheoryClassSessions",
                columns: new[] { "SchoolUserId", "SessionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TheoryClassSessions_TopicId",
                table: "TheoryClassSessions",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_TheoryExamAppointments_SchoolUserId_ExamDate_SlotTime",
                table: "TheoryExamAppointments",
                columns: new[] { "SchoolUserId", "ExamDate", "SlotTime" });

            migrationBuilder.CreateIndex(
                name: "IX_TheoryTopics_SchoolUserId_Name",
                table: "TheoryTopics",
                columns: new[] { "SchoolUserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_TheoryTrainingSettings_SchoolUserId",
                table: "TheoryTrainingSettings",
                column: "SchoolUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_SchoolId",
                table: "Usuarios",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_UltimoAccesoEn",
                table: "Usuarios",
                column: "UltimoAccesoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Valoraciones_IntentoId",
                table: "Valoraciones",
                column: "IntentoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActividadesGrupo");

            migrationBuilder.DropTable(
                name: "ApprenticePaymentAbonos");

            migrationBuilder.DropTable(
                name: "AvisosGrupo");

            migrationBuilder.DropTable(
                name: "Bancos");

            migrationBuilder.DropTable(
                name: "Bloques");

            migrationBuilder.DropTable(
                name: "CatalogMediaBlobs");

            migrationBuilder.DropTable(
                name: "EnrollmentAuthorizationEvents");

            migrationBuilder.DropTable(
                name: "EntregasActividad");

            migrationBuilder.DropTable(
                name: "Examenes");

            migrationBuilder.DropTable(
                name: "ExamenesGrupos");

            migrationBuilder.DropTable(
                name: "ExamenesPreguntas");

            migrationBuilder.DropTable(
                name: "Grupos");

            migrationBuilder.DropTable(
                name: "GruposUsuarios");

            migrationBuilder.DropTable(
                name: "HomepageAudits");

            migrationBuilder.DropTable(
                name: "HomepageSettings");

            migrationBuilder.DropTable(
                name: "HomepageStatSettings");

            migrationBuilder.DropTable(
                name: "Intentos");

            migrationBuilder.DropTable(
                name: "IntentosPreguntas");

            migrationBuilder.DropTable(
                name: "LiveAnswers");

            migrationBuilder.DropTable(
                name: "LiveDoubtVotes");

            migrationBuilder.DropTable(
                name: "LiveParticipants");

            migrationBuilder.DropTable(
                name: "MaterialesGrupo");

            migrationBuilder.DropTable(
                name: "MembershipEvents");

            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "OpcionesPregunta");

            migrationBuilder.DropTable(
                name: "PracticalLessonReservations");

            migrationBuilder.DropTable(
                name: "PresentacionDiapositivas");

            migrationBuilder.DropTable(
                name: "Presentaciones");

            migrationBuilder.DropTable(
                name: "PresentationMediaBlobs");

            migrationBuilder.DropTable(
                name: "RespuestasIntento");

            migrationBuilder.DropTable(
                name: "SchoolApprenticeProfiles");

            migrationBuilder.DropTable(
                name: "SchoolJoinRequests");

            migrationBuilder.DropTable(
                name: "SchoolProfiles");

            migrationBuilder.DropTable(
                name: "SchoolStudentEnrollments");

            migrationBuilder.DropTable(
                name: "StudentDailyCheckIns");

            migrationBuilder.DropTable(
                name: "TheoryAttendanceRecords");

            migrationBuilder.DropTable(
                name: "TheoryClassReservations");

            migrationBuilder.DropTable(
                name: "TheoryExamAppointments");

            migrationBuilder.DropTable(
                name: "TheoryTrainingSettings");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Valoraciones");

            migrationBuilder.DropTable(
                name: "LiveSessionQuestions");

            migrationBuilder.DropTable(
                name: "LiveDoubts");

            migrationBuilder.DropTable(
                name: "Preguntas");

            migrationBuilder.DropTable(
                name: "PracticalLessonSessions");

            migrationBuilder.DropTable(
                name: "TheoryClassSessions");

            migrationBuilder.DropTable(
                name: "LiveSessions");

            migrationBuilder.DropTable(
                name: "PracticalVehicles");

            migrationBuilder.DropTable(
                name: "TheoryClassrooms");

            migrationBuilder.DropTable(
                name: "TheoryTopics");
        }
    }
}
