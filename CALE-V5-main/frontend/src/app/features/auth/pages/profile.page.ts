import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MeResponse } from '../../../core/auth/session.models';
import { SessionStore } from '../../../core/auth/session.store';
import { peekReturnUrl, takeReturnUrl } from '../../../core/auth/return-url';
import { ThemeService } from '../../../core/theme/theme.service';
import { env } from '../../../core/config/env';
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
import { PushSettingsComponent } from '../components/push-settings.component';
import { ProfilePhotoComponent } from '../components/profile-photo.component';
import {
  SchoolInfo,
  SchoolInfoFormComponent
} from '../../school/components/school-info-form.component';

type ProfileTab = 'account' | 'preferences' | 'security' | 'context';

interface SchoolJoinRequestDto {
  id: number;
  direction: 'Request' | 'Invite';
  memberUserId: number;
  memberName: string;
  memberEmail: string;
  memberRole: string;
  schoolUserId: number;
  schoolLegalName: string;
  schoolTaxId: string;
  status: string;
  message?: string | null;
  rejectionReason?: string | null;
  createdAt: string;
  decidedAt?: string | null;
}

@Component({
  selector: 'app-profile-page',
  standalone: true,
  imports: [
    DatePipe,
    ProfilePhotoComponent,
    PushSettingsComponent,
    ReactiveFormsModule,
    SchoolInfoFormComponent,
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
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly session = inject(SessionStore);
  readonly auth = inject(AuthFacade);
  readonly theme = inject(ThemeService);
  readonly roleLabel = roleLabel;

  readonly loading = signal(true);
  readonly savingProfile = signal(false);
  readonly joining = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly me = signal<MeResponse | null>(null);
  readonly tab = signal<ProfileTab>('account');
  readonly joinRequests = signal<SchoolJoinRequestDto[]>([]);
  readonly decidingId = signal<number | null>(null);
  readonly schoolInfo = signal<SchoolInfo | null>(null);

  readonly pendingInvites = computed(() =>
    this.joinRequests().filter((r) => r.direction === 'Invite' && r.status === 'Pending')
  );
  readonly myRequests = computed(() =>
    this.joinRequests().filter((r) => !(r.direction === 'Invite' && r.status === 'Pending'))
  );

  readonly profileForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]]
  });

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  readonly joinForm = this.fb.nonNullable.group({
    schoolQuery: ['', [Validators.required, Validators.minLength(3)]],
    message: ['']
  });

  readonly role = computed(() => this.me()?.role || this.session.user()?.role || '');

  readonly homeLink = computed(() => this.session.homeRoute());

  readonly contextTitle = computed(() => {
    const role = this.role();
    if (role === 'School') return 'Mi escuela';
    if (role === 'Teacher' || role === 'Student') return 'Tu escuela';
    return 'Contexto';
  });

  ngOnInit(): void {
    const requested = this.route.snapshot.queryParamMap.get('tab');
    if (requested === 'account' || requested === 'preferences' || requested === 'security' || requested === 'context') {
      this.tab.set(requested);
    }
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.me().subscribe({
      next: (dto) => {
        this.me.set(dto);
        this.profileForm.patchValue({ name: dto.name });
        this.session.patchUser({
          id: dto.id,
          name: dto.name,
          email: dto.email,
          role: dto.role,
          mustChangePassword: !!dto.mustChangePassword,
          photoUrl: dto.photoUrl ?? null
        });
        this.session.applySchoolContext(dto.school ?? null, dto.freeAccess);
        if (dto.mustChangePassword) {
          this.tab.set('security');
        }
        this.loading.set(false);
        if ((dto.role === 'Teacher' || dto.role === 'Student') && !dto.school) {
          this.loadJoinRequests();
        }
        if (dto.role === 'School') {
          this.loadSchoolInfo();
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  loadSchoolInfo(): void {
    this.http.get<SchoolInfo>(`${env.apiUrl}/api/school/profile`).subscribe({
      next: (dto) => this.schoolInfo.set(dto),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  onSchoolSaved(): void {
    this.api.me().subscribe({
      next: (dto) => this.me.set(dto),
      error: () => { /* the form already confirmed the save */ }
    });
  }

  loadJoinRequests(): void {
    this.http.get<SchoolJoinRequestDto[]>(`${env.apiUrl}/api/me/school-membership`)
      .subscribe({
        next: (rows) => this.joinRequests.set(rows),
        error: () => this.joinRequests.set([])
      });
  }

  requestJoin(): void {
    if (this.joinForm.invalid) {
      this.joinForm.markAllAsTouched();
      return;
    }
    this.joining.set(true);
    this.error.set(null);
    this.success.set(null);
    const raw = this.joinForm.getRawValue();
    this.http.post<SchoolJoinRequestDto>(`${env.apiUrl}/api/me/school-membership/requests`, {
      schoolQuery: raw.schoolQuery.trim(),
      message: raw.message.trim() || null
    }).subscribe({
      next: (created) => {
        this.joining.set(false);
        this.joinForm.reset({ schoolQuery: '', message: '' });
        if (created.status === 'Accepted') {
          this.success.set(`${created.schoolLegalName} ya te había invitado: ahora formas parte de ella.`);
          this.reload();
          return;
        }
        this.joinRequests.update((rows) => [created, ...rows]);
        this.success.set(
          `Solicitud enviada a ${created.schoolLegalName}. La escuela recibirá una notificación.`
        );
      },
      error: (err) => {
        this.joining.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  cancelJoin(id: number): void {
    this.http.post(`${env.apiUrl}/api/me/school-membership/requests/${id}/cancel`, {}).subscribe({
      next: () => {
        this.joinRequests.update((rows) =>
          rows.map((r) => (r.id === id ? { ...r, status: 'Cancelled' } : r))
        );
        this.success.set('Solicitud cancelada.');
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  acceptInvite(inv: SchoolJoinRequestDto): void {
    this.decidingId.set(inv.id);
    this.error.set(null);
    this.success.set(null);
    this.http.post<SchoolJoinRequestDto>(
      `${env.apiUrl}/api/me/school-membership/invitations/${inv.id}/accept`,
      {}
    ).subscribe({
      next: () => {
        this.decidingId.set(null);
        this.success.set(`Ahora formas parte de ${inv.schoolLegalName}.`);
        this.reload();
      },
      error: (err) => {
        this.decidingId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  rejectInvite(inv: SchoolJoinRequestDto): void {
    this.decidingId.set(inv.id);
    this.error.set(null);
    this.success.set(null);
    this.http.post<SchoolJoinRequestDto>(
      `${env.apiUrl}/api/me/school-membership/invitations/${inv.id}/reject`,
      {}
    ).subscribe({
      next: () => {
        this.decidingId.set(null);
        this.joinRequests.update((rows) =>
          rows.map((r) => (r.id === inv.id ? { ...r, status: 'Rejected' } : r))
        );
        this.success.set(`Rechazaste la invitación de ${inv.schoolLegalName}.`);
      },
      error: (err) => {
        this.decidingId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  joinStatusLabel(status: string): string {
    if (status === 'Pending') return 'Pendiente';
    if (status === 'Accepted') return 'Aceptada';
    if (status === 'Rejected') return 'Rechazada';
    if (status === 'Cancelled') return 'Cancelada';
    return status;
  }

  joinStatusTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' {
    if (status === 'Accepted') return 'success';
    if (status === 'Pending') return 'warning';
    if (status === 'Rejected' || status === 'Cancelled') return 'danger';
    return 'neutral';
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
    this.api.updateMe(name).subscribe({
      next: (dto) => {
        this.me.set(dto);
        this.profileForm.patchValue({ name: dto.name });
        this.session.patchUser({ name: dto.name });
        this.savingProfile.set(false);
        this.success.set('Perfil guardado.');
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
    this.error.set(null);
    this.success.set(null);
    this.auth.loading.set(true);
    this.auth.error.set(null);
    this.auth.success.set(null);
    this.api.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        const hadForcedChange = !!this.session.user()?.mustChangePassword
          || !!peekReturnUrl();
        this.session.patchUser({ mustChangePassword: false });
        this.passwordForm.reset();
        this.auth.loading.set(false);
        this.success.set('Contraseña actualizada. Usa la nueva clave en el próximo inicio de sesión.');
        const dest = takeReturnUrl();
        if (hadForcedChange && dest) {
          void this.router.navigateByUrl(dest);
          return;
        }
        this.reload();
      },
      error: (err) => {
        this.auth.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
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
