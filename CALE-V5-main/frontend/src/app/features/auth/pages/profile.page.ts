import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MeResponse } from '../../../core/auth/session.models';
import { SessionStore } from '../../../core/auth/session.store';
import { ThemeService } from '../../../core/theme/theme.service';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { roleLabel } from '../../../shared/utils/role-label';
import { AuthApi } from '../api/auth.api';
import { AuthFacade } from '../application/auth.facade';

type ProfileTab = 'account' | 'preferences' | 'security' | 'context';

@Component({
  selector: 'app-profile-page',
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  templateUrl: './profile.page.html',
  styleUrl: './profile.page.css'
})
export class ProfilePage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AuthApi);
  readonly session = inject(SessionStore);
  readonly auth = inject(AuthFacade);
  readonly theme = inject(ThemeService);
  readonly roleLabel = roleLabel;

  readonly loading = signal(true);
  readonly savingProfile = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly me = signal<MeResponse | null>(null);
  readonly tab = signal<ProfileTab>('account');

  readonly profileForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]]
  });

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  readonly role = computed(() => this.me()?.role || this.session.user()?.role || '');

  readonly homeLink = computed(() => this.session.homeRoute());

  readonly contextTitle = computed(() => {
    const role = this.role();
    if (role === 'School') return 'Tu institución';
    if (role === 'Teacher' || role === 'Student') return 'Tu escuela';
    return 'Contexto';
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.me().subscribe({
      next: (dto) => {
        this.me.set(dto);
        this.profileForm.patchValue({ name: dto.name, email: dto.email });
        this.session.patchUser({
          id: dto.id,
          name: dto.name,
          email: dto.email,
          role: dto.role,
          mustChangePassword: !!dto.mustChangePassword
        });
        if (dto.mustChangePassword) {
          this.tab.set('security');
        }
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  setTab(tab: ProfileTab): void {
    this.tab.set(tab);
    this.success.set(null);
    this.error.set(null);
    this.auth.error.set(null);
    this.auth.success.set(null);
  }

  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }
    this.savingProfile.set(true);
    this.error.set(null);
    this.success.set(null);
    const name = this.profileForm.controls.name.value.trim();
    const email = this.profileForm.controls.email.value.trim();
    this.api.updateMe(name, email).subscribe({
      next: (dto) => {
        this.me.set(dto);
        this.session.patchUser({ name: dto.name, email: dto.email });
        this.savingProfile.set(false);
        this.success.set('Datos de perfil actualizados.');
      },
      error: (err) => {
        this.savingProfile.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }
    const { currentPassword, newPassword } = this.passwordForm.getRawValue();
    this.auth.changePassword(currentPassword, newPassword);
    this.passwordForm.reset();
  }

  setTheme(mode: 'light' | 'dark'): void {
    this.theme.set(mode);
    this.success.set(`Tema cambiado a modo ${mode === 'dark' ? 'noche' : 'día'}.`);
  }

  statusLabel(status: string): string {
    if (status === 'Active') return 'Activo';
    if (status === 'Expiring') return 'Por vencer';
    if (status === 'None') return 'Sin membresía';
    if (status === 'PendingPayment') return 'Pago pendiente';
    if (status === 'UnderReview' || status === 'PaymentSubmitted') return 'En revisión';
    if (status === 'Rejected') return 'Rechazada';
    if (status === 'Cancelled') return 'Cancelada';
    if (status === 'Suspended') return 'Suspendida';
    if (status === 'Expired') return 'Vencido';
    return status;
  }

  statusTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' {
    if (status === 'Active') return 'success';
    if (status === 'Expiring' || status === 'PendingPayment') return 'warning';
    if (status === 'UnderReview' || status === 'PaymentSubmitted') return 'warning';
    if (status === 'Rejected' || status === 'Expired' || status === 'Suspended' || status === 'Cancelled') {
      return 'danger';
    }
    return 'neutral';
  }

  logout(): void {
    this.auth.logout();
  }
}
