import {
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  OnInit,
  effect,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterOutlet
} from '@angular/router';
import { of } from 'rxjs';
import { catchError, filter, tap } from 'rxjs/operators';
import {
  NotificationDto,
  NotificationsApi,
  notificationRelativeTime,
  notificationTypeLabel
} from '../core/notifications/notifications.api';
import { PushService } from '../core/notifications/push.service';
import { pollWhileVisible } from '../core/rxjs/poll-while-visible';
import { PlayFxService } from '../features/play/play-fx.service';
import { PushPromptComponent } from './push-prompt.component';
import { SessionStore } from '../core/auth/session.store';
import { BRAND } from '../core/brand';
import { AuthFacade } from '../features/auth/application/auth.facade';
import { UiBadgeComponent } from '../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../shared/ui/ui-button.component';
import { UiIconComponent } from '../shared/ui/ui-icon.component';
import { UiMotivationComponent } from '../shared/ui/ui-motivation.component';
import { UiThemeToggleComponent } from '../shared/ui/ui-theme-toggle.component';
import { RequestsApi } from '../features/requests/api/requests.api';
import {
  NavBadge,
  NavChild,
  NavItem,
  navChildActive,
  navForRole
} from './nav.config';

const SIDEBAR_COLLAPSED_KEY = 'cale.sidebar.collapsed';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    PushPromptComponent,
    UiBadgeComponent,
    UiButtonComponent,
    UiIconComponent,
    UiMotivationComponent,
    UiThemeToggleComponent
  ],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.css'
})
export class AppShellComponent implements OnInit {
  readonly brand = BRAND;
  readonly session = inject(SessionStore);
  private readonly auth = inject(AuthFacade);
  private readonly router = inject(Router);
  private readonly notificationsApi = inject(NotificationsApi);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly destroyRef = inject(DestroyRef);
  private readonly push = inject(PushService);
  private readonly fx = inject(PlayFxService);
  private readonly requestsApi = inject(RequestsApi);

  readonly pendingRequests = this.requestsApi.pendingCount;
  readonly menuOpen = signal(false);
  readonly panelOpen = signal(false);
  readonly unread = signal(0);
  readonly recent = signal<NotificationDto[]>([]);
  readonly panelLoading = signal(false);
  readonly url = signal(this.router.url);
  readonly sidebarCollapsed = signal(this.readCollapsedPref());
  readonly openGroups = signal<Record<string, boolean>>({});
  private unreadLoaded = false;

  constructor() {
    effect(() => syncAppBadge(this.unread()));
    this.destroyRef.onDestroy(() => syncAppBadge(0));
  }

  get role(): string | undefined {
    return this.session.user()?.role;
  }

  get mustChangePassword(): boolean {
    return !!this.session.user()?.mustChangePassword;
  }

  /** Students use the home launcher; sidebar duplicates it. */
  get isStudentShell(): boolean {
    return this.role === 'Student';
  }

  get showStudentHomeLink(): boolean {
    if (!this.isStudentShell || this.mustChangePassword) {
      return false;
    }
    const path = this.url().split('?')[0].replace(/\/$/, '') || '/';
    return path !== '/student';
  }

  get items() {
    const user = this.session.user();
    return navForRole(this.role, {
      hasSchool: !!user?.schoolId || user?.role === 'School',
      freeAccess: this.session.freeAccess()
    });
  }

  get initials(): string {
    const name = this.session.user()?.name?.trim() || 'U';
    const parts = name.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  ngOnInit(): void {
    this.refreshUnread();
    this.listenToServiceWorker();
    if (!this.mustChangePassword) {
      void this.push.syncIfGranted();
    }
    this.syncOpenGroups(this.router.url);
    pollWhileVisible(
      30000,
      () => this.fetchUnreadCount(),
      { leading: false }
    )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe();
    if (this.role === 'Admin') {
      pollWhileVisible(60000, () => this.fetchPendingRequests())
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe();
    }
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((e) => {
        this.menuOpen.set(false);
        this.panelOpen.set(false);
        const nextUrl = (e as NavigationEnd).urlAfterRedirects;
        this.url.set(nextUrl);
        this.syncOpenGroups(nextUrl);
        this.refreshUnread();
        if (this.role === 'Admin') {
          this.fetchPendingRequests().subscribe();
        }
      });
  }

  badgeCount(badge: NavBadge | undefined): number {
    return badge === 'pendingRequests' ? this.pendingRequests() : 0;
  }

  /** Sum of child counters, shown on the collapsed group header. */
  groupBadge(item: NavItem): number {
    return (item.children ?? []).reduce((sum, c) => sum + this.badgeCount(c.badge), 0);
  }

  private fetchPendingRequests() {
    return this.requestsApi.adminCounts().pipe(catchError(() => of(null)));
  }

  isNavOn(item: NavItem): boolean {
    const path = this.url().split('?')[0];
    if (item.children?.length) {
      return item.children.some((c) => this.isChildOn(c));
    }
    if (!item.path) {
      return false;
    }
    const pathOk = item.exact
      ? path === item.path
      : path === item.path || path.startsWith(item.path + '/');
    if (!pathOk) {
      return false;
    }
    if (item.queryParams) {
      return navChildActive(this.url(), {
        label: item.label,
        path: item.path,
        exact: item.exact,
        queryParams: item.queryParams
      });
    }
    return true;
  }

  isChildOn(child: NavChild): boolean {
    return navChildActive(this.url(), child);
  }

  isGroupOpen(item: NavItem): boolean {
    const map = this.openGroups();
    if (Object.prototype.hasOwnProperty.call(map, item.label)) {
      return !!map[item.label];
    }
    return this.isNavOn(item);
  }

  toggleGroup(item: NavItem, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (this.sidebarCollapsed()) {
      this.sidebarCollapsed.set(false);
      this.persistCollapsed(false);
      this.openGroups.update((m) => ({ ...m, [item.label]: true }));
      return;
    }
    const next = !this.isGroupOpen(item);
    this.openGroups.update((m) => ({ ...m, [item.label]: next }));
  }

  toggleSidebarCollapse(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const next = !this.sidebarCollapsed();
    this.sidebarCollapsed.set(next);
    this.persistCollapsed(next);
  }

  logout(): void {
    this.auth.logout();
  }

  toggleMenu(): void {
    this.menuOpen.update((v) => !v);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  refreshUnread(): void {
    this.fetchUnreadCount().subscribe();
  }

  private fetchUnreadCount() {
    if (this.mustChangePassword) {
      return of(0);
    }
    return this.notificationsApi.unreadCount().pipe(
      tap((count) => {
        if (this.unreadLoaded && count > this.unread()) {
          this.fx.play('notify');
        }
        this.unreadLoaded = true;
        this.unread.set(count);
      }),
      catchError(() => {
        this.unread.set(0);
        return of(0);
      })
    );
  }

  togglePanel(event: Event): void {
    event.stopPropagation();
    const next = !this.panelOpen();
    this.panelOpen.set(next);
    if (next) {
      this.loadRecent();
    }
  }

  loadRecent(): void {
    this.panelLoading.set(true);
    this.notificationsApi.list({ take: 8 }).subscribe({
      next: (res) => {
        this.recent.set(res.items);
        this.unread.set(res.unreadCount);
        this.panelLoading.set(false);
      },
      error: () => {
        this.recent.set([]);
        this.panelLoading.set(false);
      }
    });
  }

  relative(iso: string): string {
    return notificationRelativeTime(iso);
  }

  typeLabel(type: string): string {
    return notificationTypeLabel(type);
  }

  openNotification(n: NotificationDto): void {
    const navigate = () => {
      this.panelOpen.set(false);
      if (n.link) {
        void this.router.navigateByUrl(n.link);
      } else {
        void this.router.navigateByUrl('/notifications');
      }
    };
    if (n.isRead) {
      navigate();
      return;
    }
    this.notificationsApi.markRead(n.id).subscribe({
      next: () => {
        this.unread.update((c) => Math.max(0, c - 1));
        navigate();
      },
      error: () => navigate()
    });
  }

  markAllFromPanel(): void {
    this.notificationsApi.markAllRead().subscribe({
      next: () => {
        this.unread.set(0);
        this.loadRecent();
      }
    });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.panelOpen()) {
      return;
    }
    const root = this.host.nativeElement.querySelector('.notif');
    const target = event.target as Node | null;
    if (root && target && !root.contains(target)) {
      this.panelOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.menuOpen.set(false);
    this.panelOpen.set(false);
  }

  private listenToServiceWorker(): void {
    if (typeof navigator === 'undefined' || !('serviceWorker' in navigator)) {
      return;
    }
    const onMessage = (event: MessageEvent) => {
      const data = event.data as { type?: string; url?: string } | null;
      if (data?.type === 'cale-push') {
        this.refreshUnread();
      } else if (data?.type === 'cale-open' && data.url) {
        const target = new URL(data.url, window.location.origin);
        if (target.origin === window.location.origin) {
          void this.router.navigateByUrl(target.pathname + target.search + target.hash);
        }
      }
    };
    navigator.serviceWorker.addEventListener('message', onMessage);
    this.destroyRef.onDestroy(() =>
      navigator.serviceWorker.removeEventListener('message', onMessage)
    );
  }

  private syncOpenGroups(url: string): void {
    const next: Record<string, boolean> = { ...this.openGroups() };
    for (const item of this.items) {
      if (!item.children?.length) {
        continue;
      }
      if (item.children.some((c) => navChildActive(url, c))) {
        next[item.label] = true;
      }
    }
    this.openGroups.set(next);
  }

  private readCollapsedPref(): boolean {
    try {
      return localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === '1';
    } catch {
      return false;
    }
  }

  private persistCollapsed(value: boolean): void {
    try {
      localStorage.setItem(SIDEBAR_COLLAPSED_KEY, value ? '1' : '0');
    } catch {
      /* ignore */
    }
  }
}

function syncAppBadge(count: number): void {
  const nav = typeof navigator === 'undefined'
    ? null
    : (navigator as Navigator & {
        setAppBadge?: (n?: number) => Promise<void>;
        clearAppBadge?: () => Promise<void>;
      });
  if (!nav?.setAppBadge || !nav.clearAppBadge) {
    return;
  }
  const done = count > 0 ? nav.setAppBadge(count) : nav.clearAppBadge();
  done.catch(() => undefined);
}
