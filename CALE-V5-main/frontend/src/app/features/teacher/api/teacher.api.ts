import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';
import { ExamDto } from '../../student/api/exam.api';
import { ActivityDto, AnnouncementDto, GroupDto } from '../../student/api/student.api';

export interface TeacherSchoolDto {
  schoolId: number;
  legalName: string;
  planLabel: string;
  city: string;
  department: string;
  subscriptionStatus: string;
  daysRemaining: number;
  isMembershipActive: boolean;
}

export interface TeacherDashboardDto {
  teacherName: string;
  groups: GroupDto[];
  pendingGrades: Array<{
    id: number;
    activityId: number;
    groupId: number;
    userId: number;
    userName: string;
    status: string;
    submittedAt: string;
    text?: string | null;
  }>;
  lowScores: Array<{
    userId: number;
    userName: string;
    percent: number;
    passed: boolean;
  }>;
  school?: TeacherSchoolDto | null;
  activeStudents: number;
  publishedExams: number;
  totalExams: number;
}

export interface QuestionListDto {
  id: number;
  text: string;
  type: string;
  bankId: number;
  bankName: string;
  topic?: string | null;
  isActive: boolean;
}

export interface PagedQuestions {
  items: QuestionListDto[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface QuestionDetailDto {
  id: number;
  text: string;
  type: string;
  bankId: number;
  blockId: number;
  topic?: string | null;
  imageUrl?: string | null;
  explanation?: string | null;
  isActive: boolean;
  options: Array<{
    id: number;
    text: string;
    isCorrect: boolean;
    imageUrl?: string | null;
  }>;
}

export interface MemberDto {
  userId: number;
  name: string;
  email: string;
  status: string;
}

export interface MaterialDto {
  id: number;
  module: string;
  title: string;
  description?: string | null;
  type: string;
  url?: string | null;
  textContent?: string | null;
}

export interface SubmissionDto {
  id: number;
  activityId: number;
  groupId: number;
  userId: number;
  userName: string;
  text?: string | null;
  fileUrl?: string | null;
  submittedAt: string;
  score?: number | null;
  teacherComment?: string | null;
  status: string;
}

export interface BankThemeDto {
  name: string;
  questionCount: number;
}

export interface BankAdminDto {
  id: number;
  name: string;
  description?: string | null;
  isActive: boolean;
  questionCount: number;
  themeLabel?: string | null;
  themes?: BankThemeDto[] | null;
}

@Injectable({ providedIn: 'root' })
export class TeacherApi {
  private readonly http = inject(HttpClient);
  private readonly base = env.apiUrl;

  dashboard() {
    return this.http.get<TeacherDashboardDto>(
      `${this.base}/api/teacher/dashboard`
    );
  }

  groups() {
    return this.http.get<GroupDto[]>(`${this.base}/api/groups`);
  }

  group(id: number) {
    return this.http.get<GroupDto>(`${this.base}/api/groups/${id}`);
  }

  members(groupId: number) {
    return this.http.get<MemberDto[]>(
      `${this.base}/api/groups/${groupId}/members`
    );
  }

  createGroup(name: string, description?: string) {
    return this.http.post<GroupDto>(`${this.base}/api/groups`, {
      name,
      description,
      isActive: true
    });
  }

  addMember(groupId: number, email: string) {
    return this.http.post<void>(
      `${this.base}/api/groups/${groupId}/members`,
      { email }
    );
  }

  removeMember(groupId: number, userId: number) {
    return this.http.delete<void>(
      `${this.base}/api/groups/${groupId}/members/${userId}`
    );
  }

  announcements(groupId: number) {
    return this.http.get<AnnouncementDto[]>(
      `${this.base}/api/classroom/${groupId}/announcements`
    );
  }

  materials(groupId: number) {
    return this.http.get<MaterialDto[]>(
      `${this.base}/api/classroom/${groupId}/materials`
    );
  }

  activities(groupId: number) {
    return this.http.get<ActivityDto[]>(
      `${this.base}/api/classroom/${groupId}/activities`
    );
  }

  submissions(activityId: number) {
    return this.http.get<SubmissionDto[]>(
      `${this.base}/api/classroom/activities/${activityId}/submissions`
    );
  }

  grade(activityId: number, studentId: number, score: number, comment?: string) {
    return this.http.post<void>(
      `${this.base}/api/classroom/activities/${activityId}/submissions/${studentId}/grade`,
      { score, comment }
    );
  }

  questions(page = 1) {
    return this.http.get<PagedQuestions>(
      `${this.base}/api/questions?page=${page}&pageSize=20`
    );
  }

  question(id: number) {
    return this.http.get<QuestionDetailDto>(
      `${this.base}/api/questions/${id}`
    );
  }

  blocks() {
    return this.http.get<Array<{ id: number; name: string }>>(
      `${this.base}/api/questions/blocks`
    );
  }

  saveQuestion(body: unknown, id?: number) {
    return id
      ? this.http.put<unknown>(`${this.base}/api/questions/${id}`, body)
      : this.http.post<unknown>(`${this.base}/api/questions`, body);
  }

  upload(file: File) {
    const data = new FormData();
    data.append('file', file);
    return this.http.post<{ url: string }>(
      `${this.base}/api/media/upload`,
      data
    );
  }

  publishAnnouncement(groupId: number, title: string, body: string) {
    return this.http.post<void>(
      `${this.base}/api/classroom/${groupId}/announcements`,
      { title, body }
    );
  }

  publishMaterial(
    groupId: number,
    title: string,
    module: string,
    type: string,
    url?: string,
    textContent?: string
  ) {
    return this.http.post<void>(
      `${this.base}/api/classroom/${groupId}/materials`,
      { title, module, type, url, textContent }
    );
  }

  publishActivity(
    groupId: number,
    title: string,
    description: string,
    type: string,
    dueAt?: string | null,
    maxScore?: number | null
  ) {
    return this.http.post<void>(
      `${this.base}/api/classroom/${groupId}/activities`,
      { title, description, type, dueAt, maxScore }
    );
  }

  exams() {
    return this.http.get<ExamDto[]>(`${this.base}/api/exams`);
  }

  createExam(body: {
    name: string;
    description?: string | null;
    bankId?: number | null;
    questionCount: number;
    timeMinutes: number;
    allowedAttempts: number;
    randomize: boolean;
    startsAt?: string | null;
    endsAt?: string | null;
  }) {
    return this.http.post<ExamDto>(`${this.base}/api/exams`, body);
  }

  publishExam(id: number, published: boolean) {
    return this.http.post<void>(
      `${this.base}/api/exams/${id}/publish?published=${published}`,
      {}
    );
  }

  assignExam(
    examId: number,
    groupId: number,
    startsAt?: string | null,
    endsAt?: string | null
  ) {
    return this.http.post<void>(`${this.base}/api/exams/${examId}/assign`, {
      groupId,
      startsAt,
      endsAt
    });
  }

  updateGroup(
    id: number,
    body: {
      name: string;
      description?: string | null;
      startsOn?: string | null;
      isActive: boolean;
    }
  ) {
    return this.http.put<GroupDto>(`${this.base}/api/groups/${id}`, body);
  }

  results() {
    return this.http.get<
      Array<{
        attemptId: number;
        userName: string;
        percent: number;
        passed: boolean;
        mode: string;
      }>
    >(`${this.base}/api/teacher/results`);
  }

  banks(activeOnly = false, includeThemes = false) {
    return this.http.get<BankAdminDto[]>(
      `${this.base}/api/banks?activeOnly=${activeOnly}&includeThemes=${includeThemes}`
    );
  }

  createBank(name: string, description?: string) {
    return this.http.post<BankAdminDto>(`${this.base}/api/banks`, {
      name,
      description,
      isActive: true
    });
  }

  updateBank(id: number, name: string, description: string | null, isActive: boolean) {
    return this.http.put<BankAdminDto>(`${this.base}/api/banks/${id}`, {
      name,
      description,
      isActive
    });
  }
}
