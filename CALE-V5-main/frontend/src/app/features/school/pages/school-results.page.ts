import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { mapApiError } from '../../../core/http/map-api-error';
import { env } from '../../../core/config/env';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import {
  ResultAttempt,
  UiResultsByStudentComponent
} from '../../../shared/ui/ui-results-by-student.component';

@Component({
  selector: 'app-school-results-page',
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
      eyebrow="Escuela"
      title="Resultados"
      subtitle="Intentos finalizados de tus estudiantes, agrupados por estudiante y ordenados por fecha." />

    <ui-error [message]="error()" />

    @if (loading()) {
      <ui-loading />
    } @else if (!items().length) {
      <ui-empty
        title="Sin resultados"
        message="Cuando tus estudiantes terminen evaluaciones o el simulador, verás los puntajes aquí." />
    } @else {
      <ui-results-by-student [items]="items()" csvName="resultados-escuela.csv" />
    }
  `
})
export class SchoolResultsPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly items = signal<ResultAttempt[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.http.get<ResultAttempt[]>(`${env.apiUrl}/api/school/results`).subscribe({
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
