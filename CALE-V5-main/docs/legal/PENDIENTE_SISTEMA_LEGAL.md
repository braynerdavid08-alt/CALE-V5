# PENDIENTE: Sistema legal, términos, privacidad y consentimientos

**Estado:** aplazado por decisión del propietario (4 oct 2026). No se ha implementado nada de este encargo.
Cuando se retome, empezar por la auditoría del punto 28 del encargo y no duplicar lo que ya existe.

## Lo que falta definir antes de empezar (el propietario)

- [RESPONSABLE LEGAL DE LUZ VERDE]: persona natural o futura persona jurídica.
- [NOMBRE LEGAL], [NIT O DOCUMENTO], [DIRECCIÓN], [CIUDAD], [CORREO LEGAL], [TELÉFONO].
- [PROVEEDOR DE PAGOS - PENDIENTE].
- Política de reembolsos (configurable; distinguir B2C y B2B; sin limitar derechos irrenunciables).
- Períodos de conservación por tipo de dato (`RETENTION_POLICY`), con su justificación.
- Licencias y titularidad del contenido: [VERIFICAR LICENCIAS Y TITULARIDAD DEL CONTENIDO].
- Abogado colombiano que revise los documentos: [REVISIÓN JURÍDICA REQUERIDA].

## Contradicciones con el código actual (resolver al retomar)

| Encargo | Código actual | Qué decidir |
|---------|---------------|-------------|
| "Sin precios definidos" y "sin planes por cantidad de estudiantes" | `src/Cale.Modules.Identity/Domain/SchoolPlans.cs` ya tiene Mensual 150.000 COP (máx. 5 instructores, 50 estudiantes) y Anual 1.500.000 COP (máx. 25 instructores, 400 estudiantes) | ¿Esos precios y cupos son reales o hay que quitarlos o marcarlos como pendientes? |
| "Las escuelas podrán crear y administrar cuentas de estudiantes e instructores" | Desde el [PR #195](https://github.com/braynerdavid08-alt/CALE-V5/pull/195) las escuelas **no** crean cuentas; la vinculación es por solicitud o invitación aceptada | ¿Se mantiene la vinculación con consentimiento (recomendado, sobre todo con menores) o se vuelve a permitir crear cuentas? |
| Pagos electrónicos con proveedor | Hoy la escuela sube un comprobante y el administrador lo revisa a mano; el estado del plan lo decide el servidor | Integración futura con webhooks verificados |

## Lo que ya existe y sirve como base (no duplicar)

- Consentimiento para aparecer en el directorio público de instructores ([PR #198](https://github.com/braynerdavid08-alt/CALE-V5/pull/198)): tabla `InstructorListings`, desactivado por defecto.
- Opción de ocultarse en el ranking (`PlayerProfile.ShowInRanking`).
- Vinculación escuela–usuario solo con consentimiento de ambas partes (`SchoolJoinRequest`).
- El correo de acceso no se puede cambiar (`email_change_disabled`).
- Asistente de IA: la API key solo está en el servidor y hay límites por usuario, por escuela y globales. El pentest como estudiante no encontró filtraciones entre usuarios.
- Usuarios activos e inactivos (`User.IsActive`). Hay que verificar si existen motivo, evidencia y revisión de una suspensión (probablemente no).
- Auditoría y pruebas de seguridad: `SECURITY_AUDIT.md` y `SECURITY_TEST_MATRIX.md`.
- **Sin auditar todavía:** si ya hay textos de términos o privacidad en el frontend, aceptaciones registradas, fecha de nacimiento o datos de menores, PQR, o conservación de datos.

## Encargo original completo (texto del propietario)

Se conserva íntegro para retomarlo sin perder requisitos.

```text
PROYECTO: LUZ VERDE — IMPLEMENTACIÓN LEGAL, TÉRMINOS, PRIVACIDAD Y CONSENTIMIENTOS

Implementar: 1) Términos y Condiciones; 2) Política de Tratamiento de Datos Personales;
3) Aviso/Política de Privacidad; 4) Condiciones para escuelas/CEA; 5) Política de uso de IA;
6) Consentimientos y autorizaciones; 7) Registro de aceptación; 8) Control de versiones;
9) Gestión de cambios; 10) Evidencia de aceptación; 11) Protección de menores;
12) Propiedad intelectual; 13) Reglas contra abuso/scraping/extracción/ataques;
14) Pagos y suscripciones; 15) Suspensión y reclamación; 16) Datos al terminar una escuela;
17) PQR y solicitudes de datos personales.

REGLA ABSOLUTA: no inventar NIT, razón social, responsable, dirección, teléfono, correo legal,
precios, proveedor de pagos, domicilio, representante legal, registro, abogado, entidad jurídica
ni licencias de contenido. Usar [PENDIENTE DE DEFINIR] / placeholders. No afirmar que algo es
legal porque parece razonable: marcar [REVISIÓN JURÍDICA REQUERIDA].

1. Luz Verde NO está constituida: sin razón social, NIT, domicilio ni correo legal. Placeholders:
   [RESPONSABLE LEGAL DE LUZ VERDE] [NOMBRE LEGAL] [NIT O DOCUMENTO] [DIRECCIÓN] [CIUDAD]
   [CORREO LEGAL] [TELÉFONO]. Checklist que impida marcar documentos como "finales" con pendientes.
2. Modelo: acceso directo para estudiantes (B2C) y venta a CEA (B2B); mensual y anual; sin planes
   por cantidad de estudiantes; sin prueba gratuita; pagos electrónicos con
   [PROVEEDOR DE PAGOS - PENDIENTE]; condiciones adaptables al proveedor.
3. Reembolsos: no escribir "no existen reembolsos". Política configurable, respetar derechos
   legales, distinguir B2C/B2B, no limitar derechos irrenunciables, informar antes del pago.
4. MENORES (PRIORIDAD ALTA): identificar menor; representante legal; autorización; registro y fecha;
   versión aceptada; trazabilidad; actualizar/revocar; solicitudes sobre datos. Un checkbox no basta.
   Requisitos colombianos. Minimizar datos del representante. Interés superior del menor y su
   opinión según edad y madurez.
5. Datos tratados: nombre, documento, fecha de nacimiento, teléfono, correo, dirección, escuela,
   resultados, progreso, calificaciones, fotografías, datos de instructores, pagos, IP, logs de
   seguridad, uso, conversaciones con IA. Clasificarlos (no todos iguales; no marcar sensibles
   automáticamente). Principios: finalidad, libertad, transparencia, acceso restringido, seguridad,
   confidencialidad, minimización, necesidad, temporalidad, derechos del titular.
6. Responsable/Encargado: documentar escenarios B2B (quién recolecta, quién fija finalidades, qué
   procesa Luz Verde por cuenta del CEA). No fijar artificialmente un rol. Cláusulas de encargo.
7. Derechos del titular: conocer, actualizar, rectificar, prueba de autorización, conocer el uso,
   consultas, reclamos, supresión y revocatoria cuando proceda. Estados: PENDIENTE, EN REVISIÓN,
   RESPONDIDA, RECHAZADA CON JUSTIFICACIÓN, ESCALADA. No borrar si hay obligación de conservar.
8. RETENTION_POLICY configurable: tipo de dato, finalidad, período, motivo, evento que inicia,
   acción final (eliminar/anonimizar/conservar). La obligación legal prevalece. No inventar plazos.
9. Fin de relación con una escuela: identificar sus datos, exportar, período de transición,
   conservación legal, eliminar/anonimizar al final, evidencia. No borrar de inmediato (pagos,
   reclamos, auditoría, seguridad, contratos, derechos de titulares).
10. IA para estudiantes, instructores, escuelas y admin. Mismas reglas de autorización que la API
    para el contexto del modelo; mínimo de datos al proveedor; sin secretos; sin API keys en el
    navegador. Política: herramienta de apoyo, puede errar, no reemplaza autoridades ni
    instructores, no garantiza aprobación, no es asesoría jurídica, no única fuente de decisiones.
11. Contenido: propio, de terceros, licenciado y por verificar. No declarar todo como propio.
    Cláusulas que distingan software, marca, interfaz, diseño, metodología, banco propio, contenido
    licenciado, de terceros, dominio público. TODO: [VERIFICAR LICENCIAS Y TITULARIDAD DEL CONTENIDO].
12. Propiedad intelectual: proteger código, arquitectura, interfaz, diseño, marca, logo, textos,
    banco de preguntas, metodología, bases de datos, materiales, gráficos, documentación.
    Prohibir (cuando sea jurídicamente posible) reproducción, extracción masiva, scraping,
    redistribución, comercialización, copia sustancial, ingeniería inversa prohibible, servicios
    derivados, extracción del banco, bots, evasión de controles. No prohibir lo que la ley permite.
13. Cuentas: prohibido compartir, vender o transferir, cuentas fraudulentas, suplantación, usar
    cuentas ajenas. Obligaciones sobre contraseña, seguridad, correo, sesión, dispositivos, sin
    culpar al usuario de lo que no puede controlar.
14. Abuso: prohibir acceso no autorizado, escaneo, explotación, scraping, bots, DoS, evasión,
    manipulación de resultados o pagos, escalada de privilegios, acceso entre usuarios o escuelas,
    extracción del banco o respuestas, malware, abuso de APIs. Detección y registro.
15. Suspensión con reglas (no arbitraria). Estados ACTIVA, SUSPENDIDA, BLOQUEADA, CANCELADA.
    Registrar motivo, fecha, administrador, evidencia, duración y revisión. Procedimiento de reclamo.
16. Pagos mensual/anual; sin precios inventados; el frontend nunca decide monto, estado, plan,
    vencimiento; secretos en backend; webhooks verificados; suscripciones no falsificables.
17. Limitación de responsabilidad no abusiva: plataforma tecnológica/educativa; no garantiza
    aprobación ni licencia; no reemplaza autoridades ni normas; no controla estudiantes,
    instructores, escuelas, terceros, conectividad ni servicios externos. Sujeto a la ley. Prohibido
    escribir "nunca responsable", "renuncia a todos sus derechos", "acepta cualquier daño".
18. Disponibilidad sin SLA: "procurará mantener la disponibilidad... pueden presentarse
    interrupciones por mantenimiento, actualizaciones, fallas, proveedores, conectividad,
    incidentes, fuerza mayor...". No prometer 99,x %.
19. Exámenes: resultados dependen del estudiante y de reglas de la escuela; sin garantía; las
    evaluaciones oficiales son de las autoridades. Prohibir manipulación, suplantación, compartir
    respuestas, automatización fraudulenta, explotación del banco.
20. PQR: PQR, privacidad, reclamos, reportes de seguridad, revisión de suspensiones.
    Correo [CORREO LEGAL - PENDIENTE].
21. Aceptación electrónica trazable: userId, versión, fecha/hora, documento, IP y user agent cuando
    proceda, método, estado, versión de privacidad, autorización. Sin contraseñas. Tablas tipo
    LegalDocument, LegalDocumentVersion, UserLegalAcceptance.
22. Versiones: nombre, versión, publicación, vigencia, estado, contenido, hash. TERMS v1.0,
    PRIVACY v1.0, AI_POLICY v1.0. No sobrescribir; conservar historial; decidir si exige nueva
    aceptación.
23. Registro: casillas separadas (acepto términos / he leído la política de datos / autorizo el
    tratamiento / soy representante legal del menor). Nunca premarcadas. Separar aceptación
    contractual y autorización de tratamiento.
24. Documentos separados: /legal/terms, /legal/privacy, /legal/data-processing, /legal/ai-policy,
    /legal/school-terms.
25. Seguridad: medidas razonables técnicas, administrativas y organizacionales; no prometer
    seguridad absoluta ni 100 % de disponibilidad.
26. Marco colombiano a revisar: Ley 1581 de 2012; Decreto 1074 de 2015; Ley 1480 de 2011;
    Ley 527 de 1999; normas de niños, niñas y adolescentes; comercio electrónico; consumidor;
    propiedad intelectual. No inventar artículos ni citar sin verificar vigencia.
27. Sin promesas jurídicas absolutas. Objetivo: reducir riesgos, no eliminar derechos.
28. Auditoría previa: arquitectura, términos y privacidad existentes, tablas de aceptación,
    usuarios, escuelas, pagos, IA, roles, suspensión, logs, correo, endpoints, frontend.
    Reutilizar, no duplicar.
29. Implementar: backend (modelos, entidades, migraciones, DTOs, endpoints, servicios,
    autorización, versionado, auditoría); Angular (páginas legales, modal de aceptación,
    casillas, historial, aviso de cambios, flujo de menores, flujo de privacidad); Admin (publicar
    versión, activar/desactivar, versiones, aceptaciones, quién/cuándo/qué versión, exportar).
    No editar en silencio una versión ya aceptada.
30. Nunca accepted = true automático para usuarios existentes: ACCEPTANCE_STATUS = NOT_VERIFIED.
31. PRIVACY_COMPLIANCE.md: datos, finalidad, base jurídica, responsable, encargado, terceros,
    proveedores, retención, derechos, atención, menores, IA, seguridad, transmisiones.
32. Documentos: /docs/legal/TERMINOS_Y_CONDICIONES.md, POLITICA_TRATAMIENTO_DATOS.md,
    POLITICA_PRIVACIDAD.md, POLITICA_IA.md, CONDICIONES_CEA.md, LEGAL_COMPLIANCE_CHECKLIST.md,
    PRIVACY_COMPLIANCE.md, LEGAL_CHANGELOG.md.
33. Informe final: hallazgos, lo existente, lo implementado, archivos, migraciones, endpoints,
    pantallas, riesgos legales y técnicos, datos faltantes, revisión de abogado, pruebas y
    resultados. Clasificar CRÍTICO/ALTO/MEDIO/BAJO/PENDIENTE/INFORMATIVO.
34–35. No inventar nada. Objetivo: proteger a Luz Verde, usuarios, escuelas, menores, datos y PI
    dentro de la ley colombiana, con un sistema defendible ante usuarios, escuelas o autoridades.
    Antes de finalizar: listas "DATOS QUE EL PROPIETARIO DE LUZ VERDE DEBE DEFINIR" y
    "ASUNTOS QUE DEBE REVISAR UN ABOGADO COLOMBIANO".
```
