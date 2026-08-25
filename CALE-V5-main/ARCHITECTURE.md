# CALE v5 — Arquitectura

Monolito modular + Clean Architecture por módulo.
Frontend: Feature-Sliced (`core` / `shared` / `features` / `layout`).

## Mapa de módulos (backend)

| Módulo | Estado Fase 1 | Responsabilidad |
|--------|---------------|-----------------|
| `BuildingBlocks.Domain` | listo | Roles, IClock, excepciones, Result, puertos |
| `BuildingBlocks.Infrastructure` | listo | EF DbContext, JWT, hasher, reloj |
| `Identity` | listo | login, registro Student, perfil, seed admin |
| `Catalog` | listo | bancos, preguntas, opciones, exámenes |
| `Assessment` | listo | start/answer/finish/review + valoraciones |
| `Classroom` | listo | grupos, avisos, material, actividades |
| `Engagement` | listo | notificaciones reales |
| `Analytics` | después del MVP | lecturas / agregados |
| `Platform` | incremental | FeatureSchema (`ExpiresAt`) |

## Dependencias

```
Api → Modules.* → BuildingBlocks.Domain
Infrastructure → Domain
Domain NO referencia EF ni ASP.NET
```

Un módulo no usa el `DbSet` interno de otro. Habla por interfaces de aplicación.
`CaleDbContext` aplica `IEntityTypeConfiguration` por ensamblado registrado.

## Frontend

```
core/        auth, http, guards, media, config
shared/ui    button, badge, empty, error, loading
features/    auth, student, teacher, admin
layout/      shell: header + router-outlet (sin negocio)
```

## Reglas fijas de negocio

- Roles JWT: `Admin` | `Teacher` | `Student`
- Registro público → solo Student
- Aprobar ≥ 80% (Assessment)
- Reloj de examen: servidor (`IClock`)
- Start no expone respuestas correctas
- UI en español, API/código en inglés
- Cero datos ficticios

## MVP (hecho)

- Auth, Catalog, Assessment, Classroom, Engagement
- Dashboards de 3 roles con datos reales
- Simulador start/finish/review + cronómetro + volver al panel
- Preguntas con radio de respuesta correcta
- Aula: avisos, material, actividades, entregas y calificación
- Exámenes: crear, publicar y asignar a grupo

## Cómo arrancar

1. `scripts\INICIAR_CALE.bat` (libera `:5000` y `:4200` antes de compilar)
2. UI: http://localhost:4200
3. API: http://localhost:5000/swagger
4. Admin: `admin@cale.local` / `Admin123!`

## Tests de arquitectura

`tests/Cale.ArchitectureTests` falla si Domain referencia EF.
