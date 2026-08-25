import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { ExamDto } from '../../student/api/exam.api';
import { GroupDto } from '../../student/api/student.api';
import { BankAdminDto, TeacherApi } from '../api/teacher.api';

@Component({
  selector: 'app-teacher-exams-page',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  template: `
    <ui-page-header title="Exámenes" subtitle="Crea, publica y asigna evaluaciones." />
    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />
    <ui-card>
      <h2>Nuevo examen</h2>
      <form class="stack" (ngSubmit)="create()">
        <label class="field">Nombre
          <input [(ngModel)]="name" name="name" required />
        </label>
        <label class="field">Banco (opcional)
          <select [(ngModel)]="bankId" name="bankId">
            <option [ngValue]="null">Sin banco fijo</option>
            @for (bank of banks(); track bank.id) {
              <option [ngValue]="bank.id">{{ bank.name }}</option>
            }
          </select>
        </label>
        <div class="grid-stats">
          <label class="field">Preguntas
            <input type="number" [(ngModel)]="questionCount" name="qc" min="1" />
          </label>
          <label class="field">Minutos
            <input type="number" [(ngModel)]="timeMinutes" name="tm" min="1" />
          </label>
          <label class="field">Intentos
            <input type="number" [(ngModel)]="allowedAttempts" name="aa" min="1" />
          </label>
        </div>
        <div class="grid-2">
          <label class="field">Inicio (opcional)
            <input type="datetime-local" [(ngModel)]="startsAt" name="sa" />
          </label>
          <label class="field">Fin (opcional)
            <input type="datetime-local" [(ngModel)]="endsAt" name="ea" />
          </label>
        </div>
        <label class="row">
          <input type="checkbox" [(ngModel)]="randomize" name="rnd" /> Aleatorio
        </label>
        <ui-button type="submit">Crear examen</ui-button>
      </form>
    </ui-card>

    @if (!exams().length) {
      <ui-empty title="No hay exámenes" message="Crea el primero para publicarlo." />
    } @else {
      <ui-card>
        <div class="table-wrap">
          <table class="data">
            <thead>
              <tr>
                <th>Nombre</th>
                <th>Preguntas</th>
                <th>Tiempo</th>
                <th>Ventana</th>
                <th>Estado</th>
                <th>Asignar</th>
              </tr>
            </thead>
            <tbody>
              @for (exam of exams(); track exam.id) {
                <tr>
                  <td>{{ exam.name }}</td>
                  <td>{{ exam.questionCount }}</td>
                  <td>{{ exam.timeMinutes }} min</td>
                  <td class="muted">
                    {{ exam.startsAt ? (exam.startsAt | date:'short') : '—' }}
                    →
                    {{ exam.endsAt ? (exam.endsAt | date:'short') : '—' }}
                  </td>
                  <td>
                    <ui-badge [tone]="exam.published ? 'success' : 'warning'">
                      {{ exam.published ? 'Publicado' : 'Borrador' }}
                    </ui-badge>
                    <ui-button type="button" variant="ghost" (click)="toggle(exam)">
                      {{ exam.published ? 'Despublicar' : 'Publicar' }}
                    </ui-button>
                  </td>
                  <td>
                    <div class="row">
                      <select class="select" [(ngModel)]="assignTo[exam.id]" [name]="'g'+exam.id">
                        <option [ngValue]="null">Grupo</option>
                        @for (group of groups(); track group.id) {
                          <option [ngValue]="group.id">{{ group.name }}</option>
                        }
                      </select>
                      <ui-button type="button" variant="secondary" (click)="assign(exam.id)">
                        Asignar
                      </ui-button>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </ui-card>
    }
  `
})
export class TeacherExamsPage implements OnInit {
  private readonly api = inject(TeacherApi);
  readonly exams = signal<ExamDto[]>([]);
  readonly banks = signal<BankAdminDto[]>([]);
  readonly groups = signal<GroupDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  name = '';
  bankId: number | null = null;
  questionCount = 20;
  timeMinutes = 30;
  allowedAttempts = 1;
  randomize = true;
  startsAt = '';
  endsAt = '';
  assignTo: Record<number, number | null> = {};

  ngOnInit(): void {
    this.reload();
    this.api.banks(true).subscribe({ next: (items) => this.banks.set(items) });
    this.api.groups().subscribe({ next: (items) => this.groups.set(items) });
  }

  reload(): void {
    this.api.exams().subscribe({
      next: (items) => this.exams.set(items),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  create(): void {
    if (!this.name.trim()) {
      return;
    }
    this.api.createExam({
      name: this.name.trim(),
      bankId: this.bankId,
      questionCount: this.questionCount,
      timeMinutes: this.timeMinutes,
      allowedAttempts: this.allowedAttempts,
      randomize: this.randomize,
      startsAt: this.startsAt ? new Date(this.startsAt).toISOString() : null,
      endsAt: this.endsAt ? new Date(this.endsAt).toISOString() : null
    }).subscribe({
      next: () => {
        this.name = '';
        this.startsAt = '';
        this.endsAt = '';
        this.reload();
        this.ok.set('Examen creado.');
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  toggle(exam: ExamDto): void {
    this.api.publishExam(exam.id, !exam.published).subscribe({
      next: () => {
        this.ok.set('Estado actualizado.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  assign(examId: number): void {
    const groupId = this.assignTo[examId];
    if (!groupId) {
      this.error.set('Elige un grupo.');
      return;
    }
    this.api.assignExam(examId, groupId).subscribe({
      next: () => this.ok.set('Examen asignado.'),
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
