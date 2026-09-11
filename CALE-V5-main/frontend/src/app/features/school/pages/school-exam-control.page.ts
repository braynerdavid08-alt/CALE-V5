import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import {
  ApprenticeApi,
  TheoryExamControlBoard,
  TheoryExamControlRow
} from '../api/apprentice.api';

@Component({
  selector: 'app-school-exam-control-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  templateUrl: './school-exam-control.page.html',
  styleUrl: './school-exam-control.page.css'
})
export class SchoolExamControlPage implements OnInit, OnDestroy {
  private readonly api = inject(ApprenticeApi);
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  readonly loading = signal(true);
  readonly acting = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly board = signal<TheoryExamControlBoard | null>(null);
  readonly filter = signal<'all' | 'Scheduled' | 'CheckedIn' | 'InProgress' | 'Passed' | 'Failed' | 'NoShow'>('all');

  day = this.toInputDate(new Date());

  readonly rows = computed(() => {
    const all = this.board()?.rows ?? [];
    const f = this.filter();
    if (f === 'all') {
      return all;
    }
    return all.filter((r) => r.boardStatus === f);
  });

  ngOnInit(): void {
    this.reload();
    this.refreshTimer = setInterval(() => this.reload(true), 20000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
    }
  }

  reload(silent = false): void {
    if (!silent) {
      this.loading.set(true);
      this.error.set(null);
    }
    this.api.getExamControlBoard(this.day).subscribe({
      next: (board) => {
        this.board.set(board);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (!silent) {
          this.error.set(mapApiError(err));
        }
      }
    });
  }

  onDayChange(value: string): void {
    this.day = value;
    this.reload();
  }

  goToday(): void {
    this.day = this.toInputDate(new Date());
    this.reload();
  }

  setFilter(
    value: 'all' | 'Scheduled' | 'CheckedIn' | 'InProgress' | 'Passed' | 'Failed' | 'NoShow'
  ): void {
    this.filter.set(value);
  }

  checkIn(row: TheoryExamControlRow): void {
    if (this.acting() || !row.studentUserId) {
      return;
    }
    this.acting.set(true);
    this.error.set(null);
    this.api.examCheckIn(row.appointmentId).subscribe({
      next: () => {
        this.acting.set(false);
        this.ok.set(`Llegada marcada: ${row.studentName}`);
        this.reload(true);
      },
      error: (err) => {
        this.acting.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  noShow(row: TheoryExamControlRow): void {
    if (this.acting()) {
      return;
    }
    this.acting.set(true);
    this.error.set(null);
    this.api.examNoShow(row.appointmentId).subscribe({
      next: () => {
        this.acting.set(false);
        this.ok.set(`Marcado no asistió: ${row.studentName}`);
        this.reload(true);
      },
      error: (err) => {
        this.acting.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Scheduled':
        return 'Programado';
      case 'CheckedIn':
        return 'Presente';
      case 'InProgress':
        return 'En curso';
      case 'Passed':
        return 'Aprobado';
      case 'Failed':
        return 'No aprobado';
      case 'NoShow':
        return 'No asistió';
      default:
        return status;
    }
  }

  private toInputDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}
