import { Component, EventEmitter, Input, Output } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  NotificationDto,
  notificationRelativeTime,
  notificationTypeLabel
} from '../../core/notifications/notifications.api';
import { UiIconComponent } from './ui-icon.component';

@Component({
  selector: 'ui-dash-notifs',
  standalone: true,
  imports: [RouterLink, UiIconComponent],
  template: `
    <section class="dash-panel">
      <div class="dash-panel-head">
        <h2>Notificaciones</h2>
        <a routerLink="/notifications">Ver todas</a>
      </div>
      @if (!items.length) {
        <p class="dash-empty">No tienes notificaciones nuevas.</p>
      } @else {
        <ul class="dash-feed">
          @for (n of items; track n.id) {
            <li [class.unread]="!n.isRead">
              <button type="button" class="open" (click)="open.emit(n)">
                <span class="feed-icon" [attr.data-tone]="toneFor(n.type)" aria-hidden="true">
                  <ui-icon [name]="iconFor(n.type)" />
                </span>
                <span class="feed-body">
                  <strong>{{ n.title }}</strong>
                  <p class="meta">{{ n.message }}</p>
                  <time class="meta" [attr.datetime]="n.createdAt">{{ relative(n.createdAt) }}</time>
                </span>
              </button>
            </li>
          }
        </ul>
      }
    </section>
  `,
  styles: [`
    .open {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 0.75rem;
      width: 100%;
      text-align: left;
      border: 0;
      background: transparent;
      padding: 0;
      color: inherit;
      font: inherit;
      cursor: pointer;
      align-items: start;
    }
    .feed-icon ::ng-deep svg {
      width: 1rem;
      height: 1rem;
    }
  `]
})
export class UiDashNotifsComponent {
  @Input() items: NotificationDto[] = [];
  @Output() open = new EventEmitter<NotificationDto>();

  relative(iso: string): string {
    return notificationRelativeTime(iso);
  }

  typeLabel(type: string): string {
    return notificationTypeLabel(type);
  }

  iconFor(type: string): string {
    switch (type) {
      case 'exam':
      case 'exam_result':
        return 'exam';
      case 'membership':
        return 'bank';
      case 'grade':
      case 'submission':
        return 'chart';
      case 'activity':
        return 'clock';
      default:
        return 'bell';
    }
  }

  toneFor(type: string): string {
    switch (type) {
      case 'membership':
        return 'warning';
      case 'exam_result':
      case 'grade':
        return 'success';
      default:
        return 'primary';
    }
  }
}
