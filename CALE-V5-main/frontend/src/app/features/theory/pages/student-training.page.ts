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
  readonly weekStart = signal(this.startOfWeekIso(new Date()));

  readonly bookingLabel = theoryBookingLabel;
  readonly cupoTone = cupoTone;

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.loadData(() => this.loading.set(false));
  }

  private loadData(onDone?: () => void): void {
    this.api.studentDashboard().subscribe({
      next: (d) => {
        this.dashboard.set(d);
        this.api.studentSchedule(this.weekStart()).subscribe({
          next: (s) => {
            this.schedule.set(s);
            onDone?.();
          },
          error: (err) => {
            onDone?.();
            this.error.set(mapApiError(err));
          }
        });
      },
      error: (err) => {
        onDone?.();
        this.error.set(mapApiError(err));
      }
    });
  }

  private refreshSchedule(): void {
    this.loadData();
  }

  weekRangeLabel(): string {
    const sch = this.schedule();
    if (!sch?.weekStart || !sch?.weekEnd) {
      return '';
    }
    return `${this.formatDisplayDate(sch.weekStart)} – ${this.formatDisplayDate(sch.weekEnd)}`;
  }

  prevWeek(): void {
    this.weekStart.set(this.addDaysIso(this.weekStart(), -7));
    this.refreshSchedule();
  }

  nextWeek(): void {
    this.weekStart.set(this.addDaysIso(this.weekStart(), 7));
    this.refreshSchedule();
  }

  goCurrentWeek(): void {
    this.weekStart.set(this.startOfWeekIso(new Date()));
    this.refreshSchedule();
  }

  sessionsByDay(dayIndex: number): TheoryClassSessionDto[] {
    const s = this.schedule();
    if (!s) {
      return [];
    }
    const base = this.parseDate(s.weekStart);
    const d = this.addDays(base, dayIndex);
    const dateKey = this.formatDateOnly(d);
    return s.sessions.filter((x) => x.sessionDate === dateKey);
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
        this.refreshSchedule();
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
        this.refreshSchedule();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  checkIn(): void {
    this.api.checkIn().subscribe({
      next: () => this.refreshSchedule(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private startOfWeekIso(date: Date): string {
    const d = new Date(date);
    const day = (d.getDay() + 6) % 7;
    d.setDate(d.getDate() - day);
    return this.formatDateOnly(d);
  }

  private addDaysIso(iso: string, days: number): string {
    const d = this.parseDate(iso);
    d.setDate(d.getDate() + days);
    return this.formatDateOnly(d);
  }

  private addDays(date: Date, days: number): Date {
    const d = new Date(date);
    d.setDate(d.getDate() + days);
    return d;
  }

  private parseDate(iso: string): Date {
    const [y, m, d] = iso.split('-').map(Number);
    return new Date(y, m - 1, d);
  }

  private formatDateOnly(date: Date): string {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  private formatDisplayDate(iso: string): string {
    return this.parseDate(iso).toLocaleDateString('es-CO', {
      day: 'numeric',
      month: 'short'
    });
  }
}
