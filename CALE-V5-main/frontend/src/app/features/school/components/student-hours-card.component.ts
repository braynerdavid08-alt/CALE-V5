import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiSheetComponent } from '../../../shared/ui/ui-sheet.component';
import { formatInstant, hoursLabel } from '../../../shared/utils/wall-clock';
import {
  ExamScheduleApi,
  HoursCategory,
  HoursLineDto,
  StudentHoursDto
} from '../api/exam-schedule.api';

/** Theory / workshop hours of one student, with manual correction and its history. */
@Component({
  selector: 'app-student-hours-card',
  standalone: true,
  imports: [FormsModule, UiErrorComponent, UiLoadingComponent, UiSheetComponent],
  templateUrl: './student-hours-card.component.html',
  styleUrls: ['../../../shared/styles/easy-schedule.css', './student-hours-card.component.css']
})
export class StudentHoursCardComponent implements OnChanges {
  private readonly api = inject(ExamScheduleApi);

  @Input({ required: true }) studentUserId!: number;
  @Output() changed = new EventEmitter<StudentHoursDto>();

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly hours = signal<StudentHoursDto | null>(null);
  readonly notice = signal<string | null>(null);

  readonly formOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  category: HoursCategory = 'Theory';
  newHours: number | null = null;
  reason = '';

  readonly hoursLabel = hoursLabel;
  readonly formatInstant = formatInstant;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['studentUserId'] && this.studentUserId > 0) {
      this.load();
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getStudentHours(this.studentUserId).subscribe({
      next: (h) => {
        this.hours.set(h);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  line(category: HoursCategory | string): HoursLineDto | null {
    const h = this.hours();
    if (!h) return null;
    return category === 'Workshop' ? h.workshop : h.theory;
  }

  categoryLabel(category: HoursCategory | string): string {
    return category === 'Workshop' ? 'Taller' : 'Teoría';
  }

  currentTotal(): number {
    return this.line(this.category)?.total ?? 0;
  }

  openForm(): void {
    this.category = 'Theory';
    this.newHours = this.currentTotal();
    this.reason = '';
    this.formError.set(null);
    this.notice.set(null);
    this.formOpen.set(true);
  }

  onCategoryChange(): void {
    this.newHours = this.currentTotal();
  }

  submit(): void {
    const value = Number(this.newHours);
    const reason = this.reason.trim();
    if (this.newHours === null || Number.isNaN(value) || value < 0) {
      this.formError.set('Escribe el nuevo total de horas (0 o más).');
      return;
    }
    if (Math.round(value * 2) !== value * 2) {
      this.formError.set('Usa horas completas o medias horas (por ejemplo 10 o 10,5).');
      return;
    }
    if (value === this.currentTotal()) {
      this.formError.set('El nuevo total es igual al actual. No hay nada que cambiar.');
      return;
    }
    if (reason.length < 3) {
      this.formError.set('Escribe el motivo del cambio.');
      return;
    }
    this.saving.set(true);
    this.formError.set(null);
    const label = this.categoryLabel(this.category);
    this.api
      .adjustStudentHours(this.studentUserId, { category: this.category, newHours: value, reason })
      .subscribe({
        next: (h) => {
          this.saving.set(false);
          this.hours.set(h);
          this.formOpen.set(false);
          this.notice.set(`Listo. Las horas de ${label.toLowerCase()} ahora son ${hoursLabel(value)}.`);
          this.changed.emit(h);
        },
        error: (err) => {
          this.saving.set(false);
          this.formError.set(mapApiError(err));
        }
      });
  }
}
