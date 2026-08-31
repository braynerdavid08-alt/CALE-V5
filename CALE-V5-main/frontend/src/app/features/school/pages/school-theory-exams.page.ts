import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  PracticalApi,
  PracticalSchedulingStudentDto
} from '../../practical/api/practical.api';
import { ApprenticeApi, TheoryExamSlotDto } from '../api/apprentice.api';

const EXAM_SLOTS = ['09:00', '10:00', '11:00', '12:00', '13:00', '14:00', '15:00', '16:00'];

@Component({
  selector: 'app-school-theory-exams-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  templateUrl: './school-theory-exams.page.html',
  styleUrl: './school-theory-exams.page.css'
})
export class SchoolTheoryExamsPage implements OnInit {
  private readonly api = inject(ApprenticeApi);
  private readonly practicalApi = inject(PracticalApi);

  readonly slots = EXAM_SLOTS;
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly rows = signal<TheoryExamSlotDto[]>([]);
  readonly students = signal<PracticalSchedulingStudentDto[]>([]);
  readonly studentSearch = signal('');
  readonly weekStart = signal(this.monday(new Date()));

  readonly filteredStudents = computed(() => {
    const q = this.studentSearch().trim().toLowerCase();
    const rows = this.students();
    if (!q) {
      return rows;
    }
    return rows.filter((s) => s.studentName.toLowerCase().includes(q));
  });

  form = { examDate: '', slotTime: '09:00', studentUserId: null as number | null, notes: '' };

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    const start = this.weekStart();
    const end = this.addDays(start, 13);
    forkJoin({
      rows: this.api.listExamSlots(start, end),
      students: this.practicalApi.listSchedulingStudents().pipe(
        catchError(() => of([] as PracticalSchedulingStudentDto[]))
      )
    }).subscribe({
      next: ({ rows, students }) => {
        this.rows.set(rows);
        this.students.set(students);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  prevWeek(): void {
    this.weekStart.set(this.addDays(this.weekStart(), -7));
    this.reload();
  }

  nextWeek(): void {
    this.weekStart.set(this.addDays(this.weekStart(), 7));
    this.reload();
  }

  slotAt(date: string, time: string): TheoryExamSlotDto | undefined {
    return this.rows().find((r) => r.examDate === date && r.slotTime.slice(0, 5) === time);
  }

  dayDates(): string[] {
    return Array.from({ length: 14 }, (_, i) => this.addDays(this.weekStart(), i));
  }

  dayLabel(date: string): string {
    const d = new Date(date + 'T12:00:00');
    return d.toLocaleDateString('es-CO', { weekday: 'short', day: 'numeric', month: 'short' });
  }

  openForm(date: string, time: string): void {
    const existing = this.slotAt(date, time);
    this.studentSearch.set('');
    this.form = {
      examDate: date,
      slotTime: time,
      studentUserId: existing?.studentUserId ?? this.matchStudentId(existing) ?? null,
      notes: existing?.notes ?? ''
    };
  }

  save(): void {
    if (!this.form.studentUserId) {
      this.error.set('Selecciona un aprendiz autorizado.');
      return;
    }
    const existing = this.slotAt(this.form.examDate, this.form.slotTime);
    this.api.saveExamSlot({
      examDate: this.form.examDate,
      slotTime: this.form.slotTime,
      studentUserId: this.form.studentUserId,
      notes: this.form.notes.trim() || null
    }, existing?.id).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  remove(slot: TheoryExamSlotDto): void {
    if (!confirm('¿Quitar esta cita de examen?')) return;
    this.api.deleteExamSlot(slot.id).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private monday(date: Date): string {
    const d = new Date(date);
    const day = d.getDay();
    const diff = day === 0 ? -6 : 1 - day;
    d.setDate(d.getDate() + diff);
    return this.key(d);
  }

  private addDays(iso: string, days: number): string {
    const d = new Date(iso + 'T12:00:00');
    d.setDate(d.getDate() + days);
    return this.key(d);
  }

  private key(d: Date): string {
    return `${d.getFullYear()}-${`${d.getMonth() + 1}`.padStart(2, '0')}-${`${d.getDate()}`.padStart(2, '0')}`;
  }

  private matchStudentId(slot?: TheoryExamSlotDto): number | null {
    if (!slot) {
      return null;
    }
    const label = (slot.studentName || slot.studentLabel || '').trim().toLowerCase();
    if (!label) {
      return null;
    }
    const match = this.students().find((s) => s.studentName.trim().toLowerCase() === label);
    return match?.studentUserId ?? null;
  }
}
