import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { StudentApi } from '../api/student.api';
import { StudentTheoryApi } from '../api/student-theory.api';

interface CertificateItem {
  title: string;
  detail: string;
  earnedAt?: string | null;
  tone: 'success' | 'muted';
}

@Component({
  selector: 'app-student-certificates-page',
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Estudiante"
      title="Mis avances"
      subtitle="Resumen informativo de tu progreso en la plataforma. No son certificados oficiales ni tienen validez legal." />

    <ui-error [message]="error()" />

    @if (loading()) {
      <ui-loading />
    } @else {
      <ui-card class="notice">
        <p class="lead">
          Estos registros son solo para tu seguimiento en Mi CALE.
          Tu escuela es quien acredita horas, exámenes RUNT y habilitaciones.
        </p>
        <a routerLink="/student/progress">
          <ui-button type="button" variant="secondary">Ver mi progreso</ui-button>
        </a>
      </ui-card>

      @if (items().length === 0) {
        <ui-card>
          <p class="lead">
            Aún no hay avances registrados. Practica en el simulador o presenta evaluaciones
            para ver tu historial aquí.
          </p>
          <a routerLink="/student/simulator">
            <ui-button type="button">Ir al simulador</ui-button>
          </a>
        </ui-card>
      } @else {
        <div class="grid">
          @for (item of items(); track item.title) {
            <ui-card>
              <p class="badge" [class.muted]="item.tone === 'muted'">
                {{ item.tone === 'success' ? 'Logrado' : 'Registro' }}
              </p>
              <h2>{{ item.title }}</h2>
              <p class="detail">{{ item.detail }}</p>
              @if (item.earnedAt) {
                <p class="date">{{ item.earnedAt | date: 'mediumDate' }}</p>
              }
            </ui-card>
          }
        </div>
      }
    }
  `,
  styles: [`
    .lead { margin: 0 0 0.85rem; color: var(--color-text-secondary); line-height: 1.5; }
    .notice { margin-bottom: 1rem; display: block; }
    .grid {
      display: grid;
      gap: 1rem;
      grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
    }
    .badge {
      display: inline-block;
      margin: 0 0 0.5rem;
      padding: 0.2rem 0.55rem;
      border-radius: 999px;
      background: color-mix(in srgb, var(--color-success) 18%, transparent);
      color: var(--color-success);
      font-size: 0.78rem;
      font-weight: 600;
    }
    .badge.muted {
      background: color-mix(in srgb, var(--color-text-secondary) 14%, transparent);
      color: var(--color-text-secondary);
    }
    h2 { margin: 0 0 0.35rem; font-size: 1.05rem; }
    .detail { margin: 0; color: var(--color-text-secondary); line-height: 1.45; }
    .date { margin: 0.65rem 0 0; font-size: 0.85rem; color: var(--color-text-muted, var(--color-text-secondary)); }
  `]
})
export class StudentCertificatesPage implements OnInit {
  private readonly api = inject(StudentApi);
  private readonly theoryApi = inject(StudentTheoryApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<CertificateItem[]>([]);

  ngOnInit(): void {
    forkJoin({
      results: this.api.results().pipe(catchError(() => of([]))),
      theory: this.theoryApi.dashboard().pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ results, theory }) => {
        const certs: CertificateItem[] = [];
        const passed = results.filter((r) => r.passed);
        for (const r of passed) {
          certs.push({
            title: r.mode === 'Exam' ? 'Evaluación aprobada' : 'Simulador aprobado',
            detail: `Calificación: ${Math.round(r.percent)}%`,
            earnedAt: r.finishedAt ?? null,
            tone: 'success'
          });
        }

        if (theory) {
          if (theory.progressPercent >= 100) {
            certs.push({
              title: 'Horas teóricas completadas',
              detail: `${theory.hoursCompleted}/${theory.hoursRequired} horas registradas`,
              earnedAt: null,
              tone: 'success'
            });
          } else if (theory.hoursCompleted > 0) {
            certs.push({
              title: 'Avance teórico en curso',
              detail: `${Math.round(theory.progressPercent)}% · ${theory.hoursCompleted}/${theory.hoursRequired} h`,
              earnedAt: null,
              tone: 'muted'
            });
          }

          if (theory.workshopHoursRequired > 0 && theory.workshopHoursCompleted >= theory.workshopHoursRequired) {
            certs.push({
              title: 'Taller práctico completado',
              detail: `${theory.workshopHoursCompleted}/${theory.workshopHoursRequired} horas de taller`,
              earnedAt: null,
              tone: 'success'
            });
          }

          if (theory.practicalEligibility?.canBookPractical) {
            certs.push({
              title: 'Habilitado para práctica en vía',
              detail: 'Cumples los requisitos teóricos.',
              earnedAt: null,
              tone: 'success'
            });
          }

          if (theory.platformExam) {
            certs.push({
              title: 'Examen de plataforma asignado',
              detail: theory.platformExam.name,
              earnedAt: null,
              tone: 'muted'
            });
          }
        }

        this.items.set(certs);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
