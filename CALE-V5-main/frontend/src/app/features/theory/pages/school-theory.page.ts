import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  TheoryApi,
  AttendanceRowDto,
  EnrollmentDto,
  TheoryClassroomDto,
  TheoryClassSessionDto,
  TheoryMonthScheduleDto,
  TheorySchoolDashboardDto,
  TheorySettingsDto,
  TheoryTopicDto,
  TheoryWeekScheduleDto
} from '../api/theory.api';

@Component({
  selector: 'app-school-theory-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent, UiPageHeaderComponent],
  templateUrl: './school-theory.page.html',
  styleUrl: './school-theory.page.css'
})
export class SchoolTheoryPage implements OnInit {
  private readonly api = inject(TheoryApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly tab = signal<'schedule' | 'students' | 'attendance' | 'topics' | 'classrooms' | 'settings'>('schedule');
  readonly dashboard = signal<TheorySchoolDashboardDto | null>(null);
  readonly schedule = signal<TheoryWeekScheduleDto | null>(null);
  readonly monthSchedule = signal<TheoryMonthScheduleDto | null>(null);
  readonly weekStart = signal(this.startOfWeekIso(new Date()));
  readonly monthKey = signal(this.currentMonthKey());
  readonly editingSessionId = signal<number | null>(null);
  readonly scheduleLoading = signal(false);
  readonly topics = signal<TheoryTopicDto[]>([]);
  readonly classrooms = signal<TheoryClassroomDto[]>([]);
  readonly attendanceSessions = signal<TheoryClassSessionDto[]>([]);
  readonly selectedAttendanceSessionId = signal<number | null>(null);
  readonly attendanceRows = signal<AttendanceRowDto[]>([]);
  readonly settings = signal<TheorySettingsDto | null>(null);
  readonly examOptions = signal<Array<{ id: number; name: string }>>([]);
  readonly enrollments = signal<EnrollmentDto[]>([]);
  readonly enrollmentFilter = signal<'all' | 'weekday' | 'saturday' | 'unassigned'>('all');
  readonly enrollmentSearch = signal('');
  readonly attendanceLoading = signal(false);

  readonly licenseCategoryOptions = [
    { value: 'A2', label: 'A2' },
    { value: 'B1', label: 'B1' },
    { value: 'C1', label: 'C1' },
    { value: 'A2,B1', label: 'A2 + B1' },
    { value: 'A2,C1', label: 'A2 + C1' }
  ];

  readonly dayLabels = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];
  readonly timeSlots = [
    '00:00', '02:00', '04:00', '06:00', '08:00', '10:00',
    '12:00', '14:00', '16:00', '18:00', '20:00', '22:00'
  ];

  newTopic = { name: '', description: '', color: '#3B82F6', category: 'Theory', isActive: true };
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
    this.loadScheduleData();
    this.api.getSettings().subscribe({
      next: (s) => this.settings.set(this.normalizeSettings(s)),
      error: () => this.settings.set(null)
    });
    this.api.listExamOptions().subscribe({
      next: (opts) => this.examOptions.set(opts),
      error: () => this.examOptions.set([])
    });
  }

  loadScheduleData(): void {
    this.scheduleLoading.set(true);
    const week = this.weekStart();
    const month = this.monthKey() + '-01';
    this.api.schoolSchedule(week).subscribe({
      next: (s) => {
        this.schedule.set(s);
        this.scheduleLoading.set(false);
      },
      error: () => {
        this.schedule.set(null);
        this.scheduleLoading.set(false);
      }
    });
    this.api.schoolMonthSchedule(month).subscribe({
      next: (m) => this.monthSchedule.set(m),
      error: () => this.monthSchedule.set(null)
    });
  }

  weekRangeLabel(): string {
    const sch = this.schedule();
    if (sch?.weekStart && sch?.weekEnd) {
      return `${this.formatDisplayDate(sch.weekStart)} – ${this.formatDisplayDate(sch.weekEnd)}`;
    }
    const start = this.parseDate(this.weekStart());
    const end = this.addDays(start, 6);
    return `${this.formatDisplayDate(this.weekStart())} – ${this.formatDisplayDate(this.formatDateOnly(end))}`;
  }

  monthLabel(): string {
    const [y, m] = this.monthKey().split('-').map(Number);
    const d = new Date(y, m - 1, 1);
    return d.toLocaleDateString('es-CO', { month: 'long', year: 'numeric' });
  }

  dayHeader(dayIndex: number): string {
    const sch = this.schedule();
    if (!sch) {
      return this.dayLabels[dayIndex];
    }
    const d = this.addDays(this.parseDate(sch.weekStart), dayIndex);
    return `${this.dayLabels[dayIndex]} ${d.getDate()}`;
  }

  prevWeek(): void {
    this.weekStart.set(this.addDaysIso(this.weekStart(), -7));
    this.syncMonthFromWeek();
    this.loadScheduleData();
  }

  nextWeek(): void {
    this.weekStart.set(this.addDaysIso(this.weekStart(), 7));
    this.syncMonthFromWeek();
    this.loadScheduleData();
  }

  prevMonth(): void {
    this.monthKey.set(this.shiftMonth(this.monthKey(), -1));
    this.weekStart.set(this.startOfWeekIso(this.parseDate(`${this.monthKey()}-01`)));
    this.loadScheduleData();
  }

  nextMonth(): void {
    this.monthKey.set(this.shiftMonth(this.monthKey(), 1));
    this.weekStart.set(this.startOfWeekIso(this.parseDate(`${this.monthKey()}-01`)));
    this.loadScheduleData();
  }

  goToday(): void {
    const today = new Date();
    this.monthKey.set(this.currentMonthKey(today));
    this.weekStart.set(this.startOfWeekIso(today));
    this.loadScheduleData();
  }

  onMonthPick(value: string): void {
    if (!value) {
      return;
    }
    this.monthKey.set(value);
    this.weekStart.set(this.startOfWeekIso(this.parseDate(`${value}-01`)));
    this.loadScheduleData();
  }

  jumpToSessionWeek(sessionDate: string): void {
    this.weekStart.set(this.startOfWeekIso(this.parseDate(sessionDate)));
    const [y, m] = sessionDate.split('-');
    this.monthKey.set(`${y}-${m}`);
    this.loadScheduleData();
  }

  selectSessionForEdit(session: TheoryClassSessionDto): void {
    this.editingSessionId.set(session.id);
    this.createForm = {
      sessionDate: session.sessionDate,
      startTime: session.startTime.slice(0, 5),
      endTime: session.endTime.slice(0, 5),
      topicId: session.topicId,
      classroomId: session.classroomId,
      capacity: session.capacity,
      notes: session.notes ?? ''
    };
    this.jumpToSessionWeek(session.sessionDate);
  }

  resetSessionForm(): void {
    this.editingSessionId.set(null);
    this.createForm = {
      sessionDate: '',
      startTime: '08:00',
      endTime: '09:59',
      topicId: this.topics()[0]?.id ?? 0,
      classroomId: this.classrooms()[0]?.id ?? 0,
      capacity: 0,
      notes: ''
    };
  }

  cancelEditingSession(): void {
    if (!this.editingSessionId()) {
      return;
    }
    const id = this.editingSessionId()!;
    if (!confirm('¿Eliminar esta clase? Se quitarán todas las reservas y desaparecerá del horario.')) {
      return;
    }
    this.api.cancelSession(id).subscribe({
      next: () => {
        this.resetSessionForm();
        this.loadScheduleData();
        this.api.schoolDashboard().subscribe({
          next: (d) => this.dashboard.set(d),
          error: () => this.dashboard.set(null)
        });
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private syncMonthFromWeek(): void {
    const week = this.parseDate(this.weekStart());
    this.monthKey.set(this.currentMonthKey(week));
  }

  private currentMonthKey(date = new Date()): string {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    return `${y}-${m}`;
  }

  private shiftMonth(key: string, delta: number): string {
    const [y, m] = key.split('-').map(Number);
    const d = new Date(y, m - 1 + delta, 1);
    return this.currentMonthKey(d);
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

  setTab(value: 'schedule' | 'students' | 'attendance' | 'topics' | 'classrooms' | 'settings'): void {
    this.tab.set(value);
    if (value === 'attendance') {
      this.loadAttendanceSessions();
    }
    if (value === 'students') {
      this.loadEnrollments();
    }
  }

  loadEnrollments(): void {
    this.api.listEnrollments().subscribe({
      next: (rows) => this.enrollments.set(rows),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  saveEnrollment(row: EnrollmentDto, activate = false): void {
    const current = this.enrollments().find((r) => r.studentUserId === row.studentUserId) ?? row;
    const dayType = current.attendanceDayType ?? null;
    const license = current.licenseCategories ?? null;
    const status = activate ? 'Active' : current.status;
    this.api.updateEnrollment(row.studentUserId, {
      status,
      attendanceDayType: dayType,
      allowedStartTime: null,
      licenseCategories: license,
      theoryExamAuthorized: current.theoryExamAuthorized,
      practicalAuthorized: current.practicalAuthorized
    }).subscribe({
      next: () => {
        this.error.set(null);
        this.loadEnrollments();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  canAuthorizeTheoryExam(row: EnrollmentDto): boolean {
    const pe = row.practicalEligibility;
    return !!pe
      && pe.theoryHoursComplete
      && pe.workshopHoursComplete
      && !pe.theoryExamPassed
      && (row.balanceDue ?? 0) <= 0;
  }

  canAuthorizePractical(row: EnrollmentDto): boolean {
    return !!row.practicalEligibility?.theoryExamPassed
      && (row.balanceDue ?? 0) <= 0;
  }

  hasBalanceDue(row: EnrollmentDto): boolean {
    return (row.balanceDue ?? 0) > 0;
  }

  toggleTheoryExamAuth(row: EnrollmentDto, authorized: boolean): void {
    this.api.updateEnrollment(row.studentUserId, {
      status: row.status,
      attendanceDayType: row.attendanceDayType,
      allowedStartTime: null,
      licenseCategories: row.licenseCategories,
      theoryExamAuthorized: authorized
    }).subscribe({
      next: () => {
        this.error.set(null);
        this.loadEnrollments();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  togglePracticalAuth(row: EnrollmentDto, authorized: boolean): void {
    this.api.updateEnrollment(row.studentUserId, {
      status: row.status,
      attendanceDayType: row.attendanceDayType,
      allowedStartTime: null,
      licenseCategories: row.licenseCategories,
      practicalAuthorized: authorized
    }).subscribe({
      next: () => {
        this.error.set(null);
        this.loadEnrollments();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  eligibleForExamCount(): number {
    return this.enrollments().filter((row) => this.canAuthorizeTheoryExam(row)).length;
  }

  eligibleForPracticalCount(): number {
    return this.enrollments().filter((row) => this.canAuthorizePractical(row) && !row.practicalAuthorized).length;
  }

  bulkAuthorizeExam(): void {
    const count = this.eligibleForExamCount();
    if (count === 0) {
      this.error.set('No hay estudiantes elegibles para autorizar examen.');
      return;
    }
    if (!confirm(`¿Autorizar examen teórico a ${count} estudiante(s) elegible(s)?`)) {
      return;
    }
    this.api.bulkAuthorize({ theoryExam: true, practical: false }).subscribe({
      next: (result) => {
        this.error.set(null);
        this.loadEnrollments();
        alert(`Autorizados: ${result.authorizedCount}. Omitidos: ${result.skippedCount}.`);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  bulkAuthorizePractical(): void {
    const count = this.eligibleForPracticalCount();
    if (count === 0) {
      this.error.set('No hay estudiantes elegibles para autorizar manejo.');
      return;
    }
    if (!confirm(`¿Autorizar clases de manejo a ${count} estudiante(s) elegible(s)?`)) {
      return;
    }
    this.api.bulkAuthorize({ theoryExam: false, practical: true }).subscribe({
      next: (result) => {
        this.error.set(null);
        this.loadEnrollments();
        alert(`Autorizados: ${result.authorizedCount}. Omitidos: ${result.skippedCount}.`);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  onEnrollmentSelect(
    row: EnrollmentDto,
    field: 'attendanceDayType' | 'licenseCategories',
    value: string
  ): void {
    this.updateEnrollmentField(row, field, value);
    if (field === 'attendanceDayType') {
      this.syncEnrollmentGroupFilter(value);
    }
    this.saveEnrollment(row, false);
  }

  private syncEnrollmentGroupFilter(dayType: string): void {
    if (dayType === 'Weekday') {
      this.enrollmentFilter.set('weekday');
      return;
    }
    if (dayType === 'Saturday') {
      this.enrollmentFilter.set('saturday');
      return;
    }
    this.enrollmentFilter.set('unassigned');
  }

  private applyEnrollmentSearch(rows: EnrollmentDto[]): EnrollmentDto[] {
    const query = this.enrollmentSearch().trim().toLowerCase();
    if (!query) {
      return rows;
    }
    return rows.filter(
      (r) =>
        r.studentName.toLowerCase().includes(query)
        || (r.studentEmail ?? '').toLowerCase().includes(query)
    );
  }

  enrollmentSections(): Array<{ key: string; label: string; rows: EnrollmentDto[] }> {
    const rows = this.applyEnrollmentSearch(this.enrollments());
    return [
      {
        key: 'weekday',
        label: 'Grupo Semana',
        rows: rows.filter((r) => r.attendanceDayType === 'Weekday')
      },
      {
        key: 'saturday',
        label: 'Grupo Sábados',
        rows: rows.filter((r) => r.attendanceDayType === 'Saturday')
      },
      {
        key: 'unassigned',
        label: 'Sin grupo asignado',
        rows: rows.filter((r) => !r.attendanceDayType)
      }
    ];
  }

  visibleEnrollmentSections(): Array<{
    key: string;
    label: string;
    rows: EnrollmentDto[];
    showHeader: boolean;
  }> {
    const filter = this.enrollmentFilter();
    if (filter !== 'all') {
      return [
        {
          key: filter,
          label: '',
          rows: this.filteredEnrollments(),
          showHeader: false
        }
      ];
    }

    return this.enrollmentSections()
      .filter((section) => section.rows.length > 0)
      .map((section) => ({ ...section, showHeader: true }));
  }

  suspendEnrollment(row: EnrollmentDto): void {
    this.api.updateEnrollment(row.studentUserId, {
      status: 'Suspended',
      attendanceDayType: row.attendanceDayType,
      allowedStartTime: null,
      licenseCategories: row.licenseCategories
    }).subscribe({
      next: () => this.loadEnrollments(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  enrollmentStatusLabel(status: string): string {
    switch (status) {
      case 'Active':
      case 'Accepted':
        return 'Autorizado';
      case 'Suspended':
        return 'Suspendido';
      default:
        return 'Pendiente';
    }
  }

  filteredEnrollments(): EnrollmentDto[] {
    const filter = this.enrollmentFilter();
    let rows = this.enrollments();
    if (filter === 'weekday') {
      rows = rows.filter((r) => r.attendanceDayType === 'Weekday');
    } else if (filter === 'saturday') {
      rows = rows.filter((r) => r.attendanceDayType === 'Saturday');
    } else if (filter === 'unassigned') {
      rows = rows.filter((r) => !r.attendanceDayType);
    }
    return this.applyEnrollmentSearch(rows);
  }

  clearEnrollmentSearch(): void {
    this.enrollmentSearch.set('');
  }

  enrollmentCount(filter: 'all' | 'weekday' | 'saturday' | 'unassigned'): number {
    if (filter === 'all') {
      return this.enrollments().length;
    }
    if (filter === 'weekday') {
      return this.enrollments().filter((r) => r.attendanceDayType === 'Weekday').length;
    }
    if (filter === 'saturday') {
      return this.enrollments().filter((r) => r.attendanceDayType === 'Saturday').length;
    }
    return this.enrollments().filter((r) => !r.attendanceDayType).length;
  }

  authorizedCount(filter: 'weekday' | 'saturday'): number {
    return this.enrollments().filter(
      (r) =>
        r.attendanceDayType === (filter === 'weekday' ? 'Weekday' : 'Saturday')
        && (r.status === 'Active' || r.status === 'Accepted')
    ).length;
  }

  groupBadgeClass(dayType?: string | null): string {
    if (dayType === 'Weekday') {
      return 'weekday';
    }
    if (dayType === 'Saturday') {
      return 'saturday';
    }
    return 'none';
  }

  attendanceDayTypeLabel(value?: string | null): string {
    if (value === 'Weekday') {
      return 'Semana';
    }
    if (value === 'Saturday') {
      return 'Sábados';
    }
    return 'Sin asignar';
  }

  licenseCategoryLabel(value?: string | null): string {
    if (!value) {
      return 'Sin asignar';
    }
    const found = this.licenseCategoryOptions.find((o) => o.value === value);
    return found?.label ?? value.replace(/,/g, ' + ');
  }

  private normalizeSettings(settings: TheorySettingsDto): TheorySettingsDto {
    return {
      ...settings,
      weekdaysEnabled: true,
      saturdayEnabled: true
    };
  }

  isDayAllowed(dayIndex: number): boolean {
    return dayIndex >= 0 && dayIndex <= 5;
  }

  schedulingDaysMessage(): string {
    return 'Solo puedes programar clases de lunes a viernes o los sábados.';
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
        this.settings.set(this.normalizeSettings(updated));
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
    const base = this.parseDate(sch.weekStart);
    const d = this.addDays(base, dayIndex);
    const dateKey = this.formatDateOnly(d);
    return sch.sessions.filter(
      (s) => s.sessionDate === dateKey && s.startTime.startsWith(start.slice(0, 2))
    );
  }

  saveTopic(): void {
    this.api.saveTopic(this.newTopic).subscribe({
      next: () => {
        this.newTopic = { name: '', description: '', color: '#3B82F6', category: 'Theory', isActive: true };
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  updateTopic(topic: TheoryTopicDto): void {
    this.api.saveTopic(topic, topic.id).subscribe({
      next: (updated) => {
        this.topics.update((rows) => rows.map((t) => (t.id === updated.id ? updated : t)));
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  onTopicCategoryChange(topic: TheoryTopicDto, category: string): void {
    this.updateTopic({ ...topic, category });
  }

  topicCategoryLabel(category: string): string {
    return category === 'Workshop' ? 'Taller' : 'Teoría';
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

  saveSession(): void {
    if (!this.createForm.sessionDate || !this.createForm.topicId || !this.createForm.classroomId) {
      this.error.set('Completa fecha, tema y aula.');
      return;
    }
    const date = this.parseDate(this.createForm.sessionDate);
    const dayIndex = (date.getDay() + 6) % 7;
    if (!this.isDayAllowed(dayIndex)) {
      this.error.set(this.schedulingDaysMessage());
      return;
    }
    const body = {
      sessionDate: this.createForm.sessionDate,
      startTime: this.createForm.startTime,
      endTime: this.createForm.endTime,
      topicId: this.createForm.topicId,
      classroomId: this.createForm.classroomId,
      capacity: this.createForm.capacity > 0 ? this.createForm.capacity : undefined,
      notes: this.createForm.notes || undefined
    };
    const editId = this.editingSessionId();
    const req = editId
      ? this.api.updateSession(editId, body)
      : this.api.createSession(body);
    req.subscribe({
      next: () => {
        this.createForm.notes = '';
        this.editingSessionId.set(null);
        this.jumpToSessionWeek(body.sessionDate);
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  createSession(): void {
    this.saveSession();
  }

  pickSlot(dayIndex: number, start: string): void {
    if (!this.isDayAllowed(dayIndex)) {
      this.error.set(this.schedulingDaysMessage());
      return;
    }
    const sch = this.schedule();
    if (!sch) {
      return;
    }
    const d = this.addDays(this.parseDate(sch.weekStart), dayIndex);
    this.createForm.sessionDate = this.formatDateOnly(d);
    this.createForm.startTime = start;
    const hour = parseInt(start.slice(0, 2), 10) + 2;
    const endHour = hour >= 24 ? 23 : hour;
    this.createForm.endTime = `${String(endHour).padStart(2, '0')}:59`;
    this.tab.set('schedule');
  }

  updateEnrollmentField(
    row: EnrollmentDto,
    field: 'attendanceDayType' | 'allowedStartTime' | 'status' | 'licenseCategories',
    value: string
  ): void {
    this.enrollments.update((rows) =>
      rows.map((r) =>
        r.studentUserId === row.studentUserId ? { ...r, [field]: value || null } : r
      )
    );
  }
}
