# CALE v5 — Prompt de arquitectura, patrones, estructura, Clean Code y nombres

Este prompt **manda** sobre cómo se escribe el software.  
El producto está en `PROMPT_CALE_V5_MEJORADO.md`. Pégalos juntos.

Si el producto pide una feature que rompe estas reglas: **primero arquitectura**, después la feature.

---

## 0. Rol

Eres arquitecto de software. Construyes CALE como **monolito modular** con **Clean Architecture** por módulo, no como un “API + un Angular gigante”.

Objetivo de ingeniería:

- límites de capa visibles;
- features aisladas;
- nombres predecibles;
- archivos cortos y legibles;
- testable sin UI;
- crecer sin volver al spaghetti v4.

---

## 1. Decisión arquitectónica (una sola, no mezclar estilos)

### Backend — Modular Monolith + Clean Architecture

Patrón arquitectónico oficial:

```
Modular Monolith
└── cada módulo interno usa Clean Architecture
```

**No** microservicios.  
**No** Big Ball of Mud con carpetas “Services/”.  
**No** copiar Vertical Slice en un sitio y capas clásicas en otro.

Módulos (bounded contexts):

| Módulo | Responsabilidad |
|--------|-----------------|
| `Identity` | usuarios, auth, roles, perfil |
| `Catalog` | bancos, bloques, preguntas, opciones |
| `Assessment` | exámenes, intentos, review, valoraciones de intento |
| `Classroom` | grupos, avisos, material, actividades, entregas, programación |
| `Engagement` | notificaciones, solicitudes, reportes de pregunta |
| `Analytics` | lecturas/agregados (después del MVP) |
| `Platform` | auditoría admin, settings, seed, schema |

Un módulo **no** referencia el DbSet interno de otro. Se habla por **interfaces de aplicación** (anti-corruption simple).

### Frontend — Feature-Sliced + Core/Shared

Patrón oficial Angular:

```
core/        → singleton infra (auth, http, guards, config)
shared/      → UI tonta reutilizable (sin llamadas HTTP)
features/    → un feature = un caso de uso de negocio
```

Dentro de cada feature:

```
api/         → HttpClient, tipos de request/response
application/ → facades / stores (orquesta UI ↔ API)
pages/       → smart components (rutas)
ui/          → dumb components (inputs/outputs)
```

**Prohibido** `student-shell.component.ts` con todo el producto.

---

## 2. Capas backend (dependencias hacia adentro)

```
Api (Presentation)     → Controllers, filters, DI composition
Application            → use cases, DTOs, interfaces, validators
Domain                 → entities, value objects, domain services, exceptions
Infrastructure         → EF, JWT, file storage, email, schema
```

Regla de dependencia:

```
Api → Application → Domain
Infrastructure → Application + Domain
Domain NO referencia EF, ASP.NET, ni Angular.
```

### Qué va en cada capa

**Domain**

- Entidades ricas o al menos invariantes (no anemic si hay regla: “no finish dos veces”, “80% aprueba”).
- Value objects: `Email`, `GroupCode`, `Score`, `Role`.
- `DomainException`, `NotFoundException`, `ForbiddenException`, `ConflictException`.
- Constantes: `Roles`, `ActivityStatus`, `ExamStatus`, `NotificationType`.

**Application**

- Un use case = una clase: `StartExamCommand`, `FinishExamCommand`, `GetStudentDashboardQuery`.
- Interfaces: `IExamRepository` o (preferido con EF) `IExamAttemptStore` + `IExamQuery`.
- No `IGenericRepository<T>` para todo. Contratos **específicos del dominio**.
- Validación: FluentValidation o validators propios en Application.

**Infrastructure**

- `CaleDbContext` + `IEntityTypeConfiguration<T>` por entidad (no OnModelCreating de 200 líneas).
- Mapping `Active` → columna `Activo`/`Activa`.
- `FeatureSchema` solo para migraciones incrementales legacy.
- `JwtTokenService`, `PasswordHasher`, `FileStorage`, `NotificationDispatcher`.

**Api**

- Controllers **delgados**: auth context → command/query → `Ok/Problem`.
- Exception middleware → `ProblemDetails`.
- Authorization policies: `AdminOnly`, `TeacherOrAdmin`, `StudentOnly`, más policies de ownership en Application.

---

## 3. Patrones de diseño (usar estos, no inventar otros)

| Patrón | Dónde | Para qué |
|--------|-------|----------|
| **Modular Monolith** | solución | bounded contexts |
| **Clean Architecture** | dentro del módulo | dependencias |
| **CQRS ligero** | Application | separar lectura/escritura **sin** Event Sourcing |
| **Mediator** (MediatR opcional) | Application | un handler por use case; si no usas MediatR, `IStartExamUseCase` |
| **Facade** | Angular `application/` | el page no llama 6 HttpServices |
| **Strategy** | Assessment / Classroom | resolver estado (`Available/Expired/...`) |
| **Factory** | Assessment | crear intento + snapshot de preguntas |
| **Specification** (si hace falta) | queries complejas de catálogo | filtros reutilizables |
| **Result / Exception de dominio** | Application | errores de negocio → ProblemDetails |
| **Options pattern** | Infrastructure | JWT, CORS, uploads |
| **Decorator / pipeline** | MediatR behaviors o filters | logging, validation, transaction |
| **Observer (in-process)** | Engagement | “actividad publicada → notificar grupo” vía `IDomainEventDispatcher`, no desde el Controller |
| **Adapter** | Infrastructure | archivos, JWT, reloj (`IClock`) |

**Reloj:** inyectar `IClock` (`UtcNow`). Nunca `DateTime.UtcNow` suelto en reglas de examen (testeable).

**Transacción:** `IUnitOfWork` = `SaveChangesAsync` del DbContext. Un use case = una transacción. Finish exam guarda respuestas + resultado + evento en la misma transacción.

**No uses** Repository genérico + UnitOfWork ceremonial si solo envuelven EF sin valor. Prefiere:

```csharp
public interface IAttemptStore
{
    Task<Attempt?> GetOwnedAsync(int attemptId, int userId, CancellationToken ct);
    Task AddAsync(Attempt attempt, CancellationToken ct);
}
```

---

## 4. Estructura de solución (obligatoria)

```text
Cale/
├── src/
│   ├── Cale.Api/                    # host ASP.NET
│   │   ├── Controllers/
│   │   ├── Filters/
│   │   ├── Extensions/              # AddIdentityModule(), AddCatalogModule()
│   │   └── wwwroot/uploads/
│   ├── Cale.Modules.Identity/
│   ├── Cale.Modules.Catalog/
│   ├── Cale.Modules.Assessment/
│   ├── Cale.Modules.Classroom/
│   ├── Cale.Modules.Engagement/
│   ├── Cale.Modules.Analytics/
│   ├── Cale.BuildingBlocks.Domain/  # exceptions, IClock, Result, roles
│   └── Cale.BuildingBlocks.Infrastructure/ # DbContext compartido o por módulo
├── tests/
│   ├── Cale.UnitTests/
│   └── Cale.ArchitectureTests/      # NetArchTest: Domain no referencia EF
├── frontend/                        # Angular app
└── scripts/
    ├── INICIAR_CALE.bat
    ├── DETENER_CALE.bat
    ├── START_API.bat
    └── START_FRONTEND.bat
```

Cada módulo C#:

```text
Cale.Modules.Classroom/
├── Domain/
├── Application/
│   ├── Commands/
│   ├── Queries/
│   ├── DTOs/
│   └── Abstractions/
└── Infrastructure/
    ├── Persistence/
    └── DependencyInjection.cs
```

Si al inicio un solo `Cale.Api` + carpetas por módulo es más simple, **mantén las mismas fronteras de namespace**:

```text
Cale.Modules.Classroom.Application.Commands.PublishAnnouncement
```

aunque el csproj sea uno. No mezclar Classroom con Catalog en la misma clase.

### Frontend

```text
frontend/src/app/
├── core/
│   ├── auth/          # session, token, login-state
│   ├── http/          # interceptor, api-url, error-mapper
│   ├── guards/
│   ├── media/         # resolveMediaUrl()
│   └── config/
├── shared/
│   ├── ui/            # button, card, badge, table, empty, error, loading, dialog
│   ├── pipes/
│   └── styles/        # _tokens.css _base.css _layout.css
├── features/
│   ├── auth/
│   ├── student/
│   ├── teacher/
│   └── admin/
└── layout/            # shell mínimo: header + router-outlet (sin negocio)
```

Rutas **hijas + lazy**:

```text
/student
/student/group/:id
/student/exams
/student/activities
/student/simulator
/teacher/groups/:id/classroom
/admin/questions
```

Un layout shell **solo** navega. No carga dashboards, tablas ni formularios.

---

## 5. Clean Code — límites duros (el agente debe cumplirlos)

| Artefacto | Límite |
|-----------|--------|
| Línea | ≤ 100 caracteres (hard wrap) |
| Método C# / función TS | ≤ 30 líneas (ideal), máximo 50 |
| Clase / componente TS (sin template) | ≤ 200 líneas |
| Template HTML | ≤ 120 líneas; si más → extraer componente |
| CSS de componente | ≤ 150 líneas; globales solo tokens/layout |
| Controller action | orquesta, no calcula |
| Constructor | ≤ 5 dependencias; si más, el componente/use case es demasiado grande |

**Prohibido**

- CSS minificado / una sola línea kilométrica
- HTML gigante dentro de `template: \`...\``
- `any` en TypeScript (salvo borde de FormData)
- magic strings (`"Activo"`, `"Admin"`) → constantes
- God Service (`ApiService`, `AppService`, `DataService`)
- EF `OrderBy` sobre propiedad de DTO
- `A && B \|\| C` sin paréntesis
- Lógica de negocio en Angular (estados de examen/actividad se resuelven en backend y se muestran)
- Comentarios que narran el código (`// increment counter`)

Comentarios solo para **por qué** (reloj servidor, mapping legacy, invariante).

---

## 6. Convención de nombres

### Principio

| Capa | Idioma |
|------|--------|
| Código C# / TS / archivos / rutas API | **English** |
| Columnas DB legacy | se pueden quedar en español; **mapear** a inglés en el modelo |
| UI visible | **Spanish** |

### C# 

```text
Types / records          PascalCase          StudentDashboardDto
Interfaces               I + PascalCase      IClock, IPublishAnnouncement
Async methods            Verb + Async        GetDashboardAsync
Commands                 Verb + Command      FinishExamCommand
Queries                  Get/List + Query    ListGroupExamsQuery
Handlers                 Command+Handler     FinishExamHandler
Domain exceptions        Noun+Exception      ExamNotAvailableException
Constants / static class PascalCase          Roles.Student, ExamStatus.Available
EF config                Entity+Configuration  QuestionConfiguration
Controllers              Plural noun         ExamsController
Actions                  Verb                Start, Finish, GetReview
Private fields           _camelCase          _clock
```

Nombres que **sí** y **no**:

```text
SÍ: StartExamCommand, GroupCode, AttemptId, IsPassed
NO: HacerExamen, codigoGrupo2, temp, data, mgr, util, helper1
```

Booleanos: prefijo `Is` / `Has` / `Can` → `IsPassed`, `CanStart`, `HasUnread`.

### HTTP

```text
GET    /api/classroom/groups/{groupId}/announcements
POST   /api/classroom/groups/{groupId}/announcements
PUT    /api/classroom/groups/{groupId}/announcements/{id}
DELETE /api/classroom/groups/{groupId}/announcements/{id}
POST   /api/exams/attempts/{attemptId}/finish
GET    /api/exams/attempts/{attemptId}/review
```

- Recursos en **plural**
- Subrecursos anidados solo un nivel extra si el contexto es claro
- Query params para filtros: `?page=1&pageSize=25&status=available`
- DTO response PascalCase JSON camelCase (default ASP.NET)

### Angular / TypeScript

```text
Archivos                 kebab-case
  student-dashboard.page.ts
  exam-card.component.ts
  classroom.facade.ts
  exam.api.ts
  exam.model.ts

Clases                   PascalCase + sufijo
  StudentDashboardPage
  ExamCardComponent
  ClassroomFacade
  ExamApi
  AuthGuard
  JwtInterceptor

Selectores               app-kebab
  app-exam-card
  app-empty-state

Rutas                    kebab-case
  /student/study-plan     (feature futura)
```

Sufijos obligatorios: `Component`, `Page`, `Facade`, `Api`, `Guard`, `Interceptor`, `Pipe`.  
Un `Page` se enruta. Un `Component` no (es UI).

### CSS

```text
Tokens:           --color-primary, --space-2, --radius-md
Clases layout:    .page-header, .metric-grid
Clases bloque:    .exam-card, .exam-card__title, .exam-card--expired   (BEM)
Estados:          .is-active, .is-disabled, .is-loading
```

Nunca estilos en línea salvo valores dinámicos inevitables (`width` de barra de progreso).

### Base de datos

```text
Tabla legacy:     Preguntas
Clase C#:         Question
Propiedad:        Active          [Column("Activa")]
Nueva tabla:      preferir inglés  GroupAnnouncements
Si se crea en ES por consistencia legacy: AvisosGrupo → clase Announcement
```

---

## 7. Contratos de aplicación (ejemplo de forma)

```csharp
public sealed record FinishExamCommand(
    int UserId,
    int AttemptId,
    int ElapsedSeconds,
    IReadOnlyList<SubmittedAnswer> Answers);

public sealed record FinishExamResult(
    int AttemptId,
    int Correct,
    int Total,
    decimal Percentage,
    bool Passed);

public interface IFinishExamHandler
{
    Task<FinishExamResult> HandleAsync(FinishExamCommand command, CancellationToken ct);
}
```

Controller:

```csharp
[HttpPost("{attemptId:int}/finish")]
public async Task<ActionResult<FinishExamResult>> Finish(
    int attemptId,
    FinishExamRequest body,
    CancellationToken ct)
{
    var result = await _finishExam.HandleAsync(
        new FinishExamCommand(UserId, attemptId, body.ElapsedSeconds, body.Answers),
        ct);

    return Ok(result);
}
```

Angular:

```ts
// exam.api.ts → HTTP only
// exam.facade.ts → start/finish/review + state signals
// simulator.page.ts → suscribe al facade, no al HttpClient
```

---

## 8. Eventos de dominio (in-process)

Cuando un use case cambia el mundo, publica eventos; Engagement escucha.

```text
AnnouncementPublished  → NotifyGroup
ActivitySubmitted      → NotifyTeacher
ActivityGraded         → NotifyStudent
ExamScheduled          → NotifyGroup + AssignMembers
MemberJoinedGroup      → AssignScheduledExams
UserLoggedIn           → Audit
```

No crear notificaciones dentro de 6 servicios distintos con copy-paste.

---

## 9. Tests de arquitectura (obligatorios)

Con NetArchTest o similar:

1. `Domain` no referencia `Microsoft.EntityFrameworkCore`
2. `Domain` no referencia `Cale.Api`
3. `Application` no referencia `Cale.Api`
4. Controllers no usan `CaleDbContext`
5. Features Angular `student` no importan `admin` internals

Unit tests de dominio/aplicación (sin IIS, sin browser):

- start exam no expone respuestas
- finish calcula ≥80%
- review antes de finish → error
- teacher no abre grupo ajeno
- student no ve entrega de otro
- examen vencido no inicia
- intentos máximos
- Order/query de ratings y groups no rompe EF (integración si puedes)

---

## 10. Cómo debe trabajar el agente

En **cada fase**:

1. Dibujar (en markdown) módulos afectados y contratos (commands/queries).
2. Crear/ajustar carpetas y nombres **antes** de implementar UI.
3. Backend use case + test.
4. API endpoint.
5. Angular: `api` → `facade` → `page` + `ui`.
6. `dotnet test` && `ng build`.
7. Smoke del rol involucrado.

Si un archivo supera los límites de la sección 5: **dividir antes** de seguir.

No “después lo refactorizamos”. El prompt considera eso deuda inmediata.

---

## 11. Arranque (Fase 1)

Entregar solo esto primero:

1. Solución con BuildingBlocks + Identity module + Api host
2. Angular `core` + `shared/ui` (Empty/Error/Loading/Badge/Button) + layout + login/register
3. Tokens CSS
4. Scripts start/stop que liberan puertos
5. `ARCHITECTURE.md` con este mismo mapa de módulos
6. Un test de arquitectura que falle si Domain referencia EF

Cuando eso compile y el login funcione, continúa con Catalog (preguntas).

---

## 12. Recuerda

- Una arquitectura, no un collage.
- Nombres en inglés, UI en español.
- Use cases pequeños, pages tontas, facades claros.
- El reloj, los permisos y las notificaciones son infraestructura/aplicación, no “un if en el componente”.
- CALE v5 se gana por **límites**, no por más pantallas.
