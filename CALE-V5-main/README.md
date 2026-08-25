# CALE v5

Plataforma educativa: simulador de conducción + aula virtual.
Reconstrucción limpia (no es una copia del código v4).

## Stack

- ASP.NET Core 8 + EF Core + SQL Server + JWT
- Angular 18 standalone
- DB: `CaleSimuladorDb_Limpia` (Trusted Connection)

## Arranque

```bat
scripts\INICIAR_CALE.bat
```

- UI: http://localhost:4200
- API: http://localhost:5000
- Swagger: http://localhost:5000/swagger
- Admin: `admin@cale.local` / `Admin123!`
- Profesor: `profesor@cale.local` / `Profesor123!`
- Estudiante: `estudiante@cale.local` / `Estudiante123!`
- Escuela: registro público en `/register-school` (facturación + plan Mensual/Semestral/Anual)
- Demo escuela: `escuela@cale.local` / `Escuela123!`

Detener:

```bat
scripts\DETENER_CALE.bat
```

## Verificar

```bat
dotnet test
cd frontend && npx ng build
```

## Fase 1 (hecha)

- Solución Modular Monolith + Clean Architecture
- Identity: login, registro, JWT, perfil, seed admin
- Angular core / shared UI / layout / auth
- Tokens CSS (sin CSS de una sola línea)
- Scripts que liberan puertos (evita MSB3027)
- Tests unitarios + test de arquitectura (Domain ≠ EF)

## MVP (hecho)

- Catalog, Assessment, Classroom, Engagement
- Dashboards de 3 roles con datos reales (Admin, Docente, Estudiante)
- Simulador start/finish/review + volver al panel
- Preguntas con radio de respuesta correcta
- Bancos oficiales al arrancar la API (idempotente):
  - **Normas de tránsito (Colombia)** — 500 preguntas (Ley 769/2002, RUCT)
  - **Reconocimiento visual de señales** — 194 preguntas (Manual 2024) con imagen `/signals/{código}.svg`

Especificación: ver `ARCHITECTURE.md` y los prompts de producto/arquitectura.
