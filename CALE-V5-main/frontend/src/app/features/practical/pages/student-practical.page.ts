import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  PracticalApi,
  PracticalLessonSessionDto,
  PracticalStudentDashboardDto
} from '../api/practical.api';

@Component({
  selector: 'app-student-practical-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent],
  templateUrl: './student-practical.page.html',
  styleUrl: './student-practical.page.css'
})
export class StudentPracticalPage implements OnInit {
  private readonly api = inject(PracticalApi);

  readonly loading = signal(true);
  readonly actionLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly dashboard = signal<PracticalStudentDashboardDto | null>(null);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.studentDashboard().subscribe({
      next: (d) => {
        this.dashboard.set(d);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  canReserve(session: PracticalLessonSessionDto): boolean {
    const elig = this.dashboard()?.eligibility;
    return !!elig?.canBookPractical && session.bookingState === 'can_reserve';
  }

  reserve(session: PracticalLessonSessionDto): void {
    if (!this.canReserve(session)) {
      return;
    }
    this.actionLoading.set(true);
    this.api.reserve(session.id).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.reload();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  cancel(session: PracticalLessonSessionDto): void {
    if (!session.myReservationId) {
      return;
    }
    this.actionLoading.set(true);
    this.api.cancelReservation(session.myReservationId).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.reload();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
