import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  TheoryApi,
  TheoryClassSessionDto,
  TheoryStudentDashboardDto,
  TheoryWeekScheduleDto,
  cupoTone,
  theoryBookingLabel
} from '../api/theory.api';

@Component({
  selector: 'app-student-training-page',
  standalone: true,
  imports: [DatePipe, RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent],
  templateUrl: './student-training.page.html',
  styleUrl: './student-training.page.css'
})
export class StudentTrainingPage implements OnInit {
  private readonly api = inject(TheoryApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly dashboard = signal<TheoryStudentDashboardDto | null>(null);
  readonly schedule = signal<TheoryWeekScheduleDto | null>(null);
  readonly view = signal<'week' | 'list'>('week');
  readonly actionLoading = signal(false);

  readonly bookingLabel = theoryBookingLabel;
  readonly cupoTone = cupoTone;

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.studentDashboard().subscribe({
      next: (d) => {
        this.dashboard.set(d);
        this.api.studentSchedule().subscribe({
          next: (s) => {
            this.schedule.set(s);
            this.loading.set(false);
          },
          error: (err) => {
            this.loading.set(false);
            this.error.set(mapApiError(err));
          }
        });
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  sessionsByDay(dayIndex: number): TheoryClassSessionDto[] {
    const s = this.schedule();
    if (!s) {
      return [];
    }
    const start = new Date(s.weekStart + 'T12:00:00');
    const d = new Date(start);
    d.setDate(start.getDate() + dayIndex);
    const key = d.toISOString().slice(0, 10);
    return s.sessions.filter((x) => x.sessionDate === key);
  }

  dayLabels = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

  studentGroupLabel(): string | null {
    const dayType =
      this.schedule()?.studentAttendanceDayType ?? this.dashboard()?.attendanceDayType ?? null;
    if (dayType === 'Weekday') {
      return 'Semana';
    }
    if (dayType === 'Saturday') {
      return 'Sábados';
    }
    return null;
  }

  visibleDayIndices(): number[] {
    const dayType =
      this.schedule()?.studentAttendanceDayType ?? this.dashboard()?.attendanceDayType ?? null;
    if (dayType === 'Weekday') {
      return [0, 1, 2, 3, 4];
    }
    if (dayType === 'Saturday') {
      return [5];
    }
    return [0, 1, 2, 3, 4, 5, 6];
  }

  visibleDayLabel(dayIndex: number): string {
    return this.dayLabels[dayIndex];
  }

  canReserve(session: TheoryClassSessionDto): boolean {
    return session.bookingState === 'can_reserve';
  }

  reserve(session: TheoryClassSessionDto): void {
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

  cancel(session: TheoryClassSessionDto): void {
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

  checkIn(): void {
    this.api.checkIn().subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
