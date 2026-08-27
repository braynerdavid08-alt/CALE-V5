import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  BroadcastNotificationRequest,
  NotificationsApi
} from '../../../core/notifications/notifications.api';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';

@Component({
  selector: 'app-admin-notifications-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent
  ],
  templateUrl: './admin-notifications.page.html',
  styleUrl: './admin-notifications.page.css'
})
export class AdminNotificationsPage implements OnInit {
  private readonly api = inject(NotificationsApi);

  title = '';
  message = '';
  type = 'admin';
  priority = 'normal';
  audience = 'all_students';
  groupId: number | null = null;
  userIdsText = '';
  link = '';
  step: 'form' | 'preview' | 'done' = 'form';
  readonly error = signal<string | null>(null);
  readonly sent = signal<number | null>(null);
  readonly sending = signal(false);

  ngOnInit(): void {
    /* form only */
  }

  get draft(): BroadcastNotificationRequest {
    const userIds =
      this.audience === 'users'
        ? this.userIdsText
            .split(/[,\s]+/)
            .map((x) => Number(x.trim()))
            .filter((n) => Number.isFinite(n) && n > 0)
        : null;
    return {
      title: this.title.trim(),
      message: this.message.trim(),
      type: this.type,
      priority: this.priority,
      link: this.link.trim() || null,
      audience: this.audience,
      groupId: this.audience === 'group' ? this.groupId : null,
      userIds
    };
  }

  get canPreview(): boolean {
    return this.title.trim().length > 0 && this.message.trim().length > 0;
  }

  goPreview(): void {
    this.error.set(null);
    if (!this.canPreview) {
      this.error.set('Completa título y mensaje.');
      return;
    }
    if (this.audience === 'group' && !(this.groupId && this.groupId > 0)) {
      this.error.set('Indica el ID del grupo.');
      return;
    }
    if (this.audience === 'users' && !(this.draft.userIds?.length)) {
      this.error.set('Indica al menos un ID de usuario.');
      return;
    }
    this.step = 'preview';
  }

  back(): void {
    this.step = 'form';
  }

  send(): void {
    this.sending.set(true);
    this.error.set(null);
    this.api.broadcast(this.draft).subscribe({
      next: (res) => {
        this.sending.set(false);
        this.sent.set(res.sent);
        this.step = 'done';
      },
      error: (err) => {
        this.sending.set(false);
        this.error.set(mapApiError(err));
        this.step = 'form';
      }
    });
  }

  reset(): void {
    this.title = '';
    this.message = '';
    this.link = '';
    this.userIdsText = '';
    this.groupId = null;
    this.sent.set(null);
    this.step = 'form';
  }
}
