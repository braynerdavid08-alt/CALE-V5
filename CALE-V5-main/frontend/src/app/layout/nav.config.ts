export interface NavChild {
  label: string;
  path: string;
  exact?: boolean;
  queryParams?: Record<string, string>;
}

export interface NavItem {
  label: string;
  path?: string;
  icon: string;
  exact?: boolean;
  /** Nested hubs: secondary panel for related routes. */
  hub?: 'library';
  queryParams?: Record<string, string>;
  /** Inline expandable submenu (admin-style). */
  children?: NavChild[];
}

export interface LibraryNavItem {
  label: string;
  path: string;
  exact?: boolean;
}

/** Primary rail for instructors (CALE — no Descubre / Kahootopia). */
export const TEACHER_PRIMARY_NAV: NavItem[] = [
  { label: 'Dashboard', path: '/teacher', icon: 'home', exact: true },
  { label: 'Aula en Vivo', path: '/teacher/live', icon: 'exam', exact: true },
  { label: 'Biblioteca', path: '/teacher/library', icon: 'book', hub: 'library' },
  { label: 'Presentaciones', path: '/teacher/presentations', icon: 'book', exact: true },
  { label: 'Informes', path: '/teacher/results', icon: 'chart', exact: true },
  { label: 'Grupos', path: '/teacher/groups', icon: 'group', exact: true }
];

/** Secondary panel when Biblioteca is open. */
export const TEACHER_LIBRARY_NAV: LibraryNavItem[] = [
  { label: 'Exámenes', path: '/teacher/library', exact: true },
  { label: 'Bancos', path: '/teacher/banks', exact: true },
  { label: 'Preguntas', path: '/teacher/questions', exact: true },
  { label: 'Presentaciones', path: '/teacher/presentations', exact: true }
];

export function isTeacherLibraryPath(url: string): boolean {
  const path = url.split('?')[0];
  return (
    path === '/teacher/library'
    || path === '/teacher/exams'
    || path === '/teacher/banks'
    || path === '/teacher/questions'
  );
}

export function navChildActive(url: string, child: NavChild): boolean {
  const [pathPart, query = ''] = url.split('?');
  const path = pathPart;
  const pathOk = child.exact
    ? path === child.path
    : path === child.path || path.startsWith(child.path + '/');
  if (!pathOk) {
    return false;
  }
  if (!child.queryParams) {
    return true;
  }
  return Object.entries(child.queryParams).every(([k, v]) =>
    new RegExp(`(?:^|&)${k}=${encodeURIComponent(v)}(?:&|$)`).test(query)
    || new RegExp(`(?:^|&)${k}=${v}(?:&|$)`).test(query)
  );
}

/** Admin dashboard — full IA mockup menu (stubs allowed for future work). */
export function navForRole(role?: string): NavItem[] {
  if (role === 'Admin') {
    return [
      { label: 'Dashboard', path: '/admin', icon: 'home', exact: true },
      { label: 'Usuarios', path: '/admin/users', icon: 'users', exact: true },
      {
        label: 'Escuelas de Manejo',
        path: '/admin/schools/queue',
        icon: 'building',
        exact: true
      },
      { label: 'Instructores', path: '/admin/instructors', icon: 'instructor', exact: true },
      { label: 'Estudiantes', path: '/admin/students', icon: 'graduate', exact: true },
      { label: 'Cursos / Clases', path: '/admin/courses', icon: 'book', exact: true },
      {
        label: 'Evaluaciones',
        icon: 'exam',
        children: [
          { label: 'Preguntas', path: '/admin/questions', exact: true },
          { label: 'Bancos', path: '/admin/banks', exact: true },
          { label: 'Exámenes', path: '/admin/exams', exact: true }
        ]
      },
      {
        label: 'Reportes',
        icon: 'chart',
        children: [
          { label: 'Actividad', path: '/admin/metrics', exact: true },
          { label: 'Resultados', path: '/admin/results', exact: true },
          { label: 'Valoraciones', path: '/admin/ratings', exact: true }
        ]
      },
      { label: 'Notificaciones', path: '/admin/notifications', icon: 'bell', exact: true },
      {
        label: 'Configuración',
        icon: 'settings',
        children: [
          { label: 'Página de inicio', path: '/admin/homepage', exact: true },
          { label: 'Ajustes', path: '/admin/settings', exact: true }
        ]
      }
    ];
  }
    if (role === 'School') {
    return [
      { label: 'Dashboard', path: '/school', icon: 'home', exact: true },
      {
        label: 'Operaciones',
        icon: 'graduate',
        children: [
          { label: 'Aprendices', path: '/school/apprentices', exact: true },
          { label: 'Importar datos', path: '/school/import', exact: true }
        ]
      },
      {
        label: 'Formación',
        icon: 'exam',
        children: [
          { label: 'Programación teórica', path: '/school/training', exact: true },
          { label: 'Exámenes teóricos', path: '/school/theory-exams', exact: true },
          { label: 'Práctica vehicular', path: '/school/practical', exact: true }
        ]
      },
      {
        label: 'Evaluaciones',
        icon: 'book',
        children: [
          { label: 'Preguntas', path: '/school/questions', exact: true },
          { label: 'Bancos', path: '/school/banks', exact: true }
        ]
      },
      {
        label: 'Administración',
        icon: 'settings',
        children: [
          { label: 'Usuarios', path: '/school/users', exact: true },
          { label: 'Pagos y membresía', path: '/school/membership', exact: true }
        ]
      }
    ];
  }
  if (role === 'Teacher') {
    return TEACHER_PRIMARY_NAV;
  }
  // Student — mockup panel (without Pagos/Suscripciones: private / school-side).
  return [
    { label: 'Dashboard', path: '/student', icon: 'home', exact: true },
    { label: 'Mi formación', path: '/student/training', icon: 'exam', exact: true },
    { label: 'Clases de manejo', path: '/student/practical', icon: 'exam', exact: true },
    { label: 'Mis Clases', path: '/student/classes', icon: 'book', exact: true },
    { label: 'Mis Evaluaciones', path: '/student/evaluations', icon: 'exam', exact: true },
    { label: 'Mi Progreso', path: '/student/progress', icon: 'chart', exact: true },
    { label: 'Mensajes', path: '/notifications', icon: 'bell', exact: true },
    { label: 'Certificados', path: '/student/certificates', icon: 'graduate', exact: true },
    { label: 'Perfil', path: '/profile', icon: 'users', exact: true }
  ];
}
