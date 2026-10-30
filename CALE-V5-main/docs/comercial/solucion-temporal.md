# Solución temporal comercial — Luz Verde (CEA)

**Fecha:** 22–23 de octubre 2026 (actualizado 30 oct 2026)  
**Estado:** implementada en código. No sustituye pasarela ni facturación electrónica.

## Modelo de producto (alineado)

| Quién | Acceso |
|--------|--------|
| **Estudiante** | Gratis (simulacro, práctica, cursos públicos / B2C). |
| **Escuela (CEA)** | Paga membresía (Mensual **$500.000** / Anual **$5.000.000** COP por defecto). |
| **Instructor** | Accede porque la **escuela** tiene plan (o cortesía) activo. |

`Access:FreeForAll` es solo un **override de piloto**: si está en `true`, las escuelas operan sin exigir membresía. El default del producto es **`false`**.

## Qué resuelve

Permite ofrecer suscripciones B2B a escuelas con:

- Catálogo editable (Mensual $500.000 / Anual $5.000.000 por defecto).
- Activación manual por administrador (pago interno: Pending / Reported / Confirmed / Rejected).
- Acceso de cortesía por escuela con fecha de fin (transición sin bloqueo masivo).
- Cuotas diarias de IA durables (reserva / commit / release), sin cobro de excesos.
- Paneles de consulta de plan/consumo para escuela y admin.

## Qué NO incluye (explícito)

- Pasarela de pagos (PSE, tarjeta, webhooks).
- Facturación electrónica / DIAN.
- Cálculo definitivo de impuestos.
- Cobro automático de excesos de IA.
- Afirmación de cumplimiento tributario: **confirmar pago en Luz Verde es solo control administrativo interno**.

## Cómo funciona el acceso

`CanOperateProduct` / `SchoolMembershipGuard`:

1. Si la escuela está pendiente de aprobación → bloqueo.
2. Si `Access:FreeForAll=true` → acceso de escuela/instructor sin membresía (piloto).
3. Si no: acceso con membresía Active vigente **o** cortesía vigente.

Los **estudiantes** pueden practicar simulacros aunque `FreeForAll=false` y aunque no estén vinculados a una escuela.

En `appsettings.json` / Production el default es `Access:FreeForAll=false`. Development local puede dejarlo en `true` para no bloquear demos. **Render no se cambia hasta que se otorgue cortesía o plan a las escuelas actuales** y se quite/actualice `Access__FreeForAll` en el entorno.

## Endpoints principales

| Método | Ruta | Quién |
|--------|------|-------|
| GET/PUT | `/api/admin/commercial-plans` | Admin — listar/editar precios y cupos (null = ilimitado) |
| POST | `/api/admin/schools/{id}/subscription` | Admin — asignar plan + vigencia + pago + activar |
| PUT | `/api/admin/schools/{id}/payment-status` | Admin |
| POST/DELETE | `/api/admin/schools/{id}/courtesy` | Admin |
| GET | `/api/school/profile` | Escuela — plan, pago, cortesía |
| GET | `/api/assistant/usage/school` | Escuela — consumo IA del día |
| GET | `/api/assistant/usage/admin` | Admin — consumo por escuela |

## Cómo probar

1. Arrancar API (schema guard aplica columnas/tablas).
2. Admin → Membresías → abrir escuela → «Suscripción manual»: plan Anual, pago Confirmed, Activar.
3. Verificar perfil escuela: estado Active, valor acordado, pago interno.
4. Otorgar cortesía +30 días; con FreeForAll=false, confirmar acceso vía cortesía.
5. Suspender (confirmación UI): datos no se borran; operaciones comerciales bloqueadas cuando FreeForAll=false.
6. Asistente: enviar mensajes hasta el límite diario; el siguiente debe pedir contactar admin; Quick no consume.
7. Forzar error de proveedor (opcional): el mensaje reservado se libera.

## Configuración relevante

- `Access:FreeForAll` — override piloto (default producto: `false`).
- `Assistant:DailyMessagesPerUser` (default 20) — cuota diaria por estudiante/usuario.
- `Assistant:DailyRequestsPerUser|School|Budget` — topes de llamadas al proveedor.
- Tabla `CommercialPlans` — precios/límites editables (seed: Monthly 500000, Annual 5000000, Semestral legacy inactivo).

## Antes de apagar el piloto en Render

1. Otorgar cortesía o activar plan a cada escuela en uso.
2. Confirmar que el registro de escuela muestra planes ($500.000 / $5.000.000).
3. Poner `Access__FreeForAll=false` (o eliminar la variable si el appsettings Production ya trae `false`).
4. Verificar: estudiante sin escuela sigue practicando; escuela sin plan no opera; instructor sin escuela vinculada con plan no usa catálogo.
