import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, concat, toArray } from 'rxjs';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiDialogComponent } from '../../../shared/ui/ui-dialog.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiSheetComponent } from '../../../shared/ui/ui-sheet.component';
import { DAY_NAMES, WEEK_ORDER, hhmm, time12 } from '../../../shared/utils/wall-clock';
import { ExamScheduleApi, ExamTemplateDto } from '../api/exam-schedule.api';
import { plural } from '../utils/exam-labels';

interface DaySection {
  dayOfWeek: number;
  name: string;
  short: string;
  templates: ExamTemplateDto[];
}

const DAY_SHORT = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
const MON_FRI = [1, 2, 3, 4, 5];
const MON_SAT = [1, 2, 3, 4, 5, 6];

/** Common starting schedule: mornings Mon–Sat, afternoons Mon–Fri, 1 seat each. */
const TYPICAL: { time: string; days: number[] }[] = [
  { time: '09:00', days: MON_SAT },
  { time: '10:00', days: MON_SAT },
  { time: '11:00', days: MON_SAT },
  { time: '14:00', days: MON_FRI },
  { time: '15:00', days: MON_FRI }
];

@Component({
  selector: 'app-school-exam-schedule-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiDialogComponent, UiErrorComponent, UiLoadingComponent, UiSheetComponent],
  templateUrl: './school-exam-schedule.page.html',
  styleUrls: ['../../../shared/styles/easy-schedule.css', './school-exam-schedule.page.css']
})
export class SchoolExamSchedulePage implements OnInit {
  private readonly api = inject(ExamScheduleApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly busy = signal(false);
  readonly templates = signal<ExamTemplateDto[]>([]);

  readonly dayChoices = WEEK_ORDER.map((d) => ({ value: d, name: DAY_NAMES[d], short: DAY_SHORT[d] }));
  readonly newDays = signal<number[]>([...MON_FRI]);
  newTime = '09:00';
  newCapacity = 1;
  readonly formError = signal<string | null>(null);

  readonly selected = signal<ExamTemplateDto | null>(null);
  readonly editing = signal(false);
  editTime = '';
  editCapacity = 1;
  readonly editError = signal<string | null>(null);

  readonly deleting = signal<ExamTemplateDto | null>(null);

  readonly sections = computed<DaySection[]>(() =>
    WEEK_ORDER.map((day) => ({
      dayOfWeek: day,
      name: DAY_NAMES[day],
      short: DAY_SHORT[day],
      templates: this.templates()
        .filter((t) => t.dayOfWeek === day)
        .sort((a, b) => hhmm(a.time).localeCompare(hhmm(b.time)))
    }))
  );

  readonly activeCount = computed(() => this.templates().filter((t) => t.isActive).length);
  readonly weeklySeats = computed(() =>
    this.templates().filter((t) => t.isActive).reduce((sum, t) => sum + t.capacity, 0)
  );

  readonly sheetTitle = computed(() => {
    const t = this.selected();
    return t ? `${DAY_NAMES[t.dayOfWeek]} · ${time12(t.time)}` : '';
  });

  readonly time12 = time12;
  readonly plural = plural;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.error.set(null);
    this.api.listTemplates().subscribe({
      next: (rows) => {
        this.templates.set(rows);
        const current = this.selected();
        if (current) this.selected.set(rows.find((r) => r.id === current.id) ?? null);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  // ── Add ───────────────────────────────────────────────────

  isDayChecked(day: number): boolean {
    return this.newDays().includes(day);
  }

  toggleDay(day: number, checked: boolean): void {
    this.newDays.update((list) =>
      checked ? [...new Set([...list, day])] : list.filter((d) => d !== day)
    );
  }

  setDays(days: number[]): void {
    this.newDays.set([...days]);
  }

  sameDays(days: number[]): boolean {
    const current = [...this.newDays()].sort();
    return current.length === days.length && [...days].sort().every((d, i) => d === current[i]);
  }

  add(): void {
    const days = this.newDays();
    const capacity = Number(this.newCapacity);
    if (!days.length) {
      this.formError.set('Marca al menos un día.');
      return;
    }
    if (!/^\d{2}:\d{2}$/.test(this.newTime || '')) {
      this.formError.set('Escribe la hora del examen.');
      return;
    }
    if (!Number.isInteger(capacity) || capacity < 1) {
      this.formError.set('Los cupos deben ser un número entero, mínimo 1.');
      return;
    }
    this.formError.set(null);
    const time = this.newTime;
    this.run(this.api.createTemplates({ daysOfWeek: days, time, capacity }), () => {
      this.notice.set(`Listo. Agregaste el horario de las ${time12(time)} (${plural(days.length, 'día', 'días')}).`);
    }, (msg) => this.formError.set(msg));
  }

  useTypical(): void {
    const requests = TYPICAL.map((t) =>
      this.api.createTemplates({ daysOfWeek: t.days, time: t.time, capacity: 1 })
    );
    this.run(concat(...requests).pipe(toArray()), () => {
      this.notice.set('Listo. Creamos el horario típico. Puedes cambiar cualquier hora cuando quieras.');
    }, (msg) => this.error.set(msg));
  }

  // ── Sheet: edit / pause / delete ──────────────────────────

  open(t: ExamTemplateDto): void {
    this.selected.set(t);
    this.editing.set(false);
    this.editError.set(null);
  }

  closeSheet(): void {
    this.selected.set(null);
    this.editing.set(false);
  }

  startEdit(t: ExamTemplateDto): void {
    this.editTime = hhmm(t.time);
    this.editCapacity = t.capacity;
    this.editError.set(null);
    this.editing.set(true);
  }

  saveEdit(t: ExamTemplateDto): void {
    const capacity = Number(this.editCapacity);
    if (!/^\d{2}:\d{2}$/.test(this.editTime || '')) {
      this.editError.set('Escribe la hora del examen.');
      return;
    }
    if (!Number.isInteger(capacity) || capacity < 1) {
      this.editError.set('Los cupos deben ser un número entero, mínimo 1.');
      return;
    }
    this.run(
      this.api.updateTemplate(t.id, { time: this.editTime, capacity, isActive: t.isActive }),
      () => {
        this.closeSheet();
        this.notice.set('Listo. Guardamos el cambio.');
      },
      (msg) => this.editError.set(msg)
    );
  }

  toggleActive(t: ExamTemplateDto): void {
    this.run(
      this.api.updateTemplate(t.id, { time: hhmm(t.time), capacity: t.capacity, isActive: !t.isActive }),
      () => {
        this.closeSheet();
        this.notice.set(
          t.isActive
            ? `El horario de las ${time12(t.time)} del ${DAY_NAMES[t.dayOfWeek].toLowerCase()} quedó pausado.`
            : `El horario de las ${time12(t.time)} del ${DAY_NAMES[t.dayOfWeek].toLowerCase()} volvió a estar activo.`
        );
      },
      (msg) => this.editError.set(msg)
    );
  }

  deleteMessage(t: ExamTemplateDto | null): string {
    if (!t) return '';
    return `Vas a borrar el horario de las ${time12(t.time)} de los ${DAY_NAMES[t.dayOfWeek].toLowerCase()}. ` +
      'Las citas que ya están agendadas no se borran.';
  }

  confirmDelete(): void {
    const t = this.deleting();
    if (!t) return;
    this.deleting.set(null);
    this.closeSheet();
    this.run(this.api.deleteTemplate(t.id), () => {
      this.notice.set('Listo. Borramos el horario.');
    }, (msg) => this.error.set(msg));
  }

  /** Runs a change and reloads the list from the server. */
  private run<T>(request: Observable<T>, onOk: () => void, onError: (message: string) => void): void {
    this.busy.set(true);
    this.notice.set(null);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        onOk();
        this.load();
      },
      error: (err) => {
        this.busy.set(false);
        onError(mapApiError(err));
        this.load();
      }
    });
  }
}
