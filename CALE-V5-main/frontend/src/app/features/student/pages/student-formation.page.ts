import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { PracticalApi } from '../../practical/api/practical.api';
import {
  ScheduleWaitlistEntryDto,
  TheoryApi
} from '../../theory/api/theory.api';
import {
  FormationStageDto,
  StudentFormationApi,
  StudentFormationStatusDto
} from '../api/student-formation.api';
import { StudentExamApi } from '../api/student-exam.api';
import { NextStepBannerComponent } from '../components/next-step-banner.component';

@Component({
  selector: 'app-student-formation-page',
  standalone: true,
  imports: [RouterLink, UiErrorComponent, UiLoadingComponent, NextStepBannerComponent],
  templateUrl: './student-formation.page.html',
  styleUrl: './student-formation.page.css'
})
export class StudentFormationPage implements OnInit {
  private readonly api = inject(StudentFormationApi);
  private readonly theory = inject(TheoryApi);
  private readonly practical = inject(PracticalApi);
  private readonly exams = inject(StudentExamApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly status = signal<StudentFormationStatusDto | null>(null);
  readonly waitlist = signal<ScheduleWaitlistEntryDto[]>([]);
  readonly waitlistBusy = signal<number | null>(null);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.api.status().subscribe({
      next: (s) => {
        this.status.set(s);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
    this.theory.myWaitlist().subscribe({
      next: (rows) => this.waitlist.set(rows),
      error: () => this.waitlist.set([])
    });
  }

  confirmWaitlist(entry: ScheduleWaitlistEntryDto): void {
    this.waitlistBusy.set(entry.id);
    const done = {
      next: () => {
        this.waitlistBusy.set(null);
        this.reload();
      },
      error: (err: unknown) => {
        this.waitlistBusy.set(null);
        this.error.set(mapApiError(err));
        this.reload();
      }
    };
    if (entry.kind === 'practical_lesson') {
      this.practical.confirmWaitlist(entry.id).subscribe(done);
      return;
    }
    if (entry.kind === 'theory_exam') {
      this.exams.confirmWaitlist(entry.id).subscribe(done);
      return;
    }
    this.theory.confirmWaitlist(entry.id).subscribe(done);
  }

  leaveWaitlist(entry: ScheduleWaitlistEntryDto): void {
    this.waitlistBusy.set(entry.id);
    this.theory.leaveWaitlist(entry.id).subscribe({
      next: () => {
        this.waitlistBusy.set(null);
        this.reload();
      },
      error: (err) => {
        this.waitlistBusy.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'completed':
        return 'Completado';
      case 'in_progress':
        return 'En progreso';
      case 'available_to_book':
        return 'Disponible para agendar';
      case 'reserved':
        return 'Reservado';
      case 'needs_authorization':
        return 'Requiere autorización';
      case 'blocked':
        return 'Bloqueado';
      case 'pending':
        return 'Pendiente';
      default:
        return status;
    }
  }

  tone(status: string): string {
    switch (status) {
      case 'completed':
        return 'ok';
      case 'available_to_book':
      case 'reserved':
      case 'in_progress':
        return 'go';
      case 'needs_authorization':
        return 'warn';
      case 'blocked':
        return 'bad';
      default:
        return 'mute';
    }
  }

  showCta(stage: FormationStageDto): boolean {
    return !!stage.ctaPath && (!!stage.ctaLabel || stage.canBook);
  }
}
