import { Component, OnInit, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';

interface PilotMetricsDto {
  dailyActiveUsers: number;
  weeklyActiveUsers: number;
  monthlyActiveUsers: number;
  activeSchools: number;
  pendingMembershipRequests: number;
  membershipRequests30d: number;
  membershipActivations30d: number;
  membershipConversionRate30d: number;
  studentsTotal: number;
  studentsActive7d: number;
  studentsInactive14d: number;
  teachersTotal: number;
  teachersActive7d: number;
  activeGroups: number;
  attemptsStarted30d: number;
  attemptsFinished30d: number;
  examCompletionRate30d: number;
  examPassRate30d: number;
  avgAttemptsPerStudent30d: number;
  questionsAnsweredTotal: number;
  avgExamTimeSeconds30d: number;
  abandonedAttempts30d: number;
  simulatorUsageShare30d: number;
  classroomSubmissions30d: number;
}

@Component({
  selector: 'app-admin-metrics-page',
  standalone: true,
  imports: [
    DecimalPipe,
    RouterLink,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Reportes"
      title="Actividad de la plataforma"
      subtitle="Resumen claro del uso real en CALE. La gestión de escuelas está en Escuelas de Manejo." />

    @if (loading()) {
      <ui-loading />
    } @else if (error()) {
      <ui-error [message]="error()" />
    } @else {
      @if (m(); as d) {
      <section class="block">
        <h2>Uso diario</h2>
        <p class="hint">Personas que entraron a la plataforma.</p>
        <div class="kpi-grid">
          <article class="kpi">
            <span class="kpi-label">Hoy</span>
            <strong class="kpi-value">{{ d.dailyActiveUsers }}</strong>
            <span class="kpi-foot">usuarios activos</span>
          </article>
          <article class="kpi">
            <span class="kpi-label">Últimos 7 días</span>
            <strong class="kpi-value">{{ d.weeklyActiveUsers }}</strong>
            <span class="kpi-foot">usuarios activos</span>
          </article>
          <article class="kpi">
            <span class="kpi-label">Últimos 30 días</span>
            <strong class="kpi-value">{{ d.monthlyActiveUsers }}</strong>
            <span class="kpi-foot">usuarios activos</span>
          </article>
          <article class="kpi accent">
            <span class="kpi-label">Escuelas con membresía vigente</span>
            <strong class="kpi-value">{{ d.activeSchools }}</strong>
            <span class="kpi-foot">operando ahora</span>
          </article>
        </div>
      </section>

      <section class="block">
        <div class="block-head">
          <div>
            <h2>Solicitudes de escuelas</h2>
            <p class="hint">Solo un resumen. Para aprobar, rechazar o editar cupos usa Escuelas de Manejo.</p>
          </div>
          <a routerLink="/admin/schools/queue">
            <ui-button type="button" variant="secondary">Ir a Escuelas de Manejo</ui-button>
          </a>
        </div>
        <div class="kpi-grid">
          <article class="kpi warn">
            <span class="kpi-label">Esperando decisión</span>
            <strong class="kpi-value">{{ d.pendingMembershipRequests }}</strong>
            <span class="kpi-foot">solicitudes abiertas</span>
          </article>
          <article class="kpi">
            <span class="kpi-label">Nuevas (30 días)</span>
            <strong class="kpi-value">{{ d.membershipRequests30d }}</strong>
            <span class="kpi-foot">solicitudes recibidas</span>
          </article>
          <article class="kpi">
            <span class="kpi-label">Activadas (30 días)</span>
            <strong class="kpi-value">{{ d.membershipActivations30d }}</strong>
            <span class="kpi-foot">membresías puestas en marcha</span>
          </article>
          <article class="kpi">
            <span class="kpi-label">Tasa de activación</span>
            <strong class="kpi-value">{{ d.membershipConversionRate30d | number:'1.0-0' }}%</strong>
            <span class="kpi-foot">de solicitudes → activadas</span>
          </article>
        </div>
      </section>

      <div class="grid-2">
        <section class="block">
          <h2>Personas</h2>
          <p class="hint">Totales registrados y actividad reciente.</p>
          <ul class="rows">
            <li><span>Estudiantes totales</span><strong>{{ d.studentsTotal }}</strong></li>
            <li><span>Estudiantes activos (7 días)</span><strong>{{ d.studentsActive7d }}</strong></li>
            <li><span>Estudiantes sin entrar (14 días)</span><strong>{{ d.studentsInactive14d }}</strong></li>
            <li><span>Instructores activos / total</span><strong>{{ d.teachersActive7d }} / {{ d.teachersTotal }}</strong></li>
            <li><span>Grupos activos</span><strong>{{ d.activeGroups }}</strong></li>
          </ul>
        </section>

        <section class="block">
          <h2>Evaluaciones (últimos 30 días)</h2>
          <p class="hint">Cómo van los exámenes y el simulador.</p>
          <ul class="rows">
            <li><span>Intentos iniciados</span><strong>{{ d.attemptsStarted30d }}</strong></li>
            <li><span>Intentos finalizados</span><strong>{{ d.attemptsFinished30d }}</strong></li>
            <li><span>% de finalización</span><strong>{{ d.examCompletionRate30d | number:'1.0-0' }}%</strong></li>
            <li><span>% aprobación (≥80%)</span><strong>{{ d.examPassRate30d | number:'1.0-0' }}%</strong></li>
            <li><span>Abandonos</span><strong>{{ d.abandonedAttempts30d }}</strong></li>
            <li><span>Tiempo promedio</span><strong>{{ formatSeconds(d.avgExamTimeSeconds30d) }}</strong></li>
            <li><span>Uso del simulador</span><strong>{{ d.simulatorUsageShare30d | number:'1.0-0' }}%</strong></li>
            <li><span>Entregas de aula</span><strong>{{ d.classroomSubmissions30d }}</strong></li>
          </ul>
        </section>
      </div>
      }
    }
  `,
  styles: [`
    .block {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: 1.1rem 1.15rem 1.25rem;
      margin-bottom: 1rem;
    }
    .block-head {
      display: flex;
      gap: 1rem;
      justify-content: space-between;
      align-items: flex-start;
      flex-wrap: wrap;
      margin-bottom: 0.85rem;
    }
    h2 {
      margin: 0 0 0.25rem;
      font-size: 1.05rem;
    }
    .hint {
      margin: 0 0 0.9rem;
      color: var(--color-text-muted);
      font-size: 0.9rem;
      line-height: 1.4;
      max-width: 42rem;
    }
    .block-head .hint { margin-bottom: 0; }
    .kpi-grid {
      display: grid;
      gap: 0.75rem;
      grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
    }
    .kpi {
      background: var(--color-surface-raised, #f8fafc);
      border: 1px solid var(--color-border);
      border-radius: 0.85rem;
      padding: 0.9rem 1rem;
      display: grid;
      gap: 0.2rem;
    }
    .kpi.accent { border-color: color-mix(in srgb, var(--color-primary) 35%, var(--color-border)); }
    .kpi.warn { border-color: color-mix(in srgb, #f59e0b 40%, var(--color-border)); }
    .kpi-label {
      font-size: 0.78rem;
      font-weight: 700;
      color: var(--color-text-secondary);
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }
    .kpi-value {
      font-size: 1.75rem;
      line-height: 1.1;
      font-weight: 800;
      color: var(--color-text);
    }
    .kpi-foot {
      font-size: 0.82rem;
      color: var(--color-text-muted);
    }
    .grid-2 {
      display: grid;
      gap: 1rem;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    }
    .rows {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.45rem;
    }
    .rows li {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      padding: 0.55rem 0;
      border-bottom: 1px solid var(--color-border);
      font-size: 0.95rem;
    }
    .rows li:last-child { border-bottom: 0; }
    .rows span { color: var(--color-text-secondary); }
    .rows strong { font-weight: 750; }
  `]
})
export class AdminMetricsPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly m = signal<PilotMetricsDto | null>(null);

  ngOnInit(): void {
    this.http.get<PilotMetricsDto>(`${env.apiUrl}/api/admin/metrics`).subscribe({
      next: (dto) => {
        this.m.set(dto);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  formatSeconds(value: number): string {
    if (!value || value <= 0) return '—';
    const mins = Math.round(value / 60);
    return mins < 1 ? `${Math.round(value)} s` : `${mins} min`;
  }
}
