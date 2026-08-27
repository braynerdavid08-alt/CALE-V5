# CALE v5

Plataforma educativa: simulador de conducción + aula virtual.
**Mi CALE** — tu CALE, en tu CEA.

## Stack

- ASP.NET Core 8 + EF Core + JWT
- Angular 18 standalone (PWA)
- DB: SQLite (dev/Docker) o SQL Server

## Arranque local (desarrollo)

```bat
scripts\INICIAR_CALE.bat
```

- UI: http://localhost:4200 (proxy → API)
- API: http://localhost:5000
- Swagger: http://localhost:5000/swagger

Credenciales demo:

| Rol | Email | Password |
|-----|-------|----------|
| Admin | `admin@cale.local` | `Admin123!` |
| Instructor | `profesor@cale.local` | `Profesor123!` |
| Estudiante | `estudiante@cale.local` | `Estudiante123!` |
| Escuela | `escuela@cale.local` | `Escuela123!` |

Detener: `scripts\DETENER_CALE.bat`

## Publicar como web (celular / internet)

Plano completo: [`docs/DEPLOY.md`](docs/DEPLOY.md)

```bat
scripts\PUBLISH_WEB.bat
```

O Docker:

```bat
docker compose up --build
```

Abrir http://127.0.0.1:8080 — desde el celular, la IP de tu PC en la misma Wi‑Fi.

## Verificar

```bat
dotnet test
cd frontend && npx ng build
```

## Módulos

Identity, Catalog, Assessment, Classroom, Engagement, Presentation  
Dashboards por rol, simulador, bancos oficiales, presentaciones del instructor.
