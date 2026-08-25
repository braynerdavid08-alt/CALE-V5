import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { TeacherApi, TeacherDashboardDto } from '../api/teacher.api';

@Component({
  selector: 'app-teacher-home-page',
  standalone: true,
  imports: [
    RouterLink,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  templateUrl: './teacher-home.page.html',
  styleUrl: './teacher-home.page.css'
})
export class TeacherHomePage implements OnInit {
  private readonly api = inject(TeacherApi);
  readonly session = inject(SessionStore);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<TeacherDashboardDto | null>(null);

  ngOnInit(): void {
    this.api.dashboard().subscribe({
      next: (dto) => {
        this.data.set(dto);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
