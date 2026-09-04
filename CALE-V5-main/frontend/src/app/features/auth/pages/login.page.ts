import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiThemeToggleComponent } from '../../../shared/ui/ui-theme-toggle.component';
import { AuthBackHomeComponent } from '../components/auth-back-home.component';
import { AuthFacade } from '../application/auth.facade';
import { BRAND } from '../../../core/brand';
import { isSafeReturnUrl, peekReturnUrl, stashReturnUrl } from '../../../core/auth/return-url';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiThemeToggleComponent,
    AuthBackHomeComponent
  ],
  templateUrl: './login.page.html',
  styleUrl: './login.page.css'
})
export class LoginPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly auth = inject(AuthFacade);
  readonly brand = BRAND;

  returnUrl: string | null = null;
  readonly sessionExpired =
    (this.router.getCurrentNavigation()?.extras.state as { reason?: string } | undefined)?.reason
      === 'session_expired'
    || (typeof history !== 'undefined'
      && (history.state as { reason?: string } | null)?.reason === 'session_expired');

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]]
  });

  ngOnInit(): void {
    const fromQuery = this.route.snapshot.queryParamMap.get('returnUrl');
    if (isSafeReturnUrl(fromQuery)) {
      stashReturnUrl(fromQuery);
      this.returnUrl = fromQuery;
    } else {
      this.returnUrl = peekReturnUrl();
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password, this.returnUrl);
  }
}
