import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export interface ApprenticeDto {
  id: number;
  studentUserId: number;
  studentName: string;
  studentEmail?: string | null;
  documentType?: string | null;
  documentNumber?: string | null;
  phone?: string | null;
  address?: string | null;
  contactEmail?: string | null;
  enrollmentMonth?: string | null;
  enrollmentDate?: string | null;
  orderNumber?: number | null;
  licenseCategories?: string | null;
  attendanceDayType?: string | null;
  scheduleSlot?: string | null;
  receiptNumber?: string | null;
  amountDue: number;
  amountPaid: number;
  balanceDue: number;
  paymentMethod?: string | null;
  balancePaymentAmount?: number | null;
  accountsReceivable: number;
  balancePaymentDate?: string | null;
  balancePaymentMethod?: string | null;
  balanceReceiptNumber?: string | null;
  enrollmentPin?: string | null;
  runtRegistered: boolean;
  isEnrolled: boolean;
  enrollmentStatus: string;
  theoryExamAuthorized: boolean;
  practicalAuthorized: boolean;
  notes?: string | null;
}

export interface ExcelImportRowPreview {
  lineNumber: number;
  label: string;
  action: string;
  severity: string;
  message?: string | null;
}

export interface ExcelImportPreview {
  previewId: string;
  fileName: string;
  importType: string;
  totalRows: number;
  createCount: number;
  updateCount: number;
  skipCount: number;
  errorCount: number;
  canCommit: boolean;
  blockingReason?: string | null;
  rows: ExcelImportRowPreview[];
}

export interface ExcelImportCommitResult {
  previewId: string;
  created: number;
  updated: number;
  skipped: number;
  failed: number;
  credentials: { name: string; email: string; temporaryPassword: string }[];
  results: ExcelImportRowPreview[];
  credentialsCsv: string;
}

export interface ApprenticePracticalSummary {
  completedLessons: number;
  requiredLessons: number;
  scheduledLessons: number;
  nextLessonDate?: string | null;
  nextLessonTime?: string | null;
}

export interface ApprenticeExamSummary {
  id: number;
  examDate: string;
  slotTime: string;
}

export interface PracticalEligibility {
  canBookPractical: boolean;
  theoryExamPassed: boolean;
  theoryHoursComplete: boolean;
  workshopHoursComplete: boolean;
  theoryHoursCompleted: number;
  theoryHoursRequired: number;
  workshopHoursCompleted: number;
  workshopHoursRequired: number;
  theoryExamAuthorized: boolean;
  practicalAuthorized: boolean;
  blockReason?: string | null;
}

export interface ApprenticeDetail {
  profile: ApprenticeDto;
  training: PracticalEligibility;
  practical: ApprenticePracticalSummary;
  nextExam?: ApprenticeExamSummary | null;
}

export interface SchoolDashboardBalanceRow {
  studentUserId: number;
  studentName: string;
  balanceDue: number;
}

export interface SchoolOperationsDashboard {
  apprenticeCount: number;
  balancePendingCount: number;
  balancePendingTotal: number;
  examsNext7Days: number;
  pendingEnrollmentCount: number;
  topBalanceDue: SchoolDashboardBalanceRow[];
  upcomingExams: TheoryExamSlotDto[];
}

export interface TheoryExamSchedulingStudentDto {
  studentUserId: number;
  studentName: string;
  licenseCategories?: string | null;
}

export interface TheoryExamSlotDto {
  id: number;
  examDate: string;
  slotTime: string;
  studentUserId?: number | null;
  studentLabel?: string | null;
  studentName?: string | null;
  notes?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ApprenticeApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/school`;

  list(search?: string, month?: string, withBalance?: boolean) {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (month) params = params.set('month', month);
    if (withBalance) params = params.set('withBalance', 'true');
    return this.http.get<ApprenticeDto[]>(`${this.base}/apprentices`, { params });
  }

  getDetail(studentUserId: number) {
    return this.http.get<ApprenticeDetail>(`${this.base}/apprentices/${studentUserId}`);
  }

  getDashboard() {
    return this.http.get<SchoolOperationsDashboard>(`${this.base}/dashboard`);
  }

  update(studentUserId: number, body: Partial<ApprenticeDto>) {
    return this.http.put<ApprenticeDto>(`${this.base}/apprentices/${studentUserId}`, body);
  }

  previewExcel(importType: string, file: File) {
    const form = new FormData();
    form.append('importType', importType);
    form.append('file', file);
    return this.http.post<ExcelImportPreview>(`${this.base}/imports/excel/preview`, form);
  }

  commitExcel(previewId: string) {
    return this.http.post<ExcelImportCommitResult>(
      `${this.base}/imports/excel/${previewId}/commit`,
      {}
    );
  }

  listExamSlots(from?: string, to?: string) {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<TheoryExamSlotDto[]>(`${this.base}/theory-exams/schedule`, { params });
  }

  listTheoryExamStudents() {
    return this.http.get<TheoryExamSchedulingStudentDto[]>(`${this.base}/theory-exams/students`);
  }

  saveExamSlot(body: {
    examDate: string;
    slotTime: string;
    studentUserId?: number | null;
    studentLabel?: string | null;
    notes?: string | null;
  }, id?: number) {
    return id
      ? this.http.put<TheoryExamSlotDto>(`${this.base}/theory-exams/schedule/${id}`, body)
      : this.http.post<TheoryExamSlotDto>(`${this.base}/theory-exams/schedule`, body);
  }

  deleteExamSlot(id: number) {
    return this.http.delete(`${this.base}/theory-exams/schedule/${id}`);
  }
}
