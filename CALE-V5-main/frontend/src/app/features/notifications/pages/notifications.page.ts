import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  NotificationDto,
  NotificationPreferenceDto,
  NotificationsApi,
  notificationRelativeTime,
  notificationTypeLabel
} from '../../../core/notifications/notifications.api';
import { pollWhileVisible } from '../../../core/rxjs/poll-while-visible';
import { SessionStore } from '../../../core/auth/session.store';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';

type FilterKey = 'all' | 'unread' | 'academic' | 'admin' | 'membership' | 'system';

@Component({
  selector: 'app-notifications-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent
  ],
  templateUrl: './notifications.page.html',
  styleUrl: './notifications.page.css'
})
export class NotificationsPage implements OnInit {
  private readonly api = inject(NotificationsApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly session = inject(SessionStore);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<NotificationDto[]>([]);
  readonly unread = signal(0);
  readonly filter = signal<FilterKey>('all');
  readonly prefs = signal<NotificationPreferenceDto | null>(null);
  readonly prefsSaved = signal(false);

  readonly filters: { key: FilterKey; label: string }[] = [
    { key: 'all', label: 'Todas' },
    { key: 'unread', label: 'No leídas' },
    { key: 'academic', label: 'Académicas' },
    { key: 'membership', label: 'Membresía' },
    { key: 'admin', label: 'Administrativas' },
    { key: 'system', label: 'Sistema' }
  ];

  ngOnInit(): void {
    this.reload();
    this.api.preferences().subscribe({
      next: (p) => this.prefs.set(p)
    });
    pollWhileVisible(45000, () => this.fetchList(false), { leading: false })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe();
  }

  reload(showLoading = true): void {
    this.fetchList(showLoading).subscribe();
  }

  private fetchList(showLoading: boolean) {
    if (showLoading) {
      this.loading.set(true);
    }
    this.error.set(null);
    const f = this.filter();
    return this.api
      .list({
        unreadOnly: f === 'unread' ? true : undefined,
        category: f === 'all' || f === 'unread' ? undefined : f,
        take: 50
      })
      .pipe(
        tap((res) => {
          this.items.set(res.items);
          this.unread.set(res.unreadCount);
          this.loading.set(false);
        }),
        catchError((err) => {
          this.loading.set(false);
          this.error.set(mapApiError(err));
          return of(null);
        })
      );
  }

  setFilter(key: FilterKey): void {
    this.filter.set(key);
    this.reload();
  }

  relative(iso: string): string {
    return notificationRelativeTime(iso);
  }

  typeLabel(type: string): string {
    return notificationTypeLabel(type);
  }

  open(n: NotificationDto): void {
    const go = () => {
      const link = n.link?.trim();
      if (link) {
        void this.router.navigateByUrl(link);
      }
    };
    if (n.isRead) {
      go();
      return;
    }
    this.api.markRead(n.id).subscribe({
      next: () => {
        this.items.update((list) =>
          list.map((x) => (x.id === n.id ? { ...x, isRead: true } : x))
        );
        this.unread.update((c) => Math.max(0, c - 1));
        go();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  markRead(id: number, event?: Event): void {
    event?.stopPropagation();
    this.api.markRead(id).subscribe({
      next: () => this.reload(false),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  markAll(): void {
    this.api.markAllRead().subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  archive(id: number, event: Event): void {
    event.stopPropagation();
    this.api.archive(id).subscribe({
      next: () => this.reload(false),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  savePrefs(): void {
    const p = this.prefs();
    if (!p) {
      return;
    }
    this.api.updatePreferences(p).subscribe({
      next: (saved) => {
        this.prefs.set(saved);
        this.prefsSaved.set(true);
        setTimeout(() => this.prefsSaved.set(false), 2500);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  get isAdmin(): boolean {
    return this.session.user()?.role === 'Admin';
  }
}
