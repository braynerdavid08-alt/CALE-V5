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
  /** Optional discrete section title shown above this item (school nav groups). */
  section?: string;
  /** Slight visual emphasis without changing brand colors (e.g. Agenda). */
  emphasize?: boolean;
}

export interface NavOptions {
  hasSchool?: boolean;
  /** App gratis: oculta planes/membresía y muestra donaciones. */
  freeAccess?: boolean;
}

const DONATE_ITEM: NavItem = { label: 'Apoyar Luz Verde', path: '/donaciones', icon: 'heart', exact: true };
const SHOP_ITEM: NavItem = { label: 'Tienda y descuentos', path: '/tienda', icon: 'image', exact: false };
const SHOP_SCHOOL_ITEM: NavItem = {
  label: 'Tienda y equipamiento',
  path: '/tienda',
  icon: 'image',
  exact: false
};
const REQUESTS_ITEM: NavItem = { label: 'Proponer y sugerir', path: '/solicitudes', icon: 'idea', exact: true };
const PQR_ITEM: NavItem = { label: 'PQR y mis datos', path: '/mis-pqr', icon: 'list', exact: true };

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
          { label: 'Estudiantes', path: '/admin/students', exact: true },
          { label: 'Documentos legales', path: '/admin/legal', exact: true },
          { label: 'PQR y datos personales', path: '/admin/pqr', exact: true }
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
          { label: 'Cursos virtuales', path: '/admin/cursos-virtuales' },
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
          { label: 'Cascos y equipo', path: '/admin/shop', exact: true },
          { label: 'Ajustes', path: '/admin/settings', exact: true }
        ]
      }
    ];
  }

  if (role === 'School') {
    // Maps to existing routes only. No monthly agenda / practical-exam modules yet.
    const formacion = 'Formación y programación';
    const seguimiento = 'Seguimiento académico';
    const administracion = 'Administración';
    const otras = 'Otras opciones';
    return [
      { label: 'Inicio', path: '/school', icon: 'home', exact: true },
      {
        label: 'Programación de la escuela',
        icon: 'play',
        section: formacion,
        children: [
          { label: 'Clases teóricas', path: '/school/training', exact: true },
          { label: 'Exámenes teóricos', path: '/school/theory-exams', exact: true },
          { label: 'Clases de manejo', path: '/school/practical', exact: true }
        ]
      },
      {
        label: 'Agenda semanal',
        path: '/school/agenda',
        icon: 'calendar',
        exact: true,
        section: formacion,
        emphasize: true
      },
      {
        label: 'Horarios de examen',
        path: '/school/theory-exams/schedule',
        icon: 'clock',
        exact: true,
        section: formacion
      },
      {
        label: 'Estudiantes y expedientes',
        path: '/school/apprentices',
        icon: 'graduate',
        exact: true,
        section: seguimiento
      },
      {
        label: 'Asistencia',
        path: '/school/attendance',
        icon: 'list',
        exact: true,
        section: seguimiento
      },
      {
        label: 'Resultados y progreso',
        path: '/school/results',
        icon: 'chart',
        exact: true,
        section: seguimiento
      },
      {
        label: 'Cartera y saldos',
        path: '/school/apprentices',
        icon: 'card',
        exact: true,
        queryParams: { withBalance: 'true' },
        section: administracion
      },
      {
        label: 'Instructores y usuarios',
        path: '/school/users',
        icon: 'instructor',
        exact: true,
        section: administracion
      },
      {
        label: 'Vehículos de hoy',
        path: '/school/practical-fleet',
        icon: 'play',
        exact: true,
        section: administracion
      },
      {
        label: 'Importaciones y bancos',
        icon: 'grid',
        section: administracion,
        children: [
          { label: 'Importar expedientes', path: '/school/import', exact: true },
          { label: 'Ver preguntas', path: '/school/questions', exact: true },
          { label: 'Ver bancos de preguntas', path: '/school/banks', exact: true }
        ]
      },
      ...(free
        ? []
        : [
            {
              label: 'Plan y membresía',
              path: '/school/membership',
              icon: 'card',
              exact: true,
              section: otras
            }
          ]),
      { ...PQR_ITEM, section: otras },
      { ...SHOP_SCHOOL_ITEM, section: otras },
      { ...REQUESTS_ITEM, section: otras },
      ...(free ? [{ ...DONATE_ITEM, section: otras }] : [])
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
      { label: 'Cursos virtuales', path: '/teacher/cursos', icon: 'book' },
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
      PQR_ITEM,
      SHOP_ITEM,
      DONATE_ITEM
    ];
  }

  const hasSchool = !!options?.hasSchool;
  const studentNav: NavItem[] = [
    { label: 'Inicio', path: '/student', icon: 'home', exact: true },
    SHOP_ITEM,
    DONATE_ITEM,
    { label: 'Aula en Vivo', path: '/live/join', icon: 'exam', exact: true },
    { label: 'Mis Evaluaciones', path: '/student/evaluations', icon: 'exam', exact: true },
    { label: 'Simulador', path: '/student/simulator', icon: 'exam', exact: true },
    { label: 'Cursos virtuales', path: '/student/cursos', icon: 'book' },
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
    PQR_ITEM
  ];

  return studentNav.filter((item) => !item.requiresSchool || hasSchool);
}
