import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { apiErrorCode, mapApiError } from '../../../core/http/map-api-error';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiSheetComponent } from '../../../shared/ui/ui-sheet.component';
import {
  addDays,
  dayTitle,
  mondayOf,
  time12,
  todayColombia,
  weekRangeLabel
} from '../../../shared/utils/wall-clock';
import { ApprenticeApi, TheoryExamSchedulingStudentDto } from '../api/apprentice.api';
import { ExamDayDto, ExamScheduleApi, ExamSlotDto, ExamWeekDto } from '../api/exam-schedule.api';
import { ExamSlotDetailComponent } from '../components/exam-slot-detail.component';
import { activeBookings, plural, slotStatusLabel, slotStatusTone } from '../utils/exam-labels';

interface DayView {
  date: string;
  title: string;
  isPast: boolean;
  isToday: boolean;
  day: ExamDayDto | null;
  slots: ExamSlotDto[];
}

const VISIBLE_NAMES = 3;

@Component({
  selector: 'app-school-theory-exams-page',
  standalone: true,
  imports: [
    ExamSlotDetailComponent,
    FormsModule,
    RouterLink,
    UiErrorComponent,
    UiLoadingComponent,
    UiSheetComponent
  ],
  templateUrl: './school-theory-exams.page.html',
  styleUrls: ['../../../shared/styles/easy-schedule.css', './school-theory-exams.page.css']
})
export class SchoolTheoryExamsPage implements OnInit {
  private readonly api = inject(ExamScheduleApi);
  private readonly apprenticeApi = inject(ApprenticeApi);

  readonly today = todayColombia();
  readonly weekStart = signal(mondayOf(this.today));
  readonly week = signal<ExamWeekDto | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  readonly students = signal<TheoryExamSchedulingStudentDto[]>([]);
  readonly studentsLoading = signal(false);
  private studentsLoaded = false;

  /** Slot open in the detail sheet, looked up again after every refresh. */
  readonly selected = signal<{ date: string; time: string } | null>(null);

  readonly extraDate = signal<string | null>(null);
  extraTime = '09:00';
  extraCapacity = 1;
  readonly extraError = signal<string | null>(null);

  readonly closeDate = signal<string | null>(null);
  closeNote = '';
  readonly closeError = signal<string | null>(null);
  readonly closeNeedsConfirm = signal(false);

  readonly busy = signal(false);

  readonly weekEnd = computed(() => addDays(this.weekStart(), 6));
  readonly weekLabel = computed(() => weekRangeLabel(this.weekStart(), this.weekEnd()));
  readonly isThisWeek = computed(() => this.weekStart() === mondayOf(this.today));

  readonly days = computed<DayView[]>(() => {
    const w = this.week();
    const byDate = new Map((w?.days ?? []).map((d) => [d.date.slice(0, 10), d]));
    return Array.from({ length: 7 }, (_, i) => {
      const date = addDays(this.weekStart(), i);
      const day = byDate.get(date) ?? null;
      const slots = [...(day?.slots ?? [])].sort((a, b) => a.time.localeCompare(b.time));
      return {
        date,
        title: dayTitle(date),
        isPast: date < this.today,
        isToday: date === this.today,
        day,
        slots
      };
    });
  });

  readonly hasAnything = computed(() =>
    this.days().some((d) => d.slots.length > 0 || d.day?.isClosed)
  );

  readonly selectedSlot = computed<ExamSlotDto | null>(() => {
    const sel = this.selected();
    if (!sel) return null;
    const day = this.days().find((d) => d.date === sel.date);
    return day?.slots.find((s) => s.time === sel.time) ?? null;
  });

  readonly selectedReadOnly = computed(() => {
    const s = this.selectedSlot();
    return !s || s.status === 'Past' || s.date < this.today;
  });

  readonly closeDayBookings = computed(() => {
    const date = this.closeDate();
    const day = this.days().find((d) => d.date === date);
    return (day?.slots ?? []).reduce((sum, s) => sum + activeBookings(s).length, 0);
  });

  readonly time12 = time12;
  readonly dayTitle = dayTitle;
  readonly statusLabel = slotStatusLabel;
  readonly statusTone = slotStatusTone;
  readonly plural = plural;

  ngOnInit(): void {
    this.load();
  }

  load(silent = false): void {
    if (!silent || !this.week()) this.loading.set(true);
    this.error.set(null);
    this.api.getWeek(this.weekStart(), this.weekEnd()).subscribe({
      next: (week) => {
        this.week.set(week);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  goWeek(offsetWeeks: number): void {
    this.weekStart.set(
      offsetWeeks === 0 ? mondayOf(this.today) : addDays(this.weekStart(), offsetWeeks * 7)
    );
    this.selected.set(null);
    this.notice.set(null);
    this.week.set(null);
    this.load();
  }

  names(slot: ExamSlotDto): { shown: string; more: number } {
    const list = activeBookings(slot).map((b) => b.studentName);
    return {
      shown: list.slice(0, VISIBLE_NAMES).join(', '),
      more: Math.max(0, list.length - VISIBLE_NAMES)
    };
  }

  // ── Detail sheet ──────────────────────────────────────────

  openSlot(slot: ExamSlotDto): void {
    this.selected.set({ date: slot.date.slice(0, 10), time: slot.time });
  }

  closeSlot(): void {
    this.selected.set(null);
  }

  loadStudents(): void {
    if (this.studentsLoaded || this.studentsLoading()) return;
    this.studentsLoading.set(true);
    this.apprenticeApi.listTheoryExamStudents().subscribe({
      next: (rows) => {
        this.students.set(rows);
        this.studentsLoaded = true;
        this.studentsLoading.set(false);
      },
      error: () => {
        this.students.set([]);
        this.studentsLoading.set(false);
      }
    });
  }

  applyDay(day: ExamDayDto): void {
    const w = this.week();
    if (!w) return;
    const date = day.date.slice(0, 10);
    const exists = w.days.some((d) => d.date.slice(0, 10) === date);
    this.week.set({
      ...w,
      days: exists
        ? w.days.map((d) => (d.date.slice(0, 10) === date ? day : d))
        : [...w.days, day]
    });
  }

  applySlot(slot: ExamSlotDto): void {
    const w = this.week();
    if (!w) return;
    const date = slot.date.slice(0, 10);
    this.week.set({
      ...w,
      days: w.days.map((d) =>
        d.date.slice(0, 10) !== date
          ? d
          : {
              ...d,
              slots: d.slots.some((s) => s.time === slot.time)
                ? d.slots.map((s) => (s.time === slot.time ? slot : s))
                : [...d.slots, slot]
            }
      )
    });
  }

  // ── Extra slot ────────────────────────────────────────────

  openExtra(date: string): void {
    this.extraTime = '09:00';
    this.extraCapacity = 1;
    this.extraError.set(null);
    this.extraDate.set(date);
  }

  saveExtra(): void {
    const date = this.extraDate();
    if (!date) return;
    const capacity = Number(this.extraCapacity);
    if (!/^\d{2}:\d{2}$/.test(this.extraTime || '')) {
      this.extraError.set('Escribe la hora del examen.');
      return;
    }
    if (!Number.isInteger(capacity) || capacity < 1) {
      this.extraError.set('Los cupos deben ser un número entero, mínimo 1.');
      return;
    }
    const time = this.extraTime;
    this.run(
      this.api.saveSlot({ date, time, capacity, isClosed: false }),
      (day) => {
        this.applyDay(day);
        this.extraDate.set(null);
        this.notice.set(`Listo. Agregaste un horario extra el ${dayTitle(date)} a las ${time12(time)}.`);
      },
      (msg) => this.extraError.set(msg)
    );
  }

  // ── Close / reopen a whole day ────────────────────────────

  openCloseDay(date: string): void {
    this.closeNote = '';
    this.closeError.set(null);
    this.closeNeedsConfirm.set(false);
    this.closeDate.set(date);
  }

  confirmCloseDay(cancelBookings = false): void {
    const date = this.closeDate();
    if (!date) return;
    this.closeNeedsConfirm.set(false);
    this.run(
      this.api.closeDay(date, { note: this.closeNote.trim() || null, cancelBookings }),
      (day) => {
        this.applyDay(day);
        this.closeDate.set(null);
        this.notice.set(`Listo. El ${dayTitle(date)} quedó cerrado para exámenes.`);
      },
      (msg, code) => {
        if (code === 'slot_has_bookings') {
          this.closeNeedsConfirm.set(true);
          return;
        }
        this.closeError.set(msg);
      }
    );
  }

  reopenDay(date: string): void {
    this.run(
      this.api.reopenDay(date),
      (day) => {
        this.applyDay(day);
        this.notice.set(`Listo. El ${dayTitle(date)} quedó abierto otra vez.`);
      },
      (msg) => this.error.set(msg)
    );
  }

  private run<T>(
    request: Observable<T>,
    onOk: (value: T) => void,
    onError: (message: string, code: string | null) => void
  ): void {
    this.busy.set(true);
    this.notice.set(null);
    request.subscribe({
      next: (value) => {
        this.busy.set(false);
        onOk(value);
      },
      error: (err) => {
        this.busy.set(false);
        onError(mapApiError(err), apiErrorCode(err));
      }
    });
  }
}
