# Solución temporal comercial — Luz Verde (CEA)

**Fecha:** 22–23 de octubre de 2026  
**Estado:** implementada en código (piloto). No sustituye pasarela ni facturación electrónica.

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
2. Si `Access:FreeForAll=true` → acceso (modo piloto actual por defecto).
3. Si no: acceso con membresía Active vigente **o** cortesía vigente.

Al desplegar, **FreeForAll sigue en true** hasta que el administrador lo apague en configuración. Antes de apagarlo, otorgue cortesía o active planes a las escuelas actuales.

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

- `Access:FreeForAll` — acceso abierto global.
- `Assistant:DailyMessagesPerUser` (default 20) — cuota diaria por estudiante/usuario.
- `Assistant:DailyRequestsPerUser|School|Budget` — topes de llamadas al proveedor.
- Tabla `CommercialPlans` — precios/límites editables (seed: Monthly 500000, Annual 5000000, Semestral legacy inactivo).

## Decisiones pendientes antes del lanzamiento comercial

1. Fecha de apagar `FreeForAll` en producción.
2. Cupos concretos de estudiantes/instructores (hoy ilimitados por defecto).
3. Canal y horario de soporte.
4. Responsable legal, NIT, términos definitivos (sistema legal aplazado).
5. Medio de recaudo real y política de reembolsos.
6. Si/cuando se implementará overage de IA cobrable (hoy solo corte + contacto).
7. Unificar materiales de venta con precios del catálogo vivo.
