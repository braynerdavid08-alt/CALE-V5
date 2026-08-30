import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { env } from '../../../core/config/env';
import {
  PracticalApi,
  PracticalLessonSessionDto,
  PracticalVehicleDto
} from '../api/practical.api';

interface MemberRow {
  id: number;
  name: string;
  email: string;
  role: string;
}

@Component({
  selector: 'app-school-practical-page',
  standalone: true,
  imports: [
    FormsModule,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  templateUrl: './school-practical.page.html',
  styleUrl: './school-practical.page.css'
})
export class SchoolPracticalPage implements OnInit {
  private readonly api = inject(PracticalApi);
  private readonly http = inject(HttpClient);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly vehicles = signal<PracticalVehicleDto[]>([]);
  readonly lessons = signal<PracticalLessonSessionDto[]>([]);
  readonly teachers = signal<MemberRow[]>([]);

  vehicleForm = { label: '', plate: '', isActive: true };
  lessonForm = {
    sessionDate: '',
    startTime: '08:00',
    endTime: '09:00',
    instructorUserId: 0,
    vehicleId: 0,
    capacity: 1,
    notes: ''
  };

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.http.get<MemberRow[]>(`${env.apiUrl}/api/school/members`).subscribe({
      next: (members) => {
        const teachers = members.filter((m) => m.role === 'Teacher');
        this.teachers.set(teachers);
        if (!this.lessonForm.instructorUserId && teachers[0]) {
          this.lessonForm.instructorUserId = teachers[0].id;
        }
      },
      error: () => this.teachers.set([])
    });
    this.api.listVehicles(true).subscribe({
      next: (v) => {
        this.vehicles.set(v);
        if (!this.lessonForm.vehicleId && v[0]) {
          this.lessonForm.vehicleId = v[0].id;
        }
      },
      error: () => this.vehicles.set([])
    });
    this.api.listLessons().subscribe({
      next: (rows) => {
        this.lessons.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  saveVehicle(): void {
    if (!this.vehicleForm.label.trim()) {
      return;
    }
    this.saving.set(true);
    this.api.saveVehicle({
      label: this.vehicleForm.label.trim(),
      plate: this.vehicleForm.plate.trim() || null,
      isActive: this.vehicleForm.isActive
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.vehicleForm = { label: '', plate: '', isActive: true };
        this.reload();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  createLesson(): void {
    if (!this.lessonForm.sessionDate || !this.lessonForm.instructorUserId || !this.lessonForm.vehicleId) {
      this.error.set('Completa fecha, instructor y vehículo.');
      return;
    }
    this.saving.set(true);
    this.api.createLesson({
      sessionDate: this.lessonForm.sessionDate,
      startTime: this.lessonForm.startTime,
      endTime: this.lessonForm.endTime,
      instructorUserId: this.lessonForm.instructorUserId,
      vehicleId: this.lessonForm.vehicleId,
      capacity: this.lessonForm.capacity,
      notes: this.lessonForm.notes.trim() || null
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.reload();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  cancelLesson(id: number): void {
    this.api.cancelLesson(id).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
