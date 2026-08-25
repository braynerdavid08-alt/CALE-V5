import { HttpErrorResponse } from '@angular/common/http';

const messages: Record<string, string> = {
  invalid_credentials: 'Correo o contraseña incorrectos.',
  email_taken: 'Ese correo ya está registrado.',
  invalid_email: 'El correo no es válido.',
  invalid_name: 'El nombre es obligatorio.',
  weak_password: 'La contraseña debe tener al menos 8 caracteres.',
  user_inactive: 'Tu cuenta está inactiva.',
  unauthorized: 'Necesitas iniciar sesión.',
  user_not_found: 'Usuario no encontrado.',
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
  rating_exists: 'Ya valoraste este intento.',
  group_not_found: 'Grupo o código no encontrado.',
  submission_exists: 'Ya entregaste esta actividad.'
};

export function mapApiError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Ocurrió un error. Inténtalo de nuevo.';
  }

  if (error.status === 0) {
    return 'No se pudo conectar con el servidor.';
  }

  const detail = error.error?.detail;
  const title = error.error?.title;
  if (detail === 'internal_error'
      && typeof title === 'string'
      && title.trim()
      && title !== 'Unexpected error.') {
    return title;
  }

  if (typeof detail === 'string' && messages[detail]) {
    return messages[detail];
  }

  if (typeof title === 'string' && title.trim()) {
    return title;
  }

  return 'Ocurrió un error. Inténtalo de nuevo.';
}
