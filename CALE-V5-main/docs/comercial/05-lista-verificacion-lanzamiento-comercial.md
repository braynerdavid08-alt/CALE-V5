# Lista de verificación — lanzamiento comercial B2B (CEA)

**Fecha:** 22 de octubre de 2026  
**Uso:** completar antes de presentar Luz Verde como producto de pago a escuelas.

Leyenda de tipo:

- **T** = técnico / producto  
- **C** = comercial / decisión de negocio  
- **L** = legal / cumplimiento (Colombia)  
- **O** = operación / servicio

---

## 1. Funcionalidades que deben probarse (T)

| Ítem | Estado sugerido | Notas |
|------|-----------------|-------|
| Login escuela / instructor / estudiante | [ ] | Cuentas demo estables |
| Agenda semanal | [ ] | Datos de ejemplo realistas |
| Expediente aprendiz + progreso | [ ] | |
| Asistencia del día | [ ] | |
| Horarios y control de examen | [ ] | |
| Clases teóricas y prácticas / flota | [ ] | Validar en campo con una escuela piloto |
| Aula en vivo (2 dispositivos) | [ ] | |
| Simulador estudiante | [ ] | |
| Invitación / solicitud de vínculo | [ ] | Confirmar que no se promete «crear cuentas» |
| Asistente IA (con ApiKey) | [ ] | Probar corte de límite diario |
| Membresía + comprobante (si FreeForAll = off) | [ ] | |
| Notificaciones básicas | [ ] | |
| Backup / restore del entorno de producción | [ ] | Procedimiento documentado |

---

## 2. Condiciones del servicio (C + L)

| Ítem | Tipo | Pendiente |
|------|------|-----------|
| Qué incluye el plan mensual / anual | C | Unificar con esta propuesta ($500k / $5M) |
| Qué pasa al vencer o no pagar | C/L | Redactar y alinear con estados técnicos |
| Permanencia mínima / cancelación | C/L | |
| SLA de soporte (horario, canal, tiempo respuesta) | O/C | |
| Capacitación incluida (horas) | O/C | |
| Migración de datos / Excel | O/C | Alcance y costo |
| Impuestos y facturación | L/C | |

---

## 3. Política de uso de IA (L + T + C)

| Ítem | Tipo | Pendiente |
|------|------|-----------|
| Texto definitivo de política de IA (sin `[PENDIENTE]`) | L | Borrador existe; falta datos y abogado |
| Proveedor y modelo divulgados al cliente | L/C | |
| Retención o no retención de chats | L/T | Hoy no hay store durable de chats |
| Usos prohibidos y disclaimer académico | L | |
| Roles con acceso (hoy: Student + School) | C | Comunicar claramente |

> Recordatorio: el sistema legal completo sigue aplazado; no afirmar cumplimiento total Ley 1581 / habeas data sin revisión jurídica.

---

## 4. Límites de consumo de IA (T + C)

| Ítem | Tipo | Pendiente |
|------|------|-----------|
| Mensajes/día/usuario (hoy default 20) | T/C | Fijar cifra comercial |
| Tope escuela / global | T/C | Ajustar a costo real |
| Contadores durables (BD) | T | **Requerido** antes de overage |
| Copy UX al agotar cupo | T | |

---

## 5. Tarifas y consentimiento de consumo adicional (C + T + L)

| Ítem | Tipo | Pendiente |
|------|------|-----------|
| ¿Se ofrecerá overage en el lanzamiento? | C | Si no → vender solo «incluido + corte» |
| Fórmula: costo proveedor + margen | C | No inventar margen aquí |
| Autorización expresa en contrato + panel | L/T | |
| Alertas 80 % / 100 % | T | |
| Ledger auditable | T | |
| Tratamiento de fallos del proveedor | C/T | |

**Regla:** sin consentimiento, bloquear; nunca cobro sorpresa.

---

## 6. Protección de datos personales (L + T)

| Ítem | Tipo | Pendiente |
|------|------|-----------|
| Responsable del tratamiento (razón social / NIT) | L | Placeholders actuales |
| Política de privacidad / tratamiento publicada como definitiva | L | |
| Menores / representante (reglas de negocio) | L | Decisiones abiertas en doc legal |
| Acceso solo por roles / aislamiento multi-escuela | T | Revisar reportes futuros de IA |
| Encargados (hosting, proveedor IA) | L | Contratos / DPAs |

---

## 7. Copias de seguridad y recuperación (T + O)

| Ítem | Tipo | Pendiente |
|------|------|-----------|
| Política de backup (frecuencia, retención) | T/O | |
| Prueba de restauración documentada | T | |
| Responsable y tiempo de recuperación objetivo (RTO) | O | Comunicar sin exagerar |

---

## 8. Soporte técnico (O + C)

| Ítem | Tipo | Pendiente |
|------|------|-----------|
| Canal oficial (correo / WhatsApp / ticket) | O | |
| Horario Colombia | O | |
| Escalamiento de incidentes críticos (aula en vivo, login) | O | |
| Qué no cubre el soporte (hardware del CEA, redes locales) | C | |

---

## 9. Contrato y facturación (L + C)

| Ítem | Tipo | Pendiente |
|------|------|-----------|
| Modelo de contrato B2B | L | |
| Medio de pago (transferencia hoy vs proveedor futuro) | C/T | |
| Datos bancarios publicados en `Payments:School` | T/O | Sin inventar NIT |
| Activar cobro: `FreeForAll = false` cuando corresponda | T/C | |
| Unificar precios código vs oferta ($500k/$5M vs SchoolPlans) | C/T | **Decisión crítica** |
| Reembolsos | L/C | |

---

## 10. Capacitación e implementación (O)

| Ítem | Tipo | Pendiente |
|------|------|-----------|
| Guion de onboarding (1 sesión escuela + 1 instructor) | O | Usar demo 10 min + práctica |
| Material PDF o video corto | O | |
| Criterio de «escuela en producción» (checklist de adopción) | O | Agenda usada, N aprendices vinculados, etc. |
| Piloto con 1–2 CEA antes de escalar ventas | C/O | Recomendado |

---

## 11. Prioridad sugerida (orden)

1. Unificar **precios y cupos** (decisión C → reflejar en código y propuesta).  
2. Decidir si el lanzamiento es **pago** o sigue piloto gratis.  
3. Completar **mínimo legal** para cobrar (responsable, términos, privacidad).  
4. Probar demo B2B de punta a punta.  
5. Definir postura de IA: **solo incluido + corte** vs **overage** (si overage → primero ingeniería de medición).  
6. Empaquetar soporte y capacitación.  
7. Recién entonces: outreach masivo a CEA.

---

## 12. Decisiones explícitas que debe tomar el propietario

1. ¿Los precios oficiales son $500.000 / $5.000.000 o los de `SchoolPlans.cs`?  
2. ¿Hay límites de instructores/estudiantes incluidos?  
3. ¿Cuándo se apaga `FreeForAll`?  
4. ¿Se vende overage de IA en v1 comercial o solo límites diarios?  
5. ¿Quién es el responsable legal y con qué datos se factura?  
6. ¿Cuál es el canal y el horario de soporte que sí puede cumplir?
