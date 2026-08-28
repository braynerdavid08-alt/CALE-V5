import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  TheoryApi,
  AttendanceRowDto,
  TheoryClassroomDto,
  TheoryClassSessionDto,
  TheorySchoolDashboardDto,
  TheorySettingsDto,
  TheoryTopicDto,
  TheoryWeekScheduleDto
} from '../api/theory.api';

@Component({
  selector: 'app-school-theory-page',
  standalone: true,
  imports: [FormsModule, UiButtonComponent, UiErrorComponent, UiLoadingComponent],
  templateUrl: './school-theory.page.html',
  styleUrl: './school-theory.page.css'
})
export class SchoolTheoryPage implements OnInit {
  private readonly api = inject(TheoryApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly tab = signal<'schedule' | 'attendance' | 'topics' | 'classrooms' | 'settings'>('schedule');
  readonly dashboard = signal<TheorySchoolDashboardDto | null>(null);
  readonly schedule = signal<TheoryWeekScheduleDto | null>(null);
  readonly topics = signal<TheoryTopicDto[]>([]);
  readonly classrooms = signal<TheoryClassroomDto[]>([]);
  readonly attendanceSessions = signal<TheoryClassSessionDto[]>([]);
  readonly selectedAttendanceSessionId = signal<number | null>(null);
  readonly attendanceRows = signal<AttendanceRowDto[]>([]);
  readonly settings = signal<TheorySettingsDto | null>(null);
  readonly attendanceLoading = signal(false);

  readonly dayLabels = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];
  readonly timeSlots = [
    '00:00', '02:00', '04:00', '06:00', '08:00', '10:00',
    '12:00', '14:00', '16:00', '18:00', '20:00', '22:00'
  ];

  newTopic = { name: '', description: '', color: '#3B82F6', isActive: true };
  newClassroom = { name: '', identifier: '', capacity: 15, location: '', isActive: true };
  createForm = {
    sessionDate: '',
    startTime: '08:00',
    endTime: '09:59',
    topicId: 0,
    classroomId: 0,
    capacity: 0,
    notes: ''
  };

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.schoolDashboard().subscribe({
      next: (d) => this.dashboard.set(d),
      error: () => this.dashboard.set(null)
    });
    this.api.listTopics().subscribe({
      next: (t) => {
        this.topics.set(t);
        if (!this.createForm.topicId && t.length) {
          this.createForm.topicId = t[0].id;
        }
      },
      error: () => this.topics.set([])
    });
    this.api.listClassrooms().subscribe({
      next: (c) => {
        this.classrooms.set(c);
        if (!this.createForm.classroomId && c.length) {
          this.createForm.classroomId = c[0].id;
        }
        this.loading.set(false);
      },
      error: () => {
        this.classrooms.set([]);
        this.loading.set(false);
      }
    });
    this.api.schoolSchedule().subscribe({
      next: (s) => this.schedule.set(s),
      error: () => this.schedule.set(null)
    });
    this.api.getSettings().subscribe({
      next: (s) => this.settings.set(s),
      error: () => this.settings.set(null)
    });
  }

  setTab(value: 'schedule' | 'attendance' | 'topics' | 'classrooms' | 'settings'): void {
    this.tab.set(value);
    if (value === 'attendance') {
      this.loadAttendanceSessions();
    }
  }

  loadAttendanceSessions(): void {
    this.attendanceLoading.set(true);
    this.api.listAttendanceSessions().subscribe({
      next: (sessions) => {
        this.attendanceSessions.set(sessions);
        const current = this.selectedAttendanceSessionId();
        if (!current && sessions.length) {
          this.selectAttendanceSession(sessions[0].id);
        } else if (current && !sessions.some((s) => s.id === current)) {
          this.selectAttendanceSession(sessions[0]?.id ?? null);
        } else if (current) {
          this.loadAttendanceRows(current);
        }
        this.attendanceLoading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.attendanceSessions.set([]);
        this.attendanceLoading.set(false);
      }
    });
  }

  selectAttendanceSession(sessionId: number | null): void {
    this.selectedAttendanceSessionId.set(sessionId);
    if (sessionId) {
      this.loadAttendanceRows(sessionId);
    } else {
      this.attendanceRows.set([]);
    }
  }

  loadAttendanceRows(sessionId: number): void {
    this.api.listAttendance(sessionId).subscribe({
      next: (rows) => this.attendanceRows.set(rows),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  markStudentAttendance(studentUserId: number, status: string): void {
    const sessionId = this.selectedAttendanceSessionId();
    if (!sessionId) {
      return;
    }
    this.api.markAttendance(sessionId, { studentUserId, status }).subscribe({
      next: () => this.loadAttendanceRows(sessionId),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  markAllPresent(): void {
    const sessionId = this.selectedAttendanceSessionId();
    const rows = this.attendanceRows();
    if (!sessionId || !rows.length) {
      return;
    }
    this.api.markAttendanceBatch(
      sessionId,
      rows.map((r) => ({ studentUserId: r.studentUserId, status: 'Present' }))
    ).subscribe({
      next: () => this.loadAttendanceRows(sessionId),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  saveSettings(): void {
    const s = this.settings();
    if (!s) {
      return;
    }
    this.api.updateSettings(s).subscribe({
      next: (updated) => {
        this.settings.set(updated);
        this.error.set(null);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  attendanceStatusLabel(status: string): string {
    switch (status) {
      case 'Present':
        return 'Presente';
      case 'Absent':
        return 'Ausente';
      case 'Late':
        return 'Tarde';
      default:
        return 'Pendiente';
    }
  }

  formatSessionLabel(s: TheoryClassSessionDto): string {
    return `${s.sessionDate} · ${s.startTime.slice(0, 5)} · ${s.topicName} · ${s.classroomName}`;
  }

  sessionsAt(dayIndex: number, start: string): TheoryClassSessionDto[] {
    const sch = this.schedule();
    if (!sch) {
      return [];
    }
    const base = new Date(sch.weekStart + 'T12:00:00');
    const d = new Date(base);
    d.setDate(base.getDate() + dayIndex);
    const dateKey = d.toISOString().slice(0, 10);
    return sch.sessions.filter(
      (s) => s.sessionDate === dateKey && s.startTime.startsWith(start.slice(0, 2))
    );
  }

  saveTopic(): void {
    this.api.saveTopic(this.newTopic).subscribe({
      next: () => {
        this.newTopic = { name: '', description: '', color: '#3B82F6', isActive: true };
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  saveClassroom(): void {
    this.api.saveClassroom(this.newClassroom).subscribe({
      next: () => {
        this.newClassroom = { name: '', identifier: '', capacity: 15, location: '', isActive: true };
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  createSession(): void {
    if (!this.createForm.sessionDate || !this.createForm.topicId || !this.createForm.classroomId) {
      this.error.set('Completa fecha, tema y aula.');
      return;
    }
    this.api.createSession({
      sessionDate: this.createForm.sessionDate,
      startTime: this.createForm.startTime,
      endTime: this.createForm.endTime,
      topicId: this.createForm.topicId,
      classroomId: this.createForm.classroomId,
      capacity: this.createForm.capacity > 0 ? this.createForm.capacity : undefined,
      notes: this.createForm.notes || undefined
    }).subscribe({
      next: () => {
        this.createForm.notes = '';
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  pickSlot(dayIndex: number, start: string): void {
    const sch = this.schedule();
    if (!sch) {
      return;
    }
    const base = new Date(sch.weekStart + 'T12:00:00');
    const d = new Date(base);
    d.setDate(base.getDate() + dayIndex);
    this.createForm.sessionDate = d.toISOString().slice(0, 10);
    this.createForm.startTime = start;
    const hour = parseInt(start.slice(0, 2), 10) + 2;
    const endHour = hour >= 24 ? 23 : hour;
    this.createForm.endTime = `${String(endHour).padStart(2, '0')}:59`;
    this.tab.set('schedule');
  }
}
