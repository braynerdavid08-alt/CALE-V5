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
          { label: 'Uso de la app', path: '/admin/usage', exact: true },
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
    // Flat menu with everyday words: school owners are often older and less used to apps.
    return [
      { label: 'Inicio', path: '/school', icon: 'home', exact: true },
      { label: 'Agenda de la semana', path: '/school/agenda', icon: 'calendar', exact: true },
      { label: 'Estudiantes', path: '/school/apprentices', icon: 'graduate', exact: true },
      { label: 'Asistencia de hoy', path: '/school/attendance', icon: 'list', exact: true },
      { label: 'Clases teóricas', path: '/school/training', icon: 'clock', exact: true },
      { label: 'Exámenes', path: '/school/theory-exams', icon: 'exam', exact: true },
      { label: 'Clases de manejo', path: '/school/practical', icon: 'play', exact: true },
      { label: 'Resultados', path: '/school/results', icon: 'chart', exact: true },
      { label: 'Instructores y usuarios', path: '/school/users', icon: 'instructor', exact: true },
      ...(free ? [] : [{ label: 'Pagos y membresía', path: '/school/membership', icon: 'card', exact: true }]),
      {
        label: 'Más opciones',
        icon: 'grid',
        children: [
          { label: 'Horarios de examen', path: '/school/theory-exams/schedule', exact: true },
          { label: 'Examen en curso', path: '/school/exam-control', exact: true },
          { label: 'Vehículos de hoy', path: '/school/practical-fleet', exact: true },
          { label: 'Importar estudiantes', path: '/school/import', exact: true },
          { label: 'Ver preguntas', path: '/school/questions', exact: true },
          { label: 'Ver bancos de preguntas', path: '/school/banks', exact: true }
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
    { label: 'Agendar examen', path: '/student/exam', icon: 'calendar', exact: true, requiresSchool: true },
    { label: 'Clases de manejo', path: '/student/practical', icon: 'exam', exact: true, requiresSchool: true },
    { label: 'Mi proceso', path: '/student/progress', icon: 'chart', exact: true },
    { label: 'Mensajes', path: '/notifications', icon: 'bell', exact: true },
    { label: 'Perfil', path: '/profile', icon: 'users', exact: true },
    REQUESTS_ITEM,
    DONATE_ITEM
  ];

  return studentNav.filter((item) => !item.requiresSchool || hasSchool);
}
