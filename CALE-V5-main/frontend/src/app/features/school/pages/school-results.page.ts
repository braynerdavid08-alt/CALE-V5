import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { mapApiError } from '../../../core/http/map-api-error';
import { env } from '../../../core/config/env';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';

interface ResultRow {
  attemptId: number;
  userName: string;
  percent: number;
  passed: boolean;
  mode: string;
}

@Component({
  selector: 'app-school-results-page',
  standalone: true,
  imports: [
    UiBadgeComponent,
    UiButtonComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Escuela"
      title="Resultados"
      subtitle="Intentos finalizados de tus aprendices en la plataforma." />

    <ui-error [message]="error()" />

    @if (loading()) {
      <ui-loading />
    } @else if (!items().length) {
      <ui-empty
        title="Sin resultados"
        message="Cuando tus aprendices terminen evaluaciones o el simulador, verás los puntajes aquí." />
    } @else {
      <div class="toolbar">
        <p class="hint">{{ items().length }} intento(s)</p>
        <ui-button type="button" variant="secondary" (click)="exportCsv()">Exportar CSV</ui-button>
      </div>
      <section class="table-card">
        <div class="table-wrap">
          <table class="data">
            <thead>
              <tr>
                <th>Estudiante</th>
                <th>Modo</th>
                <th>%</th>
                <th>Estado</th>
              </tr>
            </thead>
            <tbody>
              @for (item of items(); track item.attemptId) {
                <tr>
                  <td>{{ item.userName }}</td>
                  <td>{{ item.mode }}</td>
                  <td>{{ item.percent }}%</td>
                  <td>
                    <ui-badge [tone]="item.passed ? 'success' : 'danger'">
                      {{ item.passed ? 'Aprobado' : 'No aprobado' }}
                    </ui-badge>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    }
  `,
  styles: [`
    .toolbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
      margin: 0 0 0.85rem;
    }
    .hint {
      margin: 0;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
    }
    .table-card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      overflow: hidden;
    }
    .table-wrap { overflow-x: auto; }
    table.data {
      width: 100%;
      border-collapse: collapse;
      font-size: var(--text-sm);
    }
    table.data th,
    table.data td {
      padding: 0.75rem 1rem;
      text-align: left;
      border-bottom: 1px solid var(--color-border);
    }
    table.data th {
      color: var(--color-text-secondary);
      font-weight: 600;
      background: var(--color-surface-raised, transparent);
    }
  `]
})
export class SchoolResultsPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly items = signal<ResultRow[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.http.get<ResultRow[]>(`${env.apiUrl}/api/school/results`).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  exportCsv(): void {
    const rows = this.items();
    if (!rows.length) {
      return;
    }
    const esc = (v: string | number | boolean) => {
      const s = String(v);
      return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
    };
    const lines = [
      ['Estudiante', 'Modo', 'Porcentaje', 'Aprobado'].join(','),
      ...rows.map((r) =>
        [esc(r.userName), esc(r.mode), esc(r.percent), esc(r.passed ? 'Sí' : 'No')].join(',')
      )
    ];
    const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'resultados-escuela.csv';
    a.click();
    URL.revokeObjectURL(url);
  }
}
