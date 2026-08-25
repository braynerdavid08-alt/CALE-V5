import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { ExamDto } from '../../student/api/exam.api';
import { ActivityDto, AnnouncementDto, GroupDto } from '../../student/api/student.api';
import {
  MaterialDto,
  MemberDto,
  SubmissionDto,
  TeacherApi
} from '../api/teacher.api';

@Component({
  selector: 'app-teacher-group-page',
  standalone: true,
  imports: [
    FormsModule,
    UiButtonComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  template: `
    <ui-page-header
      [title]="group()?.name || 'Aula'"
      [subtitle]="group() ? ('Código ' + group()!.code) : ''">
      @if (group(); as g) {
        <ui-button type="button" variant="secondary" (click)="copy(g.code)">
          Copiar código
        </ui-button>
      }
    </ui-page-header>
    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />

    <h2>Integrantes</h2>
    <form (ngSubmit)="addMember()">
      <input [(ngModel)]="memberEmail" name="email" placeholder="Correo del estudiante" />
      <ui-button type="submit">Agregar</ui-button>
    </form>
    @if (!members().length) {
      <ui-empty title="Sin integrantes" message="" />
    } @else {
      <ul>
        @for (m of members(); track m.userId) {
          <li>
            {{ m.name || ('Usuario #' + m.userId) }}
            <ui-button type="button" variant="ghost" (click)="remove(m.userId)">
              Quitar
            </ui-button>
          </li>
        }
      </ul>
    }

    <h2>Aviso</h2>
    <input [(ngModel)]="title" name="title" placeholder="Título" />
    <textarea [(ngModel)]="body" name="body" placeholder="Contenido"></textarea>
    <ui-button type="button" (click)="publishAviso()">Publicar aviso</ui-button>
    @for (item of announcements(); track item.id) {
      <article>
        <h3>{{ item.title }}</h3>
        <p>{{ item.body }}</p>
      </article>
    }

    <h2>Material</h2>
    <input [(ngModel)]="materialTitle" name="mt" placeholder="Título" />
    <input [(ngModel)]="materialUrl" name="mu" placeholder="URL (opcional)" />
    <textarea [(ngModel)]="materialText" name="mx" placeholder="Texto"></textarea>
    <ui-button type="button" (click)="publishMaterial()">Publicar material</ui-button>
    @for (item of materials(); track item.id) {
      <article>
        <h3>{{ item.title }}</h3>
        <p>{{ item.textContent || item.url || item.description }}</p>
      </article>
    }

    <h2>Actividad</h2>
    <input [(ngModel)]="activityTitle" name="at" placeholder="Título" />
    <textarea [(ngModel)]="activityBody" name="ab" placeholder="Descripción"></textarea>
    <ui-button type="button" (click)="publishActivity()">Publicar actividad</ui-button>

    <h2>Entregas</h2>
    @if (!activities().length) {
      <ui-empty title="Sin actividades" message="" />
    } @else {
      @for (act of activities(); track act.id) {
        <article>
          <h3>{{ act.title }}</h3>
          @for (sub of submissions()[act.id] || []; track sub.id) {
            <div class="sub">
              <p>
                <strong>{{ sub.userName }}</strong> · {{ sub.status }}
                <span>{{ sub.text || 'Sin texto' }}</span>
              </p>
              @if (sub.status !== 'Graded') {
                <input
                  type="number"
                  [(ngModel)]="scores[sub.userId]"
                  [name]="'sc'+sub.id"
                  placeholder="Nota" />
                <input
                  [(ngModel)]="comments[sub.userId]"
                  [name]="'cm'+sub.id"
                  placeholder="Comentario" />
                <ui-button type="button" (click)="grade(act.id, sub.userId)">
                  Calificar
                </ui-button>
              } @else {
                <p>Nota: {{ sub.score }} · {{ sub.teacherComment }}</p>
              }
            </div>
          }
        </article>
      }
    }

    <h2>Asignar examen</h2>
    <select [(ngModel)]="examId" name="examId">
      <option [ngValue]="null">Selecciona examen</option>
      @for (exam of exams(); track exam.id) {
        <option [ngValue]="exam.id">{{ exam.name }}</option>
      }
    </select>
    <ui-button type="button" (click)="assignExam()">Asignar al grupo</ui-button>
  `,
  styles: [`
    input, textarea, select { display: block; width: min(520px, 100%); margin: .4rem 0 1rem;
      padding: .5rem .7rem; border: 1px solid var(--color-border);
      border-radius: var(--radius-md); font: inherit; }
    textarea { min-height: 90px; }
    form { display: flex; gap: .5rem; align-items: center; max-width: 520px; }
    form input { flex: 1; margin: 0; }
    article { background: var(--color-surface); border: 1px solid var(--color-border);
      border-radius: var(--radius-lg); padding: 1rem; margin: .6rem 0; }
    .meta { color: var(--color-muted); display: flex; gap: .6rem; align-items: center; }
    .sub { border-top: 1px solid var(--color-border); padding-top: .6rem; margin-top: .6rem; }
    .sub span { display: block; color: var(--color-muted); }
    .ok { color: var(--color-primary); font-weight: 600; }
  `]
})
export class TeacherGroupPage implements OnInit {
  private readonly api = inject(TeacherApi);
  private readonly route = inject(ActivatedRoute);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly group = signal<GroupDto | null>(null);
  readonly members = signal<MemberDto[]>([]);
  readonly announcements = signal<AnnouncementDto[]>([]);
  readonly materials = signal<MaterialDto[]>([]);
  readonly activities = signal<ActivityDto[]>([]);
  readonly submissions = signal<Record<number, SubmissionDto[]>>({});
  readonly exams = signal<ExamDto[]>([]);
  groupId = 0;
  title = '';
  body = '';
  activityTitle = '';
  activityBody = '';
  materialTitle = '';
  materialUrl = '';
  materialText = '';
  memberEmail = '';
  examId: number | null = null;
  scores: Record<number, number> = {};
  comments: Record<number, string> = {};

  ngOnInit(): void {
    this.groupId = Number(this.route.snapshot.paramMap.get('id'));
    this.reload();
    this.api.exams().subscribe({
      next: (items) => this.exams.set(items),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  reload(): void {
    this.api.group(this.groupId).subscribe({
      next: (g) => this.group.set(g),
      error: (err) => this.error.set(mapApiError(err))
    });
    this.api.members(this.groupId).subscribe({
      next: (items) => this.members.set(items),
      error: (err) => this.error.set(mapApiError(err))
    });
    this.api.announcements(this.groupId).subscribe({
      next: (items) => this.announcements.set(items)
    });
    this.api.materials(this.groupId).subscribe({
      next: (items) => this.materials.set(items)
    });
    this.api.activities(this.groupId).subscribe({
      next: (items) => {
        this.activities.set(items);
        if (!items.length) {
          this.submissions.set({});
          return;
        }
        forkJoin(items.map((a) => this.api.submissions(a.id))).subscribe({
          next: (lists) => {
            const map: Record<number, SubmissionDto[]> = {};
            items.forEach((a, i) => {
              map[a.id] = lists[i];
            });
            this.submissions.set(map);
          }
        });
      }
    });
  }

  copy(code: string): void {
    void navigator.clipboard.writeText(code);
    this.ok.set('Código copiado.');
  }

  addMember(): void {
    if (!this.memberEmail.trim()) {
      return;
    }
    this.api.addMember(this.groupId, this.memberEmail.trim()).subscribe({
      next: () => {
        this.memberEmail = '';
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  remove(userId: number): void {
    this.api.removeMember(this.groupId, userId).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  publishAviso(): void {
    this.api.publishAnnouncement(this.groupId, this.title, this.body).subscribe({
      next: () => {
        this.title = '';
        this.body = '';
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  publishMaterial(): void {
    this.api.publishMaterial(
      this.groupId,
      this.materialTitle,
      'general',
      this.materialUrl ? 'link' : 'text',
      this.materialUrl || undefined,
      this.materialText || undefined
    ).subscribe({
      next: () => {
        this.materialTitle = '';
        this.materialUrl = '';
        this.materialText = '';
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  publishActivity(): void {
    this.api.publishActivity(
      this.groupId,
      this.activityTitle,
      this.activityBody,
      'actividad'
    ).subscribe({
      next: () => {
        this.activityTitle = '';
        this.activityBody = '';
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  grade(activityId: number, userId: number): void {
    const score = Number(this.scores[userId] ?? 0);
    this.api.grade(activityId, userId, score, this.comments[userId]).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  assignExam(): void {
    if (!this.examId) {
      this.error.set('Elige un examen.');
      return;
    }
    this.api.assignExam(this.examId, this.groupId).subscribe({
      next: () => this.ok.set('Examen asignado al grupo.'),
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
