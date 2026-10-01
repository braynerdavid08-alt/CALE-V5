import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiIconComponent } from '../../../shared/ui/ui-icon.component';
import { Badge, PlayApi, PlaySummary } from '../../play/api/play.api';
import { PlayBadgesToastComponent } from '../../play/components/play-badges-toast.component';
import { PlayFxService } from '../../play/play-fx.service';

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
  imports: [RouterLink, UiButtonComponent, UiIconComponent, PlayBadgesToastComponent],
  templateUrl: './student-home.page.html',
  styleUrl: './student-home.page.css'
})
export class StudentHomePage implements OnInit {
  readonly session = inject(SessionStore);
  private readonly play = inject(PlayApi);
  private readonly fx = inject(PlayFxService);

  readonly summary = signal<PlaySummary | null>(null);
  readonly newBadges = signal<Badge[]>([]);

  readonly greetingName = computed(
    () => this.summary()?.firstName || this.session.user()?.name?.split(' ')[0] || ''
  );

  readonly readinessTone = computed(() => {
    const v = this.summary()?.readiness ?? 0;
    return v >= 80 ? 'good' : v >= 60 ? 'mid' : 'low';
  });

  readonly games = [
    { id: 'signs', label: 'Señal relámpago', hint: '¿Cuántas señales en 60 s?', path: '/student/play/signs', emoji: '⚡' },
    { id: 'duel', label: 'Duelo 1 vs 1', hint: 'Reta a un compañero', path: '/student/play/duel', emoji: '⚔️' },
    { id: 'achievements', label: 'Mis logros', hint: 'Nivel e insignias', path: '/student/play/achievements', emoji: '🏅' }
  ];

  ngOnInit(): void {
    this.play.summary().subscribe({
      next: (s) => {
        this.summary.set(s);
        if (s.newBadges.length) {
          this.newBadges.set(s.newBadges);
          this.fx.play('badge');
          this.fx.confetti(90);
        }
      },
      error: () => this.summary.set(null)
    });
  }

  /** Large shortcuts — home is only a launcher for seniors. */
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
      hint: 'Ver materiales',
      path: '/student/classes',
      icon: 'book',
      tone: 'blue'
    },
    {
      id: 'evaluations',
      label: 'Evaluaciones',
      hint: 'Ver resultados',
      path: '/student/evaluations',
      icon: 'exam',
      tone: 'blue'
    },
    {
      id: 'biblioteca-juridica',
      label: 'Biblioteca Jurídica',
      hint: 'Normas de tránsito',
      path: '/student/normas-transito',
      icon: 'book',
      tone: 'green'
    },
    {
      id: 'progress',
      label: 'Mi progreso',
      hint: 'Teoría, manejo y avances',
      path: '/student/progress',
      icon: 'list',
      tone: 'green'
    },
    {
      id: 'profile',
      label: 'Mi perfil',
      hint: 'Ver mis datos',
      path: '/profile',
      icon: 'users',
      tone: 'violet'
    },
    {
      id: 'requests',
      label: 'Proponer y sugerir',
      hint: 'Crea preguntas o ideas',
      path: '/solicitudes',
      icon: 'idea',
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
