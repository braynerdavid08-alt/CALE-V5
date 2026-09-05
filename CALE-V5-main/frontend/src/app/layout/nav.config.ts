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
  queryParams?: Record<string, string>;
  /** Inline expandable submenu (admin/school-style). */
  children?: NavChild[];
  /** Hide when student is not linked to a school. */
  requiresSchool?: boolean;
}

export interface NavOptions {
  hasSchool?: boolean;
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

/** Role navigation — ordered by daily workflow. */
export function navForRole(role?: string, options?: NavOptions): NavItem[] {
  if (role === 'Admin') {
    return [
      { label: 'Inicio', path: '/admin', icon: 'home', exact: true },
      {
        label: 'Escuelas',
        icon: 'building',
        children: [
          { label: 'Solicitudes', path: '/admin/schools/queue', exact: true },
          { label: 'Usuarios', path: '/admin/users', exact: true },
          { label: 'Instructores', path: '/admin/instructors', exact: true },
          { label: 'Estudiantes', path: '/admin/students', exact: true }
        ]
      },
      {
        label: 'Contenido',
        icon: 'exam',
        children: [
          { label: 'Preguntas', path: '/admin/questions', exact: true },
          { label: 'Bancos', path: '/admin/banks', exact: true },
          { label: 'Exámenes', path: '/admin/exams', exact: true },
          { label: 'Cursos / Clases', path: '/admin/courses', exact: true }
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
      { label: 'Inicio', path: '/school', icon: 'home', exact: true },
      {
        label: 'Operaciones',
        icon: 'graduate',
        children: [
          { label: 'Aprendices', path: '/school/apprentices', exact: true },
          { label: 'Resultados', path: '/school/results', exact: true },
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
        label: 'Biblioteca',
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
    return [
      { label: 'Inicio', path: '/teacher', icon: 'home', exact: true },
      {
        label: 'Clase',
        icon: 'exam',
        children: [
          { label: 'Aula en vivo', path: '/teacher/live', exact: true },
          { label: 'Presentaciones', path: '/teacher/presentations', exact: true },
          { label: 'Grupos', path: '/teacher/groups', exact: true }
        ]
      },
      {
        label: 'Biblioteca',
        icon: 'book',
        children: [
          { label: 'Exámenes', path: '/teacher/library', exact: true },
          { label: 'Bancos', path: '/teacher/banks', exact: true },
          { label: 'Preguntas', path: '/teacher/questions', exact: true }
        ]
      },
      { label: 'Informes', path: '/teacher/results', icon: 'chart', exact: true }
    ];
  }

  const hasSchool = !!options?.hasSchool;
  const studentNav: NavItem[] = [
    { label: 'Inicio', path: '/student', icon: 'home', exact: true },
    { label: 'Aula en Vivo', path: '/live/join', icon: 'exam', exact: true },
    { label: 'Mis Evaluaciones', path: '/student/evaluations', icon: 'exam', exact: true },
    { label: 'Simulador', path: '/student/simulator', icon: 'exam', exact: true },
    { label: 'Mis Clases', path: '/student/classes', icon: 'book', exact: true },
    { label: 'Mi formación', path: '/student/training', icon: 'exam', exact: true, requiresSchool: true },
    { label: 'Clases de manejo', path: '/student/practical', icon: 'exam', exact: true, requiresSchool: true },
    { label: 'Mi Progreso', path: '/student/progress', icon: 'chart', exact: true },
    { label: 'Mensajes', path: '/notifications', icon: 'bell', exact: true },
    { label: 'Perfil', path: '/profile', icon: 'users', exact: true }
  ];

  return studentNav.filter((item) => !item.requiresSchool || hasSchool);
}
