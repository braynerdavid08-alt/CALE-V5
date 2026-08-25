export interface NavItem {
  label: string;
  path: string;
  icon: string;
  exact?: boolean;
}

export function navForRole(role?: string): NavItem[] {
  if (role === 'Admin') {
    return [
      { label: 'Inicio', path: '/admin', icon: 'home', exact: true },
      { label: 'Usuarios', path: '/admin/users', icon: 'users', exact: true },
      { label: 'Preguntas', path: '/admin/questions', icon: 'book', exact: true },
      { label: 'Bancos', path: '/admin/banks', icon: 'bank', exact: true },
      { label: 'Exámenes', path: '/admin/exams', icon: 'exam', exact: true },
      { label: 'Resultados', path: '/admin/results', icon: 'chart', exact: true },
      { label: 'Valoraciones', path: '/admin/ratings', icon: 'star', exact: true },
      { label: 'Grupos', path: '/teacher/groups', icon: 'group', exact: true },
      { label: 'Simulador', path: '/student/simulator', icon: 'play', exact: true },
      { label: 'Configuración', path: '/admin/settings', icon: 'settings', exact: true }
    ];
  }
  if (role === 'School') {
    return [
      { label: 'Inicio', path: '/school', icon: 'home', exact: true },
      { label: 'Membresía', path: '/school/membership', icon: 'clock', exact: true },
      { label: 'Usuarios', path: '/school/users', icon: 'users', exact: true },
      { label: 'Preguntas', path: '/school/questions', icon: 'book', exact: true },
      { label: 'Bancos', path: '/school/banks', icon: 'bank', exact: true }
    ];
  }
  if (role === 'Teacher') {
    return [
      { label: 'Inicio', path: '/teacher', icon: 'home', exact: true },
      { label: 'Grupos', path: '/teacher/groups', icon: 'group', exact: true },
      { label: 'Preguntas', path: '/teacher/questions', icon: 'book', exact: true },
      { label: 'Bancos', path: '/teacher/banks', icon: 'bank', exact: true },
      { label: 'Exámenes', path: '/teacher/exams', icon: 'exam', exact: true },
      { label: 'Resultados', path: '/teacher/results', icon: 'chart', exact: true },
      { label: 'Simulador', path: '/student/simulator', icon: 'play', exact: true }
    ];
  }
  return [
    { label: 'Inicio', path: '/student', icon: 'home', exact: true },
    { label: 'Simulador', path: '/student/simulator', icon: 'play', exact: true }
  ];
}
