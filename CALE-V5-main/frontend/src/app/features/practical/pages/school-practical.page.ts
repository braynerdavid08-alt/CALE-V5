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
  PRACTICAL_MAX_DAILY_HOURS,
  PRACTICAL_START_HOURS,
  PracticalApi,
  PracticalDurationHours,
  PracticalLessonSessionDto,
  PracticalSchedulingStudentDto,
  PracticalSessionKind,
  PracticalVehicleDto,
  TimeSlot,
  buildPracticalSlot,
  formatLessonWindow,
  isPracticalExam,
  lessonCoversStart,
  lessonDurationHours,
  practicalKindLabel
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

  readonly maxDailyHours = PRACTICAL_MAX_DAILY_HOURS;
  readonly dayLabels = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];
  readonly kindChoices: { value: PracticalSessionKind; label: string }[] = [
    { value: 'Lesson', label: 'Clase de manejo' },
    { value: 'Exam', label: 'Examen de manejo' }
  ];
  readonly durationChoices: { value: PracticalDurationHours; label: string }[] = [
    { value: 1, label: '1 hora' },
    { value: 2, label: '2 horas' }
  ];

  /** Lesson (1h/2h) or Exam (always 1h). */
  readonly sessionKind = signal<PracticalSessionKind>('Lesson');
  /** Selected class length for grid publish/assign and fixed panel. */
  readonly durationHours = signal<PracticalDurationHours>(2);

  readonly loading = signal(true);
  readonly gridLoading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly vehicles = signal<PracticalVehicleDto[]>([]);
  readonly lessons = signal<PracticalLessonSessionDto[]>([]);
  readonly teachers = signal<MemberRow[]>([]);
  readonly students = signal<PracticalSchedulingStudentDto[]>([]);
  readonly weekStart = signal(this.startOfWeek(new Date()));
  readonly instructorId = signal(0);
  readonly vehicleId = signal(0);
  readonly showVehicles = signal(false);
  readonly showFixedPanel = signal(false);
  readonly picker = signal<PickerCell | null>(null);
  readonly studentSearch = signal('');
  readonly mobileDay = signal(this.defaultMobileDay());
  readonly notice = signal<string | null>(null);

  vehicleForm = { label: '', plate: '', isActive: true };

  /** one = single day slot; range = month/year bulk of empty slots. */
  createMode: 'one' | 'range' = 'one';
  fixedDate = '';
  bulkFrom = '';
  bulkTo = '';
  /** .NET DayOfWeek: Mon=1 … Sat=6 */
  bulkDays: number[] = [1, 2, 3, 4, 5];
  readonly bulkDayChoices = [
    { value: 1, label: 'Lun' },
    { value: 2, label: 'Mar' },
    { value: 3, label: 'Mié' },
    { value: 4, label: 'Jue' },
    { value: 5, label: 'Vie' },
    { value: 6, label: 'Sáb' }
  ];
  fixedSlotStart = '10:00';

  /** Hourly grid rows (06:00–22:00) built with the selected duration. */
  readonly timeSlots = computed(() =>
    PRACTICAL_START_HOURS.map((h) => buildPracticalSlot(h, this.durationHours()))
  );

  readonly startHourOptions = computed(() =>
    PRACTICAL_START_HOURS.filter((h) => h + this.durationHours() <= 24).map((h) =>
      buildPracticalSlot(h, this.durationHours())
    )
  );

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
    this.mobileDay.set(this.defaultMobileDay());
    this.loadWeek();
  }

  setMobileDay(dayIndex: number): void {
    this.mobileDay.set(dayIndex);
  }

  dayChipLabel(dayIndex: number): string {
    const d = this.parseDate(this.weekStart());
    d.setDate(d.getDate() + dayIndex);
    const short = this.dayLabels[dayIndex].slice(0, 3);
    return `${short} ${d.getDate()}`;
  }

  private defaultMobileDay(): number {
    const dow = new Date().getDay();
    if (dow >= 1 && dow <= 6) {
      return dow - 1;
    }
    return 0;
  }

  dayDate(dayIndex: number): string {
    const d = this.parseDate(this.weekStart());
    d.setDate(d.getDate() + dayIndex);
    return this.toDateKey(d);
  }

  dayHeader(dayIndex: number): string {
    const d = this.parseDate(this.weekStart());
    d.setDate(d.getDate() + dayIndex);
    const hours = this.dayScheduledHours(dayIndex);
    const hoursLabel = hours > 0 ? ` · ${hours}h/${this.maxDailyHours}h` : '';
    return `${this.dayLabels[dayIndex]} ${d.getDate()}${hoursLabel}`;
  }

  dayScheduledHours(dayIndex: number): number {
    const date = this.dayDate(dayIndex);
    return this.lessons()
      .filter((l) => l.sessionDate === date)
      .reduce((sum, l) => sum + lessonDurationHours(l), 0);
  }

  setSessionKind(kind: PracticalSessionKind): void {
    this.sessionKind.set(kind);
    if (kind === 'Exam') {
      this.setDurationHours(1);
    }
  }

  setDurationHours(hours: PracticalDurationHours): void {
    if (this.sessionKind() === 'Exam' && hours !== 1) {
      hours = 1;
    }
    this.durationHours.set(hours);
    const options = this.startHourOptions();
    if (!options.some((s) => s.start === this.fixedSlotStart) && options[0]) {
      this.fixedSlotStart = options[0].start;
    }
  }

  kindLabel(lesson?: PracticalLessonSessionDto | null): string {
    return practicalKindLabel(lesson?.kind ?? this.sessionKind());
  }

  isExam(lesson?: PracticalLessonSessionDto | null): boolean {
    return lesson ? isPracticalExam(lesson) : this.sessionKind() === 'Exam';
  }

  /** True when this hour row can start a new empty slot with the selected duration. */
  canPublishSlot(dayIndex: number, slot: TimeSlot): boolean {
    if (this.coveringLesson(dayIndex, slot)) {
      return false;
    }
    const hours = this.durationHours();
    if (parseInt(slot.start, 10) + hours > 24) {
      return false;
    }
    for (let i = 0; i < hours; i++) {
      const hourStart = `${String(parseInt(slot.start, 10) + i).padStart(2, '0')}:00`;
      if (this.coveringLessonAt(dayIndex, hourStart)) {
        return false;
      }
    }
    return this.dayScheduledHours(dayIndex) + hours <= this.maxDailyHours;
  }

  canAssignSlot(dayIndex: number, slot: TimeSlot): boolean {
    const existing = this.lessonStartingAt(dayIndex, slot);
    if (existing) {
      return !existing.assignment;
    }
    return this.canPublishSlot(dayIndex, slot);
  }

  isDayAtHourLimit(dayIndex: number): boolean {
    return this.dayScheduledHours(dayIndex) >= this.maxDailyHours;
  }

  fixedSlot(): TimeSlot {
    return (
      this.startHourOptions().find((s) => s.start === this.fixedSlotStart) ??
      this.startHourOptions()[0] ??
      buildPracticalSlot(10, this.durationHours())
    );
  }

  lessonWindow(lesson: PracticalLessonSessionDto): string {
    return formatLessonWindow(lesson);
  }

  setCreateMode(mode: 'one' | 'range'): void {
    this.createMode = mode;
    this.notice.set(null);
    if (mode === 'range' && (!this.bulkFrom || !this.bulkTo)) {
      this.applyBulkMonth();
    }
    if (mode === 'one' && !this.fixedDate) {
      this.fixedDate = this.dayDate(this.mobileDay());
    }
  }

  applyBulkMonth(): void {
    const start = this.parseDate(this.weekStart());
    const from = new Date(start.getFullYear(), start.getMonth(), 1);
    const to = new Date(start.getFullYear(), start.getMonth() + 1, 0);
    this.bulkFrom = this.toDateKey(from);
    this.bulkTo = this.toDateKey(to);
  }

  applyBulkYear(): void {
    const start = this.parseDate(this.weekStart());
    const today = new Date();
    const from = today > start ? today : start;
    const to = new Date(start.getFullYear(), 11, 31);
    this.bulkFrom = this.toDateKey(from);
    this.bulkTo = this.toDateKey(to);
  }

  toggleBulkDay(day: number, on: boolean): void {
    if (on) {
      if (!this.bulkDays.includes(day)) {
        this.bulkDays = [...this.bulkDays, day].sort((a, b) => a - b);
      }
      return;
    }
    this.bulkDays = this.bulkDays.filter((d) => d !== day);
  }

  isBulkDayOn(day: number): boolean {
    return this.bulkDays.includes(day);
  }

  publishFixedHours(): void {
    const instructor = this.instructorId();
    const vehicle = this.vehicleId();
    const slot = this.fixedSlot();
    if (!instructor || !vehicle || !slot) {
      this.error.set('Selecciona instructor y vehículo primero.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    if (this.createMode === 'one') {
      if (!this.fixedDate) {
        this.saving.set(false);
        this.error.set('Elige la fecha del cupo.');
        return;
      }
      this.api
        .createLesson({
          sessionDate: this.fixedDate,
          startTime: slot.start,
          endTime: slot.end,
          instructorUserId: instructor,
          vehicleId: vehicle,
          capacity: 1,
          kind: this.sessionKind()
        })
        .subscribe({
          next: () => {
            this.saving.set(false);
            this.notice.set(
              `${this.kindLabel()} publicado. La escuela puede asignar o el estudiante agendar.`
            );
            this.loadWeek();
          },
          error: (err) => {
            this.saving.set(false);
            this.error.set(mapApiError(err));
          }
        });
      return;
    }

    if (!this.bulkFrom || !this.bulkTo) {
      this.saving.set(false);
      this.error.set('Indica el rango de fechas.');
      return;
    }
    if (!this.bulkDays.length) {
      this.saving.set(false);
      this.error.set('Selecciona al menos un día de la semana.');
      return;
    }

    this.api
      .bulkCreateLessons({
        fromDate: this.bulkFrom,
        toDate: this.bulkTo,
        daysOfWeek: this.bulkDays,
        startTime: slot.start,
        endTime: slot.end,
        instructorUserId: instructor,
        vehicleId: vehicle,
        capacity: 1,
        kind: this.sessionKind()
      })
      .subscribe({
        next: (res) => {
          this.saving.set(false);
          const parts = [`${res.created} ${this.kindLabel().toLowerCase()}(s) publicados`];
          if (res.skippedExisting) {
            parts.push(`${res.skippedExisting} ya existían`);
          }
          if (res.skippedConflictOrInvalid) {
            parts.push(`${res.skippedConflictOrInvalid} omitidos (conflicto o límite 8h)`);
          }
          this.notice.set(parts.join(' · ') + '. Quedan libres para asignar o que el estudiante agende.');
          this.loadWeek();
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  publishEmptySlot(dayIndex: number, slot: TimeSlot): void {
    const instructor = this.instructorId();
    const vehicle = this.vehicleId();
    if (!instructor || !vehicle) {
      this.error.set('Selecciona instructor y vehículo primero.');
      return;
    }
    if (!this.canPublishSlot(dayIndex, slot)) {
      this.error.set(`Este instructor ya alcanzó el máximo de ${this.maxDailyHours} horas ese día.`);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.api
      .createLesson({
        sessionDate: this.dayDate(dayIndex),
        startTime: slot.start,
        endTime: slot.end,
        instructorUserId: instructor,
        vehicleId: vehicle,
        capacity: 1,
        kind: this.sessionKind()
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.loadWeek();
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  cancelOpenLesson(lesson: PracticalLessonSessionDto): void {
    if (lesson.assignment) {
      return;
    }
    if (!confirm('¿Quitar este cupo libre del horario?')) {
      return;
    }
    this.api.cancelLesson(lesson.id).subscribe({
      next: () => this.loadWeek(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  /** Lesson that starts exactly on this hour row. */
  lessonStartingAt(dayIndex: number, slot: TimeSlot): PracticalLessonSessionDto | null {
    const date = this.dayDate(dayIndex);
    return (
      this.lessons().find((l) => l.sessionDate === date && l.startTime.slice(0, 5) === slot.start) ?? null
    );
  }

  /** Alias used by templates for the primary (starting) cell. */
  lessonAt(dayIndex: number, slot: TimeSlot): PracticalLessonSessionDto | null {
    return this.lessonStartingAt(dayIndex, slot);
  }

  coveringLesson(dayIndex: number, slot: TimeSlot): PracticalLessonSessionDto | null {
    return this.coveringLessonAt(dayIndex, slot.start);
  }

  coveringLessonAt(dayIndex: number, hourStart: string): PracticalLessonSessionDto | null {
    const date = this.dayDate(dayIndex);
    return this.lessons().find((l) => l.sessionDate === date && lessonCoversStart(l, hourStart)) ?? null;
  }

  /** Continuation row of a multi-hour lesson (not the start hour). */
  continuationLesson(dayIndex: number, slot: TimeSlot): PracticalLessonSessionDto | null {
    const covering = this.coveringLesson(dayIndex, slot);
    if (!covering) {
      return null;
    }
    if (covering.startTime.slice(0, 5) === slot.start) {
      return null;
    }
    return covering;
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
    const existing = this.lessonStartingAt(dayIndex, slot);
    if (!existing && !this.canPublishSlot(dayIndex, slot)) {
      this.error.set(
        this.coveringLesson(dayIndex, slot)
          ? 'Ese horario ya está ocupado por otra clase.'
          : `Este instructor ya alcanzó el máximo de ${this.maxDailyHours} horas ese día.`
      );
      return;
    }
    if (existing?.assignment) {
      return;
    }
    const activeSlot = existing
      ? buildPracticalSlot(
          parseInt(existing.startTime, 10),
          (lessonDurationHours(existing) === 1 ? 1 : 2) as PracticalDurationHours
        )
      : buildPracticalSlot(parseInt(slot.start, 10), this.durationHours());
    this.studentSearch.set('');
    this.picker.set({
      dayIndex,
      slot: activeSlot,
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
      studentUserId: student.studentUserId,
      kind: this.sessionKind()
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

  toggleFixedPanel(): void {
    const next = !this.showFixedPanel();
    this.showFixedPanel.set(next);
    if (next) {
      this.notice.set(null);
      if (!this.fixedDate) {
        this.fixedDate = this.dayDate(this.mobileDay());
      }
      if (!this.bulkFrom || !this.bulkTo) {
        this.applyBulkMonth();
      }
    }
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
