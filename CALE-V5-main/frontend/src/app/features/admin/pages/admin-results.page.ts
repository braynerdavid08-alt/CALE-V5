import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import {
  ResultAttempt,
  UiResultsByStudentComponent
} from '../../../shared/ui/ui-results-by-student.component';

@Component({
  selector: 'app-admin-results-page',
  standalone: true,
  imports: [
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiResultsByStudentComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Administración"
      title="Resultados"
      subtitle="Intentos finalizados, agrupados por estudiante y ordenados por fecha." />
    <ui-error [message]="error()" />
    @if (loading()) {
      <ui-loading />
    } @else if (!items().length) {
      <ui-empty title="No hay intentos finalizados" message="Cuando alguien termine un examen verás el resultado aquí." />
    } @else {
      <ui-results-by-student [items]="items()" csvName="resultados-mi-cale.csv" />
    }
  `
})
export class AdminResultsPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly items = signal<ResultAttempt[]>([]);
  readonly error = signal<string | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.http.get<ResultAttempt[]>(`${env.apiUrl}/api/admin/results`).subscribe({
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
}
