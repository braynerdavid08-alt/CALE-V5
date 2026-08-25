import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export interface GroupDto {
  id: number;
  name: string;
  code: string;
  teacherName?: string | null;
  description?: string | null;
  memberCount: number;
  isActive: boolean;
}

export interface ActivityDto {
  id: number;
  groupId: number;
  type: string;
  title: string;
  description: string;
  instructions?: string | null;
  dueAt?: string | null;
  maxScore?: number | null;
  status: string;
  myScore?: number | null;
  teacherComment?: string | null;
}

export interface AnnouncementDto {
  id: number;
  title: string;
  body: string;
  authorName: string;
  createdAt: string;
}

export interface StudentDashboardDto {
  name: string;
  groups: GroupDto[];
  pendingActivities: ActivityDto[];
  announcements: AnnouncementDto[];
  unreadNotifications: number;
  bestPercent?: number | null;
}

export interface NotificationDto {
  id: number;
  title: string;
  message: string;
  type: string;
  isRead: boolean;
  createdAt: string;
  groupId?: number | null;
}

@Injectable({ providedIn: 'root' })
export class StudentApi {
  private readonly http = inject(HttpClient);
  private readonly base = env.apiUrl;

  dashboard() {
    return this.http.get<StudentDashboardDto>(
      `${this.base}/api/student/dashboard`
    );
  }

  notifications() {
    return this.http.get<NotificationDto[]>(
      `${this.base}/api/notifications`
    );
  }

  markRead(id: number) {
    return this.http.post<void>(
      `${this.base}/api/notifications/${id}/read`,
      {}
    );
  }

  joinGroup(code: string) {
    return this.http.post<GroupDto>(`${this.base}/api/groups/join`, { code });
  }

  groups() {
    return this.http.get<GroupDto[]>(`${this.base}/api/groups`);
  }

  announcements(groupId: number) {
    return this.http.get<AnnouncementDto[]>(
      `${this.base}/api/classroom/${groupId}/announcements`
    );
  }

  materials(groupId: number) {
    return this.http.get<Array<{
      id: number;
      title: string;
      description?: string | null;
      url?: string | null;
      textContent?: string | null;
    }>>(
      `${this.base}/api/classroom/${groupId}/materials`
    );
  }

  activities(groupId: number) {
    return this.http.get<ActivityDto[]>(
      `${this.base}/api/classroom/${groupId}/activities`
    );
  }

  submit(activityId: number, text: string) {
    return this.http.post<void>(
      `${this.base}/api/classroom/activities/${activityId}/submit`,
      { text }
    );
  }
}
