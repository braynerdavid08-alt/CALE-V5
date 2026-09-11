import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiIconComponent } from '../../../shared/ui/ui-icon.component';

interface LauncherTile {
  id: string;
  label: string;
  hint: string;
  path: string;
  icon: string;
  tone: 'blue' | 'green' | 'violet';
  requiresSchool?: boolean;
}

@Component({
  selector: 'app-student-home-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiIconComponent],
  templateUrl: './student-home.page.html',
  styleUrl: './student-home.page.css'
})
export class StudentHomePage {
  readonly session = inject(SessionStore);

  /** Nine large shortcuts — home is only a launcher for seniors. */
  readonly tiles: LauncherTile[] = [
    {
      id: 'live',
      label: 'Aula en vivo',
      hint: 'Entrar a clase',
      path: '/live/join',
      icon: 'play',
      tone: 'blue'
    },
    {
      id: 'classes',
      label: 'Mis clases',
      hint: 'Materiales',
      path: '/student/classes',
      icon: 'book',
      tone: 'blue'
    },
    {
      id: 'evaluations',
      label: 'Evaluaciones',
      hint: 'Presentar y ver',
      path: '/student/evaluations',
      icon: 'exam',
      tone: 'blue'
    },
    {
      id: 'simulator',
      label: 'Simulador',
      hint: 'Practicar',
      path: '/student/simulator',
      icon: 'panel',
      tone: 'green'
    },
    {
      id: 'progress',
      label: 'Qué me falta',
      hint: 'Pendientes',
      path: '/student/progress',
      icon: 'list',
      tone: 'green'
    },
    {
      id: 'practical',
      label: 'Manejo',
      hint: 'Clases prácticas',
      path: '/student/practical',
      icon: 'graduate',
      tone: 'green',
      requiresSchool: true
    },
    {
      id: 'messages',
      label: 'Mensajes',
      hint: 'Avisos',
      path: '/notifications',
      icon: 'bell',
      tone: 'violet'
    },
    {
      id: 'training',
      label: 'Formación',
      hint: 'Horario teórico',
      path: '/student/training',
      icon: 'clock',
      tone: 'violet',
      requiresSchool: true
    },
    {
      id: 'profile',
      label: 'Mi perfil',
      hint: 'Mis datos',
      path: '/profile',
      icon: 'users',
      tone: 'violet'
    }
  ];

  visibleTiles(): LauncherTile[] {
    const hasSchool = !!this.session.user()?.schoolId;
    return this.tiles.filter((t) => !t.requiresSchool || hasSchool);
  }

  /** Keep a full 3×3 grid when school tiles are hidden. */
  displayTiles(): LauncherTile[] {
    const visible = this.visibleTiles();
    if (visible.length >= 9) {
      return visible.slice(0, 9);
    }
    const extras: LauncherTile[] = [
      {
        id: 'results',
        label: 'Resultados',
        hint: 'Mis notas',
        path: '/student/evaluations',
        icon: 'chart',
        tone: 'green'
      },
      {
        id: 'home-help',
        label: 'Inicio',
        hint: 'Volver aquí',
        path: '/student',
        icon: 'home',
        tone: 'blue'
      }
    ];
    const out = [...visible];
    for (const e of extras) {
      if (out.length >= 9) {
        break;
      }
      if (!out.some((t) => t.id === e.id || t.path === e.path)) {
        out.push(e);
      }
    }
    return out.slice(0, 9);
  }
}
