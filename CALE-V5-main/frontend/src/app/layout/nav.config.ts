/** Live counters the shell can show next to a menu entry. */
export type NavBadge = 'pendingRequests';

export interface NavChild {
  label: string;
  path: string;
  exact?: boolean;
  queryParams?: Record<string, string>;
  badge?: NavBadge;
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
  /** App gratis: oculta planes/membresía y muestra donaciones. */
  freeAccess?: boolean;
}

const DONATE_ITEM: NavItem = { label: 'Apoyar CALE', path: '/donaciones', icon: 'heart', exact: true };
const REQUESTS_ITEM: NavItem = { label: 'Proponer y sugerir', path: '/solicitudes', icon: 'idea', exact: true };

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
  const free = options?.freeAccess !== false;
  if (role === 'Admin') {
    return [
      { label: 'Inicio', path: '/admin', icon: 'home', exact: true },
      {
        label: 'Escuelas',
        icon: 'building',
        children: [
          { label: free ? 'Directorio' : 'Solicitudes', path: '/admin/schools/queue', exact: true },
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
          { label: 'Cursos / Clases', path: '/admin/courses', exact: true },
          { label: 'Solicitudes de usuarios', path: '/admin/requests', exact: true, badge: 'pendingRequests' },
          { label: 'Señal relámpago', path: '/admin/signs-game', exact: true }
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
          { label: 'Asistencia del día', path: '/school/attendance', exact: true },
          { label: 'Programación teórica', path: '/school/training', exact: true },
          { label: 'Exámenes teóricos', path: '/school/theory-exams', exact: true },
          { label: 'Sala de control examen', path: '/school/exam-control', exact: true },
          { label: 'Práctica vehicular', path: '/school/practical', exact: true },
          { label: 'Flota práctica (hoy)', path: '/school/practical-fleet', exact: true }
        ]
      },
      {
        label: 'Catálogo',
        icon: 'book',
        children: [
          { label: 'Preguntas (lectura)', path: '/school/questions', exact: true },
          { label: 'Bancos (lectura)', path: '/school/banks', exact: true }
        ]
      },
      {
        label: 'Administración',
        icon: 'settings',
        children: free
          ? [{ label: 'Usuarios', path: '/school/users', exact: true }]
          : [
              { label: 'Usuarios', path: '/school/users', exact: true },
              { label: 'Pagos y membresía', path: '/school/membership', exact: true }
            ]
      },
      REQUESTS_ITEM,
      ...(free ? [DONATE_ITEM] : [])
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
          { label: '100 Estudiantes Dijeron', path: '/teacher/game-show', exact: true },
          { label: 'Presentaciones', path: '/teacher/presentations', exact: true },
          { label: 'Grupos', path: '/teacher/groups', exact: true }
        ]
      },
      {
        label: 'Biblioteca',
        icon: 'book',
        children: [
          { label: 'Biblioteca Jurídica', path: '/teacher/normas-transito' },
          { label: 'Exámenes', path: '/teacher/library', exact: true },
          { label: 'Bancos', path: '/teacher/banks', exact: true },
          { label: 'Preguntas', path: '/teacher/questions', exact: true }
        ]
      },
      { label: 'Informes', path: '/teacher/results', icon: 'chart', exact: true },
      REQUESTS_ITEM,
      DONATE_ITEM
    ];
  }

  const hasSchool = !!options?.hasSchool;
  const studentNav: NavItem[] = [
    { label: 'Inicio', path: '/student', icon: 'home', exact: true },
    { label: 'Aula en Vivo', path: '/live/join', icon: 'exam', exact: true },
    { label: 'Mis Evaluaciones', path: '/student/evaluations', icon: 'exam', exact: true },
    { label: 'Simulador', path: '/student/simulator', icon: 'exam', exact: true },
    { label: 'Reto diario', path: '/student/play/daily', icon: 'star', exact: true },
    { label: 'Juegos y ranking', path: '/student/play/ranking', icon: 'play' },
    { label: 'Mis logros', path: '/student/play/achievements', icon: 'star', exact: true },
    { label: 'Biblioteca Jurídica', path: '/student/normas-transito', icon: 'book' },
    { label: 'Mis Clases', path: '/student/classes', icon: 'book', exact: true },
    { label: 'Teoría', path: '/student/training', icon: 'exam', exact: true, requiresSchool: true },
    { label: 'Clases de manejo', path: '/student/practical', icon: 'exam', exact: true, requiresSchool: true },
    { label: 'Mi proceso', path: '/student/progress', icon: 'chart', exact: true },
    { label: 'Mensajes', path: '/notifications', icon: 'bell', exact: true },
    { label: 'Perfil', path: '/profile', icon: 'users', exact: true },
    REQUESTS_ITEM,
    DONATE_ITEM
  ];

  return studentNav.filter((item) => !item.requiresSchool || hasSchool);
}
