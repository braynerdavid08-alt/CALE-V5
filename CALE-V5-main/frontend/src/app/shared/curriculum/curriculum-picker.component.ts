import { HttpClient } from '@angular/common/http';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { env } from '../../core/config/env';

export interface CurriculumChoice {
  subject: string;
  topic: string;
  subtopic: string;
}

export interface CurriculumSubtopic {
  id: string;
  name: string;
  courseSlug?: string | null;
  courseTitle?: string | null;
  lessonTitle?: string | null;
}

interface CurriculumTheme {
  id: string;
  name: string;
  subtopics: CurriculumSubtopic[];
}

interface CurriculumNucleus {
  id: string;
  name: string;
  themes: CurriculumTheme[];
}

/** Keeps a previous free-text topic when the editor is saved without a new subtopic. */
export function curriculumToSave(current: CurriculumChoice, original: CurriculumChoice): CurriculumChoice {
  return current.subtopic.trim() ? current : original;
}

@Component({
  selector: 'app-curriculum-picker',
  standalone: true,
  imports: [FormsModule],
  styles: [':host { display: contents; }'],
  template: `
    <label class="field">Núcleo
      <select [ngModel]="nucleusId" (ngModelChange)="onNucleus($event)" name="curriculumNucleus">
        <option value="">Sin clasificar</option>
        @for (nucleus of nuclei(); track nucleus.id) {
          <option [value]="nucleus.id">{{ nucleus.name }}</option>
        }
      </select>
    </label>
    <label class="field">Tema
      <select [ngModel]="themeId" (ngModelChange)="onTheme($event)" name="curriculumTheme" [disabled]="!nucleusId">
        <option value="">Elige un tema</option>
        @for (theme of themes(); track theme.id) {
          <option [value]="theme.id">{{ theme.name }}</option>
        }
      </select>
    </label>
    <label class="field">Subtema
      <select [ngModel]="subtopicId" (ngModelChange)="onSubtopic($event)" name="curriculumSubtopic" [disabled]="!themeId">
        <option value="">Elige un subtema</option>
        @for (item of subtopics(); track item.id) {
          <option [value]="item.id">{{ item.name }}{{ item.lessonTitle ? '' : ' (sin lección aún)' }}</option>
        }
      </select>
    </label>
    @if (hint) {
      <p class="hint">{{ hint }}</p>
    }
    @if (legacy) {
      <p class="hint">Esta pregunta tenía el tema «{{ legacy }}», que no está en la malla. Si no eliges un subtema, se conserva.</p>
    }
  `
})
export class CurriculumPickerComponent implements OnChanges {
  private readonly http = inject(HttpClient);

  @Input() subject = '';
  @Input() topic = '';
  @Input() subtopic = '';
  @Output() readonly changed = new EventEmitter<CurriculumChoice>();

  readonly nuclei = signal<CurriculumNucleus[]>([]);
  nucleusId = '';
  themeId = '';
  subtopicId = '';
  legacy = '';
  private applied = '';

  ngOnChanges(): void {
    const key = `${this.subject}|${this.topic}|${this.subtopic}`;
    if (key === this.applied) {
      return;
    }
    this.applied = key;
    this.ensureLoaded();
    this.applyInputs();
  }

  themes(): CurriculumTheme[] {
    return this.nuclei().find((n) => n.id === this.nucleusId)?.themes ?? [];
  }

  subtopics(): CurriculumSubtopic[] {
    return this.themes().find((t) => t.id === this.themeId)?.subtopics ?? [];
  }

  get hint(): string {
    const item = this.subtopics().find((s) => s.id === this.subtopicId);
    if (!item) {
      return '';
    }
    if (!item.lessonTitle) {
      return 'Este subtema todavía no tiene una lección en los cursos.';
    }
    return `Se estudia en «${item.courseTitle}», lección «${item.lessonTitle}».`;
  }

  onNucleus(id: string): void {
    this.nucleusId = id;
    this.themeId = '';
    this.subtopicId = '';
    this.legacy = '';
    this.emitCleared();
  }

  onTheme(id: string): void {
    this.themeId = id;
    this.subtopicId = '';
    this.emitCleared();
  }

  onSubtopic(id: string): void {
    this.subtopicId = id;
    const nucleus = this.nuclei().find((n) => n.id === this.nucleusId);
    const theme = nucleus?.themes.find((t) => t.id === this.themeId);
    const item = theme?.subtopics.find((s) => s.id === id);
    if (!nucleus || !theme || !item) {
      this.emitCleared();
      return;
    }
    this.legacy = '';
    this.emit({ subject: nucleus.name, topic: theme.name, subtopic: item.name });
  }

  private emitCleared(): void {
    const nucleus = this.nuclei().find((n) => n.id === this.nucleusId);
    const theme = nucleus?.themes.find((t) => t.id === this.themeId);
    this.emit({
      subject: nucleus?.name ?? '',
      topic: theme?.name ?? '',
      subtopic: ''
    });
  }

  private emit(choice: CurriculumChoice): void {
    this.applied = `${choice.subject}|${choice.topic}|${choice.subtopic}`;
    this.changed.emit(choice);
  }

  private ensureLoaded(): void {
    if (this.nuclei().length > 0) {
      return;
    }
    this.http.get<CurriculumNucleus[]>(`${env.apiUrl}/api/questions/curriculum`).subscribe({
      next: (tree) => {
        this.nuclei.set(tree);
        this.applyInputs();
      }
    });
  }

  private applyInputs(): void {
    const tree = this.nuclei();
    if (tree.length === 0) {
      return;
    }
    const match = this.find(tree, this.subject, this.topic, this.subtopic);
    if (match) {
      this.nucleusId = match.nucleusId;
      this.themeId = match.themeId;
      this.subtopicId = match.subtopicId;
      this.legacy = '';
      return;
    }
    this.nucleusId = '';
    this.themeId = '';
    this.subtopicId = '';
    this.legacy = this.topic.trim();
  }

  private find(tree: CurriculumNucleus[], subject: string, topic: string, subtopic: string) {
    const same = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();
    for (const nucleus of tree) {
      if (subject && !same(nucleus.name, subject)) {
        continue;
      }
      for (const theme of nucleus.themes) {
        if (topic && !same(theme.name, topic)) {
          continue;
        }
        const item = theme.subtopics.find((s) => same(s.name, subtopic));
        if (item) {
          return { nucleusId: nucleus.id, themeId: theme.id, subtopicId: item.id };
        }
      }
    }
    return null;
  }
}
