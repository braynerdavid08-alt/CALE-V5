export function roleLabel(role?: string | null): string {
  if (role === 'Admin') return 'Administrador';
  if (role === 'School') return 'Escuela';
  if (role === 'Teacher') return 'Instructor';

  if (role === 'Student') return 'Estudiante';
  return role ?? '';
}
