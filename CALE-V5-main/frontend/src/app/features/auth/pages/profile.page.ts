import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { SessionStore } from '../../../core/auth/session.store';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { roleLabel } from '../../../shared/utils/role-label';
import { AuthFacade } from '../application/auth.facade';

@Component({
  selector: 'app-profile-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  templateUrl: './profile.page.html',
  styleUrl: './profile.page.css'
})
export class ProfilePage {
  private readonly fb = inject(FormBuilder);
  readonly session = inject(SessionStore);
  readonly auth = inject(AuthFacade);
  readonly roleLabel = roleLabel;

  readonly form = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { currentPassword, newPassword } = this.form.getRawValue();
    this.auth.changePassword(currentPassword, newPassword);
  }
}
