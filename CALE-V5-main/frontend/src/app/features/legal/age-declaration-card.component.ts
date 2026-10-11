import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { mapApiError } from '../../core/http/map-api-error';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiSuccessComponent } from '../../shared/ui/ui-success.component';
import {
  GuardianFieldsComponent,
  guardianGroup,
  guardianValue,
  latestBirthDate,
  minAgeValidator,
  syncGuardianGroup
} from './guardian-fields.component';
import { GUARDIAN_STATUS_LABEL, Guardian, LegalApi, MyAgeStatus } from './legal.api';
import { LegalStatusStore } from './legal-status.store';

/** Fecha de nacimiento del estudiante y estado de la autorización de su representante legal. */
@Component({
  selector: 'legal-age-declaration-card',
  standalone: true,
  imports: [DatePipe, ReactiveFormsModule, UiButtonComponent, UiCardComponent, UiErrorComponent, UiSuccessComponent, GuardianFieldsComponent],
  template: `
    @if (store.age(); as age) {
      @if (age.required) {
        <ui-card>
          <h2>Edad y representante legal</h2>
          <ui-error [message]="error()" />
          <ui-success [message]="ok()" />

          @if (!age.declared) {
            <p class="muted">
              Necesitamos tu fecha de nacimiento para saber si tu cuenta requiere la autorización de tu representante legal.
              Solo se registra una vez.
            </p>
            <form [formGroup]="declareForm" (ngSubmit)="declare()" class="grid">
              <label class="field">
                Fecha de nacimiento
                <input type="date" formControlName="birthDate" min="1920-01-01" [max]="maxBirthDate" />
                @if (declareForm.controls.birthDate.hasError('minAge')) {
                  <span class="field-error" role="alert">Debes tener al menos 16 años para usar Luz Verde.</span>
                }
              </label>
              @if (minor()) {
                <legal-guardian-fields [group]="declareForm.controls.guardian" />
              }
              <ui-button type="submit" [loading]="busy()">Guardar</ui-button>
            </form>
          } @else if (age.isMinor) {
            <p>
              Estado: <strong>{{ statusLabel(age) }}</strong>
            </p>
            <p class="muted">
              Representante: {{ age.guardianName }} ({{ age.guardianRelationship?.toLowerCase() }}) · {{ age.guardianEmailMasked }}
              @if (age.guardianStatus === 'PENDING' && age.guardianLinkExpiresAt) {
                <br />El enlace que le enviamos vence el {{ age.guardianLinkExpiresAt | date: 'dd/MM/yyyy' }}.
              }
            </p>
            @if (age.guardianStatus === 'PENDING') {
              @if (!editing()) {
                <ui-button type="button" variant="secondary" [disabled]="!age.canResend" (click)="startEdit(age)">
                  Corregir datos o reenviar el correo
                </ui-button>
                @if (!age.canResend) {
                  <p class="muted small">Podrás reenviarlo en unos minutos.</p>
                }
              } @else {
                <form [formGroup]="guardianForm" (ngSubmit)="resend()" class="grid">
                  <legal-guardian-fields [group]="guardianForm" />
                  <div class="row">
                    <ui-button type="submit" [loading]="busy()">Enviar de nuevo</ui-button>
                    <ui-button type="button" variant="ghost" (click)="editing.set(false)">Cancelar</ui-button>
                  </div>
                </form>
              }
            }
          } @else {
            <p class="muted">Tu fecha de nacimiento ya está registrada.</p>
          }
        </ui-card>
      }
    }
  `,
  styles: [`
    h2 { margin: 0 0 0.5rem; font-size: 1.05rem; }
    p { margin: 0 0 0.6rem; line-height: 1.45; }
    .muted { color: var(--color-text-secondary); }
    .small { font-size: 0.8rem; margin-top: 0.4rem; }
    .grid { display: grid; gap: 0.75rem; max-width: 32rem; }
    .row { display: flex; flex-wrap: wrap; gap: 0.5rem; }
  `]
})
export class AgeDeclarationCardComponent implements OnInit {
  readonly store = inject(LegalStatusStore);
  private readonly api = inject(LegalApi);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  readonly maxBirthDate = latestBirthDate();
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly minor = signal(false);
  readonly editing = signal(false);

  readonly declareForm = this.fb.nonNullable.group({
    birthDate: ['', [Validators.required, minAgeValidator]],
    guardian: guardianGroup(this.fb)
  });
  readonly guardianForm = guardianGroup(this.fb);

  ngOnInit(): void {
    this.declareForm.controls.birthDate.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => this.minor.set(syncGuardianGroup(this.declareForm.controls.guardian, value)));
  }

  statusLabel(age: MyAgeStatus): string {
    return age.guardianStatus ? GUARDIAN_STATUS_LABEL[age.guardianStatus] : '';
  }

  declare(): void {
    if (this.declareForm.invalid) {
      this.declareForm.markAllAsTouched();
      return;
    }
    const { birthDate } = this.declareForm.getRawValue();
    this.run(this.api.declareAge(birthDate, guardianValue(this.declareForm.controls.guardian)),
      this.minor()
        ? 'Listo. Enviamos un correo a tu representante legal para que firme la autorización. Puedes seguir usando Luz Verde.'
        : 'Listo. Gracias.');
  }

  startEdit(age: MyAgeStatus): void {
    this.guardianForm.enable();
    this.guardianForm.reset({ name: age.guardianName ?? '', relationship: age.guardianRelationship ?? '', email: '' });
    this.editing.set(true);
  }

  resend(): void {
    if (this.guardianForm.invalid) {
      this.guardianForm.markAllAsTouched();
      return;
    }
    this.run(this.api.updateGuardian(this.guardianForm.getRawValue() as Guardian), 'Enviamos de nuevo el correo a tu representante legal.');
  }

  private run(request: ReturnType<LegalApi['declareAge']>, message: string): void {
    this.busy.set(true);
    this.error.set(null);
    this.ok.set(null);
    request.subscribe({
      next: (age) => {
        this.busy.set(false);
        this.editing.set(false);
        this.store.setAge(age);
        this.ok.set(message);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
