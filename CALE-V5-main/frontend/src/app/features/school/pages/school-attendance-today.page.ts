import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import {
  AttendanceRowDto,
  TheoryApi,
  TheoryClassSessionDto,
  TheorySchoolDashboardDto
} from '../../theory/api/theory.api';

interface ClassroomGroup {
  classroomId: number;
  classroomName: string;
  sessions: TheoryClassSessionDto[];
}

@Component({
  selector: 'app-school-attendance-today-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  templateUrl: './school-attendance-today.page.html',
  styleUrl: './school-attendance-today.page.css'
})
export class SchoolAttendanceTodayPage implements OnInit {
  private readonly api = inject(TheoryApi);

  readonly loading = signal(true);
  readonly marking = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly dashboard = signal<TheorySchoolDashboardDto | null>(null);
  readonly daySessions = signal<TheoryClassSessionDto[]>([]);
  readonly selectedSessionId = signal<number | null>(null);
  readonly attendanceRows = signal<AttendanceRowDto[]>([]);
  readonly attendanceLoading = signal(false);

  /** yyyy-MM-dd in local calendar (school day picker). */
  day = this.toInputDate(new Date());

  readonly groups = computed(() => this.groupByClassroom(this.daySessions()));
  readonly selectedSession = computed(() => {
    const id = this.selectedSessionId();
    return this.daySessions().find((s) => s.id === id) ?? null;
  });
  readonly counts = computed(() => {
    const rows = this.attendanceRows();
    return {
      present: rows.filter((r) => r.status === 'Present').length,
      late: rows.filter((r) => r.status === 'Late').length,
      absent: rows.filter((r) => r.status === 'Absent').length,
      pending: rows.filter((r) => r.status === 'Pending' || !r.status).length,
      total: rows.length
    };
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.ok.set(null);
    const weekStart = this.mondayOf(this.day);
    forkJoin({
      dash: this.api.schoolDashboard().pipe(catchError(() => of(null))),
      week: this.api.schoolSchedule(weekStart)
    }).subscribe({
      next: ({ dash, week }) => {
        this.dashboard.set(dash);
        const sessions = (week.sessions ?? [])
          .filter((s) => this.sameDay(s.sessionDate, this.day))
          .sort((a, b) => a.startTime.localeCompare(b.startTime));
        this.daySessions.set(sessions);
        this.loading.set(false);
        const current = this.selectedSessionId();
        if (current && sessions.some((s) => s.id === current)) {
          this.selectSession(current);
        } else if (sessions.length) {
          this.selectSession(sessions[0].id);
        } else {
          this.selectedSessionId.set(null);
          this.attendanceRows.set([]);
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  onDayChange(value: string): void {
    this.day = value;
    this.reload();
  }

  goToday(): void {
    this.day = this.toInputDate(new Date());
    this.reload();
  }

  selectSession(sessionId: number): void {
    this.selectedSessionId.set(sessionId);
    this.attendanceLoading.set(true);
    this.error.set(null);
    this.api.listAttendance(sessionId).subscribe({
      next: (rows) => {
        this.attendanceRows.set(rows);
        this.attendanceLoading.set(false);
      },
      error: (err) => {
        this.attendanceLoading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  mark(studentUserId: number, status: string): void {
    const sessionId = this.selectedSessionId();
    if (!sessionId || this.marking()) {
      return;
    }
    this.marking.set(true);
    this.error.set(null);
    this.api.markAttendance(sessionId, { studentUserId, status }).subscribe({
      next: () => {
        this.marking.set(false);
        this.ok.set(
          status === 'Present'
            ? 'Marcado presente (suma horas).'
            : status === 'Late'
              ? 'Marcado tarde (suma horas).'
              : status === 'Absent'
                ? 'Marcado ausente (no suma horas).'
                : 'Asistencia actualizada.'
        );
        this.selectSession(sessionId);
        this.refreshSessionCounts();
      },
      error: (err) => {
        this.marking.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  markAllPresent(): void {
    const sessionId = this.selectedSessionId();
    const rows = this.attendanceRows();
    if (!sessionId || !rows.length || this.marking()) {
      return;
    }
    this.marking.set(true);
    this.error.set(null);
    this.api
      .markAttendanceBatch(
        sessionId,
        rows.map((r) => ({ studentUserId: r.studentUserId, status: 'Present' }))
      )
      .subscribe({
        next: () => {
          this.marking.set(false);
          this.ok.set('Todos marcados presentes. Solo Presente/Tarde suma horas.');
          this.selectSession(sessionId);
          this.refreshSessionCounts();
        },
        error: (err) => {
          this.marking.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Present':
        return 'Presente';
      case 'Late':
        return 'Tarde';
      case 'Absent':
        return 'Ausente';
      case 'Pending':
        return 'Pendiente';
      default:
        return status || 'Pendiente';
    }
  }

  durationLabel(session: TheoryClassSessionDto): string {
    const start = this.parseMinutes(session.startTime);
    const end = this.parseMinutes(session.endTime);
    if (start == null || end == null || end <= start) {
      return '—';
    }
    const hours = Math.round(((end - start) / 60) * 10) / 10;
    return `${hours} h`;
  }

  private refreshSessionCounts(): void {
    const weekStart = this.mondayOf(this.day);
    this.api.schoolSchedule(weekStart).subscribe({
      next: (week) => {
        const sessions = (week.sessions ?? [])
          .filter((s) => this.sameDay(s.sessionDate, this.day))
          .sort((a, b) => a.startTime.localeCompare(b.startTime));
        this.daySessions.set(sessions);
      }
    });
  }

  private groupByClassroom(sessions: TheoryClassSessionDto[]): ClassroomGroup[] {
    const map = new Map<number, ClassroomGroup>();
    for (const s of sessions) {
      const existing = map.get(s.classroomId);
      if (existing) {
        existing.sessions.push(s);
      } else {
        map.set(s.classroomId, {
          classroomId: s.classroomId,
          classroomName: s.classroomName || `Aula ${s.classroomId}`,
          sessions: [s]
        });
      }
    }
    return [...map.values()].sort((a, b) =>
      a.classroomName.localeCompare(b.classroomName, 'es')
    );
  }

  private mondayOf(isoDate: string): string {
    const d = this.parseLocalDate(isoDate);
    const day = d.getDay(); // 0 Sun
    const diff = day === 0 ? -6 : 1 - day;
    d.setDate(d.getDate() + diff);
    return this.toInputDate(d);
  }

  private sameDay(sessionDate: string, day: string): boolean {
    return (sessionDate || '').slice(0, 10) === day;
  }

  private toInputDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  private parseLocalDate(iso: string): Date {
    const [y, m, d] = iso.split('-').map(Number);
    return new Date(y, (m || 1) - 1, d || 1);
  }

  private parseMinutes(time: string): number | null {
    const m = /^(\d{1,2}):(\d{2})/.exec(time || '');
    if (!m) {
      return null;
    }
    return Number(m[1]) * 60 + Number(m[2]);
  }
}
