import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { catalogAccessGuard, simulacroAccessGuard } from './core/guards/catalog-access.guard';
import { guestGuard } from './core/guards/guest.guard';
import { roleGuard } from './core/guards/role.guard';
import { LoginPage } from './features/auth/pages/login.page';

const staffRoles = ['Teacher', 'Admin'] as const;

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/public/public-shell.component')
        .then((m) => m.PublicShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/public/landing.page')
            .then((m) => m.LandingPage)
      },
      {
        path: 'nosotros',
        loadComponent: () =>
          import('./features/public/public-about.page')
            .then((m) => m.PublicAboutPage)
      },
      {
        path: 'cursos',
        loadComponent: () =>
          import('./features/public/public-courses.page')
            .then((m) => m.PublicCoursesPage)
      },
      {
        path: 'escuelas',
        loadComponent: () =>
          import('./features/public/public-schools.page')
            .then((m) => m.PublicSchoolsPage)
      },
      {
        path: 'instructores',
        loadComponent: () =>
          import('./features/public/public-instructors.page')
            .then((m) => m.PublicInstructorsPage)
      },
      {
        path: 'blog',
        loadComponent: () =>
          import('./features/public/public-blog.page')
            .then((m) => m.PublicBlogPage)
      },
      {
        path: 'contacto',
        loadComponent: () =>
          import('./features/public/public-contact.page')
            .then((m) => m.PublicContactPage)
      }
    ]
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    component: LoginPage
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/pages/register.page')
        .then((m) => m.RegisterPage)
  },
  {
    path: 'register-teacher',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/pages/register-teacher.page')
        .then((m) => m.RegisterTeacherPage)
  },
  {
    path: 'register-school',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/pages/register-school.page')
        .then((m) => m.RegisterSchoolPage)
  },
  {
    path: 'verify-email',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/pages/verify-email.page')
        .then((m) => m.VerifyEmailPage)
  },
  {
    path: 'live/join',
    pathMatch: 'full',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/live/pages/live-join.page')
        .then((m) => m.LiveJoinPage)
  },
  {
    path: 'live/join/:code',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/live/pages/live-join.page')
        .then((m) => m.LiveJoinPage)
  },
  {
    path: 'live/play/:sessionId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/live/pages/live-play.page')
        .then((m) => m.LivePlayPage)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layout/app-shell.component')
        .then((m) => m.AppShellComponent),
    children: [
      {
        path: 'school',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['School'] },
        loadComponent: () =>
          import('./features/school/pages/school-home.page')
            .then((m) => m.SchoolHomePage)
      },
      {
        path: 'school/membership',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['School'] },
        loadComponent: () =>
          import('./features/school/pages/school-membership.page')
            .then((m) => m.SchoolMembershipPage)
      },
      {
        path: 'school/users',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['School'] },
        loadComponent: () =>
          import('./features/school/pages/school-users.page')
            .then((m) => m.SchoolUsersPage)
      },
      {
        path: 'school/import',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['School'] },
        loadComponent: () =>
          import('./features/school/pages/school-import.page')
            .then((m) => m.SchoolImportPage)
      },
      {
        path: 'school/apprentices',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['School'] },
        loadComponent: () =>
          import('./features/school/pages/school-apprentices.page')
            .then((m) => m.SchoolApprenticesPage)
      },
      {
        path: 'school/theory-exams',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['School'] },
        loadComponent: () =>
          import('./features/school/pages/school-theory-exams.page')
            .then((m) => m.SchoolTheoryExamsPage)
      },
      {
        path: 'school/questions',
        pathMatch: 'full',
        canActivate: [roleGuard, catalogAccessGuard],
        data: { roles: ['School'] },
        loadComponent: () =>
          import('./features/catalog/pages/questions.page')
            .then((m) => m.QuestionsPage)
      },
      {
        path: 'school/banks',
        pathMatch: 'full',
        canActivate: [roleGuard, catalogAccessGuard],
        data: { roles: ['School'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-banks.page')
            .then((m) => m.AdminBanksPage)
      },
      {
        path: 'school/practical',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['School'] },
        loadComponent: () =>
          import('./features/practical/pages/school-practical.page')
            .then((m) => m.SchoolPracticalPage)
      },
      {
        path: 'school/training',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['School'] },
        loadComponent: () =>
          import('./features/theory/pages/school-theory.page')
            .then((m) => m.SchoolTheoryPage)
      },
      {
        path: 'student/simulator',
        canActivate: [roleGuard, simulacroAccessGuard],
        data: { roles: ['Student', 'Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/student/pages/simulator.page')
            .then((m) => m.SimulatorPage)
      },
      {
        path: 'student/classes',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Student'] },
        loadComponent: () =>
          import('./features/student/pages/student-classes.page')
            .then((m) => m.StudentClassesPage)
      },
      {
        path: 'student/evaluations',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Student'] },
        loadComponent: () =>
          import('./features/student/pages/student-evaluations.page')
            .then((m) => m.StudentEvaluationsPage)
      },
      {
        path: 'student/progress',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Student'] },
        loadComponent: () =>
          import('./features/student/pages/student-progress.page')
            .then((m) => m.StudentProgressPage)
      },
      {
        path: 'student/certificates',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: {
          roles: ['Student'],
          title: 'Certificados',
          subtitle: 'Constancias y certificados de avance cuando tu escuela o instructor los emita.',
          notes: [
            'Aquí verás certificados de aprobación y asistencia.',
            'Por ahora el simulador y las evaluaciones ya registran tu progreso.'
          ]
        },
        loadComponent: () =>
          import('./features/student/pages/student-certificates.page')
            .then((m) => m.StudentCertificatesPage)
      },
      {
        path: 'student/group/:id',
        canActivate: [roleGuard],
        data: { roles: ['Student'] },
        loadComponent: () =>
          import('./features/student/pages/student-group.page')
            .then((m) => m.StudentGroupPage)
      },
      {
        path: 'student/practical',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Student'] },
        loadComponent: () =>
          import('./features/practical/pages/student-practical.page')
            .then((m) => m.StudentPracticalPage)
      },
      {
        path: 'student/training',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Student'] },
        loadComponent: () =>
          import('./features/theory/pages/student-training.page')
            .then((m) => m.StudentTrainingPage)
      },
      {
        path: 'student',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Student'] },
        loadComponent: () =>
          import('./features/student/pages/student-home.page')
            .then((m) => m.StudentHomePage)
      },
      {
        path: 'teacher/presentations/:id/present',
        canActivate: [roleGuard],
        data: { roles: ['Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/teacher/presentations/presentation-present.page')
            .then((m) => m.PresentationPresentPage)
      },
      {
        path: 'teacher/presentations/:id/edit',
        canActivate: [roleGuard],
        data: { roles: ['Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/teacher/presentations/presentation-editor.page')
            .then((m) => m.PresentationEditorPage)
      },
      {
        path: 'teacher/presentations',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/teacher/presentations/presentation-list.page')
            .then((m) => m.PresentationListPage)
      },
      {
        path: 'teacher/groups/:id',
        canActivate: [roleGuard],
        data: { roles: [...staffRoles] },
        loadComponent: () =>
          import('./features/teacher/pages/teacher-group.page')
            .then((m) => m.TeacherGroupPage)
      },
      {
        path: 'teacher/groups',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: [...staffRoles] },
        loadComponent: () =>
          import('./features/teacher/pages/teacher-groups.page')
            .then((m) => m.TeacherGroupsPage)
      },
      {
        path: 'teacher/questions',
        pathMatch: 'full',
        canActivate: [roleGuard, catalogAccessGuard],
        data: { roles: ['Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/catalog/pages/questions.page')
            .then((m) => m.QuestionsPage)
      },
      {
        path: 'teacher/exams',
        pathMatch: 'full',
        redirectTo: '/teacher/library'
      },
      {
        path: 'teacher/library',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Teacher'] },
        loadComponent: () =>
          import('./features/teacher/pages/teacher-library.page')
            .then((m) => m.TeacherLibraryPage)
      },
      {
        path: 'teacher/results',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: [...staffRoles] },
        loadComponent: () =>
          import('./features/teacher/pages/teacher-results.page')
            .then((m) => m.TeacherResultsPage)
      },
      {
        path: 'teacher/banks',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-banks.page')
            .then((m) => m.AdminBanksPage)
      },
      {
        path: 'teacher',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Teacher'] },
        loadComponent: () =>
          import('./features/teacher/pages/teacher-home.page')
            .then((m) => m.TeacherHomePage)
      },
      {
        path: 'teacher/live',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: staffRoles },
        loadComponent: () =>
          import('./features/teacher/live/teacher-live-hub.page')
            .then((m) => m.TeacherLiveHubPage)
      },
      {
        path: 'teacher/live/:sessionId/host',
        canActivate: [roleGuard],
        data: { roles: staffRoles },
        loadComponent: () =>
          import('./features/teacher/live/teacher-live-host.page')
            .then((m) => m.TeacherLiveHostPage)
      },
      {
        path: 'admin/questions/:id',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/catalog/pages/question-editor.page')
            .then((m) => m.QuestionEditorPage)
      },
      {
        path: 'admin/questions',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/catalog/pages/questions.page')
            .then((m) => m.QuestionsPage)
      },
      {
        path: 'admin/users',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-users.page')
            .then((m) => m.AdminUsersPage)
      },
      {
        path: 'admin/schools/queue',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-memberships.page')
            .then((m) => m.AdminMembershipsPage)
      },
      {
        path: 'admin/schools',
        pathMatch: 'full',
        redirectTo: '/admin/schools/queue'
      },
      {
        path: 'admin/schools/seats',
        pathMatch: 'full',
        redirectTo: '/admin/schools/queue'
      },
      {
        path: 'admin/schools/decisions',
        pathMatch: 'full',
        redirectTo: '/admin/schools/queue'
      },
      {
        path: 'admin/instructors',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: {
          roles: ['Admin'],
          title: 'Instructores',
          subtitle: 'Gestión global de instructores del ecosistema CALE.',
          notes: [
            'Alta, baja, escuela vinculada y estado operativo.',
            'Hoy parte de esto vive en Usuarios (rol Teacher).'
          ]
        },
        loadComponent: () =>
          import('./features/admin/pages/admin-placeholder.page')
            .then((m) => m.AdminPlaceholderPage)
      },
      {
        path: 'admin/students',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: {
          roles: ['Admin'],
          title: 'Estudiantes',
          subtitle: 'Directorio y seguimiento de estudiantes a nivel plataforma.',
          notes: [
            'Filtros por escuela, actividad y membresía.',
            'Hoy puedes ver roles desde Usuarios.'
          ]
        },
        loadComponent: () =>
          import('./features/admin/pages/admin-placeholder.page')
            .then((m) => m.AdminPlaceholderPage)
      },
      {
        path: 'admin/courses',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: {
          roles: ['Admin'],
          title: 'Cursos / Clases',
          subtitle: 'Administración de cursos, clases y grupos formativos.',
          notes: [
            'Se prevé unificar grupos de aula y catálogo de cursos.',
            'La gestión operativa de grupos ya existe en /teacher/groups.'
          ]
        },
        loadComponent: () =>
          import('./features/admin/pages/admin-placeholder.page')
            .then((m) => m.AdminPlaceholderPage)
      },
      {
        path: 'admin/memberships',
        pathMatch: 'full',
        redirectTo: '/admin/schools/queue'
      },
      {
        path: 'admin/metrics',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-metrics.page')
            .then((m) => m.AdminMetricsPage)
      },
      {
        path: 'admin/settings',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-settings.page')
            .then((m) => m.AdminSettingsPage)
      },
      {
        path: 'admin/homepage',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-homepage.page')
            .then((m) => m.AdminHomepagePage)
      },
      {
        path: 'admin/ratings',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-ratings.page')
            .then((m) => m.AdminRatingsPage)
      },
      {
        path: 'admin/results',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-results.page')
            .then((m) => m.AdminResultsPage)
      },
      {
        path: 'admin/banks',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-banks.page')
            .then((m) => m.AdminBanksPage)
      },
      {
        path: 'admin/exams',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/teacher/pages/teacher-exams.page')
            .then((m) => m.TeacherExamsPage)
      },
      {
        path: 'admin',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-home.page')
            .then((m) => m.AdminHomePage)
      },
      {
        path: 'notifications',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/notifications/pages/notifications.page')
            .then((m) => m.NotificationsPage)
      },
      {
        path: 'admin/notifications',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-notifications.page')
            .then((m) => m.AdminNotificationsPage)
      },
      {
        path: 'profile',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/auth/pages/profile.page')
            .then((m) => m.ProfilePage)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
