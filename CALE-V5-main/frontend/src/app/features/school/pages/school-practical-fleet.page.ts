import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { mapApiError } from '../../../core/http/map-api-error';
import { pollWhileVisible } from '../../../core/rxjs/poll-while-visible';
import {
  PracticalApi,
  PracticalLessonSessionDto,
  PracticalVehicleDto
} from '../../practical/api/practical.api';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';

type FleetStatus = 'Free' | 'Reserved' | 'Present' | 'Absent' | 'Completed';
type FleetFilter = 'all' | FleetStatus;

@Component({
  selector: 'app-school-practical-fleet-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  templateUrl: './school-practical-fleet.page.html',
  styleUrl: './school-practical-fleet.page.css'
})
export class SchoolPracticalFleetPage implements OnInit {
  private readonly api = inject(PracticalApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly acting = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly lessons = signal<PracticalLessonSessionDto[]>([]);
  readonly vehicles = signal<PracticalVehicleDto[]>([]);
  readonly filter = signal<FleetFilter>('all');

  day = this.toInputDate(new Date());

  readonly dayLessons = computed(() => {
    const day = this.day;
    return this.lessons()
      .filter((l) => (l.sessionDate || '').slice(0, 10) === day)
      .sort((a, b) => {
        const t = a.startTime.localeCompare(b.startTime);
        return t !== 0 ? t : a.vehicleLabel.localeCompare(b.vehicleLabel, 'es');
      });
  });

  readonly rows = computed(() => {
    const f = this.filter();
    const all = this.dayLessons();
    if (f === 'all') {
      return all;
    }
    return all.filter((l) => this.boardStatus(l) === f);
  });

  readonly counts = computed(() => {
    const all = this.dayLessons();
    return {
      total: all.length,
      free: all.filter((l) => this.boardStatus(l) === 'Free').length,
      reserved: all.filter((l) => this.boardStatus(l) === 'Reserved').length,
      present: all.filter((l) => this.boardStatus(l) === 'Present').length,
      absent: all.filter((l) => this.boardStatus(l) === 'Absent').length,
      completed: all.filter((l) => this.boardStatus(l) === 'Completed').length
    };
  });

  readonly idleVehicles = computed(() => {
    const used = new Set(this.dayLessons().map((l) => l.vehicleId));
    return this.vehicles().filter((v) => v.isActive && !used.has(v.id));
  });

  readonly idleVehiclesLabel = computed(() =>
    this.idleVehicles()
      .map((v) => (v.plate ? `${v.label} (${v.plate})` : v.label))
      .join(', ')
  );

  ngOnInit(): void {
    this.reload();
    pollWhileVisible(30000, () => this.fetchFleet(true), { leading: false })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe();
  }

  reload(silent = false): void {
    this.fetchFleet(silent).subscribe();
  }

  private fetchFleet(silent: boolean) {
    if (!silent) {
      this.loading.set(true);
      this.error.set(null);
    }
    const weekStart = this.mondayOf(this.day);
    return forkJoin({
      lessons: this.api.listLessons(weekStart).pipe(catchError(() => of([] as PracticalLessonSessionDto[]))),
      vehicles: this.api.listVehicles(true).pipe(catchError(() => of([] as PracticalVehicleDto[])))
    }).pipe(
      tap(({ lessons, vehicles }) => {
        this.lessons.set(lessons);
        this.vehicles.set(vehicles);
        this.loading.set(false);
      }),
      catchError((err) => {
        this.loading.set(false);
        if (!silent) {
          this.error.set(mapApiError(err));
        }
        return of(null);
      })
    );
  }

  onDayChange(value: string): void {
    this.day = value;
    this.reload();
  }

  goToday(): void {
    this.day = this.toInputDate(new Date());
    this.reload();
  }

  setFilter(value: FleetFilter): void {
    this.filter.set(value);
  }

  boardStatus(lesson: PracticalLessonSessionDto): FleetStatus {
    if (lesson.status === 'Completed') {
      return 'Completed';
    }
    const res = lesson.assignment?.reservationStatus;
    if (!lesson.assignment) {
      return 'Free';
    }
    if (res === 'Attended') {
      return 'Present';
    }
    if (res === 'NoShow') {
      return 'Absent';
    }
    return 'Reserved';
  }

  statusLabel(status: FleetStatus | string): string {
    switch (status) {
      case 'Free':
        return 'Libre';
      case 'Reserved':
        return 'Reservado';
      case 'Present':
        return 'Presente';
      case 'Absent':
        return 'Ausente';
      case 'Completed':
        return 'Completada';
      default:
        return status;
    }
  }

  studentLabel(lesson: PracticalLessonSessionDto): string {
    const a = lesson.assignment;
    if (!a) {
      return 'Sin aprendiz';
    }
    const cat = a.licenseCategory ? `${a.licenseCategory} · ` : '';
    return `${cat}${a.studentName} (${a.lessonNumber}/${a.lessonsRequired})`;
  }

  mark(lesson: PracticalLessonSessionDto, status: 'Present' | 'Absent'): void {
    const studentId = lesson.assignment?.studentUserId;
    if (!studentId || this.acting()) {
      return;
    }
    this.acting.set(true);
    this.error.set(null);
    this.api.markAttendance(lesson.id, studentId, status).subscribe({
      next: () => {
        this.acting.set(false);
        this.ok.set(
          status === 'Present'
            ? `Presente: ${lesson.assignment?.studentName}`
            : `Ausente: ${lesson.assignment?.studentName}`
        );
        this.reload(true);
      },
      error: (err) => {
        this.acting.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private mondayOf(isoDate: string): string {
    const d = this.parseLocalDate(isoDate);
    const day = d.getDay();
    const diff = day === 0 ? -6 : 1 - day;
    d.setDate(d.getDate() + diff);
    return this.toInputDate(d);
  }

  private toInputDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  private parseLocalDate(iso: string): Date {
    const [y, m, d] = iso.split('-').map(Number);
    return new Date(y, (m || 1) - 1, d || 1);
  }
}
