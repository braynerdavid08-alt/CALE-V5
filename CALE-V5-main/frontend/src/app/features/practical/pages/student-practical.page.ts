import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { mapApiError } from '../../../core/http/map-api-error';
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
    this.api.reserve(session.id).subscribe({
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

  cancel(session: PracticalLessonSessionDto): void {
    if (!session.myReservationId) {
      return;
    }
    this.actionLoading.set(true);
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
