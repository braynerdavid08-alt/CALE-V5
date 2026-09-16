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
      id: 'progress',
      label: 'Mi proceso',
      hint: 'Teoría y manejo',
      path: '/student/progress',
      icon: 'list',
      tone: 'green'
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
      id: 'profile',
      label: 'Mi perfil',
      hint: 'Mis datos',
      path: '/profile',
      icon: 'users',
      tone: 'violet'
    }
  ];

  visibleTiles(): LauncherTile[] {
    return this.tiles;
  }

  /** Keep only distinct destinations; empty filler tiles add cognitive noise. */
  displayTiles(): LauncherTile[] {
    return this.visibleTiles();
  }
}
