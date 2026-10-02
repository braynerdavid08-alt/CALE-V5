import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { SessionStore } from '../../../core/auth/session.store';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import {
  ResultAttempt,
  UiResultsByStudentComponent
} from '../../../shared/ui/ui-results-by-student.component';
import { TeacherApi } from '../api/teacher.api';

@Component({
  selector: 'app-teacher-results-page',
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
      eyebrow="Informes"
      title="Resultados"
      subtitle="Intentos finalizados de los estudiantes de tus grupos, agrupados por estudiante y ordenados por fecha." />
    <ui-error [message]="error()" />
    @if (loading()) {
      <ui-loading />
    } @else if (!items().length) {
      <ui-empty title="Sin resultados" message="Cuando tus estudiantes terminen exámenes aparecerán aquí." />
    } @else {
      <ui-results-by-student [items]="items()" csvName="resultados-instructor.csv" />
    }
  `
})
export class TeacherResultsPage implements OnInit {
  private readonly api = inject(TeacherApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  readonly items = signal<ResultAttempt[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    if (this.session.user()?.role === 'Admin') {
      void this.router.navigate(['/admin/results']);
      return;
    }
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
}
