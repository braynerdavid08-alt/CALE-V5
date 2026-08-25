import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/pages/login.page').then((m) => m.LoginPage)
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/pages/register.page')
        .then((m) => m.RegisterPage)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layout/app-shell.component')
        .then((m) => m.AppShellComponent),
    children: [
      {
        path: 'student/simulator',
        canActivate: [roleGuard],
        data: { roles: ['Student', 'Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/student/pages/simulator.page')
            .then((m) => m.SimulatorPage)
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
        path: 'student',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Student'] },
        loadComponent: () =>
          import('./features/student/pages/student-home.page')
            .then((m) => m.StudentHomePage)
      },
      {
        path: 'teacher/groups/:id',
        canActivate: [roleGuard],
        data: { roles: ['Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/teacher/pages/teacher-group.page')
            .then((m) => m.TeacherGroupPage)
      },
      {
        path: 'teacher/groups',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/teacher/pages/teacher-groups.page')
            .then((m) => m.TeacherGroupsPage)
      },
      {
        path: 'teacher/questions/:id',
        canActivate: [roleGuard],
        data: { roles: ['Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/catalog/pages/question-editor.page')
            .then((m) => m.QuestionEditorPage)
      },
      {
        path: 'teacher/questions',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/catalog/pages/questions.page')
            .then((m) => m.QuestionsPage)
      },
      {
        path: 'teacher/exams',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Teacher', 'Admin'] },
        loadComponent: () =>
          import('./features/teacher/pages/teacher-exams.page')
            .then((m) => m.TeacherExamsPage)
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
        path: 'admin/settings',
        pathMatch: 'full',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () =>
          import('./features/admin/pages/admin-settings.page')
            .then((m) => m.AdminSettingsPage)
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
        path: 'profile',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/auth/pages/profile.page')
            .then((m) => m.ProfilePage)
      }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
