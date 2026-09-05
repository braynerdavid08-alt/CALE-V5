import {
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet
} from '@angular/router';
import { filter } from 'rxjs';
import {
  NotificationDto,
  NotificationsApi,
  notificationRelativeTime,
  notificationTypeLabel
} from '../core/notifications/notifications.api';
import { SessionStore } from '../core/auth/session.store';
import { BRAND } from '../core/brand';
import { AuthFacade } from '../features/auth/application/auth.facade';
import { UiBadgeComponent } from '../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../shared/ui/ui-button.component';
import { UiIconComponent } from '../shared/ui/ui-icon.component';
import { UiMotivationComponent } from '../shared/ui/ui-motivation.component';
import { UiThemeToggleComponent } from '../shared/ui/ui-theme-toggle.component';
import {
  NavChild,
  NavItem,
  TEACHER_LIBRARY_NAV,
  isTeacherLibraryPath,
  navChildActive,
  navForRole
} from './nav.config';

const SIDEBAR_COLLAPSED_KEY = 'cale.sidebar.collapsed';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    FormsModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    UiBadgeComponent,
    UiButtonComponent,
    UiIconComponent,
    UiMotivationComponent,
    UiThemeToggleComponent
  ],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.css'
})
export class AppShellComponent implements OnInit, OnDestroy {
  readonly brand = BRAND;
  readonly session = inject(SessionStore);
  private readonly auth = inject(AuthFacade);
  private readonly router = inject(Router);
  private readonly notificationsApi = inject(NotificationsApi);
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly menuOpen = signal(false);
  readonly panelOpen = signal(false);
  readonly unread = signal(0);
  readonly recent = signal<NotificationDto[]>([]);
  readonly panelLoading = signal(false);
  readonly url = signal(this.router.url);
  readonly sidebarCollapsed = signal(this.readCollapsedPref());
  readonly openGroups = signal<Record<string, boolean>>({});
  headerSearch = '';

  private poll?: ReturnType<typeof setInterval>;

  get role(): string | undefined {
    return this.session.user()?.role;
  }

  get mustChangePassword(): boolean {
    return !!this.session.user()?.mustChangePassword;
  }

  get isTeacherStudio(): boolean {
    return this.role === 'Teacher';
  }

  get items() {
    const user = this.session.user();
    return navForRole(this.role, {
      hasSchool: !!user?.schoolId || user?.role === 'School'
    });
  }

  get libraryItems() {
    return TEACHER_LIBRARY_NAV;
  }

  get showLibraryPanel(): boolean {
    return this.isTeacherStudio && isTeacherLibraryPath(this.url());
  }

  get showSidePanel(): boolean {
    return this.showLibraryPanel;
  }

  get sidePanelTitle(): string {
    return 'Biblioteca';
  }

  get sidePanelFoot(): string {
    return 'Solo contenido de tu escuela';
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
    this.syncOpenGroups(this.router.url);
    this.poll = setInterval(() => this.refreshUnread(), 30000);
    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe((e) => {
        this.menuOpen.set(false);
        this.panelOpen.set(false);
        const nextUrl = (e as NavigationEnd).urlAfterRedirects;
        this.url.set(nextUrl);
        this.syncOpenGroups(nextUrl);
        this.refreshUnread();
      });
  }

  ngOnDestroy(): void {
    if (this.poll) {
      clearInterval(this.poll);
    }
  }

  isNavOn(item: NavItem): boolean {
    const path = this.url().split('?')[0];
    if (item.hub === 'library') {
      return isTeacherLibraryPath(path);
    }
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

  goCreate(): void {
    void this.router.navigateByUrl('/teacher/library?crear=1');
  }

  onHeaderSearch(): void {
    if (!this.isTeacherStudio) {
      return;
    }
    const q = this.headerSearch.trim();
    void this.router.navigate(['/teacher/library'], {
      queryParams: q ? { q } : {}
    });
  }

  refreshUnread(): void {
    this.notificationsApi.unreadCount().subscribe({
      next: (count) => this.unread.set(count),
      error: () => this.unread.set(0)
    });
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
