import { Component, inject, OnInit, signal } from '@angular/core';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { TeacherApi } from '../api/teacher.api';

interface ResultRow {
  attemptId: number;
  userName: string;
  percent: number;
  passed: boolean;
  mode: string;
}

@Component({
  selector: 'app-teacher-results-page',
  standalone: true,
  imports: [
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      title="Resultados"
      subtitle="Intentos finalizados de estudiantes en tus grupos." />
    <ui-error [message]="error()" />
    @if (loading()) {
      <ui-loading />
    } @else if (!items().length) {
      <ui-empty title="Sin resultados" message="Cuando tus estudiantes terminen exámenes aparecerán aquí." />
    } @else {
      <div class="toolbar">
        <ui-button type="button" variant="secondary" (click)="exportCsv()">Exportar CSV</ui-button>
      </div>
      <ui-card>
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
      </ui-card>
    }
  `,
  styles: [`
    .toolbar { margin: 0 0 0.85rem; display: flex; justify-content: flex-end; }
  `]
})
export class TeacherResultsPage implements OnInit {
  private readonly api = inject(TeacherApi);
  readonly items = signal<ResultRow[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.results().subscribe({
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
    a.download = 'cale-resultados.csv';
    a.click();
    URL.revokeObjectURL(url);
  }
}
