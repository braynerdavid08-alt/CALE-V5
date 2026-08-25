export function roleLabel(role?: string | null): string {
  if (role === 'Admin') return 'Administrador';
  if (role === 'Teacher') return 'Docente';
  if (role === 'Student') return 'Estudiante';
  return role ?? '';
}
