import { HttpErrorResponse } from '@angular/common/http';
import { extractTraceId } from './observability.interceptor';

const messages: Record<string, string> = {
  invalid_credentials: 'Correo o contraseña incorrectos.',
  email_taken: 'Ese correo ya está registrado.',
  invalid_email: 'El correo no es válido.',
  email_not_public: 'Usa un correo real (Gmail, Outlook, institucional, etc.).',
  email_not_confirmed: 'Debes confirmar tu correo con el código que te enviamos.',
  email_already_confirmed: 'Ese correo ya está confirmado. Puedes iniciar sesión.',
  invalid_confirmation_code: 'El código de verificación no es correcto.',
  confirmation_expired: 'El código expiró. Solicita uno nuevo.',
  invalid_name: 'El nombre es obligatorio.',
  weak_password: 'La contraseña debe tener al menos 8 caracteres.',
  user_inactive: 'Tu cuenta está inactiva.',
  unauthorized: 'Necesitas iniciar sesión.',
  user_not_found: 'Usuario no encontrado.',
  student_not_found: 'Estudiante no encontrado.',
  cannot_deactivate_self: 'No puedes desactivar tu propia cuenta.',
  membership_admin_only: 'Solo el administrador puede activar la membresía tras verificar el pago.',
  membership_inactive: 'Tu membresía no está activa. Solicita un plan, sube el comprobante y espera la verificación del administrador.',
  catalog_access_denied: 'No tienes acceso al catálogo de preguntas. Tu escuela necesita un plan activo.',
  simulacro_access_denied: 'No puedes usar simulacros sin una escuela con plan activo.',
  school_not_linked: 'No estás vinculado a una escuela. Ve a Perfil → Tu escuela y solicita unirte con el NIT o correo de la escuela.',
  school_not_found: 'No encontramos una escuela con ese NIT o correo.',
  invalid_school_query: 'Indica el NIT o el correo de la escuela (mínimo 3 caracteres).',
  teacher_only: 'Solo los instructores pueden solicitar unirse a una escuela.',
  already_in_school: 'Ya estás vinculado a una escuela.',
  join_request_pending: 'Ya tienes una solicitud pendiente con esa escuela.',
  join_request_closed: 'Esa solicitud ya fue resuelta.',
  join_request_not_found: 'Solicitud no encontrada.',
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
  already_answered: 'Ya respondiste esta pregunta.',
  question_closed: 'La pregunta está cerrada.',
  session_ended: 'La sesión ya terminó.',
  session_not_found: 'Sala no encontrada.',
  invalid_join_code: 'Código de sala inválido.',
  no_questions: 'No hay preguntas en el banco con esos filtros.',
  not_host: 'Solo el instructor de la sala puede controlar la actividad.',
  bank_required: 'No hay bancos de preguntas disponibles.',
  bank_inactive: 'Uno de los bancos seleccionados no está activo.',
  invalid_doubt_text: 'Escribe una duda de 3 a 280 caracteres.',
  doubt_not_found: 'Duda no encontrada.',
  already_voted: 'Ya votaste esta duda.',
  doubt_resolved: 'Esa duda ya está resuelta.',
  no_surprise_left: 'No quedan preguntas sorpresa en el banco.',
  surprise_unavailable: 'La sorpresa solo está disponible con una pregunta activa.',
  quick_unavailable: 'No se puede añadir una pregunta rápida ahora.',
  session_not_ended: 'La revancha solo se puede crear cuando la sesión terminó.',
  db_error: 'Error de base de datos. Inténtalo de nuevo.',
  timeout: 'La operación tardó demasiado. Inténtalo de nuevo.',
  enrollment_not_active: 'Tu escuela aún no te ha habilitado para reservar clases.',
  session_day_taken: 'Ya existe una clase programada para esa fecha.',
  class_already_started: 'No puedes editar una clase que ya comenzó.',
  class_cancelled: 'Esta clase está cancelada.',
  capacity_below_reserved: 'El cupo no puede ser menor que las reservas actuales.',
  day_already_reserved: 'Ya tienes una clase reservada ese día.',
  saturday_day_limit: 'Ya reservaste el máximo de 4 clases este sábado.',
  saturday_disabled: 'Las clases de sábado están desactivadas.',
  weekday_disabled: 'La escuela solo programa clases los sábados.',
  weekday_only: 'La escuela solo programa clases de lunes a viernes.',
  sunday_disabled: 'No se programan clases los domingos.',
  slot_not_assigned: 'Tu escuela aún no te ha asignado un horario.',
  slot_not_allowed: 'Ese horario no está habilitado para ti.',
  slot_required: 'Asigna un horario al estudiante.',
  day_not_allowed: 'No estás autorizado para clases en ese día.',
  day_type_required: 'Indica si el estudiante asiste en Semana o los sábados.',
  day_type_mismatch: 'El día asignado no coincide con los grupos habilitados de la escuela.',
  no_schedule_group: 'Activa al menos un grupo: entre semana o sábados.',
  invalid_license_category: 'La categoría de licencia no es válida.',
  license_category_required: 'Asigna la categoría de licencia del estudiante.',
  schedule_conflict: 'El instructor o el vehículo ya tienen clase en ese horario.',
  slot_taken: 'Ese horario ya tiene un estudiante asignado.',
  student_day_taken: 'El estudiante ya tiene una clase ese día.',
  student_not_authorized: 'El estudiante debe estar autorizado para asignar clases.',
  daily_hour_limit: 'El instructor ya alcanzó el máximo de 8 horas de manejo ese día.'
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

  const detail = typeof error.error?.detail === 'string' ? error.error.detail : null;
  const title = typeof error.error?.title === 'string' ? error.error.title : null;

  if (detail === 'internal_error'
      && title
      && title.trim()
      && title !== 'Unexpected error.') {
    return withSupportCode(title, error);
  }

  if (detail && messages[detail]) {
    return withSupportCode(messages[detail], error);
  }

  if (error.status === 401) {
    return withSupportCode(messages['unauthorized'], error);
  }

  if (error.status === 403) {
    return withSupportCode('No tienes permiso para esta acción.', error);
  }

  if (error.status === 404) {
    return withSupportCode('No se encontró el recurso solicitado.', error);
  }

  if (error.status >= 500) {
    return withSupportCode(messages['internal_error'], error);
  }

  if (title && title.trim() && title !== 'Unexpected error.') {
    return withSupportCode(title, error);
  }

  return withSupportCode('Ocurrió un error. Inténtalo de nuevo.', error);
}
