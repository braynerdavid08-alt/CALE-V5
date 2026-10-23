# Contrato: tarjeta de cupos de examen en el asistente

Cuando el backend responde a `cupos_examen` (estudiante) o `cupos_examen_escuela` (escuela),
`AssistantChatResult` puede incluir `examAvailability` además de `reply`.

## JSON (camelCase)

```json
{
  "reply": "Cupos disponibles para el examen.\nRango consultado: …",
  "action": null,
  "remainingToday": 12,
  "examAvailability": {
    "type": "exam_availability",
    "title": "Cupos disponibles para el examen",
    "dateRangeFrom": "2026-10-20",
    "dateRangeTo": "2026-11-03",
    "canBook": true,
    "blockReason": null,
    "myBookingSummary": null,
    "selectable": true,
    "days": [
      {
        "date": "2026-10-22",
        "availableSlots": [
          { "time": "09:00", "availableCapacity": 2 }
        ]
      }
    ]
  }
}
```

- Solo se listan horarios con `availableCapacity > 0`.
- `selectable: true` solo para estudiantes que pueden agendar; la escuela ve las mismas tarjetas sin botones de reserva.
- El texto de `reply` es un resumen corto; la UI no interpreta HTML del modelo.

## Reserva desde el chat

`POST /api/assistant/actions/prepare-exam` con `{ "date": "yyyy-MM-dd", "time": "HH:mm" }`
crea la misma acción pendiente `agendar_examen` que el LLM. El estudiante debe confirmar
con `POST /api/assistant/actions/{id}/confirm`, que vuelve a validar cupo vía `BookAsStudentAsync`.
