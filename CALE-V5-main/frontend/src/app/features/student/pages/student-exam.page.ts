import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiErrorCode, mapApiError } from '../../../core/http/map-api-error';
import { UiDialogComponent } from '../../../shared/ui/ui-dialog.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiSheetComponent } from '../../../shared/ui/ui-sheet.component';
import {
  addDays,
  dayTitle,
  formatDeadline,
  hhmm,
  mondayOf,
  time12,
  todayColombia,
  weekRangeLabel
} from '../../../shared/utils/wall-clock';
import {
  StudentExamApi,
  StudentExamAvailabilityDto,
  StudentExamBookingDto,
  StudentExamSlotDto
} from '../api/student-exam.api';

type SheetState = 'confirm' | 'success' | 'full';

@Component({
  selector: 'app-student-exam-page',
  standalone: true,
  imports: [RouterLink, UiDialogComponent, UiErrorComponent, UiLoadingComponent, UiSheetComponent],
  templateUrl: './student-exam.page.html',
  styleUrls: ['../../../shared/styles/easy-schedule.css', './student-exam.page.css']
})
export class StudentExamPage implements OnInit {
  private readonly api = inject(StudentExamApi);

  readonly today = todayColombia();
  readonly weekStart = signal(mondayOf(this.today));
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<StudentExamAvailabilityDto | null>(null);

  readonly picked = signal<StudentExamSlotDto | null>(null);
  readonly sheetState = signal<SheetState>('confirm');
  readonly sheetError = signal<string | null>(null);
  readonly booking = signal(false);
  readonly booked = signal<StudentExamBookingDto | null>(null);

  readonly confirmCancel = signal(false);
  readonly cancelling = signal(false);
  readonly notice = signal<string | null>(null);

  readonly weekEnd = computed(() => addDays(this.weekStart(), 6));
  readonly weekLabel = computed(() => weekRangeLabel(this.weekStart(), this.weekEnd()));
  readonly isFirstWeek = computed(() => this.weekStart() <= mondayOf(this.today));

  readonly myBooking = computed(() => {
    const b = this.data()?.myBooking;
    return b && b.status !== 'Cancelled' ? b : null;
  });

  /** Booking buttons only when the school allows it and there is no active booking. */
  readonly canPick = computed(() => !!this.data()?.canBook && !this.myBooking());

  readonly days = computed(() =>
    (this.data()?.days ?? [])
      .filter((d) => d.date.slice(0, 10) >= this.today && d.slots.length > 0)
      .map((d) => ({
        date: d.date.slice(0, 10),
        title: dayTitle(d.date),
        slots: [...d.slots].sort((a, b) => hhmm(a.time).localeCompare(hhmm(b.time)))
      }))
      .sort((a, b) => a.date.localeCompare(b.date))
  );

  readonly dayTitle = dayTitle;
  readonly time12 = time12;
  readonly formatDeadline = formatDeadline;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (!this.data()) this.loading.set(true);
    this.error.set(null);
    const start = this.weekStart();
    const from = start < this.today ? this.today : start;
    this.api.availability(from, this.weekEnd()).subscribe({
      next: (res) => {
        this.data.set(res);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  goWeek(offset: number): void {
    this.weekStart.set(addDays(this.weekStart(), offset * 7));
    this.data.set(null);
    this.load();
  }

  isFull(slot: StudentExamSlotDto): boolean {
    return slot.status !== 'Available' || slot.available <= 0;
  }

  cuposLabel(n: number): string {
    return n === 1 ? '1 cupo' : `${n} cupos`;
  }

  pick(slot: StudentExamSlotDto): void {
    if (!this.canPick() || this.isFull(slot)) return;
    this.sheetState.set('confirm');
    this.sheetError.set(null);
    this.booked.set(null);
    this.picked.set(slot);
  }

  closeSheet(): void {
    this.picked.set(null);
  }

  book(): void {
    const slot = this.picked();
    if (!slot) return;
    this.booking.set(true);
    this.sheetError.set(null);
    this.api.book(slot.date.slice(0, 10), slot.time).subscribe({
      next: (b) => {
        this.booking.set(false);
        this.booked.set(b);
        this.sheetState.set('success');
        this.load();
      },
      error: (err) => {
        this.booking.set(false);
        if (apiErrorCode(err) === 'exam_slot_full') {
          this.sheetState.set('full');
        }
        this.sheetError.set(mapApiError(err));
        this.load();
      }
    });
  }

  cancelMessage(b: StudentExamBookingDto | null): string {
    if (!b) return '';
    return `Vas a cancelar tu examen del ${dayTitle(b.date)} a las ${time12(b.time)}. Después puedes elegir otra hora.`;
  }

  doCancel(): void {
    const b = this.myBooking();
    this.confirmCancel.set(false);
    if (!b) return;
    this.cancelling.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.cancel(b.id).subscribe({
      next: () => {
        this.cancelling.set(false);
        this.notice.set('Listo. Cancelaste tu cita. Ya puedes elegir otra hora si quieres.');
        this.load();
      },
      error: (err) => {
        this.cancelling.set(false);
        this.error.set(mapApiError(err));
        this.load();
      }
    });
  }
}
