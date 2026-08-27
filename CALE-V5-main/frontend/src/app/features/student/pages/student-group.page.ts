import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import {
  ActivityDto,
  AnnouncementDto,
  GroupDto,
  StudentApi
} from '../api/student.api';

interface MaterialRow {
  id: number;
  title: string;
  description?: string | null;
  url?: string | null;
  textContent?: string | null;
}

@Component({
  selector: 'app-student-group-page',
  standalone: true,
  imports: [
    FormsModule,
    UiButtonComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      [title]="group()?.name || 'Mi grupo'"
      [subtitle]="group() ? ((group()!.teacherName || 'Sin instructor') + ' · ' + group()!.code) : ''" />
    <ui-error [message]="error()" />

    <h2>Avisos</h2>
    @if (!announcements().length) {
      <ui-empty title="Sin avisos" message="" />
    } @else {
      @for (item of announcements(); track item.id) {
        <article>
          <h3>{{ item.title }}</h3>
          <p>{{ item.body }}</p>
        </article>
      }
    }

    <h2>Material</h2>
    @if (!materials().length) {
      <ui-empty title="Sin material" message="" />
    } @else {
      @for (item of materials(); track item.id) {
        <article>
          <h3>{{ item.title }}</h3>
          @if (item.url) {
            <p><a [href]="item.url" target="_blank" rel="noopener">Abrir enlace</a></p>
          }
          @if (item.textContent) {
            <p>{{ item.textContent }}</p>
          }
        </article>
      }
    }

    <h2>Actividades</h2>
    @if (!activities().length) {
      <ui-empty title="Sin actividades" message="" />
    } @else {
      @for (item of activities(); track item.id) {
        <article>
          <h3>{{ item.title }}</h3>
          <p>{{ item.description }}</p>
          <p class="meta">{{ item.status }}</p>
          @if (item.status === 'Available' || item.status === 'Expired') {
            <textarea [(ngModel)]="drafts[item.id]" [name]="'a'+item.id"></textarea>
            <ui-button type="button" (click)="submit(item.id)">Entregar</ui-button>
          }
          @if (item.myScore != null) {
            <p>Nota: {{ item.myScore }} · {{ item.teacherComment }}</p>
          }
        </article>
      }
    }
  `,
  styles: [`
    article { background: var(--color-surface); border: 1px solid var(--color-border);
      border-radius: var(--radius-lg); padding: 1rem; margin-bottom: 1rem; }
    textarea { width: 100%; min-height: 80px; margin: .5rem 0; font: inherit;
      padding: .5rem; border: 1px solid var(--color-border); border-radius: var(--radius-md); }
    .meta { color: var(--color-muted); }
    a { color: var(--color-primary); font-weight: 600; }
  `]
})
export class StudentGroupPage implements OnInit {
  private readonly api = inject(StudentApi);
  private readonly route = inject(ActivatedRoute);
  readonly error = signal<string | null>(null);
  readonly group = signal<GroupDto | null>(null);
  readonly announcements = signal<AnnouncementDto[]>([]);
  readonly materials = signal<MaterialRow[]>([]);
  readonly activities = signal<ActivityDto[]>([]);
  drafts: Record<number, string> = {};
  groupId = 0;

  ngOnInit(): void {
    this.groupId = Number(this.route.snapshot.paramMap.get('id'));
    this.api.groups().subscribe({
      next: (items) =>
        this.group.set(items.find((g) => g.id === this.groupId) ?? null)
    });
    this.api.announcements(this.groupId).subscribe({
      next: (items) => this.announcements.set(items),
      error: (err) => this.error.set(mapApiError(err))
    });
    this.api.materials(this.groupId).subscribe({
      next: (items) => this.materials.set(items),
      error: (err) => this.error.set(mapApiError(err))
    });
    this.api.activities(this.groupId).subscribe({
      next: (items) => this.activities.set(items),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  submit(activityId: number): void {
    const text = this.drafts[activityId] ?? '';
    this.api.submit(activityId, text).subscribe({
      next: () => this.ngOnInit(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
