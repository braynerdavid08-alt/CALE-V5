import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { apiErrorCode, mapApiError } from '../../../core/http/map-api-error';
import {
  PracticalApi,
  PracticalLessonSessionDto,
  PracticalStudentDashboardDto,
  formatLessonWindow,
  isPracticalExam,
  practicalKindLabel
} from '../api/practical.api';

type SlotFilter = 'all' | 'lesson' | 'exam';

@Component({
  selector: 'app-student-practical-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent],
  templateUrl: './student-practical.page.html',
  styleUrl: './student-practical.page.css'
})
export class StudentPracticalPage implements OnInit {
  private readonly api = inject(PracticalApi);

  readonly loading = signal(true);
  readonly actionLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly rescheduleFromId = signal<number | null>(null);
  readonly dashboard = signal<PracticalStudentDashboardDto | null>(null);
  readonly selectedInstructorId = signal(0);
  readonly slotFilter = signal<SlotFilter>('all');

  readonly practiceRequired = computed(() => {
    const dash = this.dashboard();
    if (!dash) {
      return 0;
    }
    if (dash.practiceRequiredLessons && dash.practiceRequiredLessons > 0) {
      return dash.practiceRequiredLessons;
    }
    return Math.max(0, (dash.requiredLessons ?? 0) - 1);
  });

  readonly filteredLessons = computed(() => {
    const dash = this.dashboard();
    const instructorId = this.selectedInstructorId();
    const filter = this.slotFilter();
    if (!dash || !instructorId) {
      return [];
    }
    return dash.availableLessons.filter((l) => {
      if (l.instructorUserId !== instructorId) {
        return false;
      }
      if (filter === 'lesson') {
        return !isPracticalExam(l);
      }
      if (filter === 'exam') {
        return isPracticalExam(l);
      }
      return true;
    });
  });

  readonly selectedInstructorName = computed(() => {
    const dash = this.dashboard();
    const id = this.selectedInstructorId();
    return dash?.availableInstructors?.find((i) => i.instructorUserId === id)?.instructorName
      ?? 'tu instructor';
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.studentDashboard().subscribe({
      next: (d) => {
        this.dashboard.set(d);
        const current = this.selectedInstructorId();
        const instructors = d.availableInstructors ?? [];
        if (instructors.length === 0) {
          this.selectedInstructorId.set(0);
        } else if (!current || !instructors.some((i) => i.instructorUserId === current)) {
          this.selectedInstructorId.set(instructors[0].instructorUserId);
        }
        if (d.canBookPracticalExam && this.slotFilter() === 'all') {
          this.slotFilter.set('exam');
        }
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  selectInstructor(instructorUserId: number): void {
    this.selectedInstructorId.set(instructorUserId);
  }

  setSlotFilter(filter: SlotFilter): void {
    this.slotFilter.set(filter);
  }

  canReserve(session: PracticalLessonSessionDto): boolean {
    const dash = this.dashboard();
    const elig = dash?.eligibility;
    if (!elig?.canBookPractical || session.bookingState !== 'can_reserve') {
      return false;
    }
    if (isPracticalExam(session)) {
      return !!dash?.canBookPracticalExam;
    }
    return true;
  }

  canJoinWaitlist(session: PracticalLessonSessionDto): boolean {
    if (session.bookingState !== 'full') {
      return false;
    }
    const dash = this.dashboard();
    if (!dash?.eligibility.canBookPractical) {
      return false;
    }
    if (isPracticalExam(session)) {
      return !!dash.canBookPracticalExam;
    }
    return true;
  }

  reserveBlockedReason(session: PracticalLessonSessionDto): string | null {
    if (session.bookingState === 'reserved' || session.bookingState === 'full') {
      return null;
    }
    if (isPracticalExam(session) && !this.dashboard()?.canBookPracticalExam) {
      return this.dashboard()?.practicalExamBlockReason ?? 'Aún no puedes agendar el examen.';
    }
    if (!this.dashboard()?.eligibility.canBookPractical) {
      return this.dashboard()?.eligibility.blockReason ?? 'Aún no puedes reservar.';
    }
    return session.bookingMessage ?? null;
  }

  onSessionAction(session: PracticalLessonSessionDto): void {
    if (this.canReserve(session)) {
      const fromId = this.rescheduleFromId();
      if (fromId) {
        this.rescheduleTo(session, fromId);
        return;
      }
      this.reserve(session);
      return;
    }
    if (this.canJoinWaitlist(session)) {
      this.joinWaitlist(session);
    }
  }

  startReschedule(session: PracticalLessonSessionDto): void {
    if (!session.myReservationId) {
      return;
    }
    this.rescheduleFromId.set(session.myReservationId);
    this.notice.set('Elige el nuevo horario. Tu cupo actual se mantiene hasta confirmar el cambio.');
    this.error.set(null);
  }

  cancelRescheduleMode(): void {
    this.rescheduleFromId.set(null);
    this.notice.set(null);
  }

  rescheduleTo(session: PracticalLessonSessionDto, reservationId: number): void {
    this.actionLoading.set(true);
    this.error.set(null);
    this.api.rescheduleReservation(reservationId, session.id).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.rescheduleFromId.set(null);
        this.notice.set('Listo. Reprogramaste tu clase de manejo.');
        this.reload();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  lessonWindow(session: PracticalLessonSessionDto): string {
    return formatLessonWindow(session);
  }

  kindLabel(session: PracticalLessonSessionDto): string {
    return practicalKindLabel(session.kind);
  }

  isExam(session: PracticalLessonSessionDto): boolean {
    return isPracticalExam(session);
  }

  reserve(session: PracticalLessonSessionDto): void {
    if (!this.canReserve(session)) {
      return;
    }
    this.actionLoading.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.reserve(session.id).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.reload();
      },
      error: (err) => {
        this.actionLoading.set(false);
        if (apiErrorCode(err) === 'lesson_full') {
          this.joinWaitlist(session);
          return;
        }
        this.error.set(mapApiError(err));
      }
    });
  }

  joinWaitlist(session: PracticalLessonSessionDto): void {
    this.actionLoading.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.joinWaitlist(session.id).subscribe({
      next: (w) => {
        this.actionLoading.set(false);
        this.notice.set(
          `Quedaste en lista de espera (puesto ${w.position}). Te avisamos si se libera un cupo; confírmalo en Mi formación.`
        );
        this.reload();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  cancel(session: PracticalLessonSessionDto): void {
    if (!session.myReservationId) {
      return;
    }
    this.actionLoading.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.cancelReservation(session.myReservationId).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.reload();
      },
      error: (err) => {
        this.actionLoading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
