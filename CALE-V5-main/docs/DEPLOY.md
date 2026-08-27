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
4. SQLite en volumen `/data` (o SQL Server vía connection string).
5. Variables: `Jwt__Key`, `Cors__Origins`, `ConnectionStrings__Cale`. **Nunca** actives `Seed:DemoUsers` en internet.

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

### Correo (verificación de cuenta)

El registro público exige un correo real y envía un **código de 6 dígitos**. Sin confirmar no se puede iniciar sesión.

Variables de entorno SMTP (producción):

```env
Email__Enabled=true
Email__From=noreply@tudominio.com
Email__FromName=Mi CALE
Email__Smtp__Host=smtp.tudominio.com
Email__Smtp__Port=587
Email__Smtp__User=...
Email__Smtp__Password=...
Email__Smtp__UseSsl=true
```

Sin SMTP configurado, el código se escribe en los **logs del servidor** (útil en local).
