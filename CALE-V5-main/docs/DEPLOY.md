# Plano de mejora — Mi CALE en celular e internet

Objetivo: que cualquier persona abra **Mi CALE desde el navegador del celular** (y escritorio) como una **página web / PWA**, con HTTPS, sin depender de `localhost`.

---

## Estado actual (tras esta entrega)

| Capacidad | Estado |
|-----------|--------|
| UI responsive (drawer móvil) | Listo |
| PWA (manifest + SW + iconos) | Listo (básico) |
| API same-origin + SPA fallback | Listo |
| `apiUrl` configurable / vacío | Listo |
| Docker one-box | Listo |
| Seed demo solo en Development | Listo |
| CORS por entorno | Listo |
| HTTPS en el contenedor | Delegado al proxy (Cloudflare / nginx) |
| Auth HttpOnly / refresh tokens | Pendiente (Fase B) |
| CI/CD automático | Pendiente (Fase B) |
| Offline completo de chunks | Pendiente (Fase C) |

---

## Fase A — Publicar (HECHA en código)

1. Frontend llama a `/api` en el **mismo origen** (`apiUrl: ""` + `config.js`).
2. La API sirve el build de Angular desde `wwwroot` + `MapFallbackToFile`.
3. Docker multi-stage: build Angular + publish .NET → un contenedor en el puerto **8080**.
4. **PostgreSQL / SQL Server** vía `ConnectionStrings__Cale` (SQLite solo para dev local opcional).
5. Variables: `Jwt__Key`, `Cors__Origins`, `ConnectionStrings__Cale` o `DATABASE_URL`. **Nunca** actives `Seed:DemoUsers` en internet.

### Cómo probar en local (como en internet)

```bat
scripts\PUBLISH_WEB.bat
```

O con Docker:

```bat
set CALE_JWT_KEY=UNA-CLAVE-LARGA-DE-AL-MENOS-32-CARACTERES
docker compose up --build
```

Abrir: http://127.0.0.1:8080  

En el celular (misma Wi‑Fi): http://IP-DE-TU-PC:8080

### Cómo publicar en internet (resumen)

1. Comprar/usar un dominio y un VPS o PaaS (Railway, Fly.io, Azure, Contabo, etc.).
2. Poner TLS delante (Caddy / nginx / Cloudflare).
3. Desplegar la imagen Docker o el publish de `PUBLISH_WEB`.
4. Definir `Jwt__Key` fuerte y **no** activar `Seed__DemoUsers` en producción real.
5. Crear el primer admin manualmente (script SQL / endpoint interno) o un seed controlado.

---

## Fase B — Endurecer (siguiente)

1. CI (GitHub Actions): build + test + imagen.
2. Secretos solo por env / vault (nunca en git).
3. HSTS + HTTPS redirect en el edge.
4. Backups de DB y de `/uploads`.
5. Rate limit login; rotar JWT; valorar cookies HttpOnly.
6. Desactivar Swagger (ya off fuera de Development).
7. Monitoreo con `/api/health`.

---

## Fase C — Pulir móvil / PWA

1. Precache de assets hasheados (Angular SW o Workbox).
2. Toast “Nueva versión disponible”.
3. Icono maskable con safe-zone.
4. Auditoría editor de presentaciones en pantallas chicas (ver / presentar primero).
5. Open Graph con imagen social.
6. “Añadir a pantalla de inicio” guía en onboarding.

---

## Arquitectura recomendada (producción)

```
[Celular / PC]
      │  HTTPS
      ▼
[Cloudflare / Caddy / nginx]  ← TLS
      │
      ▼
[Contenedor Cale.Api :8080]
   ├── /           → Angular (wwwroot)
   ├── /api/*      → Controllers
   ├── /uploads/*  → Archivos
   └── SQLite o SQL Server
```

Same-origin evita dolores de CORS y mixed-content en el celular.

---

## Datos de usuario y seguridad (resumen)

| Dato | Dónde | Protección |
|------|--------|------------|
| Contraseña | Solo servidor (DB) | Hash ASP.NET Identity (PBKDF2); **nunca** se guarda en texto plano ni se envía al frontend |
| Perfil (nombre, email, rol) | DB en servidor | Acceso solo con JWT autenticado |
| Sesión (token) | `sessionStorage` en el navegador | Se borra al cerrar el navegador; no mostrar credenciales en la UI |
| Usuarios demo | Solo Development / `Seed:DemoUsers=true` | Desactivado en Production |

En producción: crea el primer admin por registro controlado o seed **offline**, cambia `Jwt__Key`, y usa disco/DB persistente para no perder cuentas.

### CALE Aula en Vivo (SignalR)

- Hub: `/hubs/live` (WebSocket; host JWT vía query `access_token`)
- Instructor: `/teacher/live` → crear sala → `/teacher/live/{id}/host` (QR + código)
- Estudiantes: `/live/join` o `/live/join/{CODE}` → `/live/play/{id}`
- Una sola instancia Render Free: SignalR in-memory OK (multi-instancia requeriría Redis)

### Torneos Luz Verde (SignalR)

- Hub: `/hubs/tournaments` (JWT vía query `access_token`)
- API: `/api/tournaments`
- El estado del torneo (inscripciones, bracket, respuestas, campeón, trofeos) **persiste en PostgreSQL** (`TournamentSchemaGuard` / tablas `Torneos*`, `TrofeosTorneo`).
- **Una instancia (Render Free actual):** SignalR in-memory es suficiente. Los clientes pueden recuperar estado con `GET /api/tournaments/{id}` y `GET /api/tournaments/matches/{id}` tras reconectar.
- **Varias instancias:** SignalR **no** replica grupos entre nodos sin backplane. Hace falta Redis:
  1. Servicio Redis gestionado (o Key Value en Render).
  2. Paquete `Microsoft.AspNetCore.SignalR.StackExchangeRedis`.
  3. En DI: `services.AddSignalR().AddStackExchangeRedis(Configuration["ConnectionStrings:Redis"])`.
  4. Variable `ConnectionStrings__Redis=...`.
- **Riesgo sin Redis al escalar:** un jugador puede unirse al hub en la instancia A y no recibir `MatchAssigned` emitido desde la instancia B. La DB sigue siendo la fuente de verdad, pero la UX en vivo se rompe.
- **No contratar Redis en esta fase** si se mantiene una sola instancia.

### Correo (verificación de cuenta)

**Cambiar la clave en Mi perfil no envía correo.** Solo se guarda en el servidor.

El código por email aplica al **registro** de cuentas nuevas cuando el envío está habilitado y hay un proveedor real.

Render **Free bloquea SMTP** (puertos 25/465/587). Usar **Hostinger Mail API (HTTPS)**.

#### Producción recomendada — Hostinger Mail API

1. En Hostinger → Email → **Agentic Mail → API access**: crear token (copiar una sola vez).
2. Con el token: `GET https://api.mail.hostinger.com/api/v1/me` y anotar el `resourceId` del buzón `notificaciones@cealuzverde.com`.
3. Revisar que la **allow list** de Agentic Mail no bloquee destinatarios reales (vacía = permitir todos).
4. En Render → Environment:

```env
Email__Enabled=true
Email__Provider=HostingerMail
Email__From=notificaciones@cealuzverde.com
Email__FromName=Luz Verde
Email__HostingerMail__ApiToken=REEMPLAZAR_TOKEN
Email__HostingerMail__MailboxResourceId=REEMPLAZAR_RESOURCE_ID
Email__HostingerMail__BaseUrl=https://api.mail.hostinger.com
Email__HostingerMail__TimeoutSeconds=15
```

5. Redeploy. Probar registro → pantalla `/verify-email` → código en el buzón.

`Email__Provider` admite: `HostingerMail` | `Smtp` | `Auto` (Hostinger → SMTP → log).

Si `Email__Enabled=true` pero falta token/mailbox (o SMTP host), **la API no arranca o falla de forma segura**; no auto-confirma usuarios.

#### Desarrollo local — SMTP opcional

```env
Email__Enabled=true
Email__Provider=Smtp
Email__From=notificaciones@cealuzverde.com
Email__FromName=Luz Verde
Email__Smtp__Host=smtp.hostinger.com
Email__Smtp__Port=587
Email__Smtp__User=notificaciones@cealuzverde.com
Email__Smtp__Password=REEMPLAZAR
Email__Smtp__UseSsl=true
```

Con `Email__Enabled=false` (default local) el registro **auto-confirma** y el cuerpo se escribe en logs (solo desarrollo).

#### Diagnóstico

| Síntoma | Causa probable |
|---------|----------------|
| `Network is unreachable` / SMTP | Plan Free de Render + SMTP |
| `email_delivery_misconfigured` | Enabled sin proveedor completo |
| `email_send_failed` | API Hostinger rechazó o timeout; usuario queda pendiente → reenviar |
| Registro entra sin código | `Email__Enabled=false` o Provider no configurado en Render |

### Base de datos SQL (Render / producción)

**Ya no uses SQLite en internet** (se borra al dormirse el Free). Usa PostgreSQL:

1. En Render: **New → PostgreSQL** (mismo región que MICALE).
2. Copia la **Internal Database URL** (o External).
3. En el Web Service MICALE → Environment:

```env
ConnectionStrings__Cale=postgresql://USER:PASSWORD@HOST:5432/DBNAME
```

También funciona si pegas la URL en `DATABASE_URL` (Render a veces la inyecta solo al linkear la DB).

4. Redeploy. En **Development** la API puede crear tablas (`Database:AllowEnsureCreated`) y parches (`Database:ApplyFeatureSchema`). En **Production** ambos van en `false`: el esquema debe existir ya (copia/migración previa).

SQL Server (Azure/local) sigue soportado con connection string tipo `Server=...;Database=...;...`.

### Persistencia en Render Free

El plan Free **borra el disco del contenedor** cuando la instancia se duerme. Por eso SQLite en `/data` no sirve en producción.

Con **PostgreSQL** los usuarios y datos **sí se mantienen**.

Si no hay ningún administrador y `Seed:BootstrapAdmin=true` **con** `Seed:Admin:Email` + `Seed:Admin:Password` definidos, la API puede crear un admin temporal. **Nunca** uses claves hardcodeadas en el código.

### Admin único (producción)

**Nunca** pongas la contraseña en el código ni en GitHub. En el panel del hosting define:

```env
Seed__Admin__Email=tu-correo@dominio.com
Seed__Admin__Name=Tu Nombre
Seed__Admin__Password=TU_CLAVE_FUERTE
Seed__Admin__PurgeOthers=false
```

`PurgeOthers=true` **borra el resto de cuentas**. Fuera de Development se ignora salvo que también pongas `Seed__Admin__AllowPurgeInNonDevelopment=true` (solo un one-shot deliberado). Luego deja `PurgeOthers=false`.

Esquema en producción (recomendado):

```env
Database__AllowEnsureCreated=false
Database__ApplyFeatureSchema=false
Database__AllowRequestPathRepair=false
Database__UseEfMigrations=false
```

Cuando la base existente ya esté baselined con `__EFMigrationsHistory` (ver [MIGRATIONS.md](MIGRATIONS.md) y `scripts/BASELINE_PROD.ps1`), activa:

```env
Database__UseEfMigrations=true
```

Checklist corto en Render:

1. Backup Postgres.
2. Ejecutar `scripts/baseline-ef-migrations.sql` (PSQL de Render o `.\scripts\BASELINE_PROD.ps1` con External URL).
3. Env: `Database__UseEfMigrations=true` (+ EnsureCreated/FeatureSchema en `false`).
4. Manual Deploy → comprobar `/api/health`.

Para un bootstrap inicial controlado en un entorno vacío puedes usar `UseEfMigrations=true` (DB vacía) o, solo en Development, `AllowEnsureCreated` / `ApplyFeatureSchema`.
