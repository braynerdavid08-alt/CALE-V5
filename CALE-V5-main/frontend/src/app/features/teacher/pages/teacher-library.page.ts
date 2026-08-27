import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiIconComponent } from '../../../shared/ui/ui-icon.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { ExamDto } from '../../student/api/exam.api';
import { GroupDto } from '../../student/api/student.api';
import { BankAdminDto, TeacherApi } from '../api/teacher.api';

type LibraryTab = 'recent' | 'drafts' | 'published';
type ViewMode = 'grid' | 'list';

@Component({
  selector: 'app-teacher-library-page',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    UiBadgeComponent,
    UiButtonComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiIconComponent,
    UiSuccessComponent
  ],
  templateUrl: './teacher-library.page.html',
  styleUrl: './teacher-library.page.css'
})
export class TeacherLibraryPage implements OnInit {
  private readonly api = inject(TeacherApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly session = inject(SessionStore);

  readonly exams = signal<ExamDto[]>([]);
  readonly banks = signal<BankAdminDto[]>([]);
  readonly groups = signal<GroupDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly tab = signal<LibraryTab>('recent');
  readonly view = signal<ViewMode>('grid');
  readonly query = signal('');
  readonly showCreate = signal(false);
  readonly menuFor = signal<number | null>(null);

  name = '';
  bankId: number | null = null;
  questionCount = 20;
  timeMinutes = 30;
  allowedAttempts = 1;
  randomize = true;
  startsAt = '';
  endsAt = '';
  assignTo: Record<number, number | null> = {};

  readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    let items = [...this.exams()];
    const t = this.tab();
    if (t === 'drafts') {
      items = items.filter((e) => !e.published);
    } else if (t === 'published') {
      items = items.filter((e) => e.published);
    }
    if (q) {
      items = items.filter((e) => e.name.toLowerCase().includes(q));
    }
    return items;
  });

  readonly authorLabel = computed(
    () => this.session.user()?.email?.split('@')[0]
      || this.session.user()?.name
      || 'instructor'
  );

  ngOnInit(): void {
    this.reload();
    this.api.banks(true).subscribe({ next: (items) => this.banks.set(items) });
    this.api.groups().subscribe({ next: (items) => this.groups.set(items) });

    this.route.queryParamMap.subscribe((params) => {
      const q = params.get('q') ?? '';
      this.query.set(q);
      if (params.get('crear') === '1') {
        this.showCreate.set(true);
      }
    });
  }

  setTab(tab: LibraryTab): void {
    this.tab.set(tab);
  }

  setView(view: ViewMode): void {
    this.view.set(view);
  }

  onLocalSearch(value: string): void {
    this.query.set(value);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { q: value.trim() || null },
      queryParamsHandling: 'merge'
    });
  }

  reload(): void {
    this.api.exams().subscribe({
      next: (items) => this.exams.set(items),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  openCreate(): void {
    this.showCreate.set(true);
  }

  closeCreate(): void {
    this.showCreate.set(false);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { crear: null },
      queryParamsHandling: 'merge'
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
        this.closeCreate();
        this.reload();
        this.ok.set('Examen creado.');
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  toggleMenu(id: number, event: Event): void {
    event.stopPropagation();
    this.menuFor.update((current) => (current === id ? null : id));
  }

  togglePublish(exam: ExamDto): void {
    this.menuFor.set(null);
    this.api.publishExam(exam.id, !exam.published).subscribe({
      next: () => {
        this.ok.set(exam.published ? 'Pasó a borrador.' : 'Examen publicado.');
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
    this.menuFor.set(null);
    this.api.assignExam(examId, groupId).subscribe({
      next: () => {
        this.ok.set('Examen asignado al grupo.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  thumbTone(exam: ExamDto): string {
    const n = exam.id % 4;
    return `tone-${n}`;
  }
}
