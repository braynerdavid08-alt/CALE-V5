# Reportes de programación (escuelas)

Módulo para generar un Excel diario de la programación escolar (teoría, manejo y exámenes teóricos) y, en una fase posterior, enviarlo por WhatsApp Cloud API a las **14:00** y **17:00** (America/Bogota).

## Estado de esta fase

| Capacidad | Estado |
|-----------|--------|
| Descarga manual Excel (hoy / mañana) | Activa si `AllowManualDownload=true` |
| Envío WhatsApp manual o automático | Preparado; API rechaza; UI «Pendiente de activación» |
| Job 14:00 / 17:00 | Handlers invocables en código; **sin** `HostedService` ni Cron en Render |
| Costos Meta / Cron Render | **Ninguno** en esta fase |

Esta fase **no crea** un Cron Job en Render y **no cobra** conversaciones de WhatsApp.

## Interruptores (`SchedulingReports`)

Valores por defecto en Development y Production:

```json
"SchedulingReports": {
  "Enabled": false,
  "AllowManualDownload": true,
  "AllowManualWhatsAppSend": false,
  "EnableDailyFullReportJob": false,
  "EnableDailyChangesReportJob": false,
  "EmptyChangesReports": false,
  "WhatsApp": {
    "Enabled": false,
    "PhoneNumberId": "",
    "AccessToken": "",
    "ApiVersion": "v21.0"
  }
}
```

- `AllowManualDownload`: permite `POST /api/school/scheduling-reports/generate/today|tomorrow`.
- Cualquier endpoint de **envío** valida flags en el backend; ocultar botones en Angular no basta.
- Secretos (`AccessToken`) solo en variables de entorno / secretos del host (`SchedulingReports__WhatsApp__AccessToken`), nunca en el frontend.

### Activación segura (futuro)

1. Completar WABA, plantillas con documento y opt-in de cada escuela.
2. Cargar `PhoneNumberId` + token en el backend.
3. Poner `WhatsApp:Enabled=true`, luego `AllowManualWhatsAppSend=true` y probar **un** envío manual.
4. Solo después: `Enabled=true` + `EnableDailyFullReportJob` / `EnableDailyChangesReportJob`.
5. Registrar un hosted service o Cron HTTP **después** de verificar precios.

## API (rol School)

- `GET /api/school/scheduling-reports/status` — flags efectivos + copy de activación.
- `POST /api/school/scheduling-reports/generate/today` — Excel + auditoría `Generated` (no enviado).
- `POST /api/school/scheduling-reports/generate/tomorrow` — igual, día siguiente.
- `POST /api/school/scheduling-reports/send/...` — **403** mientras WhatsApp/flags estén off.

Datos: `SchoolAgendaService` (mismo origen que `GET /api/school/agenda`). Hora local: `ColombiaTime`.

## Meta WhatsApp Cloud API (cuando se active)

Requisitos típicos:

- Cuenta WhatsApp Business (WABA) y Phone Number ID.
- Token de acceso de sistema con permiso de mensajería.
- Plantilla aprobada que permita adjuntar documento (Excel).
- Opt-in explícito del destinatario de la escuela.
- Costos por conversación / categoría según la política vigente de Meta — **verificar precio antes de encender**.

El stub `DisabledWhatsAppCloudSender` **nunca** llama a Graph en esta fase.

## Render / cron (futuro, no creado ahora)

Si más adelante se usa Cron Job HTTP hacia endpoints internos protegidos:

| Evento Colombia | UTC (COT = UTC−5, sin DST) |
|-----------------|----------------------------|
| 14:00 reporte completo + snapshot baseline | **19:00 UTC** |
| 17:00 novedades vs corte 14:00 | **22:00 UTC** |

Buenas prácticas: idempotencia por `(school, type, date, cut)`, reintentos acotados, auditoría en `SchedulingReportRuns`, y **revisar el precio del Cron de Render y de Meta** antes de crear el recurso.

Handlers listos en código (sin host activo):

- `DailyFullReportHandler`
- `DailyChangesReportHandler`

## Auditoría

- `SchedulingReportRuns`: generación / fallo / skip de envío. No marcar `Sent` si solo se generó el archivo.
- `SchedulingReportSnapshots`: payload del corte 14:00 para el diff de novedades.

## UI

Ruta escolar: `/school/reports` (nav + atajo en inicio de escuela). Banner «Pendiente de activación / Próximamente»; descarga manual; WhatsApp deshabilitado.
