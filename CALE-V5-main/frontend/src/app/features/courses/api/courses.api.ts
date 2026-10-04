import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';
import { Badge } from '../../play/api/play.api';

// ── Lesson content blocks ──────────────────────────────────────────────

export interface TextBlock { type: 'text'; title?: string | null; body: string; }
export interface TipBlock { type: 'tip'; body: string; }
export interface MediaBlock { type: 'image' | 'video' | 'audio'; url: string; caption?: string | null; }
export interface SignItem { code: string; name: string; note?: string | null; }
export interface SignsBlock { type: 'signs'; title?: string | null; items: SignItem[]; }
export interface QuizBlock {
  type: 'quiz';
  question: string;
  imageUrl?: string | null;
  options: string[];
  /** Only present in the editor; students never receive it. */
  correct?: number;
  explanation?: string | null;
}
export interface FlipCard { front: string; back: string; imageUrl?: string | null; }
export interface FlipcardsBlock { type: 'flipcards'; title?: string | null; cards: FlipCard[]; }
export interface MatchPair { left?: string | null; imageUrl?: string | null; right: string; }
export interface MatchBlock { type: 'match'; instructions?: string | null; pairs: MatchPair[]; }

export interface TrueFalseBlock {
  type: 'truefalse';
  statement: string;
  imageUrl?: string | null;
  /** Editor only. */
  answer?: boolean;
  explanation?: string | null;
}
export interface OrderBlock {
  type: 'order';
  instructions?: string | null;
  /** Editor: correct order. Student: shuffled. */
  steps: string[];
  explanation?: string | null;
}
export interface HotspotItem { x: number; y: number; label: string; note?: string | null; }
export interface HotspotBlock { type: 'hotspot'; instructions?: string | null; imageUrl: string; spots: HotspotItem[]; }
export interface ScenarioChoice { text: string; outcome?: string; best?: boolean; }
export interface ScenarioBlock { type: 'scenario'; situation: string; imageUrl?: string | null; choices: ScenarioChoice[]; }
export interface FillBlankBlock {
  type: 'fillblank';
  /** Editor only: sentence with [[answer]] or [[answer|alternative]]. */
  text?: string;
  distractors?: string[];
  explanation?: string | null;
  /** Student only: text around the blanks and the word bank. */
  parts?: string[];
  bank?: string[];
}
export interface ClassifyItem { text?: string | null; imageUrl?: string | null; group?: number; }
export interface ClassifyBlock {
  type: 'classify';
  instructions?: string | null;
  groups: string[];
  items: ClassifyItem[];
  explanation?: string | null;
}

export type LessonBlock =
  | TextBlock
  | TipBlock
  | MediaBlock
  | SignsBlock
  | QuizBlock
  | FlipcardsBlock
  | MatchBlock
  | TrueFalseBlock
  | OrderBlock
  | HotspotBlock
  | ScenarioBlock
  | FillBlankBlock
  | ClassifyBlock;

/** Answer sent for a graded activity: option index, or a list for order, fill-in and classify. */
export type ActivityAnswer = number | string[] | number[];

export type BlockType = LessonBlock['type'];

// ── Editor ─────────────────────────────────────────────────────────────

export interface CourseManageItem {
  id: number;
  title: string;
  description?: string | null;
  category: string;
  coverUrl?: string | null;
  isPublished: boolean;
  isPlatform: boolean;
  canEdit: boolean;
  lessonCount: number;
  updatedAt: string;
}

export interface LessonManage {
  id: number;
  position: number;
  title: string;
  summary?: string | null;
  estimatedMinutes: number;
  content: LessonBlock[];
}

export interface CourseManageDetail {
  id: number;
  title: string;
  description?: string | null;
  category: string;
  coverUrl?: string | null;
  isPublished: boolean;
  isPlatform: boolean;
  canEdit: boolean;
  lessons: LessonManage[];
  categories: string[];
}

export interface CourseSaveRequest {
  title: string;
  description?: string | null;
  category?: string | null;
  coverUrl?: string | null;
  isPublished: boolean;
}

export interface LessonSaveRequest {
  title: string;
  summary?: string | null;
  estimatedMinutes: number;
  content: LessonBlock[];
}

export interface CourseStudentProgress {
  studentUserId: number;
  studentName: string;
  completedLessons: number;
  totalLessons: number;
  percent: number;
  averageScore: number;
  lastActivityAt?: string | null;
}

export interface CourseProgressReport {
  courseId: number;
  title: string;
  totalLessons: number;
  students: CourseStudentProgress[];
}

export interface CatalogSign {
  code: string;
  family: string;
  name: string;
  imageUrl: string;
}

// ── Student ────────────────────────────────────────────────────────────

export interface StudentCourseItem {
  id: number;
  title: string;
  description?: string | null;
  category: string;
  coverUrl?: string | null;
  isPlatform: boolean;
  totalLessons: number;
  completedLessons: number;
  percent: number;
  nextLessonId?: number | null;
}

export interface StudentLessonItem {
  id: number;
  position: number;
  title: string;
  summary?: string | null;
  estimatedMinutes: number;
  completed: boolean;
  score?: number | null;
}

export interface StudentCourseDetail {
  id: number;
  title: string;
  description?: string | null;
  category: string;
  coverUrl?: string | null;
  totalLessons: number;
  completedLessons: number;
  percent: number;
  lessons: StudentLessonItem[];
}

export interface StudentLesson {
  id: number;
  courseId: number;
  courseTitle: string;
  position: number;
  totalLessons: number;
  title: string;
  summary?: string | null;
  estimatedMinutes: number;
  content: LessonBlock[];
  quizCount: number;
  completed: boolean;
  bestScore?: number | null;
  previousLessonId?: number | null;
  nextLessonId?: number | null;
}

export interface QuizCheckResult {
  correct: boolean;
  correctIndex: number;
  explanation?: string | null;
  /** Order: correct steps. Fill-in: answers. Classify: group per item. Scenario: outcome per choice. */
  solution?: (string | number)[] | null;
}

export interface LessonCompleteResult {
  score: number;
  bestScore: number;
  completedLessons: number;
  totalLessons: number;
  courseCompleted: boolean;
  firstCompletion: boolean;
  nextLessonId?: number | null;
}

export interface LessonCompleteResponse {
  result: LessonCompleteResult;
  newBadges: Badge[];
}

export interface CurriculumLessonPlan {
  key: string | null;
  title: string;
  action: 'create' | 'update' | 'unchanged' | 'remove' | 'keep-edited' | 'keep-unknown';
  detail: string | null;
  positionBefore: number | null;
  positionAfter: number | null;
  progress: number;
  manualQuestions: string[];
  questionsLikeSeed: number;
  seedQuestionsMissing: number;
}

export interface CurriculumCoursePlan {
  slug: string;
  title: string;
  action: 'create' | 'update' | 'unchanged' | 'skip-inactive';
  lessonsBefore: number;
  lessonsAfter: number;
  hasEditedLessons: boolean;
  lessons: CurriculumLessonPlan[];
}

export interface CurriculumPlan {
  applied: boolean;
  progressReset: boolean;
  lessonsBefore: number;
  lessonsAfter: number;
  seedLessons: number;
  progressRows: number;
  progressRowsRemoved: number;
  schoolCoursesUntouched: number;
  courses: CurriculumCoursePlan[];
}

@Injectable({ providedIn: 'root' })
export class CurriculumAdminApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/admin/curriculum`;

  plan() {
    return this.http.get<CurriculumPlan>(`${this.base}/plan`);
  }

  apply(resetProgress: boolean) {
    return this.http.post<CurriculumPlan>(`${this.base}/apply`, { resetProgress });
  }
}

@Injectable({ providedIn: 'root' })
export class CoursesManageApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/courses`;

  list() {
    return this.http.get<CourseManageItem[]>(this.base);
  }

  get(id: number) {
    return this.http.get<CourseManageDetail>(`${this.base}/${id}`);
  }

  create(body: CourseSaveRequest) {
    return this.http.post<CourseManageDetail>(this.base, body);
  }

  update(id: number, body: CourseSaveRequest) {
    return this.http.put<void>(`${this.base}/${id}`, body);
  }

  remove(id: number) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  duplicate(id: number) {
    return this.http.post<CourseManageDetail>(`${this.base}/${id}/duplicate`, {});
  }

  addLesson(courseId: number, title: string) {
    return this.http.post<LessonManage>(`${this.base}/${courseId}/lessons`, { title });
  }

  reorder(courseId: number, lessonIds: number[]) {
    return this.http.put<void>(`${this.base}/${courseId}/lessons/order`, { lessonIds });
  }

  updateLesson(lessonId: number, body: LessonSaveRequest) {
    return this.http.put<LessonManage>(`${this.base}/lessons/${lessonId}`, body);
  }

  removeLesson(lessonId: number) {
    return this.http.delete<void>(`${this.base}/lessons/${lessonId}`);
  }

  progress(courseId: number) {
    return this.http.get<CourseProgressReport>(`${this.base}/${courseId}/progress`);
  }

  signs() {
    return this.http.get<CatalogSign[]>(`${this.base}/signs`);
  }

  upload(file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<{ url: string; mediaType: 'image' | 'video' | 'audio' }>(`${this.base}/upload`, form);
  }
}

@Injectable({ providedIn: 'root' })
export class StudentCoursesApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/student/courses`;

  list() {
    return this.http.get<StudentCourseItem[]>(this.base);
  }

  get(id: number) {
    return this.http.get<StudentCourseDetail>(`${this.base}/${id}`);
  }

  lesson(lessonId: number) {
    return this.http.get<StudentLesson>(`${this.base}/lessons/${lessonId}`);
  }

  check(lessonId: number, blockIndex: number, answer: ActivityAnswer) {
    const body = typeof answer === 'number'
      ? { blockIndex, option: answer }
      : { blockIndex, option: -1, answer };
    return this.http.post<QuizCheckResult>(`${this.base}/lessons/${lessonId}/check`, body);
  }

  complete(lessonId: number, answers: Record<number, ActivityAnswer>) {
    return this.http.post<LessonCompleteResponse>(`${this.base}/lessons/${lessonId}/complete`, { answers });
  }
}
