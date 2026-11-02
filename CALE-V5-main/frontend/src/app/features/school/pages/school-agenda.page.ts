import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { env } from '../../../core/config/env';
import { apiErrorCode, mapApiError } from '../../../core/http/map-api-error';
import { UiDialogComponent } from '../../../shared/ui/ui-dialog.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiSheetComponent } from '../../../shared/ui/ui-sheet.component';
import {
  addDays,
  dayTitle,
  hhmm,
  mondayOf,
  time12,
  todayColombia,
  weekRangeLabel
} from '../../../shared/utils/wall-clock';
import {
  PracticalApi,
  PracticalVehicleDto
} from '../../practical/api/practical.api';
import {
  TheoryApi,
  TheoryClassroomDto,
  TheoryTopicDto
} from '../../theory/api/theory.api';
import {
  AgendaItemDto,
  AgendaItemKind,
  ExamScheduleApi,
  SchoolOpsAlertDto
} from '../api/exam-schedule.api';
import { Tone, plural } from '../utils/exam-labels';

type Filter = 'all' | AgendaItemKind;
type ViewMode = 'day' | 'week' | 'month';
type CreateKind = 'theory' | 'practical' | 'exam';

interface AgendaDay {
  date: string;
  title: string;
  isToday: boolean;
  items: AgendaItemDto[];
}

interface MemberRow {
  id: number;
  name: string;
  role: string;
}

const KIND_LABELS: Record<AgendaItemKind, string> = {
  theory: 'Clase teórica',
  practical: 'Clase de manejo',
  exam: 'Examen teórico'
};

const STATUS_LABELS: Record<string, string> = {
  scheduled: 'Programada',
  active: 'Activa',
  available: 'Con cupos',
  full: 'Llena',
  closed: 'Cerrada',
  past: 'Ya pasó',
  completed: 'Terminada',
  cancelled: 'Cancelada',
  inprogress: 'En curso',
  reserved: 'Reservado',
  booked: 'Agendado',
  attended: 'Asistió',
  present: 'Asistió',
  absent: 'No vino',
  noshow: 'No vino',
  checkedin: 'Llegó',
  pending: 'Pendiente'
};

@Component({
  selector: 'app-school-agenda-page',
  standalone: true,
  imports: [RouterLink, UiDialogComponent, UiErrorComponent, UiLoadingComponent, UiSheetComponent],
  templateUrl: './school-agenda.page.html',
  styleUrls: ['../../../shared/styles/easy-schedule.css', './school-agenda.page.css']
})
export class SchoolAgendaPage implements OnInit {
  private readonly api = inject(ExamScheduleApi);
  private readonly theory = inject(TheoryApi);
  private readonly practical = inject(PracticalApi);
  private readonly http = inject(HttpClient);

  readonly today = todayColombia();
  readonly view = signal<ViewMode>('week');
  readonly anchor = signal(mondayOf(this.today));
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly items = signal<AgendaItemDto[]>([]);
  readonly alerts = signal<SchoolOpsAlertDto[]>([]);
  readonly filter = signal<Filter>('all');
  readonly expanded = signal<Set<string>>(new Set());

  readonly cancelTarget = signal<AgendaItemDto | null>(null);
  readonly cancelBusy = signal(false);
  readonly cancelError = signal<string | null>(null);
  readonly cancelForceBookings = signal(false);

  readonly editOpen = signal(false);
  readonly editItem = signal<AgendaItemDto | null>(null);
  readonly editDate = signal(this.today);
  readonly editStart = signal('08:00');
  readonly editEnd = signal('10:00');
  readonly editTopicId = signal(0);
  readonly editClassroomId = signal(0);
  readonly editInstructorId = signal(0);
  readonly editVehicleId = signal(0);
  readonly editCapacity = signal(8);
  readonly editBusy = signal(false);
  readonly editError = signal<string | null>(null);

  readonly createOpen = signal(false);
  readonly createKind = signal<CreateKind>('theory');
  readonly createDate = signal(this.today);
  readonly createStart = signal('08:00');
  readonly createEnd = signal('10:00');
  readonly createBusy = signal(false);
  readonly createError = signal<string | null>(null);

  readonly topics = signal<TheoryTopicDto[]>([]);
  readonly classrooms = signal<TheoryClassroomDto[]>([]);
  readonly teachers = signal<MemberRow[]>([]);
  readonly vehicles = signal<PracticalVehicleDto[]>([]);
  readonly topicId = signal(0);
  readonly classroomId = signal(0);
  readonly instructorId = signal(0);
  readonly vehicleId = signal(0);
  readonly examCapacity = signal(8);
  readonly catalogReady = signal(false);

  readonly views: { value: ViewMode; label: string }[] = [
    { value: 'day', label: 'Día' },
    { value: 'week', label: 'Semana' },
    { value: 'month', label: 'Mes' }
  ];

  readonly filters: { value: Filter; label: string }[] = [
    { value: 'all', label: 'Todo' },
    { value: 'theory', label: 'Clases teóricas' },
    { value: 'practical', label: 'Clases de manejo' },
    { value: 'exam', label: 'Exámenes teóricos' }
  ];

  readonly rangeFrom = computed(() => {
    const v = this.view();
    const a = this.anchor();
    if (v === 'day') return a;
    if (v === 'month') return `${a.slice(0, 8)}01`;
    return mondayOf(a);
  });

  readonly rangeTo = computed(() => {
    const v = this.view();
    const from = this.rangeFrom();
    if (v === 'day') return from;
    if (v === 'month') {
      const [y, m] = from.split('-').map(Number);
      const last = new Date(Date.UTC(y, m, 0)).getUTCDate();
      return `${y}-${String(m).padStart(2, '0')}-${String(last).padStart(2, '0')}`;
    }
    return addDays(from, 6);
  });

  readonly rangeLabel = computed(() => {
    if (this.view() === 'day') return dayTitle(this.rangeFrom());
    if (this.view() === 'month') {
      const [y, m] = this.rangeFrom().split('-');
      return `${m}/${y}`;
    }
    return weekRangeLabel(this.rangeFrom(), this.rangeTo());
  });

  readonly isCurrentRange = computed(() => {
    if (this.view() === 'day') return this.rangeFrom() === this.today;
    if (this.view() === 'month') return this.rangeFrom().slice(0, 7) === this.today.slice(0, 7);
    return this.rangeFrom() === mondayOf(this.today);
  });

  readonly days = computed<AgendaDay[]>(() => {
    const f = this.filter();
    const visible = this.items().filter((i) => f === 'all' || i.kind === f);
    const from = this.rangeFrom();
    const to = this.rangeTo();
    const out: AgendaDay[] = [];
    let cursor = from;
    while (cursor <= to) {
      out.push({
        date: cursor,
        title: dayTitle(cursor),
        isToday: cursor === this.today,
        items: visible
          .filter((it) => it.date.slice(0, 10) === cursor)
          .sort((a, b) => hhmm(a.startTime).localeCompare(hhmm(b.startTime)))
      });
      cursor = addDays(cursor, 1);
    }
    return out;
  });

  readonly visibleCount = computed(() => this.days().reduce((n, d) => n + d.items.length, 0));

  readonly opsStats = computed(() => {
    const all = this.items();
    const theory = all.filter((i) => i.kind === 'theory');
    const practical = all.filter((i) => i.kind === 'practical');
    const exam = all.filter((i) => i.kind === 'exam');
    const free = all.reduce((n, i) => n + Math.max(0, i.available), 0);
    const occupied = all.reduce((n, i) => n + Math.max(0, i.occupied), 0);
    const capacity = all.reduce((n, i) => n + Math.max(0, i.capacity), 0);
    const highAlerts = this.alerts().filter((a) => a.priority === 'high').length;
    return {
      theory: theory.length,
      practical: practical.length,
      exam: exam.length,
      free,
      occupied,
      capacity,
      fillPct: capacity > 0 ? Math.round((occupied / capacity) * 100) : 0,
      highAlerts
    };
  });

  readonly conflictPreview = computed(() => {
    if (!this.createOpen()) return [] as string[];
    const date = this.createDate();
    const start = this.createStart();
    const kind = this.createKind();
    const end = kind === 'exam' ? start : this.createEnd();
    const hits = this.items().filter((it) => {
      if (it.date.slice(0, 10) !== date) return false;
      if (kind === 'exam') {
        return it.kind === 'exam' && hhmm(it.startTime) === hhmm(start);
      }
      if (!overlaps(start, end, it.startTime, it.endTime || it.startTime)) return false;
      if (kind === 'practical' && it.kind === 'practical') {
        const teacher = this.teachers().find((t) => t.id === this.instructorId());
        if (teacher && it.instructorName && it.instructorName === teacher.name) return true;
        return true;
      }
      if (kind === 'theory' && it.kind === 'theory') return true;
      return false;
    });
    return hits.slice(0, 4).map(
      (h) =>
        `${KIND_LABELS[h.kind]} ${time12(h.startTime)}${h.endTime ? '–' + time12(h.endTime) : ''} · ${h.title}`
    );
  });

  readonly time12 = time12;
  readonly plural = plural;

  ngOnInit(): void {
    this.load();
    this.api.getOpsAlerts().subscribe({
      next: (res) => this.alerts.set(res.alerts ?? []),
      error: () => this.alerts.set([])
    });
    this.loadCatalog();
  }

  loadCatalog(): void {
    forkJoin({
      topics: this.theory.listTopics(true),
      classrooms: this.theory.listClassrooms(true),
      members: this.http.get<MemberRow[]>(`${env.apiUrl}/api/school/members`),
      vehicles: this.practical.listVehicles(true)
    }).subscribe({
      next: ({ topics, classrooms, members, vehicles }) => {
        this.topics.set(topics);
        this.classrooms.set(classrooms);
        const teachers = members.filter((m) => m.role === 'Teacher');
        this.teachers.set(teachers);
        this.vehicles.set(vehicles);
        if (!this.topicId() && topics[0]) this.topicId.set(topics[0].id);
        if (!this.classroomId() && classrooms[0]) this.classroomId.set(classrooms[0].id);
        if (!this.instructorId() && teachers[0]) this.instructorId.set(teachers[0].id);
        if (!this.vehicleId() && vehicles[0]) this.vehicleId.set(vehicles[0].id);
        this.catalogReady.set(true);
      },
      error: () => this.catalogReady.set(false)
    });
  }

  setView(v: ViewMode): void {
    this.view.set(v);
    if (v === 'day') this.anchor.set(this.today);
    else if (v === 'week') this.anchor.set(mondayOf(this.today));
    else this.anchor.set(`${this.today.slice(0, 8)}01`);
    this.expanded.set(new Set());
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getAgenda(this.rangeFrom(), this.rangeTo()).subscribe({
      next: (res) => {
        this.items.set(res.items ?? []);
        this.loading.set(false);
      },
      error: (err) => {
        this.items.set([]);
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  go(offset: number): void {
    const v = this.view();
    if (offset === 0) {
      this.setView(v);
      return;
    }
    if (v === 'day') this.anchor.set(addDays(this.anchor(), offset));
    else if (v === 'week') this.anchor.set(addDays(mondayOf(this.anchor()), offset * 7));
    else {
      const [y, m] = this.rangeFrom().split('-').map(Number);
      const d = new Date(Date.UTC(y, m - 1 + offset, 1));
      this.anchor.set(
        `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}-01`
      );
    }
    this.expanded.set(new Set());
    this.load();
  }

  openCreate(kind: CreateKind, date?: string): void {
    this.createKind.set(kind);
    this.createDate.set(date ?? this.rangeFrom());
    this.createError.set(null);
    this.createOpen.set(true);
  }

  closeCreate(): void {
    this.createOpen.set(false);
    this.createError.set(null);
  }

  submitCreate(): void {
    if (this.createBusy()) return;
    const kind = this.createKind();
    const date = this.createDate();
    const start = normalizeTime(this.createStart());
    const end = normalizeTime(this.createEnd());
    if (!date || !start) {
      this.createError.set('Completa fecha y horario.');
      return;
    }
    if (kind !== 'exam' && (!end || hhmm(start) >= hhmm(end))) {
      this.createError.set('La hora de fin debe ser posterior al inicio.');
      return;
    }

    this.createBusy.set(true);
    this.createError.set(null);

    if (kind === 'exam') {
      const capacity = Number(this.examCapacity());
      if (!Number.isInteger(capacity) || capacity < 1) {
        this.createBusy.set(false);
        this.createError.set('Los cupos del examen deben ser un entero ≥ 1.');
        return;
      }
      this.api
        .saveSlot({ date, time: start, capacity, isClosed: false })
        .subscribe({
          next: () => this.afterCreate('Cupo de examen teórico creado.'),
          error: (err) => {
            this.createBusy.set(false);
            this.createError.set(mapApiError(err));
          }
        });
      return;
    }

    if (kind === 'theory') {
      const topicId = this.topicId();
      const classroomId = this.classroomId();
      if (!topicId || !classroomId) {
        this.createBusy.set(false);
        this.createError.set('Configura temas y aulas en Teoría antes de crear aquí.');
        return;
      }
      const room = this.classrooms().find((c) => c.id === classroomId);
      this.theory
        .createSession({
          sessionDate: date,
          startTime: start,
          endTime: end,
          topicId,
          classroomId,
          capacity: room?.capacity
        })
        .subscribe({
          next: () => this.afterCreate('Clase teórica creada.'),
          error: (err) => {
            this.createBusy.set(false);
            this.createError.set(mapApiError(err));
          }
        });
      return;
    }

    const instructorUserId = this.instructorId();
    const vehicleId = this.vehicleId();
    if (!instructorUserId || !vehicleId) {
      this.createBusy.set(false);
      this.createError.set('Necesitas instructor y vehículo activos.');
      return;
    }
    this.practical
      .createLesson({
        sessionDate: date,
        startTime: start,
        endTime: end,
        instructorUserId,
        vehicleId,
        kind: 'Lesson'
      })
      .subscribe({
        next: () => this.afterCreate('Cupo de manejo creado.'),
        error: (err) => {
          this.createBusy.set(false);
          this.createError.set(mapApiError(err));
        }
      });
  }

  duplicatePracticalWeek(): void {
    const instructorUserId = this.instructorId();
    const vehicleId = this.vehicleId();
    if (!instructorUserId || !vehicleId) {
      this.error.set('Elige instructor y vehículo en Manejo, o crea un cupo primero desde aquí.');
      return;
    }
    this.createBusy.set(true);
    this.practical
      .duplicateWeek({
        weekStart: mondayOf(this.rangeFrom()),
        instructorUserId,
        vehicleId
      })
      .subscribe({
        next: (res) => {
          this.createBusy.set(false);
          this.notice.set(
            res.created > 0
              ? `Se duplicaron ${res.created} cupos de la semana anterior.`
              : 'No había cupos que duplicar para ese instructor y vehículo.'
          );
          this.load();
        },
        error: (err) => {
          this.createBusy.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  private afterCreate(msg: string): void {
    this.createBusy.set(false);
    this.createOpen.set(false);
    this.notice.set(msg);
    this.load();
  }

  canEdit(item: AgendaItemDto): boolean {
    const s = (item.status ?? '').replace(/[\s_-]/g, '').toLowerCase();
    if (['cancelled', 'past', 'closed', 'completed'].includes(s)) return false;
    if (item.kind === 'exam') return true;
    if (item.kind === 'theory') return item.id > 0 && !!item.topicId && !!item.classroomId;
    if (item.kind === 'practical') return item.id > 0 && !!item.instructorUserId && !!item.vehicleId;
    return false;
  }

  openEdit(item: AgendaItemDto): void {
    if (!this.canEdit(item)) return;
    this.editItem.set(item);
    this.editDate.set(item.date.slice(0, 10));
    this.editStart.set(normalizeTime(item.startTime) || '08:00');
    this.editEnd.set(normalizeTime(item.endTime || item.startTime) || '10:00');
    this.editTopicId.set(item.topicId ?? this.topicId() ?? 0);
    this.editClassroomId.set(item.classroomId ?? this.classroomId() ?? 0);
    this.editInstructorId.set(item.instructorUserId ?? this.instructorId() ?? 0);
    this.editVehicleId.set(item.vehicleId ?? this.vehicleId() ?? 0);
    this.editCapacity.set(item.capacity > 0 ? item.capacity : 8);
    this.editError.set(null);
    this.editOpen.set(true);
  }

  closeEdit(): void {
    if (this.editBusy()) return;
    this.editOpen.set(false);
    this.editItem.set(null);
    this.editError.set(null);
  }

  submitEdit(): void {
    const item = this.editItem();
    if (!item || this.editBusy()) return;
    const date = this.editDate();
    const start = normalizeTime(this.editStart());
    const end = normalizeTime(this.editEnd());
    if (!date || !start) {
      this.editError.set('Completa fecha y horario.');
      return;
    }

    this.editBusy.set(true);
    this.editError.set(null);

    if (item.kind === 'exam') {
      const capacity = Number(this.editCapacity());
      if (!Number.isInteger(capacity) || capacity < 1) {
        this.editBusy.set(false);
        this.editError.set('Los cupos deben ser un entero ≥ 1.');
        return;
      }
      this.api
        .saveSlot({
          date,
          time: start,
          capacity,
          isClosed: false
        })
        .subscribe({
          next: () => this.afterEdit('Cupo de examen actualizado.'),
          error: (err) => {
            this.editBusy.set(false);
            this.editError.set(mapApiError(err));
          }
        });
      return;
    }

    if (!end || hhmm(start) >= hhmm(end)) {
      this.editBusy.set(false);
      this.editError.set('La hora de fin debe ser posterior al inicio.');
      return;
    }

    if (item.kind === 'theory') {
      const topicId = this.editTopicId();
      const classroomId = this.editClassroomId();
      if (!topicId || !classroomId) {
        this.editBusy.set(false);
        this.editError.set('Elige tema y aula.');
        return;
      }
      const room = this.classrooms().find((c) => c.id === classroomId);
      const capacity = Number(this.editCapacity()) || room?.capacity;
      this.theory
        .updateSession(item.id, {
          sessionDate: date,
          startTime: start,
          endTime: end,
          topicId,
          classroomId,
          capacity,
          instructorUserId: item.instructorUserId ?? null,
          notes: undefined
        })
        .subscribe({
          next: () => this.afterEdit('Clase teórica actualizada.'),
          error: (err) => {
            this.editBusy.set(false);
            this.editError.set(mapApiError(err));
          }
        });
      return;
    }

    const instructorUserId = this.editInstructorId();
    const vehicleId = this.editVehicleId();
    if (!instructorUserId || !vehicleId) {
      this.editBusy.set(false);
      this.editError.set('Elige instructor y vehículo.');
      return;
    }
    this.practical
      .updateLesson(item.id, {
        sessionDate: date,
        startTime: start,
        endTime: end,
        instructorUserId,
        vehicleId,
        kind: item.sessionKind || 'Lesson'
      })
      .subscribe({
        next: () => this.afterEdit('Cupo de manejo actualizado.'),
        error: (err) => {
          this.editBusy.set(false);
          this.editError.set(mapApiError(err));
        }
      });
  }

  private afterEdit(msg: string): void {
    this.editBusy.set(false);
    this.editOpen.set(false);
    this.editItem.set(null);
    this.notice.set(msg);
    this.load();
  }

  canCancel(item: AgendaItemDto): boolean {
    const s = (item.status ?? '').replace(/[\s_-]/g, '').toLowerCase();
    if (['cancelled', 'past', 'closed', 'completed'].includes(s)) return false;
    if (item.kind === 'exam') return true;
    return item.id > 0;
  }

  cancelLabel(item: AgendaItemDto): string {
    return item.kind === 'exam' ? 'Cerrar cupo' : 'Cancelar';
  }

  openCancel(item: AgendaItemDto): void {
    if (!this.canCancel(item)) return;
    this.cancelTarget.set(item);
    this.cancelError.set(null);
    this.cancelForceBookings.set(false);
  }

  closeCancel(): void {
    if (this.cancelBusy()) return;
    this.cancelTarget.set(null);
    this.cancelError.set(null);
    this.cancelForceBookings.set(false);
  }

  cancelMessage(): string {
    const item = this.cancelTarget();
    if (!item) return '';
    const when = `${dayTitle(item.date.slice(0, 10))} · ${this.timeRange(item)}`;
    const base = `${this.kindLabel(item.kind)}: ${item.title} (${when}).`;
    if (item.kind === 'exam') {
      if (this.cancelForceBookings() || item.occupied > 0) {
        return `${base} Hay ${plural(item.occupied, 'estudiante agendado', 'estudiantes agendados')}; al cerrar se cancelarán esas reservas.`;
      }
      return `${base} El horario quedará cerrado en la agenda de exámenes.`;
    }
    if (item.occupied > 0) {
      return `${base} Hay ${item.occupied} estudiante(s) asignados; se liberará el cupo.`;
    }
    return `${base} Esta acción no se puede deshacer desde aquí.`;
  }

  confirmCancel(): void {
    const item = this.cancelTarget();
    if (!item || this.cancelBusy()) return;
    this.cancelBusy.set(true);
    this.cancelError.set(null);

    if (item.kind === 'exam') {
      this.api
        .saveSlot({
          date: item.date.slice(0, 10),
          time: normalizeTime(item.startTime),
          isClosed: true,
          cancelBookings: this.cancelForceBookings() || item.occupied > 0
        })
        .subscribe({
          next: () => this.afterCancel('Cupo de examen cerrado.'),
          error: (err) => this.onCancelError(err)
        });
      return;
    }

    if (item.kind === 'theory') {
      this.theory.cancelSession(item.id, 'Cancelado desde agenda central').subscribe({
        next: () => this.afterCancel('Clase teórica cancelada.'),
        error: (err) => this.onCancelError(err)
      });
      return;
    }

    this.practical.cancelLesson(item.id).subscribe({
      next: () => this.afterCancel('Cupo de manejo cancelado.'),
      error: (err) => this.onCancelError(err)
    });
  }

  private afterCancel(msg: string): void {
    this.cancelBusy.set(false);
    this.cancelTarget.set(null);
    this.cancelForceBookings.set(false);
    this.notice.set(msg);
    this.load();
  }

  private onCancelError(err: unknown): void {
    this.cancelBusy.set(false);
    if (apiErrorCode(err) === 'slot_has_bookings') {
      this.cancelForceBookings.set(true);
      this.cancelError.set('Hay reservas en ese horario. Confirma de nuevo para cerrarlo y cancelarlas.');
      return;
    }
    this.cancelError.set(mapApiError(err));
  }

  key(item: AgendaItemDto): string {
    return `${item.kind}-${item.id}-${item.date}-${item.startTime}`;
  }

  isOpen(item: AgendaItemDto): boolean {
    return this.expanded().has(this.key(item));
  }

  toggle(item: AgendaItemDto): void {
    const k = this.key(item);
    this.expanded.update((set) => {
      const next = new Set(set);
      if (next.has(k)) next.delete(k);
      else next.add(k);
      return next;
    });
  }

  kindLabel(kind: AgendaItemKind): string {
    return KIND_LABELS[kind] ?? kind;
  }

  timeRange(item: AgendaItemDto): string {
    const start = time12(item.startTime);
    return item.endTime ? `${start} – ${time12(item.endTime)}` : start;
  }

  statusLabel(status: string | null | undefined): string {
    if (!status) return '';
    return STATUS_LABELS[status.replace(/[\s_-]/g, '').toLowerCase()] ?? status;
  }

  statusTone(status: string | null | undefined): Tone {
    const s = (status ?? '').replace(/[\s_-]/g, '').toLowerCase();
    if (['available', 'completed', 'attended', 'present', 'checkedin'].includes(s)) return 'success';
    if (['full', 'cancelled', 'absent', 'noshow'].includes(s)) return 'danger';
    if (['inprogress', 'pending'].includes(s)) return 'warning';
    if (['scheduled', 'reserved', 'booked', 'active'].includes(s)) return 'primary';
    return 'neutral';
  }

  fillTone(item: AgendaItemDto): Tone {
    if (item.capacity <= 0) return 'neutral';
    if (item.available <= 0) return 'danger';
    if (item.occupied === 0) return 'warning';
    return 'success';
  }

  linkFor(kind: AgendaItemKind): string {
    if (kind === 'exam') return '/school/theory-exams';
    if (kind === 'practical') return '/school/practical';
    return '/school/training';
  }
}

function normalizeTime(value: string): string {
  const v = (value || '').trim();
  if (/^\d{2}:\d{2}$/.test(v)) return v;
  if (/^\d{2}:\d{2}:\d{2}$/.test(v)) return v.slice(0, 5);
  return v;
}

function overlaps(aStart: string, aEnd: string, bStart: string, bEnd: string): boolean {
  const as = hhmm(aStart);
  const ae = hhmm(aEnd);
  const bs = hhmm(bStart);
  const be = hhmm(bEnd);
  return as < be && bs < ae;
}
