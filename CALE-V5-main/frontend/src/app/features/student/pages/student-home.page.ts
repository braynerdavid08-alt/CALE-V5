import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';
import { NotificationDto, StudentApi, StudentDashboardDto } from '../api/student.api';

@Component({
  selector: 'app-student-home-page',
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiStatComponent
  ],
  templateUrl: './student-home.page.html',
  styleUrl: './student-home.page.css'
})
export class StudentHomePage implements OnInit {
  private readonly api = inject(StudentApi);
  readonly session = inject(SessionStore);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<StudentDashboardDto | null>(null);
  readonly notifications = signal<NotificationDto[]>([]);
  code = '';

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.notifications().subscribe({
      next: (items) => this.notifications.set(items)
    });
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

  join(): void {
    if (!this.code.trim()) {
      return;
    }
    this.api.joinGroup(this.code.trim()).subscribe({
      next: () => {
        this.code = '';
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  markRead(id: number): void {
    this.api.markRead(id).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
