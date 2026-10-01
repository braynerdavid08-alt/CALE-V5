import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  computed,
  inject,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { apiErrorCode, mapApiError } from '../../../core/http/map-api-error';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { dayTitle, time12 } from '../../../shared/utils/wall-clock';
import { TheoryExamSchedulingStudentDto } from '../api/apprentice.api';
import {
  ExamBookingDto,
  ExamDayDto,
  ExamScheduleApi,
  ExamSlotDto
} from '../api/exam-schedule.api';
import {
  activeBookings,
  bookingStatusLabel,
  bookingStatusTone,
  plural,
  slotStatusLabel,
  slotStatusTone
} from '../utils/exam-labels';

const MAX_STUDENT_RESULTS = 25;

/** Errors meaning our copy of the week is out of date. */
const STALE_CODES = new Set([
  'exam_slot_full',
  'exam_slot_closed',
  'exam_slot_not_found',
  'exam_slot_past',
  'booking_not_found',
  'capacity_below_bookings'
]);

/** Everything the school can do with one exam hour on one date. */
@Component({
  selector: 'app-exam-slot-detail',
  standalone: true,
  imports: [FormsModule, UiErrorComponent, UiLoadingComponent],
  templateUrl: './exam-slot-detail.component.html',
  styleUrls: ['../../../shared/styles/easy-schedule.css', './exam-slot-detail.component.css']
})
export class ExamSlotDetailComponent implements OnChanges {
  private readonly api = inject(ExamScheduleApi);

  @Input({ required: true }) slot!: ExamSlotDto;
  @Input() readOnly = false;
  @Input() students: TheoryExamSchedulingStudentDto[] = [];
  @Input() studentsLoading = false;
  @Output() dayUpdated = new EventEmitter<ExamDayDto>();
  @Output() slotUpdated = new EventEmitter<ExamSlotDto>();
  @Output() needStudents = new EventEmitter<void>();
  @Output() reload = new EventEmitter<void>();

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  /** Booking whose cancellation is being confirmed. */
  readonly cancelling = signal<number | null>(null);
  cancelReason = '';

  readonly adding = signal(false);
  readonly search = signal('');
  readonly pickedStudent = signal<TheoryExamSchedulingStudentDto | null>(null);
  private readonly studentsSig = signal<TheoryExamSchedulingStudentDto[]>([]);
  private readonly slotSig = signal<ExamSlotDto | null>(null);

  capacity = 1;
  /** Shown when closing the hour would cancel existing bookings. */
  readonly confirmCloseWithBookings = signal(false);

  readonly bookings = computed(() => {
    const s = this.slotSig();
    return s ? activeBookings(s) : [];
  });

  readonly cancelledCount = computed(() => {
    const s = this.slotSig();
    return s ? (s.bookings ?? []).length - activeBookings(s).length : 0;
  });

  readonly filteredStudents = computed(() => {
    const booked = new Set(this.bookings().map((b) => b.studentUserId).filter((id) => id != null));
    const q = this.search().trim().toLowerCase();
    return this.studentsSig()
      .filter((s) => !booked.has(s.studentUserId))
      .filter((s) => !q || s.studentName.toLowerCase().includes(q))
      .slice(0, MAX_STUDENT_RESULTS);
  });

  readonly dayTitle = dayTitle;
  readonly time12 = time12;
  readonly statusLabel = slotStatusLabel;
  readonly statusTone = slotStatusTone;
  readonly bookingLabel = bookingStatusLabel;
  readonly bookingTone = bookingStatusTone;
  readonly plural = plural;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['slot']) {
      const prev = changes['slot'].previousValue as ExamSlotDto | undefined;
      const sameSlot = prev && prev.date === this.slot.date && prev.time === this.slot.time;
      this.slotSig.set(this.slot);
      this.capacity = this.slot.capacity;
      if (!sameSlot) {
        this.error.set(null);
        this.notice.set(null);
        this.cancelling.set(null);
        this.adding.set(false);
        this.pickedStudent.set(null);
        this.confirmCloseWithBookings.set(false);
      }
    }
    if (changes['students']) {
      this.studentsSig.set(this.students);
    }
  }

  get isClosed(): boolean {
    return this.slot.status === 'Closed';
  }

  get canAdd(): boolean {
    return !this.readOnly && !this.isClosed && this.slot.available > 0;
  }

  bookedByLabel(b: ExamBookingDto): string {
    return b.bookedByStudent ? 'Lo agendó el estudiante' : 'Lo agendó la escuela';
  }

  // ── Bookings ──────────────────────────────────────────────

  startAdd(): void {
    this.adding.set(true);
    this.search.set('');
    this.pickedStudent.set(null);
    this.error.set(null);
    this.notice.set(null);
    this.needStudents.emit();
  }

  confirmAdd(): void {
    const student = this.pickedStudent();
    if (!student) {
      this.error.set('Primero toca el nombre del estudiante.');
      return;
    }
    this.run(
      this.api.createBooking({ date: this.slot.date, time: this.slot.time, studentUserId: student.studentUserId }),
      (slot: ExamSlotDto) => {
        this.slotUpdated.emit(slot);
        this.adding.set(false);
        this.pickedStudent.set(null);
        this.notice.set(`Listo. ${student.studentName} quedó agendado.`);
      }
    );
  }

  askCancel(b: ExamBookingDto): void {
    this.cancelReason = '';
    this.error.set(null);
    this.notice.set(null);
    this.cancelling.set(b.id);
  }

  confirmCancel(b: ExamBookingDto): void {
    this.run(this.api.cancelBooking(b.id, this.cancelReason.trim() || null), (slot: ExamSlotDto) => {
      this.slotUpdated.emit(slot);
      this.cancelling.set(null);
      this.notice.set(`Se canceló la cita de ${b.studentName}.`);
    });
  }

  // ── Slot changes for this date ────────────────────────────

  saveCapacity(): void {
    const value = Number(this.capacity);
    if (!Number.isInteger(value) || value < 1) {
      this.error.set('Los cupos deben ser un número entero, mínimo 1.');
      return;
    }
    if (value < this.bookings().length) {
      this.error.set(`Ya hay ${plural(this.bookings().length, 'estudiante agendado', 'estudiantes agendados')}. No puedes poner menos cupos.`);
      return;
    }
    this.run(
      this.api.saveSlot({ date: this.slot.date, time: this.slot.time, capacity: value, isClosed: false }),
      (day: ExamDayDto) => {
        this.dayUpdated.emit(day);
        this.notice.set(`Listo. Esta hora ahora tiene ${plural(value, 'cupo', 'cupos')} para este día.`);
      }
    );
  }

  closeHour(cancelBookings = false): void {
    this.confirmCloseWithBookings.set(false);
    this.run(
      this.api.saveSlot({
        date: this.slot.date,
        time: this.slot.time,
        isClosed: true,
        cancelBookings
      }),
      (day: ExamDayDto) => {
        this.dayUpdated.emit(day);
        this.notice.set('Listo. Esta hora quedó cerrada solo para este día.');
      },
      (code) => {
        if (code === 'slot_has_bookings') {
          this.confirmCloseWithBookings.set(true);
          return true;
        }
        return false;
      }
    );
  }

  revert(): void {
    const id = this.slot.overrideId;
    if (!id) return;
    this.run(this.api.deleteOverride(id), (day: ExamDayDto) => {
      this.dayUpdated.emit(day);
      this.notice.set('Listo. Esta hora volvió a su horario normal.');
    });
  }

  private run<T>(
    request: Observable<T>,
    onOk: (value: T) => void,
    onCode?: (code: string | null) => boolean
  ): void {
    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
    request.subscribe({
      next: (value) => {
        this.busy.set(false);
        onOk(value);
      },
      error: (err) => {
        this.busy.set(false);
        const code = apiErrorCode(err);
        if (onCode?.(code)) return;
        this.error.set(mapApiError(err));
        if (code && STALE_CODES.has(code)) {
          this.reload.emit();
        }
      }
    });
  }
}
