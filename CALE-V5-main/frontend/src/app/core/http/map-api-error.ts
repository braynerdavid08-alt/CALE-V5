import { HttpErrorResponse } from '@angular/common/http';
import { extractTraceId } from './observability.interceptor';

const messages: Record<string, string> = {
  invalid_credentials: 'Correo o contraseña incorrectos.',
  email_taken: 'Ese correo ya está registrado.',
  invalid_email: 'El correo no es válido.',
  invalid_name: 'El nombre es obligatorio.',
  weak_password: 'La contraseña debe tener al menos 8 caracteres.',
  user_inactive: 'Tu cuenta está inactiva.',
  unauthorized: 'Necesitas iniciar sesión.',
  user_not_found: 'Usuario no encontrado.',
  cannot_deactivate_self: 'No puedes desactivar tu propia cuenta.',
  membership_admin_only: 'Solo el administrador puede activar la membresía tras verificar el pago.',
  membership_inactive: 'Tu membresía no está activa. Solicita un plan, sube el comprobante y espera la verificación del administrador.',
  payment_proof_required: 'Debes adjuntar el comprobante de pago.',
  rejection_reason_required: 'Indica el motivo del rechazo.',
  invalid_plan: 'El plan seleccionado no es válido.',
  no_membership_request: 'No hay solicitud de membresía pendiente.',
  not_a_school: 'La cuenta no es una escuela.',
  seat_limit_reached: 'Se alcanzó el límite de cupos del plan.',
  rating_not_found: 'Valoración no encontrada.',
  internal_error: 'Error interno del servidor.',
  invalid_correct: 'Marca exactamente una respuesta correcta.',
  invalid_options: 'Cada respuesta necesita texto o imagen (mínimo dos).',
  invalid_text: 'Escribe el enunciado de la pregunta.',
  invalid_file: 'La imagen no es válida. Usa jpg, png, gif o webp.',
  file_too_large: 'La imagen debe pesar 5 MB o menos.',
  empty_bank: 'No hay preguntas activas en ese banco.',
  exam_closed: 'El examen no está disponible ahora.',
  attempts_exhausted: 'Ya no te quedan intentos.',
  attempt_finished: 'Ese intento ya fue finalizado.',
  attempt_expired: 'Se acabó el tiempo del examen.',
  attempt_closed: 'El intento ya está cerrado.',
  attempt_open: 'Ya tienes un intento abierto de este examen.',
  rating_exists: 'Ya valoraste este intento.',
  group_not_found: 'Grupo o código no encontrado.',
  submission_exists: 'Ya entregaste esta actividad.',
  db_error: 'Error de base de datos. Inténtalo de nuevo.',
  timeout: 'La operación tardó demasiado. Inténtalo de nuevo.'
};

function withSupportCode(message: string, error: unknown): string {
  const traceId = extractTraceId(error);
  if (!traceId) {
    return message;
  }
  const short = traceId.length > 12 ? traceId.slice(0, 12) : traceId;
  return `${message} (código: ${short})`;
}

export function mapApiError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return withSupportCode('Ocurrió un error. Inténtalo de nuevo.', error);
  }

  if (error.status === 0) {
    return withSupportCode('No se pudo conectar con el servidor.', error);
  }

  const detail = error.error?.detail;
  const title = error.error?.title;
  if (detail === 'internal_error'
      && typeof title === 'string'
      && title.trim()
      && title !== 'Unexpected error.') {
    return withSupportCode(title, error);
  }

  if (typeof detail === 'string' && messages[detail]) {
    return withSupportCode(messages[detail], error);
  }

  if (typeof title === 'string' && title.trim()) {
    return withSupportCode(title, error);
  }

  return withSupportCode('Ocurrió un error. Inténtalo de nuevo.', error);
}
