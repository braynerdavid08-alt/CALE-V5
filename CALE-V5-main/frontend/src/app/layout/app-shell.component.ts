import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { SessionStore } from '../core/auth/session.store';
import { AuthFacade } from '../features/auth/application/auth.facade';
import { StudentApi } from '../features/student/api/student.api';
import { UiBadgeComponent } from '../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../shared/ui/ui-button.component';
import { UiIconComponent } from '../shared/ui/ui-icon.component';
import { UiMotivationComponent } from '../shared/ui/ui-motivation.component';
import { UiThemeToggleComponent } from '../shared/ui/ui-theme-toggle.component';
import { navForRole } from './nav.config';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
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
export class AppShellComponent implements OnInit {
  readonly session = inject(SessionStore);
  private readonly auth = inject(AuthFacade);
  private readonly router = inject(Router);
  private readonly notificationsApi = inject(StudentApi);

  readonly menuOpen = signal(false);
  readonly unread = signal(0);

  get role(): string | undefined {
    return this.session.user()?.role;
  }

  get items() {
    return navForRole(this.role);
  }

  ngOnInit(): void {
    this.refreshUnread();
    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe(() => this.menuOpen.set(false));
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
    this.notificationsApi.notifications().subscribe({
      next: (items) => this.unread.set(items.filter((n) => !n.isRead).length),
      error: () => this.unread.set(0)
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.menuOpen.set(false);
  }
}
