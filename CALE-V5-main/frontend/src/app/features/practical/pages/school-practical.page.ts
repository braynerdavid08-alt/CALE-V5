import { forkJoin } from 'rxjs';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { env } from '../../../core/config/env';
import {
  PRACTICAL_TIME_SLOTS,
  PracticalApi,
  PracticalAttendanceRowDto,
  PracticalLessonSessionDto,
  PracticalSchedulingStudentDto,
  PracticalVehicleDto,
  TimeSlot
} from '../api/practical.api';

interface MemberRow {
  id: number;
  name: string;
  email: string;
  role: string;
}

interface PickerCell {
  dayIndex: number;
  slot: TimeSlot;
  sessionDate: string;
}

@Component({
  selector: 'app-school-practical-page',
  standalone: true,
  imports: [
    FormsModule,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  templateUrl: './school-practical.page.html',
  styleUrl: './school-practical.page.css'
})
export class SchoolPracticalPage implements OnInit {
  private readonly api = inject(PracticalApi);
  private readonly http = inject(HttpClient);

  readonly timeSlots = PRACTICAL_TIME_SLOTS;
  readonly dayLabels = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];

  readonly loading = signal(true);
  readonly gridLoading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly tab = signal<'schedule' | 'attendance'>('schedule');
  readonly vehicles = signal<PracticalVehicleDto[]>([]);
  readonly lessons = signal<PracticalLessonSessionDto[]>([]);
  readonly teachers = signal<MemberRow[]>([]);
  readonly students = signal<PracticalSchedulingStudentDto[]>([]);
  readonly weekStart = signal(this.startOfWeek(new Date()));
  readonly instructorId = signal(0);
  readonly vehicleId = signal(0);
  readonly showVehicles = signal(false);
  readonly picker = signal<PickerCell | null>(null);
  readonly studentSearch = signal('');

  readonly attendanceLessons = signal<PracticalLessonSessionDto[]>([]);
  readonly selectedAttendanceLessonId = signal<number | null>(null);
  readonly attendanceRows = signal<PracticalAttendanceRowDto[]>([]);
  readonly attendanceLoading = signal(false);

  vehicleForm = { label: '', plate: '', isActive: true };

  readonly weekRangeLabel = computed(() => {
    const start = this.parseDate(this.weekStart());
    const end = new Date(start);
    end.setDate(end.getDate() + 5);
    return `${this.formatDay(start)} – ${this.formatDay(end)}`;
  });

  readonly filteredStudents = computed(() => {
    const q = this.studentSearch().trim().toLowerCase();
    const rows = this.students();
    if (!q) {
      return rows;
    }
    return rows.filter((s) => s.studentName.toLowerCase().includes(q));
  });

  readonly selectedInstructorName = computed(() => {
    const id = this.instructorId();
    return this.teachers().find((t) => t.id === id)?.name ?? '';
  });

  readonly selectedVehicleLabel = computed(() => {
    const id = this.vehicleId();
    const vehicle = this.vehicles().find((v) => v.id === id);
    if (!vehicle) {
      return '';
    }
    return vehicle.plate ? `${vehicle.label} (${vehicle.plate})` : vehicle.label;
  });

  ngOnInit(): void {
    this.reload();
  }

  setTab(value: 'schedule' | 'attendance'): void {
    this.tab.set(value);
    if (value === 'attendance') {
      this.loadAttendanceLessons();
    }
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      members: this.http.get<MemberRow[]>(`${env.apiUrl}/api/school/members`),
      vehicles: this.api.listVehicles(true),
      students: this.api.listSchedulingStudents()
    }).subscribe({
      next: ({ members, vehicles, students }) => {
        const teachers = members.filter((m) => m.role === 'Teacher');
        this.teachers.set(teachers);
        if (!this.instructorId() && teachers[0]) {
          this.instructorId.set(teachers[0].id);
        }

        this.vehicles.set(vehicles);
        if (!this.vehicleId() && vehicles[0]) {
          this.vehicleId.set(vehicles[0].id);
        }

        this.students.set(students);
        this.loading.set(false);
        this.loadWeek();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  loadWeek(): void {
    const instructor = this.instructorId();
    const vehicle = this.vehicleId();
    if (!instructor || !vehicle) {
      this.lessons.set([]);
      return;
    }

    this.gridLoading.set(true);
    this.api.listLessons(this.weekStart(), instructor, vehicle).subscribe({
      next: (rows) => {
        this.lessons.set(rows);
        this.gridLoading.set(false);
      },
      error: (err) => {
        this.gridLoading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  onInstructorChange(value: number): void {
    this.instructorId.set(value);
    this.loadWeek();
  }

  onVehicleChange(value: number): void {
    this.vehicleId.set(value);
    this.loadWeek();
  }

  prevWeek(): void {
    const d = this.parseDate(this.weekStart());
    d.setDate(d.getDate() - 7);
    this.weekStart.set(this.toDateKey(d));
    this.loadWeek();
  }

  nextWeek(): void {
    const d = this.parseDate(this.weekStart());
    d.setDate(d.getDate() + 7);
    this.weekStart.set(this.toDateKey(d));
    this.loadWeek();
  }

  goToday(): void {
    this.weekStart.set(this.startOfWeek(new Date()));
    this.loadWeek();
  }

  dayDate(dayIndex: number): string {
    const d = this.parseDate(this.weekStart());
    d.setDate(d.getDate() + dayIndex);
    return this.toDateKey(d);
  }

  dayHeader(dayIndex: number): string {
    const d = this.parseDate(this.weekStart());
    d.setDate(d.getDate() + dayIndex);
    return `${this.dayLabels[dayIndex]} ${d.getDate()}`;
  }

  lessonAt(dayIndex: number, slot: TimeSlot): PracticalLessonSessionDto | null {
    const date = this.dayDate(dayIndex);
    return this.lessons().find((l) => l.sessionDate === date && l.startTime.slice(0, 5) === slot.start) ?? null;
  }

  assignmentLabel(lesson: PracticalLessonSessionDto): string {
    const a = lesson.assignment;
    if (!a) {
      return '';
    }
    const cat = a.licenseCategory ? `${a.licenseCategory} ` : '';
    return `${cat}${a.studentName} ${a.lessonNumber}/${a.lessonsRequired}`;
  }

  openPicker(dayIndex: number, slot: TimeSlot): void {
    if (!this.instructorId() || !this.vehicleId()) {
      this.error.set('Selecciona instructor y vehículo primero.');
      return;
    }
    this.studentSearch.set('');
    this.picker.set({
      dayIndex,
      slot,
      sessionDate: this.dayDate(dayIndex)
    });
  }

  closePicker(): void {
    this.picker.set(null);
  }

  assignStudent(student: PracticalSchedulingStudentDto): void {
    const cell = this.picker();
    const instructor = this.instructorId();
    const vehicle = this.vehicleId();
    if (!cell || !instructor || !vehicle) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.api.quickAssign({
      sessionDate: cell.sessionDate,
      startTime: cell.slot.start,
      endTime: cell.slot.end,
      instructorUserId: instructor,
      vehicleId: vehicle,
      studentUserId: student.studentUserId
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.closePicker();
        this.loadWeek();
        this.api.listSchedulingStudents().subscribe({
          next: (rows) => this.students.set(rows)
        });
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  unassignLesson(lesson: PracticalLessonSessionDto): void {
    if (!confirm(`¿Quitar a ${lesson.assignment?.studentName ?? 'el estudiante'} de este horario?`)) {
      return;
    }
    this.api.unassignStudent(lesson.id).subscribe({
      next: () => this.loadWeek(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  markLessonPresent(lesson: PracticalLessonSessionDto): void {
    const studentId = lesson.assignment?.studentUserId;
    if (!studentId) {
      return;
    }
    this.api.markAttendance(lesson.id, studentId, 'Present').subscribe({
      next: () => this.loadWeek(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  duplicateWeek(): void {
    const instructor = this.instructorId();
    const vehicle = this.vehicleId();
    if (!instructor || !vehicle) {
      return;
    }
    if (!confirm('¿Copiar los horarios de la semana anterior (sin estudiantes)?')) {
      return;
    }
    this.saving.set(true);
    this.api.duplicateWeek({
      weekStart: this.weekStart(),
      instructorUserId: instructor,
      vehicleId: vehicle
    }).subscribe({
      next: (res) => {
        this.saving.set(false);
        if (res.created === 0) {
          this.error.set('No había horarios en la semana anterior para copiar.');
        } else {
          this.loadWeek();
        }
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  saveVehicle(): void {
    if (!this.vehicleForm.label.trim()) {
      return;
    }
    this.saving.set(true);
    this.api.saveVehicle({
      label: this.vehicleForm.label.trim(),
      plate: this.vehicleForm.plate.trim() || null,
      isActive: this.vehicleForm.isActive
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.vehicleForm = { label: '', plate: '', isActive: true };
        this.api.listVehicles(true).subscribe({
          next: (v) => this.vehicles.set(v)
        });
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  loadAttendanceLessons(): void {
    this.attendanceLoading.set(true);
    this.api.listAttendanceLessons().subscribe({
      next: (sessions) => {
        this.attendanceLessons.set(sessions);
        const current = this.selectedAttendanceLessonId();
        if (!current && sessions.length) {
          this.selectAttendanceLesson(sessions[0].id);
        } else if (current) {
          this.loadAttendanceRows(current);
        } else {
          this.attendanceLoading.set(false);
        }
      },
      error: (err) => {
        this.attendanceLoading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  selectAttendanceLesson(id: number): void {
    this.selectedAttendanceLessonId.set(id);
    this.loadAttendanceRows(id);
  }

  loadAttendanceRows(lessonId: number): void {
    this.attendanceLoading.set(true);
    this.api.listAttendance(lessonId).subscribe({
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

  markAttendance(row: PracticalAttendanceRowDto, status: string): void {
    const lessonId = this.selectedAttendanceLessonId();
    if (!lessonId) {
      return;
    }
    this.api.markAttendance(lessonId, row.studentUserId, status).subscribe({
      next: () => this.loadAttendanceRows(lessonId),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  markAllPresent(): void {
    const lessonId = this.selectedAttendanceLessonId();
    const rows = this.attendanceRows();
    if (!lessonId || !rows.length) {
      return;
    }
    this.api.markAllPresent(lessonId, rows).subscribe({
      next: () => this.loadAttendanceRows(lessonId),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  lessonLabel(lesson: PracticalLessonSessionDto): string {
    return `${lesson.sessionDate} · ${lesson.startTime.slice(0, 5)} – ${lesson.endTime.slice(0, 5)}`;
  }

  studentProgressLabel(student: PracticalSchedulingStudentDto): string {
    return `${student.nextLessonNumber}/${student.requiredLessons}`;
  }

  private startOfWeek(date: Date): string {
    const d = new Date(date);
    const day = d.getDay();
    const diff = day === 0 ? -6 : 1 - day;
    d.setDate(d.getDate() + diff);
    return this.toDateKey(d);
  }

  private parseDate(value: string): Date {
    const [y, m, d] = value.split('-').map(Number);
    return new Date(y, m - 1, d);
  }

  private toDateKey(date: Date): string {
    const y = date.getFullYear();
    const m = `${date.getMonth() + 1}`.padStart(2, '0');
    const d = `${date.getDate()}`.padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  private formatDay(date: Date): string {
    return date.toLocaleDateString('es-CO', { day: 'numeric', month: 'short' });
  }
}
